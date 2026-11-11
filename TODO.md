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

- [x] **Phase 0**: Skeleton and test harness
- [x] **Phase 1**: Dresser becomes apparel storage
- [x] **Phase 2**: Wardrobe without VFE
- [x] **Phase 3**: Moving apparel into new dressers
- [x] **Phase 4**: Dressers from saves made before the mod
- [x] **Phase 5**: Wardrobe with VFE, and VFE added to or removed from a save
- [x] **Phase 6**: Release

## Current state

- **Next step:** none; every phase is done. The release package is `dist/TidyThreads`, build `7477b69.20261111T030637Z`.
- **Open questions for the user:** none. T11, which failed in the first gate's sequence, passed in sequence right after
  T10 in the second gate (2026-11-11), so it was left unchanged. If it fails in sequence again, end colonists'
  current jobs in its fixture and log the job's def.
- **[TT-001](docs/tickets/TT-001-apparel-lost-when-full-furniture-is-removed.md) fixed** (decisions 11, 12): dressers
  and wardrobes are emptied before deconstruction, uninstalling or destruction; T13 runs on a crowded site with three
  dressers and proves all three. The release gate reruns on a new package (user, 2026-11-11).
- **Last run:** 2026-11-11 release gate on package `7477b69.20261111T030637Z`: 17/17 PASS, 0 new errors (`docs/test-runs.md`). The game reports `rev600` since
  2026-11-11 (it was `rev598`).
- **Environment (checked 2026-11-10):** game `1.6.4871 rev598`; Steam running; `dotnet`, `inkscape`, `Xvfb`, `xdotool`,
  `import` present. Workshop: Harmony (2009463077), ASF (3033901359) and VFE (1718190143) are installed;
  Dressers Hold Clothing (Continued) (2584894865) is not. `tools/vanilla.sh init` is done (`.tools/decomp`, 9213
  files). Error baseline: 6 signatures (Harmony + Core + ASF, 2026-11-10).
- Toolchain copied from `rimworld-modding-center` at `211fc88`.

## Decisions (user, 2026-11-10)

These settle the open questions raised by the verification. They are in the living plan doc's Decisions section
(rev 26) and its snapshot.

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
6. **Pre-mod saves come from a separate test-only helper mod** (user, 2026-11-10, after Phase 2). Loaded without
   Tidy Threads, it builds the T7 fixture and writes the save; it references nothing from the mod, lives under
   `tools/` like `tools/removal-probe/`, and never ships in `dist/`. Keep it general for porting.
7. **Storage is the goal; bed comfort doesn't matter** (user, 2026-11-10). Scenarios assert storage behaviour, not
   the comfort link; T2's bed-link check was removed. The comfort facility itself stays (decision 3).
8. **T12 checks the room role only, not comfort** (user, 2026-11-10). Dressers and wardrobes are independent of beds:
   usually paired with them, but a storeroom may hold only dressers and wardrobes.
9. **T12's pass condition** (user, 2026-11-10): on a fresh map, a fixture room with a bed, 2 dressers and a wardrobe
   is a Bedroom, and one with only 2 dressers and a wardrobe is a Storeroom. No pre-mod save (a converted dresser
   has the same class as a new one).
10. **A separate error baseline for VFE runs** (user, 2026-11-10): Harmony + Core + ASF + VE Framework + VFE, no mod.
    The harness keeps one baseline file per extra-mod set and picks the one matching the launch.
11. **Empty the furniture before it is removed** (user, 2026-11-10, TT-001): before a dresser or wardrobe is
    deconstructed or uninstalled (reinstalling uninstalls first), all its stored items are moved out. C# now has a
    second job: `EmptyBeforeRemoval.cs`, two Harmony prefixes (`Building_Storage.Destroy` in Deconstruct mode,
    `MinifyUtility.MakeMinified`). T13 runs on a crowded site that reproduces the loss (user approved the fixture change).
12. **Destruction is emptied too; the last resort is accepted** (user, 2026-11-11): fire, raids and roof collapse
    (`Destroy(KillFinalize)`) empty the furniture like deconstruction; T13 kills a third dresser to prove it. An item
    with no free cell on the whole map isn't tested: that doesn't happen in normal play.

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
- Run the harness as `source tools/env.sh && tools/autotest.py ...`: env.sh now exports the identity and `MOD_DEPS`
  (ASF), which autotest.py reads from the environment. Before this, `tools/status.sh`'s call into autotest.py failed
  with "set MOD_NAME and MOD_ID".
- Scenarios read ASF state by reflection (`Renderer.ShowContainedItems`) and the inspect string (`N / 20`), so the
  project has no compile-time ASF reference. Builds and installs in scenarios go through vanilla paths:
  `Blueprint.TryReplaceWithSolidThing` + `Frame.CompleteConstruction`, `MinifyUtility.Uninstall`, `PlaceBlueprintForInstall`.
- Tool changes to port in Phase 6: `run --switch-mods`, per-set and transition baselines (`baseline --switch-mods`), lower-cased `ModsConfig.xml` ids, `VANILLA_WARNINGS`, cold restart of save-launched instances, `run --premod`/`--from-save` and `tools/premod-builder/` (+ selftest cases, fake game), the `AutoTest.LoadedSave`/`PreModManifest` game-side additions, `MOD_DEPS` in autotest.py/env.sh (+ preflight install check, selftest clears it),
  the env.sh `export` line, `rwdefs.py folders` and load-folder lint, `package.sh` load folders, and
  `export-textures.sh` no longer erroring when there is no `Art/About/`.
- `TidyThreadsMod`'s constructor logs the build id and patches; `AutoTest.Init` runs from `TT`'s static constructor
  (it creates a GameObject, so it waits until startup).

## Phase 0: Skeleton and test harness

- [x] `About/About.xml`: name Tidy Threads, packageId `josh9817.tidythreads`, supportedVersions 1.6, `modDependencies`
      Harmony and ASF, `loadAfter` both, `incompatibleWith` `Mlie.DressersHoldClothing`
- [x] `LoadFolders.xml`: `/`, `1.6`, and `NoVFE` + `NoVFE/1.6` with `IfModNotActive="VanillaExpanded.VFECore"`; defs and
      patches go in root `Defs/` and `Patches/` (decision 1)
- [x] `tools/package.sh` copies `LoadFolders.xml` and `NoVFE/`; `tools/rwdefs.py` lints `NoVFE/1.6/Defs` and the
      `NoVFE/Textures` paths (decision 1)
- [x] `Source/TidyThreads/TidyThreads.csproj`: net472, `Krafs.Rimworld.Ref` 1.6.4871, `Lib.Harmony` with
      `ExcludeAssets="runtime"`, `DebugType embedded`, output to `1.6/Assemblies/`; never ship `0Harmony.dll`
- [x] `TidyThreadsMod : Mod` runs `new Harmony("josh9817.tidythreads").PatchAll()` and logs one `[TidyThreads]` build-id line;
      `ApparelStorageExtension : DefModExtension`
- [x] Copy `tools/game-side/AutoTest.cs` and `Fixtures.cs` into `Source/TidyThreads/`, rename their namespaces, add the
      scenario runner with exact-name filtering (`docs/testing.md`)
- [x] Test harness: required dependencies that load before the mod, such as ASF (decision 2)
- [x] Record the error baseline without the mod (Harmony + ASF): `tools/autotest.py baseline`
- [x] Start `docs/test-runs.md`

## Phase 1: Dresser becomes apparel storage

- [x] Dresser patch (plan § Dresser patch) in `Patches/TT_Dresser.xml`, with the ASF fields in Verified details; marker extension `capacity 20`;
      `storageGroupTag TT_ApparelFurniture`
- [x] ASF GraphicsDef `TT_Dresser_Graphics` with `showContainedItems false`
- [x] **Prove T1 Build a dresser.** Research Complex furniture; build dressers in wood, steel and granite. Pass: the
      Storage tab shows only Apparel at Critical; the inspect pane shows a 20-item limit; nothing is drawn on top
      once filled. *Failure mode:* patch didn't apply, or a wrong filter, priority or capacity.
- [x] **Prove T3 Minified and reinstalled.** Spawn a minified dresser and install it; uninstall and reinstall a built
      one. Pass: the installed dresser is storage at Critical with Apparel. *Failure mode:* settings lost or not
      reset through the minified thing.
- [x] **Prove T4 Blueprint settings.** Place a dresser blueprint and open its Storage tab before building. Pass: the tab
      exists and shows Critical; edits carry over to the finished dresser. *Failure mode:* no `Blueprint_Storage`,
      so no tab or lost edits.
- T1, T3 and T4 proved by run build `a599149-dirty.20261110T232913Z` (3/3 PASS, 0 new errors; `docs/test-runs.md`).

## Phase 2: Wardrobe without VFE

- [x] `NoVFE/1.6/Defs/ThingDefs_Buildings/TT_Wardrobe.xml`: our own plain-furniture wardrobe with defName
      `Table_Wardrobe`, size (2,1), and our own description and stats (decision 4). It carries
      `CompProperties_Facility` (Comfort +0.05, maxDistance 6, maxSimultaneous 1) and
      `PlaceWorker_ShowFacilitiesConnections` (decision 3)
- [x] `NoVFE/1.6/Patches/TT_WardrobeBeds.xml`: add `Table_Wardrobe` to the beds' `linkableFacilities`, the same targets
      VFE patches (decision 3)
- [x] Our own wardrobe texture: `Art/NoVFE/…` SVG → `NoVFE/Textures/TidyThreads/Furniture/Wardrobe_{north,east,south}.png`
      via `tools/export-textures.sh` (decision 4; the tool's Art→Textures mapping may need the NoVFE path)
- [x] Wardrobe patch `Patches/TT_Wardrobe.xml` (`maxItemsInCell 15`, marker `capacity 30`) and GraphicsDef
      `TT_Wardrobe_Graphics` in `Defs/`, always loaded
- [x] **Prove T2 Build a wardrobe, VFE not active.** With VFE disabled, build the wardrobe in wood, steel and granite.
      Pass: same as T1 with a 30-item limit; the texture renders in all four rotations. *Failure mode:* the NoVFE
      folder doesn't load, a texture is missing, or capacity is wrong.
- T2 proved by run build `6e2179c-dirty.20261110T233235Z` (1/1 PASS, 0 new errors). Its scenario name is
  `T2 Build a wardrobe VFE not active`: the plan title's comma can't be in a name. The rotation check reads each
  rotation's material texture, not a screenshot. Its bed-link check was dropped (decision 7); rerun below.
- Our wardrobe: `ArtableFurnitureBase` (quality and art, like the dresser and VFE's), drawSize (2,2), 256 px textures,
  cost 70, HP 150, work 2600, mass 22, beauty 6, `uiOrder` 2071.
- The wardrobe patch also sets `surfaceType Item` and `canOverlapZones false` (Replace or Add), which the plan's patch
  lacks: neither wardrobe def has them, and items can only lie on a pass-through building with `surfaceType Item`
  (`GenPlace`). The dresser already has both.

## Phase 3: Moving apparel into new dressers

- [x] **Prove T5 Loose apparel, mixed types.** Spawn 25 items near one dresser: parka, tribalwear, flak vest, simple
      helmet, shield belt, one tainted item, one at 10% HP. Pass: haulers store exactly 20 and 5 stay outside; every
      type is accepted, including tainted and damaged. *Failure mode:* capacity overflow, or a subtype rejected.
- [x] **Prove T6 From lower storage.** Put 10 apparel in a Normal stockpile and 6 on a Preferred shelf, then build a
      dresser. Pass: all 16 move into the dresser and nothing moves back. *Failure mode:* Critical priority not
      honoured, or a hauling loop.
- [x] **Prove T9 Non-apparel rejected.** Spawn steel, meals and a rifle next to the dresser and open its filter. Pass:
      none are hauled in; the filter lists only Apparel. *Failure mode:* fixed filter not enforced.
- [x] **Prove T10 Linking and capacity.** Link 2 dressers and 1 wardrobe; try to link a dresser with a shelf; spawn 80
      apparel. Pass: the group holds 70; the shelf can't join the group. *Failure mode:* wrong `storageGroupTag`, or
      group capacity wrong.
- [x] **Prove T11 Unreachable dresser.** Wall in a dresser on all reachable sides. Pass: no hauling attempts loop, no
      errors, and apparel stays in other storage. *Failure mode:* job loop or errors on an unreachable target.
- T5, T6, T9, T10 and T11 proved by run build `98f9e4b-dirty.20261110T234809Z` (5/5 PASS, 0 new errors). Names drop the plan's commas
  (`T5 Loose apparel mixed types`). All five haul live on the quicktest map with a 30000-tick timeout (T10: 60000);
  T5 took 1350 ticks to store 20, T10 3150 to fill 70. `Scenarios.P3.cs` adds `ApparelStockpile` (apparel-only zone,
  deleted in `OnFinish`), `RequireHaulers`, `CountOn` and `CountSpawned`.
- T9 has a control shirt that must be hauled in first, so "nothing hauled" can't pass with idle haulers. T10 links
  through the real gizmo (`Find.Selector` + the "LinkStorageSettings" `Command_Action`), and checks the shelf's own
  Link is disabled next to a dresser. T11 asserts `StoreUtility.TryFindBestBetterStorageFor` never picks the dresser
  and samples colonists' jobs for 3000 ticks; vanilla's own job-loop error would show as a new red error.
- `Scenario.Finish` now also destroys tracked items a hauler is carrying (unspawned, so `ById` misses them).

## Phase 4: Dressers from saves made before the mod

- [x] Test harness: make a save without the mod and load it with the mod (decision 2), using a separate test-only
      helper mod that builds the pre-mod fixture and saves (decision 6). `autotest.py run --premod SPEC` (builder:
      `tools/premod-builder/`; specs: `Source/TidyThreads/PreMod/`) and `run --from-save NAME`; game side
      `AutoTest.LoadedSave` and `AutoTest.PreModManifest()` (also in `tools/game-side/AutoTest.cs`). Selftest: 4 new
      cases, all ok (2026-11-11, real instance stopped).
- [x] Converter, tried in plan order and stopping at the first that passes T7: Option 1 failed T7 at step 2
      (`Dresser9832 priority Normal, expected Critical`). Option 2, Patch B only, passed:
      `LegacyDresserConverter` postfixes `Building_Storage.ExposeData`. It runs inside ASF's `base.ExposeData()`,
      after vanilla's `Scribe_Deep` and before ASF's `settings ??= new(this)`, so a null `settings` there means the
      saved node had none: the null check works at that point, not in PostLoadInit. Fallback C wasn't needed.
- [x] **Prove T7 Conversion.** Without the mod: build 2 dressers (one Excellent with art), drop 15 apparel on the floor,
      save. Add the mod and load. Pass: the log shows the conversion message twice; both dressers have the Storage tab,
      Critical and Apparel; position, rotation, stuff, HP, quality and art title are unchanged; haulers move the 15
      items in. *Failure mode:* class not swapped, empty settings, or lost quality or art.
      Proved by run build `000782e-dirty.20261111T000221Z` (1/1 PASS, 0 new errors; two "converted pre-mod Dresser"
      lines; the Excellent dresser kept its art title).
- [x] **Prove T12 Bedroom and comfort** (decisions 8, 9): fresh map; a fixture room with a bed, 2 dressers and a
      wardrobe is a Bedroom; one with only 2 dressers and a wardrobe is a Storeroom. *Failure mode:* a room's role is
      wrong because of the apparel furniture.
      Proved by run build `c1fd1e1-dirty.20261110T235343Z` (1/1 PASS, 0 new errors): two `f.Rooms(2, 7, 5, RoomPower.None)` rooms, roles
      `Bedroom` and `Storeroom`.
- [x] **Prove T13 Uninstall and deconstruct while full.** Uninstall one full dresser and deconstruct another. Pass: all
      contents drop nearby, none are deleted, no errors. *Failure mode:* contents vanish (the 1.6 outfit stand bug).
- [x] **Prove T14 Save and reload.** Set one converted dresser to Normal, save, reload twice. Pass: contents identical;
      that dresser stays Normal and the others stay Critical. *Failure mode:* the converter re-runs and overwrites a
      player's priority.
- T13 and T14 proved by run build `000782e-dirty.20261111T000137Z` (2/2 PASS, 0 new errors) on their own pre-mod
  save (`PreMod/T13-T14.txt`: 4 empty dressers, no loose items, so T7's haul doesn't leak in). T14 reloads twice
  through `Scenario.SaveAndReload` and checks `LegacyDresserConverter.Converted` didn't grow.
- **TT-001 / T13 (2026-11-11):** T13 fills every free cell within 13 cells of its dressers with one forbidden wood log
  and checks with a probe shirt that vanilla's Near search finds no spot. Steel filler made the deconstruct refund fail
  too (`Failed to place all leavings`, a red error); wood logs take the refund as a stack. T14's dressers moved to
  x 32/35 in `PreMod/T13-T14.txt`, since T13's probe found their empty cells. Unfixed build: 36 of 40 shirts lost. The overflow-drop gotcha added to `docs/gotchas.md` is general: port it
  to the modding center.
- The builder's vanilla quicktest map isn't the harness's flat fixture tile (that patch lives in the mod); its site
  check uses `Walkable` like `Fixtures.Site`, since `Standable` rejected every forested site.

## Phase 5: Wardrobe with VFE, and VFE added to or removed from a save

- [x] Test harness: VFE as a test-only extra mod (`MOD_EXTRA_MODS=VanillaExpanded.VFECore`), and a save made with one mod
      set loaded with another (decision 2). VFE needs VE Framework, so
      `MOD_EXTRA_MODS=OskarPotocki.VanillaFactionsExpanded.Core,VanillaExpanded.VFECore`. `run --switch-mods MODS`
      runs a scenario's first half (it saves `tidythreads_handover`), then cold-loads that save under MODS for the
      second half. Baselines per extra-mod set (decision 10) and per transition (`baseline --switch-mods`: an empty
      save made under one set, loaded under the other, without the mod). Selftest: 4 more cases, all ok.
- [x] Check VFE's def is still plain furniture at build time: yes (2026-11-11, `1718190143`: `VFE_WardrobeBase` →
      `ArtableFurnitureBase`, no `thingClass`), so step 3 stays Add
- [x] **Prove T15 Add VFE to a save.** VFE off: build 2 wardrobes, fill with 30 apparel each, set one to Normal, save.
      Enable VFE, restart, load. Pass: both wardrobes still exist with VFE's graphic, 60 items, one Normal and one
      Critical. *Failure mode:* a defName mismatch deletes wardrobes, or settings are lost.
- [x] **Prove T16 Remove VFE from a save.** VFE on: build 2 VFE wardrobes, fill, save. Disable VFE, restart, load. Pass:
      both load as our copy with contents and settings; only harmless VFE warnings, no red errors from this mod.
      *Failure mode:* wardrobes lost, or red errors from leftover VFE data.
- [x] **Prove T17 Fresh game with VFE.** VFE on, new colony; open Architect › Furniture, build a wardrobe, check Loaded
      Files For Mod. Pass: exactly one wardrobe in the menu; it stores 30 apparel at Critical; NoVFE files aren't
      loaded. *Failure mode:* two wardrobe defs, a texture path collision, or wrong capacity.
- T15 proved by run build `110fc64-dirty.20261111T011310Z`, T16 and T17 by `110fc64-dirty.20261111T010920Z` /
  `…T010825Z` (each 1/1 PASS, 0 new errors; T15 and T16 in both halves). Names drop the plan's commas.
- **Harness bug found by T17:** the harness wrote `ModsConfig.xml` ids as typed (`VanillaExpanded.VFECore`). The game
  keeps them as written and looks them up lower-cased, so `IfModNotActive` loaded `NoVFE/` beside VFE (VFE's def won
  only by loading later). The game saves ids lower-cased, so players aren't affected. `write_profile` now
  lower-cases; every VFE baseline was re-recorded and T15–T17 rerun after the fix.
- T16's VFE-off half logs 37 leftover-data signatures in any save (missing VEF/KCSG/MVCF/PipeSystem component
  classes, VFE's `MF_*` research), none from this mod, ASF, Harmony or the furniture. The none → VFE transition
  logs only the 6 engine signatures. `VANILLA_WARNINGS` in `autotest.py` stops two vanilla `Log.Warning` texts
  counting as errors: `Could not find think node with key` (after adding VE Framework) and `Object with load ID …
  not deep-saved` (a random world pawn in the builder's save).
- Scenarios use `VfeActive` (`SamePackageId(…, ignorePostfix: true)`), also in T2; T2 rerun:
  `110fc64-dirty.20261111T010053Z`, 1/1 PASS.
- An instance launched from a save has no `-quicktest`; an in-game restart left it on the main menu, so a new build
  now restarts it cold.

## Phase 6: Release

- [x] Port the general tool changes (harness, package, lint, `ilspycmd` pin) to `rimworld-modding-center` (decision 2):
      modding center `44c47db` and `0542d72` (pkg-run takes `--premod`/`--from-save`/`--switch-mods` for the gate), selftest 31/31 ok in both repos (2026-11-11). `vanilla.sh` now pins `ilspycmd` 11.1.0.9782
      (`MOD_ILSPY_VERSION` overrides). The user's uncommitted `README.md` and `docs/wiki/` there were left alone.
      Queen Bee and Quadrunner were not updated (their repos are off limits from here).
- [x] `About/Preview.png` (< 1 MB: 119 KB), README with the mod-removal warning (removal is unsupported per the plan: uninstall
      dressers first), LICENSE, CHANGELOG
- [x] `tools/package.sh` produces a `dist/TidyThreads` that includes `LoadFolders.xml` and `NoVFE/` (checked on `7477b69.20261111T030637Z`)
- [x] Release gate (user-started or user-approved only): T1–T17 on the packaged copy via `tools/autotest.py pkg-run`
      Run 2026-11-11 on package `2c3e271.20261111T012806Z` (approved with the Phase 6 request), six `pkg-run` launches
      (quicktest VFE off; `--premod` T7; `--premod` T13–T14; `--switch-mods` none→VFE; VFE→none; VFE fresh): 16/17 PASS,
      0 new errors. **T11 failed inside the gate sequence** (1 colonist job aimed at the walled-in dresser's cell, no item
      moved; it ran right after T10) and **passed alone** on the same package.
      **Rerun 2026-11-11 after TT-001** (user-requested) on package `7477b69.20261111T030637Z`, the same six launches:
      **17/17 PASS, 0 new errors.** T11 passed in sequence. Launch D's first try passed both T15 halves but logged one
      vanilla red error on load (`Could not resolve reference … Thing_Human3550 … DirectPawnRelation`): the first half's
      save had logged that a random world pawn's relation pointed at a pawn that wasn't deep-saved. Nothing to do with
      the mod; D's rerun on a fresh world: 0 new errors.
