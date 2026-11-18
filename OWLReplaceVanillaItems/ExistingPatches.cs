using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace OWLReplaceVanillaItems
{
    // What the SkyPatcher INIs already installed do to records: Open World Loot's own and its patches for other mods (in its
    // OWL folders), and other mods' (WACCF's, ...). For each record they name: whether they clear it, and which items they
    // replace or remove in it (by form, by count or by keyword). This patcher leaves those to them: a record a patch clears
    // gets nothing more from its plugins, and an item a patch replaces or removes in a record is not replaced there.
    // From the INIs outside Open World Loot's folders it also reads what they add to records (and put in by replacing), so
    // those items get OWL lists as a plugin's would; Open World Loot's own additions are its choices, left as they are.
    // Only lines that name their records are read: by form (filterByLLs, filterByContainers, filterByOutfits,
    // filterByNpcs) or by EditorID (filterByEditorIdContains, ...Or, ...Excluded). An INI named after a plugin counts only
    // with that plugin active, as SkyPatcher reads it only then.
    public sealed class ExistingPatches
    {
        // An item an INI adds to a record
        public sealed record Addition(IniFiles.Ini Ini, FormKey Record, FormKey Item);

        private static readonly HashSet<string> AddKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "addToLLs", "addOnceToLLs", "addToContainers", "addOnceToContainers", "formsToAdd", "objectsToAdd", "addOnceToInventory",
        };

        public List<Addition> Additions { get; } = new();
        public sealed class RecordPatch
        {
            // The INI that clears the record, if one does
            public string? ClearedBy { get; set; }
            // Item -> the INI that replaces or removes it in the record
            public Dictionary<FormKey, string> Items { get; } = new();
            // Keyword -> the INI that removes the record's items with it
            public Dictionary<FormKey, string> Keywords { get; } = new();
        }

        private static readonly Dictionary<string, string> RecordFilters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["leveledList"] = "filterByLLs", ["container"] = "filterByContainers", ["outfit"] = "filterByOutfits", ["npc"] = "filterByNpcs",
        };
        private static readonly HashSet<string> ClearKeys = new(StringComparer.OrdinalIgnoreCase) { "clear", "clearInventory" };
        private static readonly HashSet<string> ReplaceKeys = new(StringComparer.OrdinalIgnoreCase) { "formsToReplace", "replaceInContainers", "objectsToReplace" };
        private static readonly HashSet<string> RemoveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "removeFromLLs", "removeFromContainers", "removeContainerObjectsByCount", "objectsToRemove", "removeInventoryObjectsByCount", "formsToRemove",
        };
        private static readonly HashSet<string> KeywordRemoveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "removeObjectsByKeyword", "removeContainerObjectsByKeywords", "removeInventoryObjectsByKeywords",
        };
        // Values that turn clear off; any other turns it on
        private static readonly HashSet<string> Off = new(StringComparer.OrdinalIgnoreCase) { "false", "no", "0", "off", "none" };

        private readonly Dictionary<FormKey, RecordPatch> _records = new();
        private readonly Dictionary<string, List<FormKey>> _byEditorID = new(StringComparer.OrdinalIgnoreCase);
        // Per record type folder, the records of that type and their EditorIDs (for filterByEditorIdContains)
        private readonly Dictionary<string, List<(string EditorID, FormKey FormKey)>> _byFolder = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<FormKey> _known = new();
        // Per plugin, the highest record ID seen: a plugin with none above FFF is light (only the last three digits count)
        private readonly Dictionary<ModKey, uint> _maxID = new();

        public int FilesRead { get; private set; }
        public int LinesRead { get; private set; }
        // Lines with clears, replacements or removals that name no records this can read (filterByKeywords, ...)
        public int LinesNotNamingRecords { get; private set; }
        public int RecordsCleared => _records.Values.Count(r => r.ClearedBy != null);
        public int RecordsWithItems => _records.Values.Count(r => r.Items.Count > 0 || r.Keywords.Count > 0);

        public ExistingPatches(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string skyPatcherFolder, IEnumerable<IniFiles.Ini> inis)
        {
            var order = state.LoadOrder.PriorityOrder;
            Index("leveledList", order.LeveledItem().WinningOverrides());
            Index("container", order.Container().WinningOverrides());
            Index("outfit", order.Outfit().WinningOverrides());
            Index("npc", order.Npc().WinningOverrides());
            Index(null, order.Weapon().WinningOverrides());
            Index(null, order.Armor().WinningOverrides());
            Index(null, order.Ammunition().WinningOverrides());
            Index(null, order.Keyword().WinningOverrides());

            var active = new HashSet<string>(state.LoadOrder.ListedOrder.Where(l => l.Mod != null).Select(l => l.ModKey.FileName.ToString()), StringComparer.OrdinalIgnoreCase);
            foreach (var ini in inis)
            {
                string gate = Path.GetFileNameWithoutExtension(ini.Path);
                if (IniFiles.IsPluginName(gate) && !active.Contains(gate)) continue;
                string text;
                try { text = File.ReadAllText(ini.Path); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                FilesRead++;
                string label = Path.GetRelativePath(skyPatcherFolder, ini.Path);
                foreach (var line in text.Split('\n')) Read(ini, line, label);
            }
        }

        public RecordPatch? For(FormKey record) => _records.TryGetValue(record, out var patch) ? patch : null;

        private void Index(string? folder, IEnumerable<IMajorRecordGetter> records)
        {
            foreach (var record in records)
            {
                _known.Add(record.FormKey);
                if (!_maxID.TryGetValue(record.FormKey.ModKey, out var max) || record.FormKey.ID > max) _maxID[record.FormKey.ModKey] = record.FormKey.ID;
                if (string.IsNullOrEmpty(record.EditorID)) continue;
                if (!_byEditorID.TryGetValue(record.EditorID, out var forms)) _byEditorID[record.EditorID] = forms = new();
                forms.Add(record.FormKey);
                if (folder == null) continue;
                if (!_byFolder.TryGetValue(folder, out var list)) _byFolder[folder] = list = new();
                list.Add((record.EditorID, record.FormKey));
            }
        }

        // The forms a token names: an EditorID, or Plugin.esp|FormID read as SkyPatcher reads it (without the load-order
        // digits; of a light plugin only the last three)
        private IEnumerable<FormKey> Resolve(string token)
        {
            token = token.Trim();
            if (token.Length == 0) return Enumerable.Empty<FormKey>();
            int bar = token.IndexOf('|');
            if (bar < 0) return _byEditorID.TryGetValue(token, out var forms) ? forms : Enumerable.Empty<FormKey>();
            string hex = token[(bar + 1)..].Trim();
            if (!ModKey.TryFromFileName(token[..bar].Trim(), out var modKey)
                || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint id)) return Enumerable.Empty<FormKey>();
            var full = new FormKey(modKey, id & 0xFFFFFF);
            if (_known.Contains(full)) return new[] { full };
            var light = new FormKey(modKey, id & 0xFFF);
            if (_maxID.TryGetValue(modKey, out var max) && max <= 0xFFF && _known.Contains(light)) return new[] { light };
            return Enumerable.Empty<FormKey>();
        }

        private RecordPatch PatchOf(FormKey record)
        {
            if (!_records.TryGetValue(record, out var patch)) _records[record] = patch = new RecordPatch();
            return patch;
        }

        // The first form of an entry: "item", "item~level~count", "itemA~itemB", "itemA=itemB"
        private static string FirstForm(string entry)
        {
            int end = entry.IndexOfAny(new[] { '~', '=' });
            return (end < 0 ? entry : entry[..end]).Trim();
        }

        // The second form of a replacement entry: "itemA~itemB", "itemA=itemB"
        private static string SecondForm(string entry)
        {
            var parts = entry.Split('~', '=');
            return parts.Length > 1 ? parts[1].Trim() : "";
        }

        private void Read(IniFiles.Ini ini, string raw, string label)
        {
            string folder = ini.Folder;
            bool additions = !ini.InOwlFolder;
            string line = raw;
            int comment = line.IndexOf(';');
            if (comment >= 0) line = line[..comment];
            line = line.Trim();
            if (line.Length == 0) return;

            var records = new HashSet<FormKey>();
            bool named = false;
            List<string>? contains = null, containsOr = null, excluded = null;
            var ops = new List<(string Key, string Value)>();
            foreach (var segment in line.Split(':'))
            {
                int eq = segment.IndexOf('=');
                if (eq < 0) continue;
                string key = segment[..eq].Trim(), value = segment[(eq + 1)..].Trim();
                List<string> Parts() => value.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                if (RecordFilters.TryGetValue(folder, out var filter) && key.Equals(filter, StringComparison.OrdinalIgnoreCase))
                {
                    named = true;
                    foreach (var token in Parts()) records.UnionWith(Resolve(token));
                }
                else if (key.Equals("filterByEditorIdContains", StringComparison.OrdinalIgnoreCase)) { named = true; contains = Parts(); }
                else if (key.Equals("filterByEditorIdContainsOr", StringComparison.OrdinalIgnoreCase)) { named = true; containsOr = Parts(); }
                else if (key.Equals("filterByEditorIdContainsExcluded", StringComparison.OrdinalIgnoreCase)) excluded = Parts();
                else if (!key.StartsWith("filterBy", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("noFilter", StringComparison.OrdinalIgnoreCase))
                    ops.Add((key, value));
            }
            bool relevant = ops.Any(o => ClearKeys.Contains(o.Key) || ReplaceKeys.Contains(o.Key) || RemoveKeys.Contains(o.Key) || KeywordRemoveKeys.Contains(o.Key)
                || (additions && AddKeys.Contains(o.Key)));
            if (!relevant) return;
            if (!named)
            {
                LinesNotNamingRecords++;
                return;
            }
            LinesRead++;

            // The EditorID filters find records of the folder's type on their own, beside the forms named: with both
            // Contains and ContainsOr, a record must match both; Excluded takes out what matches it
            if ((contains != null || containsOr != null) && _byFolder.TryGetValue(folder, out var candidates))
            {
                foreach (var (editorID, formKey) in candidates)
                {
                    bool match = (contains == null || contains.All(t => editorID.Contains(t, StringComparison.OrdinalIgnoreCase)))
                        && (containsOr == null || containsOr.Any(t => editorID.Contains(t, StringComparison.OrdinalIgnoreCase)));
                    if (match && (excluded == null || !excluded.Any(t => editorID.Contains(t, StringComparison.OrdinalIgnoreCase)))) records.Add(formKey);
                }
            }

            // Of several INIs that clear a record, or replace or remove an item in it, the one SkyPatcher applies last is
            // kept: whether it comes after what another INI adds decides whether that addition is left to it
            string Last(string? current) => current == null || IniFiles.AppliesAfter(label, current) ? label : current;
            foreach (var record in records)
            {
                var patch = PatchOf(record);
                foreach (var (key, value) in ops)
                {
                    var entries = value.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0);
                    if (ClearKeys.Contains(key)) { if (!Off.Contains(value)) patch.ClearedBy = Last(patch.ClearedBy); }
                    else if (ReplaceKeys.Contains(key) || RemoveKeys.Contains(key))
                        foreach (var entry in entries)
                        {
                            foreach (var item in Resolve(FirstForm(entry))) patch.Items[item] = Last(patch.Items.GetValueOrDefault(item));
                            if (additions && ReplaceKeys.Contains(key))
                                foreach (var item in Resolve(SecondForm(entry))) Additions.Add(new Addition(ini, record, item));
                        }
                    else if (KeywordRemoveKeys.Contains(key))
                        foreach (var entry in entries)
                            foreach (var keyword in Resolve(FirstForm(entry))) patch.Keywords[keyword] = Last(patch.Keywords.GetValueOrDefault(keyword));
                    else if (additions && AddKeys.Contains(key))
                        foreach (var entry in entries)
                            foreach (var item in Resolve(FirstForm(entry))) Additions.Add(new Addition(ini, record, item));
                }
            }
        }
    }
}
