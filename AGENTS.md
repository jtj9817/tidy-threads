# AGENTS.md

Instructions for any AI agent working on this repo. Claude Code reads [CLAUDE.md](CLAUDE.md), which imports this file.

**Tidy Threads** is a RimWorld 1.6 mod (Core only, no DLC; requires Harmony and Adaptive Storage Framework). It turns
every dresser into apparel-only storage for 20 items and adds a wardrobe for 30, both defaulting to Critical. The
wardrobe is VFE's `Table_Wardrobe` when Vanilla Furniture Expanded is active, or this mod's same-defName copy when it
isn't. Identity: `TidyThreads` (assembly, namespace, `Source/` dir, log tag `[TidyThreads]`),
`josh9817.tidythreads` (packageId and Harmony id; never change it once saves exist), prefix `TT_`.

**The design plan** is the living doc
[Apparel Storage for Dressers & Wardrobes — RimWorld 1.6 Mod Design](https://claude.ai/artifact/DmZxu6jsgd5qVrcYTtZhTz).
Read it with the Claude Docs tools, not a web fetch. [`docs/plans/0001-tidy-threads.md`](docs/plans/0001-tidy-threads.md)
is a versioned snapshot of it. The living doc is canonical: change it first, then refresh the snapshot. Its code uses
placeholders; [`TODO.md`](TODO.md) maps them to the real names and records which of its "unverified" items have since
been checked (and which plan code is wrong, such as Patch B's null check and the wardrobe's stats). Don't re-open a
recorded decision without asking the user.

**Method and tools** are copied from `/mnt/0B8533211952FCF2/rimworld-modding-center` at commit `211fc88`, which stays
canonical. This repo owns its `tools/` copy. Read [`docs/playbook.md`](docs/playbook.md),
[`docs/testing.md`](docs/testing.md) and [`docs/gotchas.md`](docs/gotchas.md) before writing Defs or C#;
[`docs/tooling.md`](docs/tooling.md) catalogs the tools and [`docs/scenario-fixtures.md`](docs/scenario-fixtures.md) the
fixture kit. Other repos on this mount, the Steam workshop folder and the game install are read-only from here. Look
things up, never from memory:

- Vanilla code and Core Defs: `tools/vanilla.sh grep <regex>` / `tools/vanilla.sh def <defName>` (decomp in `.tools/decomp`).
- ASF: its XML, docs (`Docs/modules/ROOT/pages/*.adoc`) and full C# source (`Source/AdaptiveStorage/`) are in
  `~/.steam/debian-installation/steamapps/workshop/content/294100/3033901359/`.
- VFE: `…/294100/1718190143/` (`1.6/Defs/Buildings_Furniture/Buildings_Modern.xml` holds `Table_Wardrobe`).
- Offline wiki: `/mnt/0B8533211952FCF2/rimworld-wiki-local/data/site/pages/`.

## Recorded decisions

From the plan unless noted; ask before changing any of them.

- **Scope:** dressers (vanilla, patched) and one wardrobe. Out of scope: per-pawn dresser ownership, the Odyssey outfit
  stand, VFE's royal and illuminated wardrobes, and RimWorld 1.7. The dresser keeps its comfort facility, quality,
  beauty, cost and research.
- **XML first:** the dresser and the wardrobe are changed with PatchOperations, never overridden, and use ASF's
  `AdaptiveStorage.ThingClass` with vanilla `maxItemsInCell` (10 × 2 cells and 15 × 2 cells). The filter is locked to
  Apparel with `fixedStorageSettings`; `defaultStorageSettings` gives Critical; `storageGroupTag TT_ApparelFurniture`.
- **Critical is a default, not a lock.** Players may lower it, and that choice must survive reloads.
- **Wardrobe:** our copy keeps VFE's defName (`Table_Wardrobe`), loads only through `LoadFolders.xml`
  `IfModNotActive="VanillaExpanded.VFECore"`, and is plain furniture. One always-loaded patch converts whichever
  wardrobe def exists. Never copy VFE Framework classes or comps.
- **C# has one job:** converting dressers that were in a save before the mod was added. Try the options in order and
  stop at the first that passes T7: ASF's built-in conversion, then Patch B (settings init), then Fallback C (respawn).
- **Removing the mod mid-save is unsupported:** the README says so and tells players to uninstall dressers first. There
  is no removal-prep scenario and no `removal-check`.
- **Core only, no DLC:** test on Core + Harmony + ASF (+ VFE only for the VFE scenarios); no `MayRequire`.

## Tracking and test rules

User rules, binding on every session:

- **[`TODO.md`](TODO.md) is the implementation tracker.** It holds every phase, its tasks, the open questions and the
  verified facts. Read it at session start, and update it in the same commit as the work.
- **The tests are the plan's scenarios T1–T17, nothing more and nothing less** (the plan has no T8). Each phase's
  **Prove** items name the ones it needs. Don't add scenarios, unit tests or test projects;
  prove behaviour with those named in-game scenarios only.
- **Scenario names** are `T<n> <plan title>`, for example `T1 Build a dresser`. No commas (the filter splits on them).
  Run them by exact name: `tools/autotest.py run "T1 Build a dresser"`.
- **Every scenario also passes only with no new red errors** from this mod, ASF or Harmony (diffed against the
  baseline, which is recorded without the mod but with Harmony and ASF).

## Roles

- **The agent** writes code and assets, builds, runs the tests itself in the isolated instance (`tools/autotest.py`,
  autotest loop), reads the logs, notes and iterates.
- **The user** settles design questions (see the TODO's open questions) and plays their own game, often at the same
  time. Never close, kill, focus, screenshot or send input to their game; never touch their profile.

## Layout

| Path | Holds |
| --- | --- |
| `About/`, `LoadFolders.xml` | Mod metadata; load folders (`/`, `1.6`, and `NoVFE` + `NoVFE/1.6` only without VFE) |
| `Defs/`, `Patches/` (layout pending, TODO open question 1) | ASF GraphicsDefs; the dresser and wardrobe patches |
| `NoVFE/` | Our wardrobe def and its textures, loaded only when VFE is absent |
| `Source/TidyThreads/` | The C# project (mod class, marker extension, converter, `AutoTest.cs`, `Fixtures.cs`, scenarios); builds to `1.6/Assemblies/TidyThreads.dll` |
| `Art/` | SVG texture sources, exported with `tools/export-textures.sh` |
| `docs/` | `plans/` (the design snapshot), `test-runs.md`, and the shared method docs from the modding center |
| `tools/`, `templates/` | The shared toolchain (`tools/env.sh` holds the identity) and the hand-off and testing-rule templates |
| `.devlog/`, `.tools/`, `dist/` | Generated; never committed |

## Commands

```bash
tools/status.sh                                 # environment check: game version, Steam/Harmony, decomp, baseline
tools/autotest.py baseline                      # error signatures without the mod
tools/autotest.py run "<exact scenario names>"  # lint, build, (re)start the isolated instance, ONLY those scenarios
tools/autotest.py stop                          # end the session's instance
tools/build.sh · tools/package.sh · tools/export-textures.sh
tools/vanilla.sh grep <regex> · tools/vanilla.sh def <defName>
```

`autotest.py` reads `MOD_NAME`/`MOD_ID` from the environment when called directly (`tools/env.sh` sets them for the
shell scripts). Options go before the subcommand. Exit codes: 0 pass, 1 fail, 2 hang/crash/timeout, 3 environment.

## Code conventions

- Follow `docs/playbook.md § Code conventions`: KISS; no abstraction the plan doesn't call for; namespace
  `TidyThreads`; defNames and tags prefixed `TT_`; log only through the mod's `[TidyThreads]` dev-mode wrapper plus
  one unconditional build-id line. Every Harmony patch is justified in the plan; ASF already patches
  `BackCompatibility.GetBackCompatibleType`, so don't add a second patch for that.
- Saves key defNames and class names by string: `Table_Wardrobe`, `TidyThreads.ApparelStorageExtension` and the
  packageId can't be renamed once saves exist.
- Folder names and `texPath`s are case-sensitive on Linux. Our textures live under `TidyThreads/`, never VFE's paths.

## Git

The mount is root-owned: always `git -c safe.directory="$PWD" ...`. Branch `main`. Small commits with imperative
subjects, prefixed with the phase when there is one (`P1: Patch dresser into apparel storage`). Never commit
`.devlog/`, `.tools/`, `dist/` or build output. Don't push unless asked.

## Testing scope (strict rule — no exceptions beyond the one named here)

- **No full sequences.** Every test run names the exact scenarios the current change needs:
  `tools/autotest.py run "<exact scenario names>"`. Never `run` or `pkg-run` with an empty filter, never the
  in-game "Run all", never every scenario in sequence "to check everything". After a fix, rerun only the
  scenarios that failed plus any whose code path the fix touched.
- **No scope creep.** Test only what the current change or plan target touches. Don't add scenarios, fixtures or
  assertions for features outside the target, and don't fix or test unrelated problems found along the way:
  note them in `TODO.md` and ask the user.
- **Well-defined state only.** A scenario that runs builds its own fixtures from a known start, states its
  precondition, its trigger and its expected outcome, asserts on its own fixture targets (never global map
  state), and removes its fixtures. It never depends on what an earlier scenario left behind.
- **Every scenario targets a failure mode.** Each run scenario names the way the change could break (wrong
  result, exception, null/despawned/dead target, missing def, save/load round trip, removal) and asserts either
  the correct result or a graceful failure (no exception, no new red error, a clean fallback). A scenario that
  catches nothing the change could break is not run.
- **Record why.** Each `docs/test-runs.md` row lists the scenarios run and the failure mode each one covers.
- **Sole exception: the release gate.** A full "Run all" on the packaged copy (`tools/autotest.py pkg-run`) is
  allowed only as the release gate, started by the user or with their explicit approval for that release.
