<!-- Snapshot of the living design doc https://claude.ai/artifact/DmZxu6jsgd5qVrcYTtZhTz (rev 35, read 2026-11-11).
     The living doc is canonical: change it first, then refresh this snapshot. Placeholder names in it
     ("Closet Space", ClosetSpace, CS_, yourname.closetspace) map to Tidy Threads: TidyThreads, TT_, josh9817.tidythreads. -->

# Apparel Storage for Dressers & Wardrobes — RimWorld 1.6 Mod Design

2026-11-10 · Josh Toledo

## Overview

The mod turns every dresser (built, already placed in a save, traded or generated) into apparel-only storage holding 20 items, and provides wardrobes holding 30, both defaulting to **Critical** priority. It targets RimWorld 1.6 with no DLC, and depends on Harmony and Adaptive Storage Framework (ASF).

**Wardrobes:** vanilla 1.6 has none, so the mod ships its own copy of Vanilla Furniture Expanded's (VFE) wardrobe that loads only when VFE is not active. With VFE active, VFE's own wardrobe is converted instead. Both use the same defName, so one patch converts whichever is loaded.

| Furniture | Size | Capacity (total) | Per cell (`maxItemsInCell`) | Filter | Default priority |
|---|---|---|---|---|---|
| Dresser (vanilla, converted) | 2×1 | 20 apparel | 10 | Apparel only, locked | Critical |
| Wardrobe (VFE's if active, else this mod's copy) | Same as VFE's; 2×1 assumed, unverified | 30 apparel | 30 ÷ cells (15 if 2×1) | Apparel only, locked | Critical |

What stays unchanged: the dresser's +0.05 bed comfort facility (6-tile radius), its quality, beauty, cost and research. Out of scope: per-pawn ownership of dressers, the Odyssey outfit stand, and RimWorld 1.7.

## Verification status

Gameplay facts below come from the wiki pages opened on 2026-11-10; anything marked Unverified must be checked against decompiled 1.6 code, the game's `Data/Core/Defs` folder, or the ASF/Harmony files.

| Fact | Status | Source |
|---|---|---|
| Dresser is vanilla Core: 2×1, has quality, minifiable, needs Complex furniture, 50 stuff, +0.05 comfort to beds within 6 tiles | Verified | [Dresser](https://rimworldwiki.com/wiki/Dresser) (edited 12 May 2026) |
| No wardrobe exists in Core or any DLC (no wiki page; not in the furniture list) | Verified | [Furniture](https://rimworldwiki.com/wiki/Furniture) (edited 26 Nov 2025) |
| Wardrobes come from mods, e.g. Vanilla Furniture Expanded (royal wardrobe; illuminated wardrobe in Spacer) | Community, lower confidence | [VFE Spacer changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/2028381079) |
| Outfit stand is Odyssey DLC, 1×1, stores one outfit | Verified, out of scope | [Outfit stand](https://rimworldwiki.com/wiki/Outfit_stand) (edited 11 Aug 2026) |
| Vanilla shelf: 3 items per cell, default priority Preferred, linkable storage groups, no deterioration, hides stored items' beauty | Verified | [Shelf](https://rimworldwiki.com/wiki/Shelf) (edited 17 Nov 2025) |
| Priority levels include Critical; haulers move items from lower to higher priority storage | Verified | [Stockpile zone](https://rimworldwiki.com/wiki/Stockpile_zone) (edited 25 Nov 2025) |
| ASF supports 1.6 (tags 1.4–1.6, updated 14 Jun 2025) and requires Harmony | Verified | [ASF Workshop page](https://steamcommunity.com/workshop/filedetails/?id=3033901359) |
| ASF thingClass is `AdaptiveStorage.ThingClass`; abstract parents `AdaptiveStorageBase` and `AdaptiveStorageMinifiableBase` | Verified (docs) | [ASF docs index](https://github.com/bbradson/Adaptive-Storage-Framework/blob/main/Docs/modules/ROOT/pages/index.adoc) |
| ASF uses vanilla `maxItemsInCell`; its Extension adds `maxItemsByCell`; ASF claims mid-save thingClass updating of defs | Changelog only; exact XML shape unverified | [ASF changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/3033901359) |
| A prior mod doing this tells users to destroy existing dressers before adding it | Verified (community) | [Dressers Hold Clothing (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2584894865) |
| Dresser defName `Dresser`, its ParentName, current thingClass, research defName `ComplexFurniture`, category `Apparel` | Unverified | Check `Data/Core/Defs/ThingDefs_Buildings/*Furniture*.xml` |
| Package IDs `brrainz.harmony` and ASF's (Harmony ID in a stack trace reads `adaptive.storage.framework`) | Unverified | Each mod's `About/About.xml` |

**1.7 warning:** the ASF author says ASF already runs on the 1.7 beta. Nothing about 1.7 is used in this design.

**Wardrobe decision****: resolved.** The mod duplicates VFE's wardrobe for players without VFE and converts VFE's own when VFE is active. The switch is a `LoadFolders.xml` entry with `IfModNotActive`, which the wiki documents for 1.6 ([Mod Folder Structure](https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure), edited 24 Oct 2026). Our copy keeps VFE's defName so saves survive VFE being added or removed.

**Before shipping VFE's files:** no license for Vanilla Expanded's textures or XML turned up in research, and community updates of VE mods state they were made with permission. Ask the Vanilla Expanded team before redistributing their texture or description, or draw your own texture and write your own description and stats.

## Decisions (2026-11-10)

Ten decisions settle the open questions; where a section below differs, these win. The repo's `TODO.md` records the verification behind them.

| # | Decision | Replaces |
| --- | --- | --- |
| 1 | Defs and patches live in root `Defs/` and `Patches/`. `LoadFolders.xml` loads `/`, `1.6` (assembly only) and, without VFE, `NoVFE` + `NoVFE/1.6`. The shared package and lint tools are extended for `LoadFolders.xml` and `NoVFE/`. | The `1.6/Defs` and `1.6/Patches` paths in Mod folder structure |
| 2 | The test harness gains three general changes, ported back to the modding center: required dependencies (ASF) load before the mod; a save made without the mod loads with it (T7, T12–T14); a save made with VFE on loads with VFE off, and the reverse (T15, T16). | — |
| 3 | Our wardrobe keeps bed comfort: a `CompProperties_Facility` (Comfort +0.05, maxDistance 6, maxSimultaneous 1) and a bed `linkableFacilities` patch, both under `NoVFE/` so they don't double up with VFE's own link. | "Plain furniture" with no comps, for the comfort facility only |
| 4 | We draw our own wardrobe texture and write our own description and stats. Only the defName `Table_Wardrobe` and the 2×1 size are shared with VFE. | Copying VFE's texture, description and stats |
| 5 | `incompatibleWith` is `Mlie.DressersHoldClothing`, the packageId in that mod's own About.xml (Workshop 2584894865). | `mlie.dressersholdclothing` (unverified) |
| 6 | The pre-mod save for T7 and T12–T14 is built by a separate test-only helper mod, loaded without Tidy Threads. It references nothing from the mod and never ships in the release package. | — |
| 7 | The goal is apparel storage; bed comfort from dressers and wardrobes doesn't matter. Scenarios assert storage behaviour, not the comfort link. The comfort facility itself stays (decision 3). | T2's bed-link check |
| 8 | T12 checks the room role only, not comfort. Dressers and wardrobes are independent of beds: usually paired with them, but a storeroom may hold only dressers and wardrobes. | T12's comfort half |
| 9 | T12 runs on a fresh map with fixture rooms: a room with a bed, 2 dressers and a wardrobe is a Bedroom, and a room with only 2 dressers and a wardrobe is a Storeroom. A converted dresser has the same class as a new one, so no pre-mod save is needed. | T12's setup "in the T7 save" |
| 10 | VFE runs (T15–T17) use their own error baseline: Harmony + Core + ASF + VE Framework + VFE, without Tidy Threads. The harness picks the baseline that matches the launched mod set; this mod's own errors always count. | — |
| 11 | Before a dresser or wardrobe is deconstructed or uninstalled (reinstalling uninstalls first), all its stored items are moved out (TT-001). Vanilla's own overflow drop ignores failed placements and silently loses items when no cell within 12.9 cells is free. This gives C# a second job; T13 runs on a crowded site that reproduces the loss. Destruction by fire, raids or roof collapse is emptied the same way (2026-11-11). | User, 2026-11-10 |

Verified facts: the VFE wardrobe defName is `Table_Wardrobe` (size 2×1); packageIds are `brrainz.harmony`, `adaptive.storage.framework` and `VanillaExpanded.VFECore`. ASF already converts a saved `Building` to its class, so Patch A isn't needed; Patch B's null check can't work as written, because ASF sets `settings` during loading.

## Dependencies and About.xml

Load order must be Core → Harmony → ASF → this mod. No DLC is required; nothing below references DLC content, so no `MayRequire` is needed.

| Dependency | Workshop ID | packageId | Required |
|---|---|---|---|
| [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077) | 2009463077 | `brrainz.harmony` (unverified) | Yes |
| [Adaptive Storage Framework](https://steamcommunity.com/workshop/filedetails/?id=3033901359) | 3033901359 | unverified — read ASF's About.xml | Yes |
| [Dressers Hold Clothing (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2584894865) | 2584894865 | unverified | Incompatible (patches the same dresser) |

File: `About/About.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>Closet Space</name>
  <author>YourName</author>
  <packageId>yourname.closetspace</packageId>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description>Dressers store 20 pieces of apparel and wardrobes store 30. Both default to Critical priority and only accept apparel. Safe to add to existing saves (see notes).</description>
  <modDependencies>
    <li>
      <packageId>brrainz.harmony</packageId>
      <displayName>Harmony</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/2009463077</steamWorkshopUrl>
      <downloadUrl>https://github.com/pardeike/HarmonyRimWorld/releases/latest</downloadUrl>
    </li>
    <li>
      <!-- UNVERIFIED: copy the exact packageId from ASF's About/About.xml -->
      <packageId>adaptive.storage.framework</packageId>
      <displayName>Adaptive Storage Framework</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/3033901359</steamWorkshopUrl>
    </li>
  </modDependencies>
  <loadAfter>
    <li>brrainz.harmony</li>
    <li>adaptive.storage.framework</li>
  </loadAfter>
  <incompatibleWith>
    <!-- UNVERIFIED: packageId of Dressers Hold Clothing (Continued) -->
    <li>mlie.dressersholdclothing</li>
  </incompatibleWith>
</ModMetaData>
```

The name `Closet Space` is a placeholder from the name list below. Mods that strip default shelf storage reportedly must load after dresser-storage mods, so add them to `loadBefore` if you hit that conflict.

## Mod folder structure

All 1.6 content lives in a `1.6/` folder selected by `LoadFolders.xml`, so a future 1.7 build can sit beside it without touching 1.6 files.

```
ClosetSpace/
├── About/
│   ├── About.xml
│   └── Preview.png
├── LoadFolders.xml
├── 1.6/                              (always loaded)
│   ├── Assemblies/
│   │   └── ClosetSpace.dll          (do NOT ship 0Harmony.dll; the Harmony mod provides it)
│   ├── Defs/
│   │   └── AdaptiveStorage/
│   │       └── CS_GraphicsDefs.xml  (dresser + wardrobe; the wardrobe defName exists either way)
│   └── Patches/
│       ├── CS_Dresser.xml
│       └── CS_Wardrobe.xml          (converts whichever wardrobe def is loaded)
├── NoVFE/                            (loaded only when VFE is NOT active)
│   ├── 1.6/Defs/ThingDefs_Buildings/
│   │   └── CS_Wardrobe.xml          (plain furniture copy of VFE's wardrobe)
│   └── Textures/ClosetSpace/Furniture/
│       └── Wardrobe_north.png / _east / _south
├── Languages/English/Keyed/CS_Keys.xml
└── Source/ClosetSpace/ (C# project, not loaded by the game)
```

File: `LoadFolders.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.6>
    <li>/</li>
    <li>1.6</li>
    <!-- UNVERIFIED packageId: copy VFE Core's exact packageId from its About.xml.
         Our wardrobe copy and its textures load only when VFE is absent. -->
    <li IfModNotActive="VanillaExpanded.VFECore">NoVFE</li>
    <li IfModNotActive="VanillaExpanded.VFECore">NoVFE/1.6</li>
  </v1.6>
</loadFolders>
```

`NoVFE` holds the textures and `NoVFE/1.6` holds the def; both entries carry the same condition. After changing the mod list, check which files loaded with the dev-mode main-menu action Loaded Files For Mod.

## XML implementation

The dresser is changed with PatchOperations, never overridden, so other mods' dresser patches still apply. `fixedStorageSettings` locks the filter to Apparel (players can narrow it but never add non-apparel), and `defaultStorageSettings` sets Critical for every newly made dresser, including blueprints.

Capacity uses vanilla `maxItemsInCell`, which ASF builds on: 10 per cell × 2 cells = 20 for the dresser, 15 × 2 = 30 for the wardrobe. Apparel never stacks in vanilla, so one stack = one garment.

### Dresser patch

File: `1.6/Patches/CS_Dresser.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Patch>
  <!-- UNVERIFIED: defName "Dresser". Confirm in Data/Core/Defs. -->

  <!-- 1. Use ASF's storage class. thingClass may be inherited, so add it if absent. -->
  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="Dresser"]/thingClass</xpath>
    <match Class="PatchOperationReplace">
      <xpath>Defs/ThingDef[defName="Dresser"]/thingClass</xpath>
      <value><thingClass>AdaptiveStorage.ThingClass</thingClass></value>
    </match>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="Dresser"]</xpath>
      <value><thingClass>AdaptiveStorage.ThingClass</thingClass></value>
    </nomatch>
  </Operation>

  <!-- 2. Make sure a <building> node exists. -->
  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="Dresser"]/building</xpath>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="Dresser"]</xpath>
      <value><building /></value>
    </nomatch>
  </Operation>

  <!-- 3. Storage behaviour: 10 per cell, apparel only, Critical by default. -->
  <Operation Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="Dresser"]/building</xpath>
    <value>
      <maxItemsInCell>10</maxItemsInCell>
      <preventDeteriorationOnTop>true</preventDeteriorationOnTop>
      <ignoreStoredThingsBeauty>true</ignoreStoredThingsBeauty>
      <!-- Dressers and wardrobes link only with each other, never with shelves. -->
      <storageGroupTag>CS_ApparelFurniture</storageGroupTag>
      <fixedStorageSettings>
        <filter>
          <categories>
            <li>Apparel</li>
          </categories>
        </filter>
      </fixedStorageSettings>
      <defaultStorageSettings>
        <priority>Critical</priority>
        <filter>
          <categories>
            <li>Apparel</li>
          </categories>
        </filter>
      </defaultStorageSettings>
    </value>
  </Operation>

  <!-- 4. Storage tab. Lists merge with the parent's, so adding is safe. -->
  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="Dresser"]/inspectorTabs</xpath>
    <match Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="Dresser"]/inspectorTabs</xpath>
      <value><li>ITab_Storage</li></value>
    </match>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="Dresser"]</xpath>
      <value><inspectorTabs><li>ITab_Storage</li></inspectorTabs></value>
    </nomatch>
  </Operation>

  <!-- 5. Marker read by the C# converter (see C# implementation). -->
  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="Dresser"]/modExtensions</xpath>
    <match Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="Dresser"]/modExtensions</xpath>
      <value><li Class="ClosetSpace.ApparelStorageExtension"><capacity>20</capacity></li></value>
    </match>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="Dresser"]</xpath>
      <value><modExtensions><li Class="ClosetSpace.ApparelStorageExtension"><capacity>20</capacity></li></modExtensions></value>
    </nomatch>
  </Operation>
</Patch>
```

Because the dresser is not reparented onto `AdaptiveStorageBase`, any other fields that base sets must be copied into step 3 (Claude Code: diff ASF's `Defs/ThingDefBase.xml` against the vanilla Shelf def). If the dresser's parent already defines any of the step 3 fields, step 3 errors with a duplicate node; switch those to `PatchOperationReplace`.

### Wardrobe: this mod's copy (VFE not active)

File: `NoVFE/``1.6/Defs/ThingDefs_Buildings/CS_Wardrobe.xml`. Exactly one wardrobe def exists at any time, VFE's or ours, and both share VFE's defName. The copy is plain furniture; the wardrobe patch below adds storage to whichever one loaded, so there is one code path to test.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <!-- Loaded only when VFE is NOT active (LoadFolders.xml).
       VFE_WARDROBE_DEFNAME: replace with VFE's exact defName (all files).
       Plain furniture only: no storage fields (the patch adds them) and
       no VFE Framework classes or comps, which would require VFE. -->
  <!-- UNVERIFIED ParentName: use the vanilla Dresser's parent. -->
  <ThingDef ParentName="FurnitureWithQualityBase">
    <defName>VFE_WARDROBE_DEFNAME</defName>
    <label>wardrobe</label>
    <description>A tall cabinet for hanging clothes.</description>
    <graphicData>
      <!-- Our own namespaced path, so it never overrides VFE's texture. -->
      <texPath>ClosetSpace/Furniture/Wardrobe</texPath>
      <graphicClass>Graphic_Multi</graphicClass>
      <drawSize>(2,1)</drawSize>          <!-- copy VFE's -->
    </graphicData>
    <size>(2,1)</size>                     <!-- copy VFE's; sets per-cell capacity -->
    <rotatable>true</rotatable>
    <altitudeLayer>Building</altitudeLayer>
    <passability>PassThroughOnly</passability>
    <pathCost>42</pathCost>
    <fillPercent>0.5</fillPercent>
    <stuffCategories>
      <li>Metallic</li>
      <li>Woody</li>
      <li>Stony</li>
    </stuffCategories>
    <costStuffCount>75</costStuffCount>    <!-- copy VFE's -->
    <statBases>                            <!-- copy VFE's values -->
      <MaxHitPoints>160</MaxHitPoints>
      <WorkToBuild>3000</WorkToBuild>
      <Mass>25</Mass>
      <Flammability>1.0</Flammability>
      <Beauty>5</Beauty>
    </statBases>
    <designationCategory>Furniture</designationCategory>
    <researchPrerequisites>
      <li>ComplexFurniture</li>          <!-- UNVERIFIED; match VFE's -->
    </researchPrerequisites>
    <terrainAffordanceNeeded>Light</terrainAffordanceNeeded>
  </ThingDef>
</Defs>
```

Copy from VFE only what a player sees: defName, size, cost, stats, label. Skip its thingClass and any VFE Framework comps. Duplicate VFE's plain wardrobe, not the royal or illuminated ones, so no Royalty-only fields come along.

### Hiding stored items (ASF GraphicsDef)

A dresser should not show 20 shirts lying on top. ASF draws contents through a `GraphicsDef` whose `targetDef` points at the building; the field that suppresses item drawing is not documented in what could be read here.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <AdaptiveStorage.GraphicsDef>
    <defName>CS_Dresser_Graphics</defName>
    <targetDef>Dresser</targetDef>
    <!-- UNVERIFIED: add ASF's field(s) that hide item graphics.
         See Docs/modules/ROOT/pages/GraphicsDef.adoc in the ASF repo. -->
  </AdaptiveStorage.GraphicsDef>
</Defs>
```

Repeat with `targetDef` set to the wardrobe defName. It can stay in the always-loaded `1.6/` folder because that defName exists with or without VFE.

### Wardrobe patch (VFE's or ours)

File: `1.6/Patches/CS_``Wardrobe``.xml`, always loaded. It is the dresser patch with the defName swapped and 15 per cell. VFE's wardrobe appears not to be storage by itself (the older dresser mod had to add storage to it); if the current VFE already makes it storage, step 3 will report duplicate nodes and those lines must become `PatchOperationReplace`.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Patch>
  <!-- VFE_WARDROBE_DEFNAME: VFE's exact defName. Applies to VFE's def when VFE
       is active, and to our copy (same defName) when it is not. -->

  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/thingClass</xpath>
    <match Class="PatchOperationReplace">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/thingClass</xpath>
      <value><thingClass>AdaptiveStorage.ThingClass</thingClass></value>
    </match>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]</xpath>
      <value><thingClass>AdaptiveStorage.ThingClass</thingClass></value>
    </nomatch>
  </Operation>

  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/building</xpath>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]</xpath>
      <value><building /></value>
    </nomatch>
  </Operation>

  <Operation Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/building</xpath>
    <value>
      <!-- 30 total ÷ cell count. 15 assumes 2×1; recompute from VFE's size. -->
      <maxItemsInCell>15</maxItemsInCell>
      <preventDeteriorationOnTop>true</preventDeteriorationOnTop>
      <ignoreStoredThingsBeauty>true</ignoreStoredThingsBeauty>
      <storageGroupTag>CS_ApparelFurniture</storageGroupTag>
      <fixedStorageSettings>
        <filter><categories><li>Apparel</li></categories></filter>
      </fixedStorageSettings>
      <defaultStorageSettings>
        <priority>Critical</priority>
        <filter><categories><li>Apparel</li></categories></filter>
      </defaultStorageSettings>
    </value>
  </Operation>

  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/inspectorTabs</xpath>
    <match Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/inspectorTabs</xpath>
      <value><li>ITab_Storage</li></value>
    </match>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]</xpath>
      <value><inspectorTabs><li>ITab_Storage</li></inspectorTabs></value>
    </nomatch>
  </Operation>

  <Operation Class="PatchOperationConditional">
    <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/modExtensions</xpath>
    <match Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]/modExtensions</xpath>
      <value><li Class="ClosetSpace.ApparelStorageExtension"><capacity>30</capacity></li></value>
    </match>
    <nomatch Class="PatchOperationAdd">
      <xpath>Defs/ThingDef[defName="VFE_WARDROBE_DEFNAME"]</xpath>
      <value><modExtensions><li Class="ClosetSpace.ApparelStorageExtension"><capacity>30</capacity></li></modExtensions></value>
    </nomatch>
  </Operation>
</Patch>
```

Other VE wardrobes (royal, illuminated) can get the same treatment later: wrap each set of operations in a `PatchOperationConditional` on that defName, so it does nothing when the def is absent.

## C# implementation

C# has two jobs: converting dressers that were already in a save before the mod was added, and emptying a dresser or wardrobe before it is deconstructed, uninstalled or destroyed (decision 11). New dressers need no code; the XML patch covers them.

### Emptying before removal (TT-001)

When a storage building leaves the map, vanilla `Building.DeSpawn` keeps one item per cell and re-places each other item with `GenPlace.TryPlaceThing(Near)`, ignoring the result. With no usable cell within 12.9 cells, that item is lost with no error. A full dresser has 18 items to move and a wardrobe 28 (a shelf has 2), so packed storerooms and mountain bases hit it.

`EmptyBeforeRemoval.cs` empties our furniture (defs with `ApparelStorageExtension`, while spawned) first: each item goes Near as vanilla would, never onto the furniture's own cells; if that fails, the same search runs from grid points 18 cells apart over the whole map, nearest first. An item with nowhere to go on the whole map is put back and reported with a `[TidyThreads] ERROR`.

| Harmony prefix | Why no vanilla hook works | Covers |
| --- | --- | --- |
| `Building_Storage.Destroy`, mode `Deconstruct` | `Building.DeSpawn` drops the overflow before any comp or virtual we own runs, and ASF seals `DeSpawn` | The deconstruct job, god mode's instant deconstruct; modes `KillFinalize` and `KillFinalizeLeavingsOnly`: fire, raids, explosions, roof collapse (`Thing.Kill`) |
| `MinifyUtility.MakeMinified` | Same; minifying despawns with `DestroyMode.Vanish`, so `Destroy` never sees it | Uninstall job, reinstall (`Toils_Construct.UninstallIfMinifiable`), `GenSpawn.Refund` |

**Why existing dressers break.** A save file is like a packing list: each building is written as "an object of class *X*, using recipe *Dresser*". The XML patch changes the recipe, but old saves still say class *Building*, so on load the game rebuilds a plain building with storage settings it cannot use. The prior dresser mod avoided this by telling players to destroy dressers first; we instead correct the class name while the list is read.

Try the options in order; stop at the first that passes test T7.

| Option | What it does | Code needed | Risk |
|---|---|---|---|
| 1. ASF built-in | ASF's changelog claims mid-save thingClass updates of defs | None | Unverified behaviour; may not create storage settings |
| 2. Load-time class swap + settings init | Harmony postfixes swap `Building` → `AdaptiveStorage.ThingClass` while reading the save, then create Critical apparel settings | Patches A and B below | Low; preserves ID, quality, art, HP |
| 3. Respawn fallback | Destroys each old dresser after load and spawns a fresh one | MapComponent C | Loses art title and any references to the old thing ID |

### Shared files

File: `Source/ClosetSpace/ClosetSpaceMod.cs`

```csharp
using HarmonyLib;
using Verse;

namespace ClosetSpace
{
    // Marker on every def this mod turns into apparel storage.
    public class ApparelStorageExtension : DefModExtension
    {
        // Informational. Real capacity = building.maxItemsInCell × cell count.
        public int capacity = 20;
    }

    public class ClosetSpaceMod : Mod
    {
        public ClosetSpaceMod(ModContentPack content) : base(content)
        {
            new Harmony("yourname.closetspace").PatchAll();
        }
    }
}
```

### Patch A: swap the class while loading (Option 2)

```csharp
using System;
using System.Xml;
using HarmonyLib;
using Verse;

namespace ClosetSpace
{
    // UNVERIFIED for 1.6: method name and signature of
    // Verse.BackCompatibility.GetBackCompatibleType(Type, string, XmlNode).
    // Check decompiled Assembly-CSharp before building.
    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.GetBackCompatibleType))]
    public static class Patch_GetBackCompatibleType
    {
        public static void Postfix(XmlNode node, ref Type __result)
        {
            if (__result == null || node == null) return;
            string defName = node["def"]?.InnerText;
            if (defName.NullOrEmpty()) return;

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || !def.HasModExtension<ApparelStorageExtension>()) return;

            // Only upgrade an old base class (e.g. Building) to the def's new class.
            if (__result != def.thingClass && def.thingClass.IsSubclassOf(__result))
                __result = def.thingClass;
        }
    }
}
```

### Patch B: give converted dressers their storage settings (Option 2)

Old saves contain no storage settings for dressers, so they would load with none. This runs once per old dresser; dressers saved with the mod already have settings and are skipped, so a player's later priority change survives reloads.

```csharp
using HarmonyLib;
using RimWorld;
using Verse;

namespace ClosetSpace
{
    // UNVERIFIED: Building_Storage.settings is a public field in 1.6, and
    // ASF's ThingClass calls base.ExposeData().
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.ExposeData))]
    public static class Patch_InitLegacySettings
    {
        public static void Postfix(Building_Storage __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (__instance.settings != null) return;
            if (!__instance.def.HasModExtension<ApparelStorageExtension>()) return;

            __instance.settings = new StorageSettings(__instance);
            StorageSettings defaults = __instance.def.building?.defaultStorageSettings;
            if (defaults != null) __instance.settings.CopyFrom(defaults);
            __instance.settings.Priority = StoragePriority.Critical;

            Log.Message($"[Closet Space] Converted legacy {__instance.def.defName} at {__instance.Position}.");
        }
    }
}
```

### Fallback C: respawn after load (Option 3)

```csharp
using System.Linq;
using RimWorld;
using Verse;

namespace ClosetSpace
{
    public class MapComponent_ApparelStorageConversion : MapComponent
    {
        private bool converted;   // saved, so conversion runs once per map

        public MapComponent_ApparelStorageConversion(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref converted, "cs_converted", false);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (converted) return;

            var legacy = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
                .OfType<Building>()
                .Where(b => b.def.HasModExtension<ApparelStorageExtension>() && !(b is Building_Storage))
                .ToList();

            foreach (Building old in legacy) Respawn(old);
            converted = true;
        }

        // UNVERIFIED API details: TryGetQuality, CompQuality.SetQuality signature, StyleDef setter.
        private void Respawn(Building old)
        {
            IntVec3 pos = old.Position;
            Rot4 rot = old.Rotation;
            ThingDef stuff = old.Stuff;
            Faction faction = old.Faction;
            int hp = old.HitPoints;
            bool hasQuality = old.TryGetQuality(out QualityCategory quality);
            ThingStyleDef style = old.StyleDef;

            old.Destroy(DestroyMode.Vanish);

            Thing fresh = ThingMaker.MakeThing(old.def, stuff);
            fresh.HitPoints = hp;
            fresh.StyleDef = style;
            if (hasQuality) fresh.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Colony);
            GenSpawn.Spawn(fresh, pos, map, rot, WipeMode.Vanish);
            if (faction != null) fresh.SetFaction(faction);
        }
    }
}
```

Enforcement choice: Critical is a default, not a lock. Players can lower it per dresser. If you want it locked, add a Harmony postfix on `StorageSettings.Priority`'s setter that ignores changes for these defs; this is not recommended because it surprises players.

## Edge cases

The highest risks are existing saves, the bedroom room role, and removing the mod later; each has a matching test.

| Edge case | Risk | Handling | Test |
|---|---|---|---|
| Dresser saved before the mod was added | Loads with the old class and no storage settings; errors or dead storage tab | C# Options 1–3 | T7 |
| Removing the mod mid-save | Saves now reference ASF's class and our extension; dressers may error or lose settings | State on the Workshop page that removal is unsupported; tell players to uninstall dressers first | — |
| Bedroom with a full dresser | Storage buildings count toward the Storeroom role (the Shelf has that role); a bedroom could be relabelled, changing mood and impressiveness | Expected to stay Bedroom because beds score higher; unverified | T12 |
| Comfort facility | Changing thingClass could drop the bed comfort link | `CompFacility` is a comp, so it should survive; verify bed comfort stays +0.05 | T12 |
| Every apparel subtype | Headgear, armour, utility (shield belts), Royalty/Biotech/Odyssey apparel | Filter uses the parent `Apparel` category, so DLC subcategories are covered without `MayRequire` | T5 |
| Tainted and damaged apparel | Brief says "any kind", but haulers would then fill dressers with tainted gear | Default allows all; players can untick via the special filters; confirm tainted is allowed by default | T5 |
| Non-apparel | Players adding steel or meals | `fixedStorageSettings` hides everything except Apparel | T9 |
| Critical vs other storage | Critical pulls apparel out of every lower stockpile and shelf on the map | Intended; equal-Critical storages do not trade items | T6 |
| Capacity overflow | 25 garments, one dresser | 20 stored, 5 stay in the next-best storage; with 2 linked dressers, 40 | T5, T10 |
| Stackable apparel from other mods | Capacity counts stacks, not garments | Document; out of scope for vanilla | — |
| Storage linking | Linking a dresser with a shelf would merge filters | Unique `storageGroupTag`; dressers link only to dressers and wardrobes | T10 |
| Unreachable dresser | Walled in or boxed by beds; haulers cannot place items | Pass-through-only buildings are filled from an adjacent standable cell; leave one open | T11 |
| Blueprint and frame | Settings should be editable before construction, like shelves | Defaults apply from blueprint stage | T4 |
| Minified, traded, or reinstalled | Dresser bought from a trader or reinstalled | Def-level change applies; verify settings reset to defaults on reinstall | T3 |
| Uninstall or deconstruct while full | Contents could vanish (the 1.6 outfit stand had this exact bug) | Emptied before removal (TT-001): contents drop nearby, or at the nearest free spot on the map; ASF's pack-contents option stays off | T13 |
| Fire | Wooden dresser burning | Stored apparel burns with it, as on shelves; no handling | — |
| Stored items drawn on top | 20 shirts drawn over the dresser texture | ASF GraphicsDef hides items | T1 |
| Ideology styles | Styled dressers | Style is a graphic on the same def; covered | — |
| Enemy or ruin dressers | Generated on non-player maps | Harmless; non-colony storage is ignored by your haulers | — |
| Conflicting mods | Dressers Hold Clothing, LWM Deep Storage, mods removing default shelf storage | `incompatibleWith` for the first; test LWM separately; load storage-stripping mods after this one | — |
| Player lowers priority | Should persist through save and reload | Patch B only runs when settings are missing | T14 |

### Wardrobe with and without VFE

The conditional is the easy part; the shared defName is what keeps saves intact when a player adds or removes VFE.

| Edge case | Risk | Handling | Test |
|---|---|---|---|
| Built without VFE, then VFE added | With a different defName, every wardrobe would vanish from the save | Same defName: they load as VFE's wardrobe with contents and settings | T15 |
| Built with VFE, then VFE removed | Same as above, reversed | Same defName: they load as our copy | T16 |
| Leftover VFE comp data after removal | Load warnings about unknown comps or fields | Harmless; mention on the Workshop page | T16 |
| Two wardrobes in the Architect menu | Both defs loaded at once | `IfModNotActive` guarantees one; confirm with Loaded Files For Mod | T17 |
| Current VFE already makes its wardrobe storage, or uses its own class | Duplicate-node errors, or VFE behaviour replaced by ASF's | Check VFE's def first; switch step 3 to Replace; decide whether ASF's class may override | T17 |
| VFE's wardrobe size differs from 2×1 | Wrong total capacity | `maxItemsInCell` = 30 ÷ cells; our copy uses the same size | T17 |
| VFE Spacer active without VFE Core | Our copy loads beside Spacer's illuminated wardrobe | Different defs; no conflict | — |
| Royal or illuminated wardrobe copied | Could drag in Royalty-only fields | Copy only the plain wardrobe | — |
| Texture path collision | Same path would override VFE's art | Namespaced `ClosetSpace/` path, loaded only without VFE | T17 |
| Redistributing VE art or text | No license found | Get VE team permission, or draw your own | — |

## Test scenarios

Tests cover creating dressers and wardrobes, then moving apparel into them, including into dressers that existed before the mod. Every test passes only if the log shows no red errors from this mod, ASF or Harmony.

**Setup:** mod list Harmony → ASF → this mod, Core only, all DLC disabled. Turn on Development mode (Options) and God mode for instant building. Clear the log before each test.

### Creation

| ID | Scenario | Steps | Pass when |
|---|---|---|---|
| T1 | Build a dresser | Research Complex furniture; build dressers in wood, steel and granite | Storage tab shows only Apparel, priority Critical; inspect pane shows a 20-item limit; no items drawn on top once filled |
| T2 | Build a wardrobe, VFE not active | With VFE disabled, build the wardrobe in the same three materials | Same as T1 with a 30-item limit; texture renders in all four rotations |
| T3 | Minified and reinstalled | Spawn a minified dresser (Spawn thing), install it; then uninstall and reinstall a built one | Installed dresser is storage with Critical and Apparel |
| T4 | Blueprint settings | Place a dresser blueprint and open its Storage tab before building | Tab exists, shows Critical; edits carry over to the finished dresser |

### Transfer into new dressers

| ID | Scenario | Steps | Pass when |
|---|---|---|---|
| T5 | Loose apparel, mixed types | Spawn 25 items near one dresser: parka, tribalwear, flak vest, simple helmet, shield belt, one tainted item, one at 10% HP | Haulers store exactly 20; 5 remain outside; all types accepted, including tainted and damaged |
| T6 | From lower storage | Put 10 apparel in a Normal stockpile and 6 on a Preferred shelf; then build a dresser | All 16 move into the dresser; nothing moves back |
| T9 | Non-apparel rejected | Spawn steel, meals and a rifle next to the dresser; open its filter | None are hauled in; the filter lists only Apparel |
| T10 | Linking and capacity | Link 2 dressers and 1 wardrobe; try to link a dresser with a shelf; spawn 80 apparel | Group holds 70; shelf cannot join the group |
| T11 | Unreachable dresser | Wall a dresser in on all reachable sides | No hauling attempts loop; no errors; apparel stays in other storage |

### Transfer into existing dressers (save made before the mod)

| ID | Scenario | Steps | Pass when |
|---|---|---|---|
| T7 | Conversion | Without the mod: build 2 dressers (one Excellent with art), drop 15 apparel on the floor, save. Add the mod, load | Log shows the conversion message twice; both dressers have the Storage tab, Critical, Apparel; position, rotation, stuff, HP, quality and art title unchanged; haulers move the 15 items in |
| T12 | Bedroom and comfort | Fresh map: a room with a bed, 2 dressers and a wardrobe; a room with only 2 dressers and a wardrobe (decisions 8, 9) | The first room is a Bedroom; the second is a Storeroom |
| T13 | Uninstall and deconstruct while full | On a crowded site (every free cell within 13 cells holds a forbidden wood log), uninstall one full dresser, deconstruct another, destroy a third | Each dresser is emptied before removal; no item deleted; no errors |
| T14 | Save and reload | Set one converted dresser to Normal, save, reload twice | Contents identical; that dresser stays Normal; the others stay Critical |

### Wardrobe with and without VFE

| ID | Scenario | Steps | Pass when |
|---|---|---|---|
| T15 | Add VFE to a save | VFE off: build 2 wardrobes, fill with 30 apparel each, set one to Normal, save. Enable VFE, restart, load | Both wardrobes still exist with VFE's graphic, 60 items, one Normal, one Critical |
| T16 | Remove VFE from a save | VFE on: build 2 VFE wardrobes, fill, save. Disable VFE, restart, load | Both load as our copy with contents and settings; only harmless VFE warnings, no red errors from this mod |
| T17 | Fresh game with VFE | VFE on, new colony; open Architect › Furniture; build a wardrobe; check Loaded Files For Mod | Exactly one wardrobe in the menu; it stores 30 apparel at Critical; `NoVFE` files are not listed as loaded |

Useful dev tools: Spawn thing (apparel and minified buildings), the Debug log window, and the inspect pane's storage count. If T7 fails with Option 1, enable Patches A and B and rerun T7 before trying Fallback C.

## Mod name suggestions

"Closet Space" is used as the placeholder in code; the `CS_` prefix would change with the name. Check each against the Workshop before publishing.

1. Closet Space
2. Dressed for Storage
3. Proper Dressers
4. Drawers & Wardrobes
5. Tidy Threads
6. Fold and Store
7. Hang It Up
8. Wardrobe Works
9. Apparel Drawers (ASF)
10. Clothes Belong in Dressers

## Notes for Claude Code

These items could not be verified without the decompiled 1.6 assembly or the ASF and Harmony mod files; each needs checking before the first build. Paths assume the game's `Data/` folder and ASF's mod folder.

### Vanilla 1.6 defs (`Data/Core/Defs`)

- Confirm the dresser's `defName` (assumed `Dresser`), its `ParentName`, and whether `thingClass`, `building`, `inspectorTabs`, `modExtensions` are set on it or inherited.
- Confirm the dresser keeps its bed comfort via a `CompProperties_Facility` comp (not via thingClass).
- Confirm the research defName for Complex furniture (assumed `ComplexFurniture`) and the furniture parent to use for `CS_Wardrobe` (assumed `FurnitureWithQualityBase`).
- Copy the vanilla Shelf's `building` storage fields and compare names: `maxItemsInCell`, `preventDeteriorationOnTop`, `ignoreStoredThingsBeauty`, `storageGroupTag`, `fixedStorageSettings`, `defaultStorageSettings`, and any `surfaceType` or similar.
- Confirm the `Apparel` ThingCategoryDef name and that tainted apparel is allowed by default with no `disallowedSpecialFilters`.

### Decompiled Assembly-CSharp (1.6)

- `Verse.BackCompatibility.GetBackCompatibleType` exists with `(Type, string, XmlNode)`, and `ScribeExtractor` calls it for every saved Thing (Patch A).
- `Building_Storage.settings` is a public field, and `ExposeData` leaves it null when the save has none (Patch B).
- `StorageSettings(IStoreSettingsParent)` constructor, `CopyFrom`, and the `Priority` setter; `StoragePriority.Critical` enum name.
- With storage groups (added 1.5), whether a linked dresser reads the group's settings instead of its own.
- `RoomRoleWorker_Storeroom` vs `RoomRoleWorker_Bedroom` scoring: does a bedroom with a full storage building stay a Bedroom (T12)?
- Fallback C APIs: `TryGetQuality`, `CompQuality.SetQuality` signature, `Thing.StyleDef` setter, `GenSpawn.Spawn` overload with `WipeMode`.

### ASF mod files

- Exact `packageId` in ASF's `About/About.xml`.
- All fields set by `AdaptiveStorageBase` in `Defs/ThingDefBase.xml`; add the ones the dresser patch is missing.
- Whether ASF's "thingClass updating of defs mid save" already converts old dressers and creates their settings (Option 1). If yes, drop Patches A and B.
- `GraphicsDef` fields that hide stored item graphics (`Docs/.../GraphicsDef.adoc`).
- Whether ASF's Extension (`maxItemsByCell`) is needed, or vanilla `maxItemsInCell` is enough.
- Whether ASF's `MinifiedExtension` pack-contents is off by default (T13 expects contents to drop).
- Whether ASF already patches `Building_Storage.ExposeData`, which could conflict with Patch B.

### Harmony and build

- Harmony `packageId` (assumed `brrainz.harmony`); reference `0Harmony.dll` at compile time but do not ship it.
- Target framework for 1.6 (net472 for 1.6, not .NET 10, which is 1.7); reference `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, and ASF's assembly only if C# references ASF types.

### Other mods

- VFE Core's exact `packageId` for the `IfModNotActive` entries.
- Which VFE wardrobe is the plain one: its defName (replace every `VFE_WARDROBE_DEFNAME`), size, drawSize, cost, stats, research and ParentName; confirm it has no Royalty-only fields.
- Whether current VFE already makes that wardrobe storage, and which thingClass and comps it uses (VFE Framework classes must not be copied).
- License or written permission from the Vanilla Expanded team for any texture or description text reused.
- Package ID of Dressers Hold Clothing (Continued) for `incompatibleWith`.

## Sources

Pages opened on 2026-11-10. Wiki pages are community-maintained; ASF's GitHub `ThingDefBase.xml` and its docs folder blocked automated reading, so their contents are unverified here.

- [Dresser — RimWorld Wiki](https://rimworldwiki.com/wiki/Dresser) (last edited 12 May 2026)
- [Furniture — RimWorld Wiki](https://rimworldwiki.com/wiki/Furniture) (last edited 26 Nov 2025)
- [Shelf — RimWorld Wiki](https://rimworldwiki.com/wiki/Shelf) (last edited 17 Nov 2025)
- [Stockpile zone — RimWorld Wiki](https://rimworldwiki.com/wiki/Stockpile_zone) (last edited 25 Nov 2025)
- [Outfit stand — RimWorld Wiki](https://rimworldwiki.com/wiki/Outfit_stand) (last edited 11 Aug 2026)
- [Mod Folder Structure — RimWorld Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure) (last edited 24 Oct 2026; `IfModActive`, `IfModNotActive`, `IfModActiveAll`)
- [RimWorld 1.6 Mod Updates — RimWorld Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.6_Mod_Updates) (last edited 18 Nov 2025; links Ludeon's 1.6 Change Log and Modder Primer)
- [Adaptive Storage Framework — Steam Workshop](https://steamcommunity.com/workshop/filedetails/?id=3033901359)
- [Adaptive Storage Framework — change notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/3033901359)
- [ASF documentation index — GitHub](https://github.com/bbradson/Adaptive-Storage-Framework/blob/main/Docs/modules/ROOT/pages/index.adoc)
- [Dressers Hold Clothing (Continued) — Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2584894865) (community, lower confidence)
- [Vanilla Furniture Expanded – Spacer — change notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/2028381079) (community, lower confidence)
