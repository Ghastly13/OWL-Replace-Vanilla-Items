# OWL Replace Vanilla Items

A Synthesis patcher, ported from the xEdit script `OWL_Replace_Vanilla_Items.pas`. Every vanilla weapon, armor or ammo
that Open World Loot hands out through one of its lists, found in a mod's leveled lists, containers, outfits or NPC
inventories, is replaced by that list, the way Open World Loot does it for the game's own records. A mod's loot then
follows OWL's tiers and picks up the items added to OWL's lists: an Iron Sword in a mod's bandit chest becomes
`OWL_Weapon_Iron_Sword`, Steel Armor on its guards `OWL_Armor_Steel_Cuirass`.

The changes are written as SkyPatcher INIs; the patch plugin gets no records.

## Which list

The `OWL_Armor_` / `OWL_Weapon_` list in `Open World Loot.esp` (its winning version) that holds the item. Where several
do: the plain material list (`OWL_Armor_Iron_Cuirass`, not `..._VanillaLike`), then the one whose material is in the
item's EditorID (`SteelDagger` -> `OWL_Weapon_Steel_Dagger`, not `..._Alikr_...`), and from a themed list up to the list
holding it that it is named after (`OWL_Armor_Fur_Cuirass_Vanilla` -> `OWL_Armor_Fur_Cuirass`). The map is read from
`Open World Loot.esp` on every run.

## What is left alone

- In a record the plugin overrides (a vanilla chest it adds to), an item the original record already has: SkyPatcher
  replaces every instance of the item in the record, so replacing it would change the game's own loot too.
- NPCs flagged Unique (in the version that wins), and the outfits any unique NPC wears, by default or to sleep (its own,
  or those of the NPC template it takes its inventory from). Both are settings.
- Enchanted items, uniques and clothing: they are in no OWL list.
- By default, `Open World Loot.esp`, its masters (the base game and the Creation Club plugins it requires, which its own
  INIs cover), and plugins Open World Loot already has patches for: a `Plugin.esp.ini` in one of its folders
  (`SKSE\Plugins\SkyPatcher\<type>\OWL\...`), from Open World Loot or another mod. INIs written by this patcher or by the
  xEdit scripts `OWL_Replace_Vanilla_Items.pas` and `OWL_Add_Items_To_Lists.pas` do not count.

Everything replaced, kept and skipped is listed in the Synthesis log and in `OWL replacements.txt` in the patcher's data
folder.

## Output

One INI per plugin and record type, named after the plugin so it loads only with it:

    SKSE\Plugins\SkyPatcher\leveledList\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini   filterByLLs=...:formsToReplace=item~list
    SKSE\Plugins\SkyPatcher\container\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini     filterByContainers=...:replaceInContainers=item~list
    SKSE\Plugins\SkyPatcher\outfit\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini        filterByOutfits=...:formsToReplace=item=list
    SKSE\Plugins\SkyPatcher\npc\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini           filterByNpcs=...:objectsToReplace=item~list

They are rewritten on every run (a file whose text is the same is left untouched), and those of plugins no longer patched
are removed. Under Mod Organizer they land in Overwrite (or the mod Synthesis's new files go to).

The folder is the patcher's own, so the leveledList INIs of `OWL_Add_Items_To_Lists.pas`
(`leveledList\OWL\Patches\<plugin>\<plugin>.ini`) are never touched. INIs the xEdit script `OWL_Replace_Vanilla_Items.pas`
wrote for a plugin this patcher patches are listed in the log: remove them, or their lines keep applying alongside these
(any for unique NPCs included).

Forms are written by EditorID where SkyPatcher can be given one: made only of letters, digits and `_`, and had by no other
leveled list, container, outfit, NPC, weapon, armor or ammo in any version (SkyPatcher's EditorID lookup is one map for
all record types, and a record a mod renamed may still answer to its old EditorID). Other forms are written as
`Plugin.esp|FormID` without leading zeros (`Skyrim.esm|12EB7`), each listed in the log.

## Installing

In Synthesis, add a Git Repository patcher with the URL `https://github.com/Ghastly13/OWL-Replace-Vanilla-Items` and pick
the `OWLReplaceVanillaItems` project.

## Requirements

- Open World Loot (SkyPatcher Edition) and SkyPatcher. SkyPatcher 8.0 reads EditorIDs in every key these INIs use,
  formsToReplace included; with an older one, turn off "Write forms by EditorID".
- powerofthree's Tweaks with Load EditorIDs on (its default), for SkyPatcher to find leveled lists, weapons, armor, NPCs
  and outfits by EditorID.
- For leveled lists put into containers: `iAllowLeveledListsAddedToContainers=1` in `SkyPatcher.ini`, the default since
  SkyPatcher 7.1.3.

## Testing outside Synthesis

    OWLReplaceVanillaItems.exe run-patcher --DataFolderPath <data folder> --ExtraDataFolder <settings folder>
        --GameRelease SkyrimSE --ModKey Synthesis.esp --LoadOrderFilePath <plugins.txt> --LoadOrderIncludesCreationClub
        --OutputPath <out>\Synthesis.esp --PatcherName Test

The INIs are written into the data folder given.
