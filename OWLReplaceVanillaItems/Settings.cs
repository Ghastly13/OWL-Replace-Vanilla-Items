using System.Collections.Generic;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;

namespace OWLReplaceVanillaItems
{
    // The patcher's settings, edited on its Settings page in Synthesis and saved as settings.json in its data folder
    public class Settings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Only these plugins")]
        [SynthesisTooltip("Patch only these plugins' records. Empty: every plugin in the load order but Open World Loot's own plugins (every plugin with Open World Loot in its name), its masters (the base game and the Creation Club plugins it requires, which its own INIs cover) and the plugins below. A plugin listed here is patched even if it is one of Open World Loot's masters. Plugins Open World Loot already has patches for are patched too, around what those patches do: records they clear, and items they replace or remove, are left to them.")]
        public List<ModKey> OnlyPlugins { get; set; } = new();

        [SynthesisOrder]
        [SynthesisSettingName("Leave out these plugins")]
        [SynthesisTooltip("Plugins whose records are never patched.")]
        public List<ModKey> SkippedPlugins { get; set; } = new();

        [SynthesisOrder]
        [SynthesisSettingName("Leveled lists")]
        [SynthesisTooltip("Replace vanilla items in the plugins' leveled item lists (formsToReplace).")]
        public bool LeveledLists { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Containers")]
        [SynthesisTooltip("Replace vanilla items in the plugins' containers (replaceInContainers). Leveled lists in containers need iAllowLeveledListsAddedToContainers=1 in SkyPatcher.ini, SkyPatcher 7.1.3's default.")]
        public bool Containers { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Outfits")]
        [SynthesisTooltip("Replace vanilla items in the plugins' outfits (formsToReplace).")]
        public bool Outfits { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("NPCs")]
        [SynthesisTooltip("Replace vanilla items in the plugins' NPC inventories (objectsToReplace).")]
        public bool Npcs { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Skip unique NPCs")]
        [SynthesisTooltip("Leave NPCs flagged Unique (in the version that wins: named characters, followers) alone: they keep the gear they were given.")]
        public bool SkipUniqueNpcs { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Skip outfits unique NPCs wear")]
        [SynthesisTooltip("Leave alone the outfits any unique NPC in the load order wears, by default or to sleep: its own, or those of the NPC template it takes its inventory from.")]
        public bool SkipOutfitsOfUniqueNpcs { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Write forms by EditorID")]
        [SynthesisTooltip("Write forms by EditorID where SkyPatcher can be given one: made only of letters, digits and _, and had by no other leveled list, container, outfit, NPC, weapon, armor or ammo in any version. Else, or with this off, as Plugin.esp|FormID. SkyPatcher 8.0 reads EditorIDs in every key these INIs use; for most record types it needs powerofthree's Tweaks with Load EditorIDs on. Turn it off with an older SkyPatcher, whose formsToReplace took only FormIDs.")]
        public bool UseEditorIDs { get; set; } = true;
    }
}
