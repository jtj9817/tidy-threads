# Testing RimWorld mods

Two proven loops built on the same toolkit. The scenarios themselves are mod-side C# (dev-mode debug
actions); the difference is who drives the game.

| | User loop (Queen Bee) | Autotest loop (Quadrunner) |
| --- | --- | --- |
| Game process | The user's own game, their profile | An isolated instance the agent owns |
| In-game steps | The user clicks numbered dev-mode steps | The agent's harness, unattended |
| Log tooling | `tools/qblog.sh` mark/since-mark/errors | `tools/autotest.py` run/errors/follow |
| Restarts | Asked of the user, batched, decided by `tools/status.sh` | In-game `GenCommandLine.Restart()`, ~20 s, automatic |
| Evidence | `SCENARIO … PASS/FAIL` + log diff per step | Run records in `.devlog/runs/`, `docs/test-runs.md` |

## Shared foundations

**Player.log discipline.** `Player.log` has no timestamps and is replaced every launch (`Player-prev.log` is
the previous run), so "since when" means byte offsets: `qblog.sh mark` stores inode + size, `since-mark`
survives restarts, and autotest cuts the log per launch. RimWorld 1.6 prints each stack trace once and later
references it as `[Ref XXXXXXXX] Duplicate stacktrace` — both log tools resolve the ref back to the original
block. Build with `DebugType embedded` so your frames print real `File.cs:line`.

**Error baselines, not error-free logs.** A big mod list (35+ mods) logs constant noise (FMOD/no-audio,
breakpad uploads, allocator stats). Capture a baseline **without the mod** (`qblog.sh baseline` /
`autotest.py baseline`; 8 signatures on the reference machine): errors are *current minus baseline*, with the
first 60 chars of the head line (digits normalized) as the signature. A fixed mod list keeps one baseline
until the game updates; a `[ModName] ERROR` line always counts, baseline or not.

**Scenario runner (mod side).** A static, never-saved runner driven every frame from the game's own update
(Quadrunner drove it from `AutoTest`): `Do` / `Expect(label, func)` / `Wait(ticks)` steps, `SCENARIO <name>:
start/PASS/FAIL` log lines with tick counts, "Run all" queuing every scenario for one summary line, and each
scenario removing the previous one's fixtures. Because assemblies are not reloaded on save/load, a scenario
can save and reload mid-sequence and keep asserting — re-find everything by `ThingID`/`loadID` after the load.
Determinism: storyteller off, Ultrafast (re-issued for ~30 ticks after a load — the speed setter silently
drops values while the screen fades in), forced normal speed disabled, `neverForceNormalSpeed`. Keep dev
actions and scenarios in release builds: they only appear in Dev mode and are the fastest real-bug diagnosis
path.

**Scenario-writing rules of thumb** (each one cost a debugging session):

- Build rooms, benches and power with `Fixtures.cs` by its conventions, not from first principles:
  [scenario-fixtures.md](scenario-fixtures.md) (layouts, bench defs, power timing, symptom → cause table).
- Assert on your own fixture targets, never global state — the quicktest map has its own filth/work that
  legitimately attracts workers.
- Stage distance-sensitive fixtures atomically; an item is eligible the instant it spawns and unfindable by
  ID while carried.
- Respect vanilla eligibility timers (fresh filth is not work for 600 ticks) and hold crafters drafted until
  the ordered job exists, or natural work finishes the bill first.
- Latched flags and thresholds beat instantaneous values; at Ultrafast a 1–2 tick state falls between
  rendered frames (~20 ticks/frame). Run short transitions at normal speed.
- Refresh colonists between scenarios (needs, injuries): long sequences starve drafted colonists and flake.
- Fixture damage is a hediff, not `TakeDamage` (armor deflects); fixture downed pawns are staged
  (`DownBleeding`), not fought for; each hostile gets its own `LordJob_DefendPoint` with a tiny radius and no
  flee toil; call `Raid.Recheck(map)` after spawning (the active flag is cached ~1 s).
- Scenario names must not contain commas: the filter splits on them, so such a scenario can never be named alone.

## The user loop

The user runs RimWorld in Dev mode and does every in-game action; the agent never launches or automates the
game. Per test step: `tools/qblog.sh mark "<step>"`, give one numbered dev-mode step (single click via the
mod's own **Debug actions → <Mod> → Scenario: …**), then `qblog.sh since-mark` + `qblog.sh errors`, checking
the `loaded build` id matches `.devlog/last-build`. `tools/qblog.sh follow` streams lines live. Mod development
happens through `Mods/<ModName>` as either the dev symlink or a release copy (`tools/install.sh dev|release`,
switched only while the game is closed; `status.sh` compares against the right build id for the mode). Back up
the user's profile (`tools/backup-saves.sh`) before anything that could reach it; test on throwaway colonies,
never their real save; turn playthrough bug reports into new scenarios and reproduce first.

## The autotest loop

The agent runs everything itself against a **separate game profile**, never the user's:

| Piece | How |
| --- | --- |
| Profile | `-savedatafolder=.devlog/rwdata` with `Config/ModsConfig.xml` (Harmony, Core, the mod) and `Config/Prefs.xml` (Dev mode on, sound off, small window, no autosave/pause, run in background), both rewritten on every launch |
| Isolation | `-logFile .devlog/rwdata/Player.log`, plus `XDG_CONFIG_HOME`/`XDG_CACHE_HOME` under `.devlog/xdg/` so Unity's prefs, cache and the temp folder (`Root.Shutdown` empties it) are never the user's |
| Map | `-quicktest`: fresh Crashlanded map, no menu, 200×200 (`MOD_MAP_SIZE`) via a test-only patch that also picks a flat, low-swampiness, inland temperate tile (random seed kept); fixtures build their own obstacles |
| Display | A private Xvfb (`:90`+), software rendering (llvmpipe) capped at 4 threads: 640×360 at ~400% CPU matched 1280×720 at ~1800% |
| Control | Launch arg `-modcenter-autotest=<dir>`; the mod's `AutoTest.cs` (a MonoBehaviour, so never in a save) touches `heartbeat` every second, appends JSON lines to `events.jsonl`, and runs one command at a time from `command`: `run [filter]`, `restart`, `quit`, `ping`, `hang`; it closes any pause window |
| Restart | `autotest.py` moves the live log into `.devlog/runs/<launch>/` (the old process keeps writing the moved inode), sends `restart`, the game relaunches itself with the same command line (`GenCommandLine.Restart()`); if nothing comes back (Mono segfaults happen) it kills and cold-starts |
| Watchdog | `HANG` when the heartbeat is stale (>120 s), `CRASH` when no instance runs for 30 s, `TIMEOUT` overall; exit codes 0 pass / 1 fail / 2 hang-crash / 3 not run |
| Mod install | `Mods/<MOD_NAME>` is a symlink to the repo, created on first launch; the user's profile never lists the mod |

### Strict testing scope (user rule, 2026-10-28; extends Colony Web 2026-10-22)

Scenarios are independent fixtures (each builds and removes its own), so a test run is a unit test, not an
integration suite. The rule, binding on every agent and copied verbatim into every mod's `AGENTS.md` from
[`templates/AGENTS-testing-rule.md`](../templates/AGENTS-testing-rule.md):

- **No full sequences.** Every run names the exact scenarios the change needs:
  `tools/autotest.py run "Social between colonists, Care events"`. Never an empty filter (`run`/`pkg-run` with no
  argument), never the in-game "Run all", never every scenario in sequence to check work. After a fix, rerun only
  the scenarios that failed plus any whose code path the fix touched.
- **No scope creep.** Test only what the current change or plan target touches; no scenarios, fixtures or
  assertions for features outside it; unrelated problems found along the way go into `TODO.md` and to the user.
- **Well-defined state only.** A scenario states its precondition, trigger and expected outcome, builds its
  fixtures from a known start, asserts on its own fixture targets, removes them, and never depends on an
  earlier scenario's leftovers.
- **Every scenario targets a failure mode.** It names how the change could break (wrong result, exception,
  null/despawned/dead target, missing def, save/load, removal) and asserts the correct result or a graceful
  failure (no exception, no new red error, clean fallback). A scenario that catches nothing the change could
  break is not run.
- **Record why**: each `docs/test-runs.md` row lists the scenarios and the failure mode each one covers.
- **Sole exception: the release gate** — a full "Run all" on the packaged copy (`pkg-run`), started by the user
  or with their explicit approval for that release.

Make an exact name select exactly that
scenario in the mod's `Scenarios.RunAll`: a part equal to a whole scenario name matches only that name, any other part
is a substring. Colony Web's `Scenarios.RunAll` is the reference; plain substring matching let "Ride" also pick
"Rider pose" on Quadrunner.

One iteration is `tools/autotest.py run "<exact scenario names>"`: build, restart if the loaded build is stale (or start),
send `run`, wait under the watchdog, refuse results from any build other than `.devlog/last-build` (and any
mod root other than the repo). Leave the instance running between iterations; `stop` at session end. Options
go before the subcommand (`autotest.py --timeout 3600 run`); run long jobs in the background and wait for
exit, don't poll.

**Prove the harness itself**: `tools/selftest.sh` drives it against `fake_game.py` (a fake RimWorld speaking
the same file protocol) through 31 cases — baseline, restart, filters, failures, error diffing, package runs,
removal verdicts, the hang watchdog. Rerun it after changing `autotest.py` or the protocol, and stop the real
instance first.

**Package verification**: `pkg-run` cold-launches the actual `dist/` package (BUILD stamp and resolved modRoot
checked, dev symlink restored on pass *and* failure) — validate the distributable, not the dev tree.

**Removal verification** (the hardest failure mode: disabling a mod on a save that contains its content):
the mod ships a *Prepare removal* scenario that writes a cleaned save (backup first, stop mod jobs, release
riders/carried pawns/vanilla cargo, strip mod things and hediffs, clear mod def references from surviving
battle history, keep record IDs and wounds). `autotest.py removal-check` loads that save in a fresh process
**without** the mod's assembly plus an independent observer mod (`tools/removal-probe/`, which references
nothing from the mod): 600 live ticks, save/reload, 600 more, retained vanilla contents, zero new errors —
and the observer's own verdict can never turn a bad result good; new log errors still fail the check.

**Adding the mod to an existing save** (the reverse of removal: a save made *before* the mod existed):
`autotest.py run --premod SPEC "<names>"` first launches **without** the mod plus a test-only builder mod
(`tools/premod-builder/`, which references nothing from the mod and never ships). It builds what the plain-text SPEC
describes on the quicktest map (`site`, `thing` with quality/HP, `items`; format in `Builder.cs`), writes a manifest
of what it spawned (ThingID, def, stuff, position, rotation, HP, quality, art title) and saves. The harness then
cold-launches **with** the mod on that save (copied to `autostart.rws`, no `-quicktest`) and runs the named
scenarios. The game side reads the save name from `AutoTest.LoadedSave` and the "before" facts from
`AutoTest.PreModManifest()`; a scenario that needs the save fails at step 1 without it. The builder launch fails the
run if it logged new errors or the mod was loaded. `run --from-save NAME` loads any named save the same way.

**Changing another mod's presence under a save** (adding or removing a test-only extra mod such as VFE):
`MOD_EXTRA_MODS=A autotest.py run --switch-mods B "<names>"` runs the scenarios under set A, where their first half
writes `Saves/<slug>_handover.rws`, then cold-launches under set B on that save and runs them again; the second half
knows itself by `AutoTest.LoadedSave`. PASS needs both halves. **Baselines follow the mod set:** each extra-mod set has
its own `baseline-errors-<set>.txt` (`MOD_EXTRA_MODS=A autotest.py baseline`, recorded without the mod), and a switch
has a transition baseline (`MOD_EXTRA_MODS=A autotest.py baseline --switch-mods B`): an empty save made under A,
loaded under B, without the mod. It holds the errors that removing or adding those mods causes in any save (missing
component classes, def references), so the second half counts only what the mod adds. Without it those errors count,
and the harness says so. An instance launched from a save never restarts in game (its command line has no
`-quicktest`); a new build restarts it cold.

`pkg-run` takes the same `--from-save`, `--premod` and `--switch-mods` options, so a release gate can run the
scenarios that need a save or a mod switch against the package too.

**Warnings the error pattern would miscount:** Player.log has no severity, so the harness matches error-like words.
`VANILLA_WARNINGS` in `autotest.py` lists vanilla `Log.Warning` texts that match anyway, each with the decompiled
source that shows it's a warning (`Could not find think node with key`: `Verse.AI/Job.cs`). Add one only after
reading that source.
