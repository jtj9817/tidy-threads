# TODO

Implementation tracker for **Tidy Threads**. Phases come from the design plan, the living doc
[Apparel Storage for Dressers & Wardrobes — RimWorld 1.6 Mod Design](https://claude.ai/artifact/DmZxu6jsgd5qVrcYTtZhTz)
(snapshot: [docs/plans/0001-tidy-threads.md](docs/plans/0001-tidy-threads.md)). The rules are in
[AGENTS.md](AGENTS.md#tracking-and-test-rules).

- **Task boxes** are ticked when the work is done.
- **Prove boxes** are the only tests the mod gets: the plan's scenarios T1–T17, nothing more and nothing less. The
  plan has no T8. Tick a Prove box only after a run proved it, and name the run.
- **Phases** are ticked once all their boxes are ticked. Work one phase at a time.

Identity: name **Tidy Threads**, assembly/namespace `TidyThreads`, packageId `josh9817.tidythreads`, defName/tag
prefix `TT_`, Harmony id `josh9817.tidythreads`. The plan's placeholders map like this: "Closet Space" → Tidy Threads,
`ClosetSpace` → `TidyThreads`, `CS_` → `TT_`, `yourname.closetspace` → `josh9817.tidythreads`, and
`VFE_WARDROBE_DEFNAME` → `Table_Wardrobe`.

## Progress

- [ ] **Phase 0**: Skeleton and test harness
- [ ] **Phase 1**: Dresser becomes apparel storage
- [ ] **Phase 2**: Wardrobe without VFE
- [ ] **Phase 3**: Moving apparel into new dressers
- [ ] **Phase 4**: Dressers from saves made before the mod
- [ ] **Phase 5**: Wardrobe with VFE, and VFE added to or removed from a save
- [ ] **Phase 6**: Release

## Current state

- **Next step:** start Phase 0. All open questions are settled (see Decisions).
- **Last run:** none yet.
- **Environment (checked 2026-11-10):** game `1.6.4871 rev598`; Steam running; `dotnet`, `inkscape`, `Xvfb`, `xdotool`,
  `import` present. Workshop: Harmony (2009463077), ASF (3033901359) and VFE (1718190143) are installed;
  Dressers Hold Clothing (Continued) (2584894865) is not. `tools/vanilla.sh init` is done (`.tools/decomp`, 9213
  files). Error baseline: not recorded yet.
- Toolchain copied from `rimworld-modding-center` at `211fc88`.

## Decisions (user, 2026-11-10)

These settle the open questions raised by the verification. They are in the living plan doc's Decisions section
(rev 21) and its snapshot.

1. **Folder layout: root `Defs/` and `Patches/`**, not the plan's `1.6/Defs` and `1.6/Patches`. `LoadFolders.xml`
   loads `/`, `1.6` (assembly only) and, without VFE, `NoVFE` + `NoVFE/1.6`. The shared tools are extended for this
   layout: `tools/package.sh` copies `LoadFolders.xml` and `NoVFE/`, and `tools/rwdefs.py` also lints `NoVFE/1.6/Defs`
   and the `NoVFE/Textures` paths. Why: the shared lint and package tools only handle root folders.
2. **Harness changes approved.** Make them in this repo's `tools/`, keep them general (no mod-specific literals), and
   port them to `rimworld-modding-center` afterwards (porting rule). Three changes are needed:
   - Required dependencies that load **before** the mod (ASF). `autotest.py` only loads `CORE_MODS` before it, and
     `MOD_EXTRA_MODS` loads after it.
   - A save made **without** the mod, then loaded **with** it (T7, T12, T13, T14). Today the harness only does the
     reverse, in `removal-check`.
   - A save made with one mod set and loaded with another, VFE on or off (T15, T16).
3. **Our wardrobe keeps bed comfort.** It gets VFE's `CompProperties_Facility` (Comfort +0.05, maxDistance 6,
   maxSimultaneous 1) and a bed `linkableFacilities` patch for `Table_Wardrobe`. Both live under `NoVFE/` so they
   don't double up with VFE's own `Linkables_Patch.xml`.
4. **We draw our own wardrobe**: our own texture (SVG in `Art/`), description and stats. We copy no VE art or text;
   only the defName `Table_Wardrobe` and the 2×1 size are shared, for save compatibility and capacity.
5. **Incompatible mod: `Mlie.DressersHoldClothing`** (agent-resolved). This is the packageId in the mod's own
   `About/About.xml` on GitHub (`emipa606/DressersHoldClothing`); its `PublishedFileId.txt` is `2584894865`, the
   plan's Workshop ID.

## Verified details (2026-11-10)

These replace the plan's "Unverified" items. Sources: the game's `Data/Core/Defs`, `.tools/decomp` (1.6.4871),
and the installed ASF and VFE workshop folders (ASF ships its source under `Source/`).

| Plan item | Finding |
| --- | --- |
| Dresser def | `Dresser`, `ParentName="ArtableFurnitureBase"` → `FurnitureWithQualityBase` → `FurnitureBase` → `BuildingBase`. It has no own `thingClass` (inherits `Building`), so step 1 takes the `nomatch` Add. It has its own `<building>` (`paintable`), so step 2 is a no-op. None of the step-3 fields exist, so Add works with no duplicates. It has no own `inspectorTabs` (inherits `ITab_Art`): step 4 adds a child list that merges. It has no `modExtensions`. |
| Dresser comfort | `CompProperties_Facility` (Comfort +0.05, maxDistance 6, maxSimultaneous 1) is a comp, so it doesn't depend on `thingClass`. |
| Research / category | `ComplexFurniture` and ThingCategoryDef `Apparel` both exist. |
| Tainted apparel | `AllowDeadmansApparel` and `AllowNonDeadmansApparel` are both `allowedByDefault=true`, so tainted apparel is allowed by default. |
| Shelf storage fields | `ShelfBase.building`: `preventDeteriorationOnTop`, `ignoreStoredThingsBeauty`, `maxItemsInCell 3`, `blueprintClass Blueprint_Storage`, `paintable`; `inspectorTabs ITab_Storage`; `StorageShelfBase` uses `Building_Storage` and `storageGroupTag Shelf`. |
| ASF fields missing from the plan's step 3 | `AdaptiveStorageBase` also sets `drawerType MapMeshAndRealTime`, `drawGUIOverlay True`, `pathCostIgnoreRepeat`, and `building/blueprintClass Blueprint_Storage`. The last one is what gives a blueprint its Storage tab (T4). Add them to both patches. ASF's own `EnhanceFixedStorageSettings` patch adds `disallowNotEverStorable` automatically. |
| Package IDs | Harmony `brrainz.harmony`, ASF `adaptive.storage.framework`, VFE `VanillaExpanded.VFECore` (all verified). ASF has `loadBefore` VFE Core. |
| Option 1 (ASF built-in conversion) | ASF's `SaveGameCompatibility` Harmony **prefix** on `BackCompatibility.GetBackCompatibleType(Type, string, XmlNode)` turns a saved `Building` into the def's ASF class. That is the plan's Patch A, already done, so Patch A isn't needed. But `ThingClass.ExposeData` then sets `settings ??= new(this)` during `LoadingVars`. `new StorageSettings(owner)` gives an **empty filter** and the fixed settings' priority (Normal), not Critical apparel. Expect T7 to fail on Option 1. |
| Patch B as written | It won't work: by `PostLoadInit`, ASF has already set `settings` to non-null, so the null check skips every converted dresser. A legacy dresser has to be detected some other way, for example by no `settings` node in its saved XML during `LoadingVars`. ASF doesn't patch `Building_Storage.ExposeData` itself. |
| `Building_Storage` | `public StorageSettings settings`; `PostMake` copies `def.building.defaultStorageSettings`; `GetStoreSettings()` returns the **storage group's** settings when the building is linked. `StoragePriority.Critical` exists. |
| Bedroom vs storeroom (T12) | `RoomRoleWorker_Bedroom` scores 100000 with a qualifying bed; `RoomRoleWorker_StoreRoom` scores 1 per `Building_Storage`. The room stays a Bedroom. |
| Fallback C APIs | `QualityUtility.TryGetQuality(this Thing, out QualityCategory)`, `CompQuality.SetQuality(QualityCategory, ArtGenerationContext?)`, a `Thing.StyleDef` setter, and `GenSpawn.Spawn(Thing, IntVec3, Map, Rot4, WipeMode, …)` all exist. |
| Hiding stored items | ASF GraphicsDef field `showContainedItems` (default `true`) set to `false`, with `targetDef`/`targetDefs`. |
| Pack contents on minify | Off unless the `minifiedDef` has `MinifiedExtension packContents`. Vanilla `MinifiedThing` has none, so contents drop (T13). |
| Capacity | Vanilla `maxItemsInCell` is enough; ASF `Extension.maxItemsByCell` is optional. |
| VFE wardrobe | `Table_Wardrobe` (label "wardrobe"), parent `VFE_WardrobeBase` → `ArtableFurnitureBase`. Size (2,1), so 15 per cell is right. drawSize (3,3); cost 60 stuff; MaxHitPoints 140, Mass 20, WorkToBuild 2500, Beauty 8, Flammability 1.0; fillPercent 0.6; research `ComplexFurniture`; `constructionSkillPrerequisite 5`; comps Facility and `CompProperties_Styleable`; `building/paintable`; textures `NewThings/Furniture/Wardrobe/Wardrobe_{north,east,south}`. It isn't storage and has no custom `thingClass`. The plan's placeholder stats (75, 160, 3000, 25, 5) are wrong. |
| VFE's other wardrobe | `VFE_RoyalWardrobe` is in the same mod (out of scope per the plan). |
| Dressers Hold Clothing (Continued) | packageId `Mlie.DressersHoldClothing`, Workshop 2584894865; supports 1.2–1.7 (from its GitHub `About/`). |

## Agent notes and gotchas

- `tools/vanilla.sh init` installs the latest `ilspycmd`. 11.2.0 needs `GLIBC_2.38`, and this machine (Pop!_OS 22.04)
  has an older one. Install the version the other mods use first:
  `dotnet tool install ilspycmd --version 11.1.0.9782 --tool-path .tools/ilspy`. Worth fixing in the modding center.
- Scenario names must not contain commas (the filter splits on them).

## Phase 0: Skeleton and test harness

- [ ] `About/About.xml`: name Tidy Threads, packageId `josh9817.tidythreads`, supportedVersions 1.6, `modDependencies`
      Harmony and ASF, `loadAfter` both, `incompatibleWith` `Mlie.DressersHoldClothing`
- [ ] `LoadFolders.xml`: `/`, `1.6`, and `NoVFE` + `NoVFE/1.6` with `IfModNotActive="VanillaExpanded.VFECore"`; defs and
      patches go in root `Defs/` and `Patches/` (decision 1)
- [ ] `tools/package.sh` copies `LoadFolders.xml` and `NoVFE/`; `tools/rwdefs.py` lints `NoVFE/1.6/Defs` and the
      `NoVFE/Textures` paths (decision 1)
- [ ] `Source/TidyThreads/TidyThreads.csproj`: net472, `Krafs.Rimworld.Ref` 1.6.4871, `Lib.Harmony` with
      `ExcludeAssets="runtime"`, `DebugType embedded`, output to `1.6/Assemblies/`; never ship `0Harmony.dll`
- [ ] `TidyThreadsMod : Mod` runs `new Harmony("josh9817.tidythreads").PatchAll()` and logs one `[TidyThreads]` build-id line;
      `ApparelStorageExtension : DefModExtension`
- [ ] Copy `tools/game-side/AutoTest.cs` and `Fixtures.cs` into `Source/TidyThreads/`, rename their namespaces, add the
      scenario runner with exact-name filtering (`docs/testing.md`)
- [ ] Test harness: required dependencies that load before the mod, such as ASF (decision 2)
- [ ] Record the error baseline without the mod (Harmony + ASF): `tools/autotest.py baseline`
- [ ] Start `docs/test-runs.md`

## Phase 1: Dresser becomes apparel storage

- [ ] Dresser patch (plan § Dresser patch) in `Patches/TT_Dresser.xml`, with the ASF fields in Verified details; marker extension `capacity 20`;
      `storageGroupTag TT_ApparelFurniture`
- [ ] ASF GraphicsDef `TT_Dresser_Graphics` with `showContainedItems false`
- [ ] **Prove T1 Build a dresser.** Research Complex furniture; build dressers in wood, steel and granite. Pass: the
      Storage tab shows only Apparel at Critical; the inspect pane shows a 20-item limit; nothing is drawn on top
      once filled. *Failure mode:* patch didn't apply, or a wrong filter, priority or capacity.
- [ ] **Prove T3 Minified and reinstalled.** Spawn a minified dresser and install it; uninstall and reinstall a built
      one. Pass: the installed dresser is storage at Critical with Apparel. *Failure mode:* settings lost or not
      reset through the minified thing.
- [ ] **Prove T4 Blueprint settings.** Place a dresser blueprint and open its Storage tab before building. Pass: the tab
      exists and shows Critical; edits carry over to the finished dresser. *Failure mode:* no `Blueprint_Storage`,
      so no tab or lost edits.

## Phase 2: Wardrobe without VFE

- [ ] `NoVFE/1.6/Defs/ThingDefs_Buildings/TT_Wardrobe.xml`: our own plain-furniture wardrobe with defName
      `Table_Wardrobe`, size (2,1), and our own description and stats (decision 4). It carries
      `CompProperties_Facility` (Comfort +0.05, maxDistance 6, maxSimultaneous 1) and
      `PlaceWorker_ShowFacilitiesConnections` (decision 3)
- [ ] `NoVFE/1.6/Patches/TT_WardrobeBeds.xml`: add `Table_Wardrobe` to the beds' `linkableFacilities`, the same targets
      VFE patches (decision 3)
- [ ] Our own wardrobe texture: `Art/NoVFE/…` SVG → `NoVFE/Textures/TidyThreads/Furniture/Wardrobe_{north,east,south}.png`
      via `tools/export-textures.sh` (decision 4; the tool's Art→Textures mapping may need the NoVFE path)
- [ ] Wardrobe patch `Patches/TT_Wardrobe.xml` (`maxItemsInCell 15`, marker `capacity 30`) and GraphicsDef
      `TT_Wardrobe_Graphics` in `Defs/`, always loaded
- [ ] **Prove T2 Build a wardrobe, VFE not active.** With VFE disabled, build the wardrobe in wood, steel and granite.
      Pass: same as T1 with a 30-item limit; the texture renders in all four rotations. *Failure mode:* the NoVFE
      folder doesn't load, a texture is missing, or capacity is wrong.

## Phase 3: Moving apparel into new dressers

- [ ] **Prove T5 Loose apparel, mixed types.** Spawn 25 items near one dresser: parka, tribalwear, flak vest, simple
      helmet, shield belt, one tainted item, one at 10% HP. Pass: haulers store exactly 20 and 5 stay outside; every
      type is accepted, including tainted and damaged. *Failure mode:* capacity overflow, or a subtype rejected.
- [ ] **Prove T6 From lower storage.** Put 10 apparel in a Normal stockpile and 6 on a Preferred shelf, then build a
      dresser. Pass: all 16 move into the dresser and nothing moves back. *Failure mode:* Critical priority not
      honoured, or a hauling loop.
- [ ] **Prove T9 Non-apparel rejected.** Spawn steel, meals and a rifle next to the dresser and open its filter. Pass:
      none are hauled in; the filter lists only Apparel. *Failure mode:* fixed filter not enforced.
- [ ] **Prove T10 Linking and capacity.** Link 2 dressers and 1 wardrobe; try to link a dresser with a shelf; spawn 80
      apparel. Pass: the group holds 70; the shelf can't join the group. *Failure mode:* wrong `storageGroupTag`, or
      group capacity wrong.
- [ ] **Prove T11 Unreachable dresser.** Wall in a dresser on all reachable sides. Pass: no hauling attempts loop, no
      errors, and apparel stays in other storage. *Failure mode:* job loop or errors on an unreachable target.

## Phase 4: Dressers from saves made before the mod

- [ ] Test harness: make a save without the mod and load it with the mod (decision 2)
- [ ] Converter, tried in plan order and stopping at the first that passes T7: Option 1 (ASF built-in; Verified
      details predict a fail on settings); then Option 2 with Patch B only, redesigned to detect legacy dressers
      without the null check; then Fallback C
- [ ] **Prove T7 Conversion.** Without the mod: build 2 dressers (one Excellent with art), drop 15 apparel on the floor,
      save. Add the mod and load. Pass: the log shows the conversion message twice; both dressers have the Storage tab,
      Critical and Apparel; position, rotation, stuff, HP, quality and art title are unchanged; haulers move the 15
      items in. *Failure mode:* class not swapped, empty settings, or lost quality or art.
- [ ] **Prove T12 Bedroom and comfort.** In the T7 save, fill a bedroom's dresser. Pass: the room still reads Bedroom,
      and the bed's comfort still includes the dresser's +0.05. *Failure mode:* room relabelled Storeroom, or the
      facility link lost.
- [ ] **Prove T13 Uninstall and deconstruct while full.** Uninstall one full dresser and deconstruct another. Pass: all
      contents drop nearby, none are deleted, no errors. *Failure mode:* contents vanish (the 1.6 outfit stand bug).
- [ ] **Prove T14 Save and reload.** Set one converted dresser to Normal, save, reload twice. Pass: contents identical;
      that dresser stays Normal and the others stay Critical. *Failure mode:* the converter re-runs and overwrites a
      player's priority.

## Phase 5: Wardrobe with VFE, and VFE added to or removed from a save

- [ ] Test harness: VFE as a test-only extra mod (`MOD_EXTRA_MODS=VanillaExpanded.VFECore`), and a save made with one mod
      set loaded with another (decision 2)
- [ ] Check VFE's def is still plain furniture at build time; if it already is storage, step 3 becomes Replace
- [ ] **Prove T15 Add VFE to a save.** VFE off: build 2 wardrobes, fill with 30 apparel each, set one to Normal, save.
      Enable VFE, restart, load. Pass: both wardrobes still exist with VFE's graphic, 60 items, one Normal and one
      Critical. *Failure mode:* a defName mismatch deletes wardrobes, or settings are lost.
- [ ] **Prove T16 Remove VFE from a save.** VFE on: build 2 VFE wardrobes, fill, save. Disable VFE, restart, load. Pass:
      both load as our copy with contents and settings; only harmless VFE warnings, no red errors from this mod.
      *Failure mode:* wardrobes lost, or red errors from leftover VFE data.
- [ ] **Prove T17 Fresh game with VFE.** VFE on, new colony; open Architect › Furniture, build a wardrobe, check Loaded
      Files For Mod. Pass: exactly one wardrobe in the menu; it stores 30 apparel at Critical; NoVFE files aren't
      loaded. *Failure mode:* two wardrobe defs, a texture path collision, or wrong capacity.

## Phase 6: Release

- [ ] Port the general tool changes (harness, package, lint, `ilspycmd` pin) to `rimworld-modding-center` (decision 2)
- [ ] `About/Preview.png` (< 1 MB), README with the mod-removal warning (removal is unsupported per the plan: uninstall
      dressers first), LICENSE, CHANGELOG
- [ ] `tools/package.sh` produces a `dist/TidyThreads` that includes `LoadFolders.xml` and `NoVFE/`
- [ ] Release gate (user-started or user-approved only): T1–T17 on the packaged copy via `tools/autotest.py pkg-run`
