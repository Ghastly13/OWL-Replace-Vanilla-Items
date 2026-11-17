using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace OWLReplaceVanillaItems
{
    // Unique NPCs, and the outfits they wear: named characters and followers keep the gear they were given
    public sealed class UniqueWearers
    {
        // Outfit -> EditorIDs of the unique NPCs that wear it
        private readonly Dictionary<FormKey, SortedSet<string>> _wearers = new();

        public int OutfitCount => _wearers.Count;

        public static bool IsUnique(INpcGetter npc) => npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Unique);

        // Every outfit a unique NPC of the load order wears, by default or to sleep (as the winning versions have them):
        // its own, or where it takes its inventory from an NPC template, that template's. Through a leveled template it
        // wears what that list's NPCs do, which is not tracked.
        public UniqueWearers(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var linkCache = state.LinkCache;
            foreach (var npc in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                if (!IsUnique(npc)) continue;
                var wearer = npc;
                bool leveled = false;
                for (int depth = 0; depth < 10; depth++)
                {
                    if (!wearer.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Inventory) || wearer.Template.IsNull) break;
                    if (!linkCache.TryResolve<INpcSpawnGetter>(wearer.Template.FormKey, out var template)) break;
                    if (template is not INpcGetter next)
                    {
                        leveled = true;
                        break;
                    }
                    wearer = next;
                }
                if (leveled) continue;
                Add(wearer.DefaultOutfit.FormKeyNullable, npc);
                Add(wearer.SleepingOutfit.FormKeyNullable, npc);
            }
        }

        private void Add(FormKey? outfit, INpcGetter npc)
        {
            if (outfit == null || outfit.Value.IsNull) return;
            if (!_wearers.TryGetValue(outfit.Value, out var names)) _wearers[outfit.Value] = names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            names.Add(npc.EditorID ?? npc.FormKey.ToString());
        }

        // "worn by the unique NPC(s) A, B, C and N more" when a unique NPC wears the outfit, else null
        public string? Describe(FormKey outfit)
        {
            if (!_wearers.TryGetValue(outfit, out var names)) return null;
            string text = "worn by the unique NPC" + (names.Count > 1 ? "s " : " ") + string.Join(", ", names.Take(3));
            if (names.Count > 3) text += " and " + (names.Count - 3) + " more";
            return text;
        }
    }
}
