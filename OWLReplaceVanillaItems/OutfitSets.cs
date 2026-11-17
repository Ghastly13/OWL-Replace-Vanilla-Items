using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace OWLReplaceVanillaItems
{
    // Open World Loot's outfit lists: OWL_Outfit_<Material>_Simple / _NoHelm / _Full, each picking one of its sets
    // (OWL_Outfit_HideSet_Simple1_BaseGame: use-all lists of exact pieces, boots and cuirass; NoHelm adds the gauntlets,
    // Full the helmet too). Where a record holds every piece of a set, the pieces are replaced by the set's outfit list,
    // larger sets first (hide boots and hide armor -> OWL_Outfit_Hide_Simple). A set under several outfit lists (the Steel
    // sets under both NoHelm and Simple, the Studded ones under Scaled too, the Elven ones under ThalmorElven) goes to: the
    // one named after a theme of the record (ThalmorElven for the Thalmor), else the one of the set's own material
    // (StuddedSet -> Studded), else the one its pieces' slots name (a helmet: Full; gauntlets: NoHelm; else Simple).
    public sealed class OutfitSets
    {
        public sealed record Set(string EditorID, string Material, FormKey[] Pieces, string Slots, List<string> Lists);

        private static readonly Regex TierList = new("^OWL_Outfit_([A-Za-z]+)_(Simple|NoHelm|Full)$", RegexOptions.Compiled);
        private static readonly Regex SetList = new("^OWL_Outfit_([A-Za-z]+?)Set_", RegexOptions.Compiled);

        private readonly List<Set> _sets;
        private readonly HashSet<FormKey> _pieces;
        // Outfit list EditorID -> the list
        private readonly Dictionary<string, FormKey> _lists;

        public int SetCount => _sets.Count;

        private OutfitSets(List<Set> sets, Dictionary<string, FormKey> lists)
        {
            _sets = sets.OrderByDescending(s => s.Pieces.Length).ThenBy(s => s.EditorID, StringComparer.OrdinalIgnoreCase).ToList();
            _pieces = sets.SelectMany(s => s.Pieces).ToHashSet();
            _lists = lists;
        }

        public FormKey ListKey(string editorID) => _lists[editorID];

        public static OutfitSets Build(ISkyrimModGetter owlMod, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
        {
            var sets = new Dictionary<FormKey, Set>();
            var lists = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var tier in owlMod.LeveledItems)
            {
                if (tier.EditorID is not { } tierID || TierList.Match(tierID) is not { Success: true }) continue;
                lists[tierID] = tier.FormKey;
                if (!linkCache.TryResolve<ILeveledItemGetter>(tier.FormKey, out var tierWinner)) continue;
                foreach (var entry in tierWinner.Entries ?? Array.Empty<ILeveledItemEntryGetter>())
                {
                    if (entry.Data?.Reference.FormKey is not { IsNull: false } setKey) continue;
                    if (sets.TryGetValue(setKey, out var known)) { if (!known.Lists.Contains(tierID)) known.Lists.Add(tierID); continue; }
                    if (!linkCache.TryResolve<ILeveledItemGetter>(setKey, out var set) || !set.Flags.HasFlag(LeveledItem.Flag.UseAll)) continue;
                    var pieces = new List<FormKey>();
                    var slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool armorOnly = true;
                    foreach (var piece in set.Entries ?? Array.Empty<ILeveledItemEntryGetter>())
                    {
                        if (piece.Data?.Reference.FormKey is not { IsNull: false } pk || !linkCache.TryResolve<IArmorGetter>(pk, out var armor)) { armorOnly = false; break; }
                        pieces.Add(pk);
                        foreach (var kw in armor.Keywords ?? Array.Empty<IFormLinkGetter<IKeywordGetter>>())
                            if (linkCache.TryResolve<IKeywordGetter>(kw.FormKey, out var k) && k.EditorID is "ArmorHelmet" or "ArmorGauntlets") slots.Add(k.EditorID!);
                    }
                    if (!armorOnly || pieces.Count < 2) continue;
                    string material = set.EditorID != null && SetList.Match(set.EditorID) is { Success: true } m ? m.Groups[1].Value : "";
                    string slotTier = slots.Contains("ArmorHelmet") ? "Full" : slots.Contains("ArmorGauntlets") ? "NoHelm" : "Simple";
                    sets[setKey] = new Set(set.EditorID ?? setKey.ToString(), material, pieces.Distinct().ToArray(), slotTier, new List<string> { tierID });
                }
            }
            // Sets of the same pieces (SteelSet_NoHelm1 and SteelSet_Simple1) are one set, under all their outfit lists
            var merged = sets.Values
                .GroupBy(s => string.Join("+", s.Pieces.OrderBy(p => p.ToString())))
                .Select(g => g.First() with { Lists = g.SelectMany(s => s.Lists).Distinct(StringComparer.OrdinalIgnoreCase).ToList() })
                .ToList();
            return new OutfitSets(merged, lists);
        }

        public bool IsPiece(FormKey item) => _pieces.Contains(item);

        // The sets whose every piece is among the items (with counts: all of one count), larger first, each piece in one
        // set only; and the outfit list each goes to
        public List<(Set Set, string List)> Match(IReadOnlyCollection<FormKey> items, IReadOnlyDictionary<FormKey, int>? counts, IReadOnlyList<string> themes)
        {
            var left = new HashSet<FormKey>(items);
            var result = new List<(Set, string)>();
            foreach (var set in _sets)
            {
                if (!set.Pieces.All(left.Contains)) continue;
                if (counts != null && set.Pieces.Select(p => counts.GetValueOrDefault(p)).Distinct().Count() != 1) continue;
                foreach (var piece in set.Pieces) left.Remove(piece);
                result.Add((set, ListFor(set, themes)));
            }
            return result;
        }

        private static string ListFor(Set set, IReadOnlyList<string> themes)
        {
            if (set.Lists.Count == 1) return set.Lists[0];
            string MaterialOf(string list) => TierList.Match(list).Groups[1].Value;
            string TierOf(string list) => TierList.Match(list).Groups[2].Value;
            foreach (var theme in themes)
                if (set.Lists.FirstOrDefault(l => MaterialOf(l).StartsWith(theme, StringComparison.OrdinalIgnoreCase) && MaterialOf(l).Length > theme.Length) is { } themed)
                    return themed;
            var candidates = set.Lists.ToList();
            var own = candidates.Where(l => MaterialOf(l).Equals(set.Material, StringComparison.OrdinalIgnoreCase)).ToList();
            if (own.Count > 0) candidates = own;
            var bySlots = candidates.Where(l => TierOf(l).Equals(set.Slots, StringComparison.OrdinalIgnoreCase)).ToList();
            if (bySlots.Count > 0) candidates = bySlots;
            return candidates.OrderBy(l => l, StringComparer.OrdinalIgnoreCase).First();
        }
    }
}
