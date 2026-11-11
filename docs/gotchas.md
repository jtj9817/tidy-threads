# RimWorld 1.6 gotchas

Engine and API behaviors that cost real debugging time in [Queen Bee (QB)](learnings/queen-bee.md) and
[Quadrunner (QR)](learnings/quadrunner.md), plus [Colony Web (CW)](learnings/colony-web.md). Each line is one lesson; the linked learnings pages cite the
evidence. Verify anything load-bearing against the decompiled source (`tools/vanilla.sh`).

## Defs and mod structure

- A ThingDef `defName` must not end in a digit: ThingIDs append a number, so save/load reference resolution
  fails. (QR; `rwdefs.py lint` checks it)
- `Corpse_<race defName>` is an implied def generated before the second cross-reference pass; XML may
  reference it. (QR)
- Share physics/rendering in an abstract def (`Name` + `Abstract="True"`), derive variants via `ParentName`
  overriding only `thingClass`/graphic; tunables in a `DefModExtension`. (QB)
- Only Core installed ⇒ never reference DLC defs or systems; `knownExpansions` being empty is the tell. (QB/QR)
- The wiki may document a newer version than the installed game (1.7 docs vs a 1.6 target: different TFM,
  assemblies, folders). Pin to `Version.txt`. (QB/QR)
- `About/Preview.png` 640×360 under 1 MB (Steam rejects larger); `ModIcon.png` 64×64 optional. (QB/QR)
- Harmony mods: reference `Lib.Harmony` with `ExcludeAssets="runtime"` (the Harmony mod supplies `0Harmony`
  at runtime) and declare `brrainz.harmony` in `modDependencies`/`loadAfter`. (QB/QR)
- Def references (`ParentName`, `costList`, lists, research prereqs) are lintable offline against Core + own
  Defs before spending a restart — that's `rwdefs.py lint`. (QB)
- `[TweakValue]` works only on static fields; XML tunables can't be tweaked live. (QB/QR)
- Recipes can produce a `PawnKindDef` — a workbench can manufacture pawns through normal bills. (QR)
- `StatPart`s on a stat are ordered; a later part can replace rather than multiply. Read the chain order. (QR)

## Startup, assemblies, logging

- Startup order: assemblies → Def parse/inheritance/mod-extension binding → textures →
  `[StaticConstructorOnStartup]`. Static `Material` construction belongs in a static constructor, not first
  draw. (QB)
- `DebugActionAttribute`/`DebugActionType`/`AllowedGameStates` live in `LudeonTK`, not `Verse` (1.6). (QB/QR)
- A method named `Log` in your class shadows `Verse.Log`; qualify `Verse.Log.Message(...)`. (QB/QR)
- Log through one dev-mode wrapper with a `[ModName]` prefix and exactly one unconditional build-id line
  (from `AssemblyInformationalVersionAttribute`); the status tooling keys on it. (QB)
- `Player.log` has no timestamps; cut by byte offset or per launch. 1.6 deduplicates stack traces and later
  writes `[Ref XXXXXXXX] Duplicate stacktrace, see ref for original` — resolve the ref. With `DebugType
  embedded`, your frames print real `File.cs:line`. (QB/QR)
- Mod log lines only appear in Dev mode. (QB)
- `GenCommandLine.TryGetCommandLineArg` splits on `=` into exactly two parts — values must not contain `=`;
  `CommandLineArgPassed("x")` matches `x` or `-x` case-insensitively, so mods can read custom flags. (QR)
- `Root.Shutdown()` empties Unity's temp/cache dirs (shared with the user's game unless `XDG_*` overridden)
  and also shuts Steam down. (QR)
- Workshop mods (incl. Harmony) load only while the Steam API initializes; `steam_appid.txt` prevents a
  relaunch. Steam must be *running* for automated launches. (QR)
- `Version.txt` and the banner can differ in revision for one build; only `Version.txt` changing is an update. (QR)
- Mono can segfault on launch/restart; record it as a crash, don't hide it; killing the process makes Steam's
  socket thread assert and write breakpad dumps — prefer in-game quit/restart. (QR)

- A comp added in C# at startup (`def.comps.Add(...)` for every `race` ThingDef in a `[StaticConstructorOnStartup]`)
  reaches modded races and leaves no def reference in saves, unlike an XML patch on `Human`. Comps of existing pawns
  are created on load (`InitializeComps` in `ExposeData`), so old saves get it too. (CW)
- Comp gizmos (`CompGetGizmosExtra`) show for **any** selected pawn, visitors and enemies included:
  `Pawn.GetGizmos` yields `base.GetGizmos()` (the comps') before its colonist-only part. (CW)

## Saves and persistence

- A save made before a storage mod existed loads its dressers through ASF's class swap with **no `settings` node**,
  and ASF then fills `settings ??= new(this)` (empty filter, Normal). Detect it in a postfix on vanilla
  `Building_Storage.ExposeData`: that runs inside ASF's `base.ExposeData()`, after `Scribe_Deep` left `settings`
  null and before ASF's fallback. A PostLoadInit null check never fires. (TT)

- Keep persistent state on Things/buildings/comps (`Scribe_Collections/References/Values/TargetInfo`);
  avoid `MapComponent`/`GameComponent` (a `GameComponent` is instantiated into every game and saved — it
  also breaks clean removal; static per-map caches rebuilt from saved objects leave no residue). (QB/QR)
- Save `ThingID`/`loadID` numbers, not object references; re-resolve per use, treat missing as finished, and
  re-find everything after a load — pre-load references belong to the dead `Game`. (QB/QR)
- Every new saved field needs a `Scribe_*` line whose default is right for old saves. Saves key defNames,
  `thingClass`/`workerClass` names, namespaces and scribe keys by string: renaming any is destructive on
  installed saves. `packageId` never changes once saves exist. (QB/QR)
- Recompute derived values in `SpawnSetup`/`PostLoadInit` (recreate null collections, drop dead refs) instead
  of saving them. (QB)
- Elect exactly one owner for shared state with a deterministic key (lowest `thingIDNumber` plus a saved
  seniority int that survives pawn rebuilds); prefer one-directional references. (QR)
- Never serialize transient visual state; rehydrate as "already true" to avoid replaying animations. (QR)
- Static (non-saved) runner state survives save/load because assemblies aren't reloaded. (QR)
- `thingIDNumber` is save-stable, but a re-created pawn ("upgrade = new pawn") loses identity; keep the
  reference or a copied-forward seniority. (QR)
- `MapPawns.AllPawns` includes pawns inside map things (via child holders); `listerThings` does not. (QR)
- A killed pawn can't be found by ThingID (the corpse has another ID); keep pawn/corpse references. (QR)
- Removing a mod mid-save is unsupported unless the mod ships a destructive "prepare removal" export, and the
  proof must run in a fresh process without the assembly. (QB/QR; see [testing.md](testing.md))
- `Map.ExposeData` calls `FillComponents` after serializing even during Saving. A removal export can omit a component
  on disk while vanilla recreates an empty one in memory; strip again before unloading to the main menu. (RimLogistics T28)

- `Scribe_Values.Look` skips writing a value equal to its default. A comp on every pawn should save nothing for
  pawns without data: call `Scribe_Collections.Look` only when the collection is non-empty while saving, always while
  loading (a missing node leaves it null). (CW)
- `WorldPawnGC` keeps every ever-colonist humanlike forever (reason "Colonist"); other world pawns, including dead
  raiders and visitors, may be discarded. History about outsiders belongs on colony pawns, with a name snapshot. (CW)
- `PlayLog` keeps only 150 entries (`PlayLog.Add` → `ReduceToCapacity`); `BattleLog` keeps 20 battles. Anything
  longer-lived needs its own record; postfix `PlayLog.Add`/`BattleLog.Add` to see every entry. (CW)
- De-duplicating backfill only against capped event details inflates full counts when vanilla still retains older
  entries. Keep saved LogIDs separately, trim them against retained vanilla logs, and test past the detail cap plus
  save/reload (CW T3: 30 chats doubled to 60 before the fix; 35 stayed 35 after it). (CW)
- `WorldPawns.RemoveAndDiscardPawnViaGC` calls `DiscardPawn(..., silentlyRemoveReferences: true)`, removing vanilla
  log references without discard warnings; a mod's outsider snapshot must survive independently. (CW)

## Pawns, races, jobs

- `RaceProperties.Animal` + custom flesh can make `Animal`, `IsMechanoid`, `IsFlesh` all false: no
  taming/training/hunting/manhunters/needs/food/rest, but `playerSettings` and `training` still exist; disease
  incidents only pick `IsFlesh`. Don't reuse vanilla `Mechanoid` flesh (game code assumes hostile mech);
  a custom flesh type gives no-bleed/no-rot/mechanical wounds on Core alone. (QR)
- Intelligence: `Animal` = no weapons; `ToolUser` = can hold equipment (vanilla mech path). (QR)
- `PawnKindDef` holds graphics, `combatPower`, scale; keep mechanics on the race def. (QR)
- Non-humanlikes get no drafter from `PawnComponentsUtility`, and `Pawn_DraftController.GetGizmos` is
  `internal` — a mod-provided draft toggle builds the gizmo itself. Drafting also needs passing the
  player-control predicate; patching one narrow predicate (`IsColonyPlayerControlled`-family) unlocks several
  vanilla behaviors at once. (QR)
- Hauling requires `CapableOf(Manipulation)` — the body needs a `ManipulationLimbCore` part. Work priorities
  don't exist for non-colonist pawns: drive work via a think tree + the race's `workGivers`. (QR)
- Think-tree node order **is** the priority contract; `ThinkNode_QueuedJob` high so player orders beat
  autonomy; `JobGiver` returns a job or null to fall through. Reuse vanilla `JobDef`s; build jobs via the
  giver's `JobOnThing` so reservations/stack counts/toils stay vanilla. (QR)
- `GenConstruct.CanConstruct` dereferences `pawn.workSettings` (null for non-colonists) unless forced —
  prefix or guard. (QR)
- A worker must be **spawned** to scan for work; a pawn despawned into a container stops ticking and thinking;
  a despawned/docked pawn must be undocked before `Notify_Teleported`. (QR)
- Drafting/undrafting mutates colonist lists — iterate `FreeColonistsSpawned.ToList()`. Drafting stops work
  but not shooting (engages ~25 cells). Vanilla `AutoUndrafter` undrafts after 10000 `Wait_Combat` ticks
  without a hostile. (QR)
- `PawnGenerator` doesn't name custom pawns; the creating code must, or name-reading code NREs. (QR)
- Zeroing a capacity (`Moving = 0`) via a hediff downs without killing; make the hediff itself the state.
  `Hediff.age` is a free elapsed-time timer. (QR)
- A pawn held by a ticking building shows job "Carried" (`JobGiver_Carried`, Despawned tree) — normal. (QR)
- `Pawn.Tick` runs comps before jobs, so a pickup is observable one tick before any `CompTick` records it.
  Explosions resolve over several ticks — "downed or dead" checks lag. (QB/QR)

- `Pawn_InteractionsTracker.TryInteractWith` logs a red **error** when called within 120 ticks of the pawn's last
  interaction (unless the def ignores it) and needs the pair within interaction range: reset the private
  `lastInteractionTime` (-9999) and put the pawns side by side before forcing one in a fixture. (CW)
- Downs and kills reach `BattleLog.Add` as `BattleLogEntry_StateTransition` with the instigator (from the
  `DamageInfo`), but only for spawned pawns, and a down only when the hediff's `recordDownedTale` is true (the
  default). `AddHediff(Anesthetic, null, new DamageInfo(..., instigator: x))` is a deterministic "x downed y". (CW)
- A violently downed non-colony pawn may **die instead** (death-on-downed chance from population intent and
  difficulty, `Pawn_HealthTracker.CheckForStateChange`); a fixture that needs a downed raider sets
  `health.overrideDeathOnDownedChance = 0f` first. (CW)
- Prisoner beds are usable only inside a prison cell (`RestUtility.CanUseBedNow`: `ForPrisoners &&
  !IsInPrisonCell` → false), so a Capture job toward an outdoor prisoner bed ends silently at `StartCarryThing`.
  Calling `guest.CapturedBy` directly on a freshly generated raider NREs on its null `workSettings`; capture through
  the real job into a walled, roofed cell with the bed placed before the walls. (CW)
- A job that ends silently: `pawn.jobs.debugLog = true` logs every start/end with the toil and end condition. (CW)
- `TendUtility.DoTend` returns early when nothing needs tending; capture that in a prefix (`__state`) if a postfix
  must know a tend happened. (CW)
- Social interactions to several pawns are `PlayLogEntry_InteractionWithMany` (a subclass of
  `PlayLogEntry_Interaction`); `GetConcerns()` yields every participant for both shapes. (CW)
- Vanilla hauling keeps moving stored stock: `HaulMerge`/`HaulGeneral` shift partial stacks between stockpiles of
  equal priority, and a stack being carried is unspawned (gone from `listerThings`) for the trip. Never assert that a
  destination keeps what was delivered; assert what your code credited, and conservation counting carried stacks. (RL)

## Buildings, power, work, bills

- `Building_WorkTable.CurrentlyUsableForBills` needs `PowerOn`; a lone consumer has no PowerNet —
  `DebugSettings.unlimitedPower` does nothing until a conduit touches it, and it refills every battery each
  tick (never use it while measuring stored energy). (QR)
- Batteries are transmitters that join a net only through a physically touching conduit (6-cell radius is for
  consumers); they self-discharge 5 W/tick. Charging draws **stored** energy (`net.batteryComps`, fullest
  first) — a producing net without batteries can't charge. Identify conduits by transmitter comp, batteries by
  `CompPowerBattery`, consumers by `CompPowerTrader`. (QB/QR)
- Power comes on **late**: a spawned consumer's connection is queued until the map's next tick
  (`PowerNetManager.delayedActions`), then `PowerNet.PowerNetTick` switches waiting consumers on only every
  `max(30, 200/n)` ticks, ~5% at a time — one bench takes up to 200 ticks. Gate on `PowerOn` in an `Expect`, never
  in the `Do` that spawned it. (decomp; [scenario-fixtures.md](scenario-fixtures.md))
- `VanometricPowerCell` (Core, 1×2, +1000 W, not in `ThingDefOf`) is a transmitter: consumers within 6 cells
  connect with no conduit. Any transmitter (battery, cell) spawned on a conduit's cell **wipes** the conduit
  (`GenSpawn.SpawningWipes`); a wall spawned over one keeps it. (RL/QR)
- `ElectricTailoringBench` works unpowered at 50% speed (`unpoweredWorkTableWorkSpeedFactor`), so
  `CurrentlyUsableForBills` is true without power; assert `CompPowerTrader.PowerOn` instead. (decomp)
- `GenSpawn.Spawn` wipes items and buildings in the footprint but **never pawns**: walls spawned on a colonist
  wall them in. Move pawns out first. Destroying walls under a constructed roof queues a collapse of roof left
  > 6.9 cells from a holder (`RoofCollapseCellsFinder`); unroof before tearing a fixture down. (decomp)
- `MakeMinified` despawns with `DestroyMode.Vanish` (never calls `Destroy`) — `DeSpawn` can't distinguish
  uninstall from destruction; put recall/refund in `DeSpawn`, destruction side effects in `Destroy`. A
  `MinifiedThing` ticks the inner building with no map — guard `Tick` with `Spawned`. (QB/QR)
- `WorkGiver_DoBill` decides via `bill.ShouldDoNow()`; `RecipeWorker.AvailableOnNow` is UI-only; "Do until
  you have X" counts through `recipe.WorkerCounter`. `RecipeDef` has no per-bill class — gating one bill is a
  postfix on `Bill_Production.ShouldDoNow`. A no-product recipe must override `CanCountProducts`/
  `CountProducts`/`CanPossiblyStore` (defaults index `products[0]`); grant effects in
  `Notify_IterationCompleted`. (QB/QR)
- The fabrication bench uses the Smithing work type with the Crafting skill. (QB/QR)
- `WorkGiver_CleanFilth` queues filth within ~5 cells of the first target — trim the list if you filter by
  ownership/radius. Fresh filth isn't work for 600 ticks. `FilthMaker` refuses dirt on natural ground (spawn
  `Filth_Dirt` directly); rain washes unroofed filth. (QR)
- `TryStartCarry(thing, count, reserve: false)` splits/despawns/transfers stacks out of the map container. (QR)
- Force a bill via `WorkGiverDef.Worker.JobOnThing(pawn, bench, forced: true)` + `TryTakeOrderedJob`. (QR)
- `Room.IsPrisonCell` recomputes only on shape changes; check `Role` too and set `ForOwnerType` (not
  `ForPrisoners = false`, which logs a vanilla error). (QR)

## Combat, targeting, explosions

- `AttackTargetsCache` holds only pawns and `Building_Turret`; other buildings come from
  `listerBuildings.allBuildingsNonColonist`. Hostiles shoot any spawned `IAttackTarget` with a hostile
  faction and `ThreatDisabled() == false`. (QB/QR)
- A custom flying/shootable non-pawn works as `ThingWithComps, IAttackTarget` with `category Ethereal`,
  `drawerType RealtimeOnly`, `altitudeLayer Projectile`: track the exact `Vector3`, write back
  `Position = exact.ToIntVec3().ClampInsideMap(Map)`, test arrival against
  `Mathf.Max(ArriveDistance, speedPerTick)`, set the protected `factionInt` **before** `GenSpawn.Spawn`
  (spawning registers the attack target; `SetFaction` refuses Ethereal). Ethereal things are skipped by
  explosion damage; fake depth with small `Altitudes.AltInc` Y offsets. (QB)
- `GenExplosion.DoExplosion(..., ignoredThings: ...)` exists and spares specific spawned things. Explosions
  land over several ticks (cell tick ≈ distance × 1.5): forget the target when the strike commits or a second
  attacker launches. Vanilla `Bomb` is 50/0.10 — marine armor deflects most of it; pass explicit damage/AP. (QB)
- Downed pawns must be excluded from valid targets or attackers re-strike corpses-in-waiting; carried pawns
  aren't spawned, so radial scans and blasts naturally skip them. (QB)
- The vanilla `Pirate` FactionDef has null `basicMemberKind` — NREs `PawnGenerationRequest`; use
  `faction.def.basicMemberKind ?? PawnKindDefOf.Pirate`. (QB/QR)
- One `LordJob_DefendPoint` shared by pawns clusters them (28-cell radius + flee toil by default); give each
  hostile its own lord with small radii and `addFleeToil: false`. `Raid.Active` is cached ~1 s —
  `Raid.Recheck(map)` after spawning. (QB/QR)
- A gun destroyed/swapped mid-cooldown leaves `Stance_Cooldown` on its verb, which the next save can't
  resolve — end stances before removing things. (QR)
- `CancelBusyStanceSoft` ends only a warmup. (QR)
- EMP-style disabling is `StunHandler.StunFor(ticks)` (1500 ≈ vanilla EMP grenade); mech-cluster turrets have
  `CompStunnable`/`CompMechanoid` dormancy — skip `startsDormant`-false clusters or you'll wake one. (QB)

## Rendering, textures, art

- Textures are unlit: bake shading, dark outline, clear silhouette; no sub-2px detail. At 128 px/tile the
  canvas matches `drawSize` (2×2 building ⇒ 256×256), multiple of 4, ideally power-of-two. In-memory
  compression adds white fringes at transparent edges — keep edge colors in transparent pixels or a ~1%
  dark outline. (QB/QR)
- Author facing north and rotate toward heading (one `Graphic_Single`) for flying things; multi-facing pawns
  need `_north/_east/_south` (west mirrors east — never author it), and layer part files keep the facing
  **last** (`X_Part_east.png`) for `Graphic_Multi`. (QB/QR)
- SVG in `Art/` is the source of truth; export via Inkscape (`export-textures.sh`), sized by the SVG's own
  width/height; `id="part-<name>"` elements export as separate layers; `Art/About/` last. `<pattern>` fills
  survive `--export-id-only`. (QB/QR)
- A missing texture is a pink square — "no pink graphics" is a valid release check. (QB)
- Build a custom `PawnRenderTreeDef` by copying Core's animal tree and replacing only the worker you need
  (no Harmony); keep a hidden `Whole`-sprite node so icons/portraits/cards work. Pawns that are neither
  Animal nor Humanlike use Core's Misc tree. (QR)
- A freshly spawned pawn has null `renderer.BodyGraphic` until drawn — call `EnsureGraphicsInitialized()`
  before reading the tree. `ParallelPreDraw` recomputes node matrices per frame **off the main thread**
  (workers must only read); the draw list rebuilds only on facing/posture change. Off-screen pawns keep their
  last matrix — keep the camera on the subject. (QR)
- Drive limb animation from distance travelled along the facing (not a timer); never serialize animation
  state. (QR)
- `PawnRenderer.BodyAngle` needs a postfix to change how a carried pawn lies (vanilla fixes it for humanlike
  carriers). (QR)
- Glow textures may have no alpha (`LightningGlow` is for additive `MoteGlow`; with `Transparent` it draws a
  dark square) — use your own alpha texture. (QR)
- `ScreenCapture.CaptureScreenshot` works on Xvfb: pause, `CameraDriver.SetRootPosAndSize`, count frames
  (no ticks pass while paused). The camera and its config are replaced on load — re-apply framing. The
  camera's minimum zoom (`CameraMapConfig.sizeRange`) can be lowered for captures. (QR)
- Game resolution ≠ display size: `Screen.SetResolution` grows the framebuffer, not Xvfb's pointer bounds —
  UI/pointer checks need a matching display (cold-start with `MOD_SCREEN`). (QR)
- `Listing_Standard` inside a scroll view needs `maxOneColumn = true`: when dynamic content grows beyond the previous
  height, its default column wrapping sends controls off-screen and can shrink the remembered height every frame. (CW)
- Canvas selection must not recenter a node between the clicks of a double-click; center only for the gizmo or
  explicit focus action, so the second click still hits the same pawn. (CW)
- At autotest's idle 5 fps, `Widgets.CheckboxLabeled` polls `Input.GetMouseButtonDown/Up`: a short `xdotool click`
  can vanish between frames even when IMGUI buttons work. Hold down for 350 ms, then release on a later frame. (CW)

## Harmony

- Patch only where vanilla exposes no virtual/hook; check def/XML extension points first (stat parts,
  render-tree workers, think-tree nodes, `FloatMenuOptionProvider` for right-click menus, vanilla job
  drivers). One `[StaticConstructorOnStartup]` + `PatchAll` bootstrap; keep a patch budget; note why per
  patch in the plan. (QB/QR)
- A prefix relaxes humanlike assumptions (`workSettings` null, skill checks); a postfix on
  `PawnComponentsUtility.AddAndRemoveDynamicComponents` injects missing vanilla components (e.g. a drafter);
  postfix draw-position/angle getters for visual-only changes; postfix AI finders but only supply a result
  when vanilla found none. (QR)
- Test-only patches: apply unconditionally, return behind a runtime flag — simpler than conditional patching. (QR)
- `AccessTools.FieldRefAccess` reaches private state (e.g. reset `AutoUndrafter` per frame). (QR)

## Engine misc, performance, tooling quirks

- `ModsConfig.IsActive` looks up the id lower-cased, but the game keeps `ModsConfig.xml` ids **as written**. A
  hand-written config with `VanillaExpanded.VFECore` makes `IsActive` and `LoadFolders.xml` `IfModNotActive` miss
  a mod that loaded fine. The game always saves lower-case; a test harness must write lower-case too. (TT)
- Player.log has no severity, so "Could not ..." from `Log.Warning` looks like an error to a word-matching scanner
  (`Could not find think node with key`, `Verse.AI/Job.cs`, after a mod changes the think tree under a save). (TT)

- `tools/export-textures.sh` with no arguments exits non-zero while `Art/About/` doesn't exist (its `find` fails under
  `pipefail`); pass the SVG paths, or create `Art/About/`. SVGs live under `Art/Textures/…`. (CW)

- A fresh quicktest map has less than 70 days of history: subtracting retention can produce negative fixture ticks
  (Colony Web's `Recorder.Record` interprets any negative tick as “now”). Test an explicit future pruning cutoff,
  capture the setup tick for later assertions, and exclude incidental opinions from a combat-only fixture. (CW)
- `TickManager.CurTimeSpeed`'s setter drops values while the screen fades in (`PlayerHasControl` false) —
  re-issue Ultrafast for ~30 ticks after a load. Ultrafast ≈ 20 ticks/frame; `UltraSpeedBoost` raises it. (QR)
- Each quicktest map is fresh and random (trees, chunks, center may be solid rock; `Walkable` ≠ `Standable`);
  fixtures find clear ground themselves, scanning cells nearest-first (`CellFinder.TryFindRandomCellNear`
  misses small clear areas). (QR)
- Mixing an autostart save with `-quicktest` races scene changes (`InitNewGame` with a null world). (QR)
- Rate-limit central AI (fast dispatch ~60 ticks, slower discovery ~120) with a `force` flag and re-entry
  guard; pathfind only when assigning; cache spatial queries per map with a change signature + max age;
  recheck volatile conditions per use; `RegionTraverser` with the pawn's `TraverseParms` respects forbidden
  doors. Cooperative ticking (comp ticks) has no guaranteed cadence — keep an always-present ticker. (QR)
- `dotnet build` writes only to `Source/<mod>/bin/`; always deploy through `build.sh` (atomic rename, so a
  running game's mapped DLL is untouched and a starting game never sees a half-written file). (QB/QR)
- On root-owned mounts run git as `git -c safe.directory="$PWD"`; prefer the edit tool over `sed -i`. (QB/QR)
- A dirty tree restamps the DLL every build ⇒ an unnecessary in-game restart every run; commit before
  measuring. A bare `dotnet build` is the safe compile check *during* a run. (QR)
- Distinguish the test instance from the user's game by command line (`-savedatafolder=`), never by window
  title. Xvfb hygiene: high display numbers, record the pid, verify the process name before killing. An idle
  test instance still renders ~370% CPU — cap its frame rate or stop it. (QR)
