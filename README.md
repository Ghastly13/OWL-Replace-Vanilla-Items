# OWL Replace Vanilla Items

A Synthesis patcher, ported from the xEdit script `OWL_Replace_Vanilla_Items.pas`. Every vanilla weapon, armor or ammo
that Open World Loot hands out through one of its lists, found in a mod's leveled lists, containers, outfits or NPC
inventories, is replaced by that list, the way Open World Loot does it for the game's own records. A mod's loot then
follows OWL's tiers and picks up the items added to OWL's lists: an Iron Sword in a mod's bandit chest becomes
`OWL_Weapon_Iron_Sword`, Steel Armor on its guards `OWL_Armor_Steel_Cuirass`.

The same goes for the vanilla items other mods' SkyPatcher INIs add to records (see below).

The changes are written as SkyPatcher INIs; the patch plugin gets no records.

## Which list

The `OWL_Armor_` / `OWL_Weapon_` list in `Open World Loot.esp` (its winning version) that holds the item. Where several
do: the plain material list (`OWL_Armor_Iron_Cuirass`, not `..._VanillaLike`), then the one whose material is in the
item's EditorID (`SteelDagger` -> `OWL_Weapon_Steel_Dagger`, not `..._Alikr_...`), and from a themed list up to the list
holding it that it is named after (`OWL_Armor_Fur_Cuirass_Vanilla` -> `OWL_Armor_Fur_Cuirass`). The map is read from
`Open World Loot.esp` on every run.

**Outfit lists.** Open World Loot's `OWL_Outfit_<Material>_Simple`, `_NoHelm` and `_Full` lists each pick one of their
sets, use-all lists of exact pieces (boots and cuirass; with the gauntlets; with the helmet too). Where an outfit holds
every piece of a set, the pieces are replaced by the set's outfit list: hide boots and hide armor become
`OWL_Outfit_Hide_Simple`, as one line (`formsToReplace=ArmorHideBoots=OWL_Outfit_Hide_Simple:formsToRemove=
ArmorHideCuirass`). Larger sets go first; the outfit's other pieces are replaced one by one. Only outfits: in NPC
inventories, containers and leveled lists every piece is replaced by its own list. A set under several outfit lists goes to the one named after a theme of the record (ThalmorElven for the Thalmor), else the
one of the set's own material (the Studded sets, also under Scaled, go to Studded), else the one its pieces' slots name.
Setting "Outfit lists".

**Themed lists.** Open World Loot also has lists for groups, named after them (`OWL_Armor_HideStudded_Cuirass_Stormcloak`,
`OWL_Weapon_Elven_Sword_Thalmor`, `OWL_Armor_Iron_Cuirass_Town`), which it gives the vanilla gear lists of those groups.
A record that matches a rule of the "Themed lists: rules" setting gets the theme's list for an item where Open World
Loot has one, else the plain list. The theme's list is the plain list's name with `_<Theme>`, else a list with the theme
in the material's place that holds the item itself or its plain list: `OWL_Weapon_Alikr_Dagger` holds the iron and steel
daggers and `OWL_Weapon_Alikr_2H` the iron and steel greatsword lists, so an Alik'r's steel dagger and iron greatsword go
there, while an Alik'r's ebony dagger stays with `OWL_Weapon_Ebony_Dagger`. A rule is
`Theme = kind patterns; kind patterns`, with these kinds:

- `faction`: the NPC, or the NPC template it takes its factions from, is a member (rank 0 or more) of a matching faction;
- `class`, `race`: the NPC's, or its traits template's;
- `npc`: the NPC's own EditorID;
- `worldspace`, `location`: every placed copy known of the record is in a matching worldspace, or a matching location
  or one inside it. A placed NPC counts for the NPCs its template chain leads to, through leveled lists too.

Patterns are EditorIDs, `*` standing for any text. The defaults, in order:

| Theme | Who |
|---|---|
| Stormcloak, Thalmor, Penitus, Vigilant, Afflicted, SilverHand, Vampire, Warlock | members of their factions |
| Boss | NPCs with Boss in their EditorID or class (bandit chiefs and other bosses) |
| Bandit, Thief, Whiterun, Blacksmith | bandits, the Thieves Guild, Whiterun's guards, blacksmiths |
| Mage | mage classes (`*Mage*`, sorcerers, spellswords, battlemages, witchblades, nightblades, mystics, wizards) |
| Orc | the Orc race (`OrcRace`: vampire Orcs, `OrcRaceVampire`, are left out) |
| Morrowind | NPCs, outfits and containers in Solstheim (or a worldspace or location named Morrowind) |
| Town | townsfolk: members of the town factions (`Town*Faction`) |

A record gets every theme it matches, and each item the first of them Open World Loot has a list for: a bandit chief's
weapons come from the Bandit lists, its armor from the Boss ones. An outfit takes the themes every NPC wearing it has (by
default or to sleep, its own or its inventory template's); a container those every placed copy has, from its owner (its
own, else its cell's: an NPC's themes, a faction's own, else those all its members share) and from where it is, or those
of the faction whose merchant chest it is (a blacksmith's chest: its merchant is a blacksmith). Placed copies are read
from the plugins patched and the plugins that add the containers, NPCs and outfits' wearers that hold items to replace.

**Alik'r lists.** The Alik'r, the Redguard warriors hunting in Skyrim, get Open World Loot's Alik'r lists for the items
those hold, as Open World Loot gives the game's own (`EncAlikr...`, `MS08Alikr...`) in its INIs: an iron or steel dagger
becomes `OWL_Weapon_Alikr_Dagger`, an iron or steel greatsword `OWL_Weapon_Alikr_2H`, a scimitar `OWL_Weapon_Alikr_1H`.
Other weapons (ebony, war axes, battleaxes) keep their plain lists: the Alik'r lists hold none of them. A mod's Alik'r are found as members of the `MS08Alikr`
factions or by Alikr in their EditorID (the Creation Club's Dead Man's Dread: `ccBGSSSE031_LvlAlikrMissileM`). This is a
built-in first rule, `Alikr = faction MS08Alikr*; npc *Alikr*`, which applies with "Themed lists" on or off; a rule of
your own for Alikr takes its place. Setting "Alik'r lists".

## Items other mods' SkyPatcher INIs add

Mods can add items to records without a plugin, through SkyPatcher INIs: `addToLLs`, `addOnceToLLs`, `addToContainers`,
`addOnceToContainers`, `formsToAdd` (outfits), `objectsToAdd`, `addOnceToInventory`, and what `formsToReplace`,
`replaceInContainers` and `objectsToReplace` put in. The vanilla items Open World Loot hands out that the INIs outside
Open World Loot's folders add are replaced too, in the records of every plugin, the base game's included, and go through
the same checks as a plugin's items:

- unique NPCs, and the outfits unique NPCs wear, are skipped;
- themed lists and outfit lists apply (a bandit given an Iron Sword gets `OWL_Weapon_Iron_Sword_Bandit`);
- an item the record already holds is kept, as replacing it would replace that one too; a record the INI itself clears
  first (`clear=true:formsToAdd=...`) holds nothing;
- an item an INI SkyPatcher applies later clears, replaces or removes is left to it. SkyPatcher applies shallower INIs
  first, then by path: Open World Loot's main INIs (`OWL\Main\...`) come after an INI directly in a mod's folder, so the
  lists they clear and refill are left to them.

The replacements go to an INI named after the source, so it loads exactly when the source does (only with its plugin
active, for an INI named after one), and nested in `zz` folders one level deeper, so SkyPatcher applies it after the
source. What Open World Loot's own INIs, and its patches for other mods, add is its choice and is left as it is. Setting
"Items other mods' SkyPatcher INIs add".

## What is left alone

- In a record the plugin overrides (a vanilla chest it adds to), an item the original record already has: SkyPatcher
  replaces every instance of the item in the record, so replacing it would change the game's own loot too.
- Open World Loot's own records (its lists): what a mod adds to them, by plugin or INI, is added to its loot.
- NPCs flagged Unique (in the version that wins), and the outfits any unique NPC wears, by default or to sleep (its own,
  or those of the NPC template it takes its inventory from). Both are settings.
- Enchanted items, uniques and clothing: they are in no OWL list.
- Open World Loot's own plugins: every plugin with Open World Loot in its name (`Open World Loot.esp`,
  `Open World Loot - Heavy Armory Patch.esp`, ...), whatever the settings say.
- By default, Open World Loot's masters (the base game and the Creation Club plugins it requires), which its own INIs
  cover.
- What the SkyPatcher INIs already installed do: Open World Loot's and its patches for other mods (in
  `SKSE\Plugins\SkyPatcher\<type>\OWL\...`), and other mods' (WACCF's, ...). A record one clears gets nothing more from
  its plugins, and an item one replaces or removes in a record (by form, by count or by keyword) is not replaced there.
  Plugins with such patches are patched around them. Only lines that name their records are read: by form
  (`filterByLLs`, `filterByContainers`, `filterByOutfits`, `filterByNpcs`) or by EditorID (`filterByEditorIdContains`,
  `...Or`, `...Excluded`). An INI named after a plugin counts only with that plugin active, as SkyPatcher reads it only
  then. This patcher's own INIs and those of the xEdit script `OWL_Replace_Vanilla_Items.pas`, which they replace, are not
  read.

Everything replaced, kept and skipped is listed in the Synthesis log and in `OWL replacements.txt` in the patcher's data
folder.

## Output

One INI per plugin and record type, named after the plugin so it loads only with it:

    SKSE\Plugins\SkyPatcher\leveledList\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini   filterByLLs=...:formsToReplace=item~list
    SKSE\Plugins\SkyPatcher\container\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini     filterByContainers=...:replaceInContainers=item~list
    SKSE\Plugins\SkyPatcher\outfit\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini        filterByOutfits=...:formsToReplace=item=list
    SKSE\Plugins\SkyPatcher\npc\OWL\Patches\OWL Replace Vanilla Items\<plugin>.ini           filterByNpcs=...:objectsToReplace=item~list

The replacements of what another mod's INI adds go in the same folders, named after that INI (`WACCF_Outfits.ini`,
and nested in one `zz` folder more for each folder past two the source is in: `zz\<name>.ini` for `Mod\A\B\<name>.ini`).

They are rewritten on every run (a file whose text is the same is left untouched), and those no longer written are
removed. Under Mod Organizer they land in Overwrite (or the mod Synthesis's new files go to).

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
