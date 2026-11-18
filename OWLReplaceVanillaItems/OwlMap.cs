using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace OWLReplaceVanillaItems
{
    // Which Open World Loot list hands out each vanilla weapon, armor and ammo: the OWL_Armor_ / OWL_Weapon_ list in Open
    // World Loot.esp (its winning version) that holds the item. Where several do: the plain material list
    // (OWL_Armor_Iron_Cuirass, not ..._VanillaLike; OWL_Armor_Elven_Boots, not OWL_Armor_Steel_Boots_Thalmor), then the
    // one whose material is in the item's EditorID (SteelDagger -> OWL_Weapon_Steel_Dagger, not ..._Alikr_...), and from a
    // themed list up to the list holding it that it is named after (OWL_Armor_Fur_Cuirass_Vanilla -> OWL_Armor_Fur_Cuirass).
    public sealed class OwlMap
    {
        public static readonly ModKey Plugin = ModKey.FromFileName("Open World Loot.esp");

        // OWL list EditorID -> the list
        public Dictionary<string, FormKey> Lists { get; } = new(StringComparer.OrdinalIgnoreCase);
        // Item -> EditorID of the OWL list that hands it out
        public Dictionary<FormKey, string> Targets { get; } = new();
        // OWL list EditorID -> the weapons, armor and ammo it holds itself (not through its sublists)
        public Dictionary<string, HashSet<FormKey>> Holds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static OwlMap Build(ISkyrimModGetter owl, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
        {
            var map = new OwlMap();
            foreach (var list in owl.LeveledItems)
            {
                string? id = list.EditorID;
                if (id == null || !(id.StartsWith("OWL_Armor_", StringComparison.Ordinal) || id.StartsWith("OWL_Weapon_", StringComparison.Ordinal))) continue;
                map.Lists.TryAdd(id, list.FormKey);
            }

            // The OWL list holding each themed list it is named after. Lists are read in alphabetical order, so where two
            // hold it the later one wins (OWL_Armor_Fur_Cuirass over OWL_Armor_Fur), as in the xEdit script
            var namedParent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (listID, formKey) in map.Lists.OrderBy(l => l.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!linkCache.TryResolve<ILeveledItemGetter>(formKey, out var winner) || winner.Entries == null) continue;
                foreach (var entry in winner.Entries)
                {
                    var link = entry.Data?.Reference;
                    if (link == null || link.IsNull || !linkCache.TryResolve<IItemGetter>(link.FormKey, out var item)) continue;
                    if (item is ILeveledItemGetter sublist)
                    {
                        if (sublist.EditorID != null && sublist.EditorID.StartsWith(listID + "_", StringComparison.Ordinal)) namedParent[sublist.EditorID] = listID;
                        continue;
                    }
                    if (item is not (IWeaponGetter or IArmorGetter or IAmmunitionGetter)) continue;
                    if (!map.Holds.TryGetValue(listID, out var held)) map.Holds[listID] = held = new();
                    held.Add(item.FormKey);
                    if (!map.Targets.TryGetValue(item.FormKey, out var current) || Better(listID, current, item.EditorID ?? ""))
                        map.Targets[item.FormKey] = listID;
                }
            }

            // From a themed list up to the list it is named after
            foreach (var key in map.Targets.Keys.ToList())
            {
                string current = map.Targets[key];
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { current };
                while (namedParent.TryGetValue(current, out var parent) && seen.Add(parent)) current = parent;
                map.Targets[key] = current;
            }
            return map;
        }

        private static int Underscores(string s) => s.Count(c => c == '_');

        // The n-th '_'-separated part of a name (from 0), else ''
        private static string Segment(string s, int n)
        {
            var parts = s.Split('_');
            return n < parts.Length ? parts[n] : "";
        }

        // Whether list a is a better home than list b for the item: fewer name parts (the plain material list), then the
        // material named in the item's EditorID, then alphabetical
        private static bool Better(string a, string b, string itemID)
        {
            if (Underscores(a) != Underscores(b)) return Underscores(a) < Underscores(b);
            bool aNamed = Named(a, itemID), bNamed = Named(b, itemID);
            if (aNamed != bNamed) return aNamed;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool Named(string list, string itemID)
        {
            string material = Segment(list, 2);
            return material.Length > 0 && itemID.Contains(material, StringComparison.OrdinalIgnoreCase);
        }
    }
}
