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
        [SynthesisTooltip("Patch only these plugins' records. Empty: every plugin in the load order but Open World Loot's own plugins (every plugin with Open World Loot in its name), its masters (the base game and the Creation Club plugins it requires, which its own INIs cover) and the plugins below. A plugin listed here is patched even if it is one of Open World Loot's masters. Plugins SkyPatcher INIs already patch (Open World Loot's patches, other mods') are patched too, around what those INIs do: records they clear, and items they replace or remove, are left to them. Also limits the records whose items other mods' SkyPatcher INIs add are replaced in.")]
        public List<ModKey> OnlyPlugins { get; set; } = new();

        [SynthesisOrder]
        [SynthesisSettingName("Leave out these plugins")]
        [SynthesisTooltip("Plugins whose records are never patched, for their own items or those other mods' SkyPatcher INIs add to them.")]
        public List<ModKey> SkippedPlugins { get; set; } = new();

        [SynthesisOrder]
        [SynthesisSettingName("Items other mods' SkyPatcher INIs add")]
        [SynthesisTooltip("Also replace the vanilla items other mods' SkyPatcher INIs (outside Open World Loot's folders) add to leveled lists, containers, outfits and NPCs, or put in by replacing: WACCF's additions to vanilla outfits, ... In the records of every plugin, Open World Loot's masters included, with the same checks: unique NPCs and the outfits they wear skipped, themed lists, items the record already holds kept, and what INIs SkyPatcher applies later replace or remove left to them. Written to an INI named after the source, so it loads only when the source does.")]
        public bool SkyPatcherAdditions { get; set; } = true;

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
        [SynthesisSettingName("Outfit lists")]
        [SynthesisTooltip("Where an outfit holds every piece of one of the sets in Open World Loot's outfit lists, replace the pieces with that outfit list: hide boots and hide armor become OWL_Outfit_Hide_Simple; with the bracers too, OWL_Outfit_Hide_NoHelm; with the helmet as well, OWL_Outfit_Hide_Full. Larger sets first. Only outfits: in NPC inventories, containers and leveled lists pieces are replaced one by one, as are an outfit's other pieces.")]
        public bool UseOutfitLists { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Themed lists")]
        [SynthesisTooltip("Give a record that matches a rule below the rule's Open World Loot list for an item, where Open World Loot has one (OWL_Armor_HideStudded_Cuirass_Stormcloak for a Stormcloak's hide armor), as Open World Loot does for the vanilla gear lists of those groups. An outfit takes the themes every NPC that wears it has; a container those every placed copy has, from its owner and from where it is, or its merchant's.")]
        public bool UseThemedLists { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Themed lists: rules")]
        [SynthesisTooltip("Theme = kind patterns; kind patterns. The theme is the end of Open World Loot's themed lists' names (OWL_..._Stormcloak), or what stands in a list's material place where that list holds the item itself or its plain list (OWL_Weapon_Alikr_Dagger, OWL_Weapon_Alikr_2H). Kinds: faction (the NPC, or the template it takes its factions from, is a member), class and race (the NPC's, or its traits template's), npc (the NPC's EditorID), worldspace and location (every placed copy known is there, or in a location inside it). Patterns are EditorIDs, * for any text. A record gets every theme it matches, and each item the first of them, in this order, Open World Loot has a list for. Open World Loot's other themes: CalcelmoEmployee, Swashbuckler.")]
        public List<string> ThemeRules { get; set; } = new()
        {
            "Stormcloak = faction CWSonsFaction",
            "Thalmor = faction ThalmorFaction",
            "Penitus = faction PenitusOculatusFaction",
            "Vigilant = faction VigilantOfStendarrFaction",
            "Afflicted = faction DA13AfflictedFaction",
            "SilverHand = faction SilverHandFaction",
            "Vampire = faction VampireFaction, DLC1VampireFaction",
            "Warlock = faction WarlockFaction",
            "Boss = npc *Boss*; class *Boss*",
            "Bandit = faction BanditFaction",
            "Thief = faction ThievesGuildFaction",
            "Whiterun = faction GuardFactionWhiterun",
            "Blacksmith = faction JobBlacksmithFaction",
            "Mage = class *Mage*, *Sorcerer*, *Spellsword*, *Battlemage*, *Witchblade*, *Nightblade*, *Mystic*, *Wizard*",
            "Orc = race OrcRace",
            "Morrowind = worldspace DLC2SolstheimWorld, *Morrowind*; location DLC2SolstheimLocation, *Morrowind*",
            "Town = faction Town*Faction",
        };

        [SynthesisOrder]
        [SynthesisSettingName("Alik'r lists")]
        [SynthesisTooltip("Give the Alik'r (the Redguard warriors hunting in Skyrim: members of the MS08Alikr factions, or NPCs with Alikr in their EditorID, such as the Creation Club's Cyrus quest's) Open World Loot's Alik'r lists for the items those hold, as Open World Loot does for the game's own: an iron or steel dagger becomes OWL_Weapon_Alikr_Dagger, an iron or steel greatsword OWL_Weapon_Alikr_2H. Works as a first themed-list rule, \"Alikr = faction MS08Alikr*; npc *Alikr*\", with \"Themed lists\" on or off; a rule of your own for Alikr takes its place.")]
        public bool AlikrLists { get; set; } = true;

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
