using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace OWLReplaceVanillaItems
{
    // How a form is named in the INIs: by EditorID where SkyPatcher can be given it, else Plugin.esp|FormID.
    // An EditorID is used when it is made only of letters, digits and _ (nothing SkyPatcher reads as a separator) and no
    // other leveled list, container, outfit, NPC, weapon, armor or ammo has it in any version: SkyPatcher's EditorID
    // lookup is one map for all record types, and a record a mod renamed may still answer to its old EditorID.
    public sealed class SkyPatcherRefs
    {
        private static readonly Regex Plain = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private readonly bool _useEditorIDs;
        private readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> _linkCache;
        // Every EditorID a version of one of those records has, with the forms that have it
        private readonly Dictionary<string, HashSet<FormKey>> _holders = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<FormKey, string> _cache = new();

        public SkyPatcherRefs(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, bool useEditorIDs)
        {
            _useEditorIDs = useEditorIDs;
            _linkCache = state.LinkCache;
            if (!useEditorIDs) return;
            foreach (var listing in state.LoadOrder.PriorityOrder)
            {
                if (listing.Mod is not { } mod) continue;
                AddHolders(mod.LeveledItems);
                AddHolders(mod.Containers);
                AddHolders(mod.Outfits);
                AddHolders(mod.Npcs);
                AddHolders(mod.Weapons);
                AddHolders(mod.Armors);
                AddHolders(mod.Ammunitions);
            }
        }

        private void AddHolders(IEnumerable<IMajorRecordGetter> records)
        {
            foreach (var record in records)
            {
                if (string.IsNullOrEmpty(record.EditorID)) continue;
                if (!_holders.TryGetValue(record.EditorID, out var forms)) _holders[record.EditorID] = forms = new();
                forms.Add(record.FormKey);
            }
        }

        // Plugin.esp|FormID, without the FormID's leading zeros (Skyrim.esm|12EB7)
        public static string FormString(FormKey formKey) => formKey.ModKey.FileName + "|" + formKey.ID.ToString("X");

        public string Ref(FormKey formKey)
        {
            if (_cache.TryGetValue(formKey, out var known)) return known;
            string result = SkyPatcherRefs.FormString(formKey);
            if (_useEditorIDs && EditorIDOf(formKey) is { } editorID && Plain.IsMatch(editorID))
            {
                var others = (_holders.TryGetValue(editorID, out var forms) ? forms.Where(f => f != formKey) : Enumerable.Empty<FormKey>()).ToList();
                if (others.Count == 0) result = editorID;
                else if (_reported.Add(editorID))
                    Console.WriteLine("  " + formKey + " is written by its FormID: its EditorID " + editorID + " is also on " + string.Join(", ", others));
            }
            _cache[formKey] = result;
            return result;
        }

        // The winning version's EditorID of an item (leveled list, weapon, armor, ammo), container, outfit or NPC
        private string? EditorIDOf(FormKey formKey)
        {
            if (_linkCache.TryResolve<IItemGetter>(formKey, out var item)) return item.EditorID;
            if (_linkCache.TryResolve<IContainerGetter>(formKey, out var container)) return container.EditorID;
            if (_linkCache.TryResolve<IOutfitGetter>(formKey, out var outfit)) return outfit.EditorID;
            if (_linkCache.TryResolve<INpcGetter>(formKey, out var npc)) return npc.EditorID;
            return null;
        }
    }
}
