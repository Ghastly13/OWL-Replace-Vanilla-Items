using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace OWLReplaceVanillaItems
{
    // Open World Loot's themed lists: beside many plain lists it has one per theme, named after it
    // (OWL_Armor_HideStudded_Cuirass_Stormcloak, OWL_Weapon_Elven_Sword_Thalmor, OWL_Armor_Iron_Cuirass_Town), which it
    // gives the vanilla gear lists of that group (LItemSoldierSons..., LItemThalmor..., LItemBanditBoss...). A record that
    // matches a theme's rule gets the theme's list for an item, where Open World Loot has one, instead of the plain list.
    //
    // A rule is "Theme = kind patterns; kind patterns": the theme matches when any of its criteria does. Kinds:
    //   faction     the NPC is a member (rank 0 or more) of a matching faction (or of its factions template's)
    //   class       the NPC's class matches (or its traits template's)
    //   race        the NPC's race matches (or its traits template's)
    //   npc         the NPC's own EditorID matches
    //   worldspace  every placed copy known of the record is in a matching worldspace
    //   location    every placed copy known is in a matching location, or one inside it
    // Patterns are EditorIDs, * standing for any text, ignoring case. A record gets every theme it matches, in the
    // rules' order, and each item the first of them Open World Loot has a list for (a bandit chief's weapons from the
    // Bandit lists, its armor from the Boss ones). An outfit takes the themes every NPC wearing it has (by default or to
    // sleep, its own or its inventory template's); a container those every placed copy has, from its owner (its own,
    // else its cell's: an NPC's themes, a faction's own, else the ones all its members share) and from where it is, or
    // those of the faction whose merchant chest it is (a blacksmith's: its merchant is a blacksmith). Placed copies are
    // read from the plugins given to ScanPlacements; a placed NPC counts for the NPCs its template chain leads to. The
    // player gives no themes, as an NPC or an owner.
    public sealed class ThemeRules
    {
        private sealed record Criterion(string Kind, Regex Pattern);
        private sealed record Rule(string Theme, List<Criterion> Criteria);

        private static readonly string[] Kinds = { "faction", "class", "race", "npc", "worldspace", "location" };

        private readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> _linkCache;
        private readonly OwlMap _owl;
        private readonly List<Rule> _rules = new();
        private readonly bool _placeRules;

        private readonly Dictionary<FormKey, List<FormKey>> _outfitWearers = new();
        private readonly Dictionary<FormKey, List<FormKey>> _factionMembers = new();
        private readonly Dictionary<FormKey, List<FormKey>> _merchantOf = new();
        // Placed container copy -> (container, owner, the themes its place gives); the last plugin scanned that has it wins
        private readonly Dictionary<FormKey, (FormKey Container, FormKey? Owner, int[] Places)> _placedContainers = new();
        // Placed NPC -> (the NPCs it stands for, the themes its place gives)
        private readonly Dictionary<FormKey, (List<FormKey> Npcs, int[] Places)> _placedNpcs = new();
        private Dictionary<FormKey, List<(FormKey? Owner, int[] Places)>>? _containerCopies;
        private Dictionary<FormKey, List<int[]>>? _npcPlaces;

        private readonly Dictionary<FormKey, int[]> _npcThemes = new();
        private readonly Dictionary<FormKey, int[]> _outfitThemes = new();
        private readonly Dictionary<FormKey, int[]> _containerThemes = new();
        private readonly Dictionary<FormKey, int[]> _factionThemes = new();
        private readonly Dictionary<FormKey, int[]> _locationThemes = new();
        private readonly Dictionary<FormKey, List<FormKey>> _spawned = new();

        public int RuleCount => _rules.Count;
        public int PlacedCopies => _placedContainers.Count + _placedNpcs.Count;
        public bool HasPlaceRules => _placeRules;

        public ThemeRules(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, OwlMap owl, IEnumerable<string> rules, Action<string> log)
        {
            _linkCache = state.LinkCache;
            _owl = owl;
            foreach (var text in rules) if (Parse(text, log) is { } rule) _rules.Add(rule);
            _placeRules = _rules.Any(r => r.Criteria.Any(c => c.Kind is "worldspace" or "location"));

            foreach (var faction in state.LoadOrder.PriorityOrder.Faction().WinningOverrides())
            {
                if (faction.MerchantContainer.FormKeyNullable is { IsNull: false } chest && _linkCache.TryResolve<IPlacedObjectGetter>(chest, out var placed)
                    && placed.Base.FormKeyNullable is { IsNull: false } container)
                {
                    if (!_merchantOf.TryGetValue(container, out var list)) _merchantOf[container] = list = new();
                    list.Add(faction.FormKey);
                }
            }
            foreach (var npc in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                foreach (var faction in FactionsOf(npc))
                {
                    if (!_factionMembers.TryGetValue(faction, out var members)) _factionMembers[faction] = members = new();
                    members.Add(npc.FormKey);
                }
                if (Source(npc, NpcConfiguration.TemplateFlag.Inventory) is not { } wearer) continue;
                foreach (var outfit in new[] { wearer.DefaultOutfit.FormKeyNullable, wearer.SleepingOutfit.FormKeyNullable })
                {
                    if (outfit is not { IsNull: false } o) continue;
                    if (!_outfitWearers.TryGetValue(o, out var list)) _outfitWearers[o] = list = new();
                    if (!list.Contains(npc.FormKey)) list.Add(npc.FormKey);
                }
            }
        }

        private Rule? Parse(string text, Action<string> log)
        {
            int eq = text.IndexOf('=');
            if (eq <= 0) { log("NOTE: theme rule \"" + text + "\" is not Theme = kind patterns: ignored"); return null; }
            string theme = text[..eq].Trim();
            if (!_owl.Lists.Keys.Any(id => id.EndsWith("_" + theme, StringComparison.OrdinalIgnoreCase)))
            {
                log("NOTE: theme rule \"" + text + "\": Open World Loot has no lists ending in _" + theme + ": ignored");
                return null;
            }
            var criteria = new List<Criterion>();
            foreach (var part in text[(eq + 1)..].Split(';'))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0) continue;
                int space = trimmed.IndexOf(' ');
                string kind = (space < 0 ? trimmed : trimmed[..space]).ToLowerInvariant();
                if (!Kinds.Contains(kind) || space < 0)
                {
                    log("NOTE: theme rule \"" + text + "\": \"" + trimmed + "\" is not one of " + string.Join(", ", Kinds) + " followed by patterns: ignored");
                    continue;
                }
                foreach (var pattern in trimmed[(space + 1)..].Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
                    criteria.Add(new Criterion(kind, new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled)));
            }
            return criteria.Count > 0 ? new Rule(theme, criteria) : null;
        }

        private bool Matches(int rule, string kind, string? editorID) =>
            editorID != null && _rules[rule].Criteria.Any(c => c.Kind == kind && c.Pattern.IsMatch(editorID));

        // The NPC whose template flag chain gives this part: the NPC, else the NPC template it takes it from; null through
        // a leveled template
        private INpcGetter? Source(INpcGetter npc, NpcConfiguration.TemplateFlag flag)
        {
            var current = npc;
            for (int depth = 0; depth < 10; depth++)
            {
                if (!current.Configuration.TemplateFlags.HasFlag(flag) || current.Template.IsNull) return current;
                if (!_linkCache.TryResolve<INpcSpawnGetter>(current.Template.FormKey, out var template)) return current;
                if (template is not INpcGetter next) return null;
                current = next;
            }
            return current;
        }

        private IEnumerable<FormKey> FactionsOf(INpcGetter npc) =>
            Source(npc, NpcConfiguration.TemplateFlag.Factions)?.Factions?.Where(f => f.Rank >= 0).Select(f => f.Faction.FormKey) ?? Enumerable.Empty<FormKey>();

        private string? EditorIDOf<T>(FormKey? formKey) where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter =>
            formKey is { IsNull: false } fk && _linkCache.TryResolve<T>(fk, out var record) ? record.EditorID : null;

        // The themes every set has, in the rules' order; none for no sets
        private static int[] Common(IEnumerable<int[]> sets)
        {
            int[]? common = null;
            foreach (var set in sets) common = common == null ? set : common.Intersect(set).ToArray();
            return common ?? Array.Empty<int>();
        }

        // The player (Skyrim.esm|7): its record's class is a placeholder (AAAPlayerSpellswordClass), so it gives no theme,
        // and a container the player owns (a player home's, a start room's) gets none from it
        private static readonly FormKey Player = new(ModKey.FromFileName("Skyrim.esm"), 0x7);

        public int[] NpcThemes(FormKey npc)
        {
            if (_npcThemes.TryGetValue(npc, out var known)) return known;
            var themes = new List<int>();
            if (npc != Player && _linkCache.TryResolve<INpcGetter>(npc, out var winner))
            {
                var factions = FactionsOf(winner).Select(f => EditorIDOf<IFactionGetter>(f)).ToList();
                var traits = Source(winner, NpcConfiguration.TemplateFlag.Traits);
                string? @class = EditorIDOf<IClassGetter>(traits?.Class.FormKey), race = EditorIDOf<IRaceGetter>(traits?.Race.FormKey);
                _npcPlaces ??= GroupNpcPlaces();
                var places = _npcPlaces.TryGetValue(npc, out var p) ? Common(p) : Array.Empty<int>();
                for (int i = 0; i < _rules.Count; i++)
                {
                    if (factions.Any(f => Matches(i, "faction", f)) || Matches(i, "class", @class) || Matches(i, "race", race)
                        || Matches(i, "npc", winner.EditorID) || places.Contains(i))
                        themes.Add(i);
                }
            }
            return _npcThemes[npc] = themes.ToArray();
        }

        public string ThemeName(int rule) => _rules[rule].Theme;

        public IEnumerable<FormKey> WearersOf(FormKey outfit) => _outfitWearers.TryGetValue(outfit, out var wearers) ? wearers : Enumerable.Empty<FormKey>();

        public int[] OutfitThemes(FormKey outfit)
        {
            if (_outfitThemes.TryGetValue(outfit, out var known)) return known;
            return _outfitThemes[outfit] = _outfitWearers.TryGetValue(outfit, out var wearers) ? Common(wearers.Select(NpcThemes)) : Array.Empty<int>();
        }

        // A faction as an owner: the rules it matches itself, else the themes all its members share
        private int[] FactionThemes(FormKey faction)
        {
            if (_factionThemes.TryGetValue(faction, out var known)) return known;
            string? editorID = EditorIDOf<IFactionGetter>(faction);
            var own = Enumerable.Range(0, _rules.Count).Where(i => Matches(i, "faction", editorID)).ToArray();
            _factionThemes[faction] = own;
            if (own.Length == 0 && _factionMembers.TryGetValue(faction, out var members)) _factionThemes[faction] = Common(members.Select(NpcThemes));
            return _factionThemes[faction];
        }

        private int[] OwnerThemes(FormKey? owner)
        {
            if (owner is not { IsNull: false } o) return Array.Empty<int>();
            return _linkCache.TryResolve<IFactionGetter>(o, out _) ? FactionThemes(o) : NpcThemes(o);
        }

        public int[] ContainerThemes(FormKey container)
        {
            if (_containerThemes.TryGetValue(container, out var known)) return known;
            _containerCopies ??= _placedContainers.Values.GroupBy(p => p.Container).ToDictionary(g => g.Key, g => g.Select(p => (p.Owner, p.Places)).ToList());
            var sets = new List<int[]>();
            if (_containerCopies.TryGetValue(container, out var copies))
                foreach (var (owner, places) in copies) sets.Add(OwnerThemes(owner).Union(places).OrderBy(i => i).ToArray());
            if (_merchantOf.TryGetValue(container, out var merchants)) sets.AddRange(merchants.Select(FactionThemes));
            return _containerThemes[container] = Common(sets);
        }

        // The themes a location gives: it, or one it is inside, matching a location rule
        private int[] LocationThemes(FormKey? location)
        {
            if (location is not { IsNull: false } start) return Array.Empty<int>();
            if (_locationThemes.TryGetValue(start, out var known)) return known;
            var themes = new HashSet<int>();
            var current = start;
            for (int depth = 0; depth < 20 && _linkCache.TryResolve<ILocationGetter>(current, out var loc); depth++)
            {
                for (int i = 0; i < _rules.Count; i++) if (Matches(i, "location", loc.EditorID)) themes.Add(i);
                if (loc.ParentLocation.FormKeyNullable is not { IsNull: false } parent) break;
                current = parent;
            }
            return _locationThemes[start] = themes.OrderBy(i => i).ToArray();
        }

        // Reads the placed copies, in these plugins (in load order; a later plugin's copy wins), of these containers and,
        // with place rules, of every NPC
        public void ScanPlacements(IEnumerable<ISkyrimModGetter> plugins, ISet<FormKey> containers)
        {
            if (_rules.Count == 0) return;
            foreach (var mod in plugins)
            {
                foreach (var block in mod.Cells.Records)
                    foreach (var sub in block.SubBlocks)
                        foreach (var cell in sub.Cells) Scan(cell, null, containers);
                foreach (var worldspace in mod.Worldspaces)
                {
                    IWorldspaceGetter world = _linkCache.TryResolve<IWorldspaceGetter>(worldspace.FormKey, out var w) ? w : worldspace;
                    if (worldspace.TopCell is { } top) Scan(top, world, containers);
                    foreach (var block in worldspace.SubCells)
                        foreach (var sub in block.Items)
                            foreach (var cell in sub.Items) Scan(cell, world, containers);
                }
            }
            _containerCopies = null;
            _npcPlaces = null;
        }

        private void Scan(ICellGetter cell, IWorldspaceGetter? world, ISet<FormKey> containers)
        {
            FormKey? cellOwner = cell.Owner.FormKeyNullable is { IsNull: false } co ? co : null;
            int[]? places = null;
            int[] Places()
            {
                if (places != null) return places;
                if (!_placeRules) return places = Array.Empty<int>();
                var set = new HashSet<int>(LocationThemes(cell.Location.FormKeyNullable ?? world?.Location.FormKeyNullable));
                if (world != null) for (int i = 0; i < _rules.Count; i++) if (Matches(i, "worldspace", world.EditorID)) set.Add(i);
                return places = set.OrderBy(i => i).ToArray();
            }
            foreach (var placed in (cell.Persistent ?? Array.Empty<IPlacedGetter>()).Concat(cell.Temporary ?? Array.Empty<IPlacedGetter>()))
            {
                if (placed is IPlacedObjectGetter refr)
                {
                    if (refr.Base.FormKeyNullable is not { IsNull: false } container || !containers.Contains(container)) continue;
                    FormKey? owner = refr.Owner.FormKeyNullable is { IsNull: false } ro ? ro : cellOwner;
                    _placedContainers[refr.FormKey] = (container, owner, Places());
                }
                else if (_placeRules && placed is IPlacedNpcGetter achr && achr.Base.FormKeyNullable is { IsNull: false } npc)
                    _placedNpcs[achr.FormKey] = (Spawned(npc), Places());
            }
        }

        // The NPCs a placed NPC stands for: it and the ones its template chain leads to, through leveled lists too
        private List<FormKey> Spawned(FormKey npc)
        {
            if (_spawned.TryGetValue(npc, out var known)) return known;
            var result = new List<FormKey>();
            var seen = new HashSet<FormKey>();
            var queue = new Queue<FormKey>();
            queue.Enqueue(npc);
            while (queue.Count > 0 && seen.Count < 200)
            {
                var current = queue.Dequeue();
                if (!seen.Add(current)) continue;
                if (_linkCache.TryResolve<INpcGetter>(current, out var n))
                {
                    result.Add(current);
                    if (!n.Template.IsNull) queue.Enqueue(n.Template.FormKey);
                }
                else if (_linkCache.TryResolve<ILeveledNpcGetter>(current, out var list))
                    foreach (var entry in list.Entries ?? Array.Empty<ILeveledNpcEntryGetter>())
                        if (entry.Data?.Reference.FormKey is { IsNull: false } r) queue.Enqueue(r);
            }
            return _spawned[npc] = result;
        }

        private Dictionary<FormKey, List<int[]>> GroupNpcPlaces()
        {
            var places = new Dictionary<FormKey, List<int[]>>();
            foreach (var (npcs, place) in _placedNpcs.Values)
                foreach (var npc in npcs)
                {
                    if (!places.TryGetValue(npc, out var list)) places[npc] = list = new();
                    list.Add(place);
                }
            return places;
        }

        // The first of the record's themes Open World Loot has a list for the item under (the plain list's name with
        // _<theme>), and that theme; null when none
        public (string List, string Theme)? ThemedList(string plainList, int[] themes)
        {
            foreach (var i in themes)
            {
                string themed = plainList + "_" + _rules[i].Theme;
                if (_owl.Lists.ContainsKey(themed)) return (themed, _rules[i].Theme);
            }
            return null;
        }
    }
}
