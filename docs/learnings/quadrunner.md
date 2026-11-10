# Quadrunner — learnings

**Project:** RimWorld 1.6 mod (Core + Harmony only). Robot dogs (Quadrunners haul/clean/build-supply/ride,
Medirunners rescue, Combatdogs defend across four tiers) coordinated by a stable building. Repo:
`/mnt/0B8533211952FCF2/Quadrunner-mod-rw` (`josh9817.quadrunner`; 39 scenarios 39 PASS from the actual
package, plus an independent save-removal check). Tested fully agent-driven beside the user's open game.

**What it contributed to this center:** the automated harness (`autotest.py`: isolated profile, private Xvfb,
in-game restarts, heartbeat/events/commands file protocol, error baseline diffing, watchdogs), the game-side
`AutoTest.cs` driver + quicktest patch (`tools/game-side/`), the harness self-test against a fake game
(`selftest.sh`), independent removal verification (`removal-probe/`), context-limit hand-off tooling
(`context-guard.py`, `handoff.sh`), `statusline.py`, and the hardened versions of `rwdefs.py` (implied
corpses, digit defNames) and `export-textures.sh` (facing-last part files).

**Design stance worth copying**

- Persistent pawn state in a `ThingComp` + `Scribe_Deep` on the stable; coordination in static per-map
  dictionaries rebuilt from saved objects; a deterministic owner election (lowest `thingIDNumber` + saved
  seniority) so shared state survives the owner's death.
- Custom race wired almost entirely from XML: abstract base ThingDef, custom flesh type, `ToolUser`
  intelligence, custom think tree + vanilla work givers, custom `PawnRenderTreeDef` with a hidden whole-sprite
  node. Six Harmony patches total, each a genuine missing seam.
- Tunables in `DefModExtension`s (including per-facing draw offsets), settings in `ModSettings`, nothing
  saved that can be recomputed.
- "Prepare removal" as a first-class feature: destructive export that releases vanilla cargo/riders, strips
  mod hediffs and def references from surviving battle history, keeps record IDs; proven by an independent
  observer with the assembly absent.

**Where the evidence lives** (in the source repo): `TODO.md` (the gotcha log), `docs/test-runs.md` (every
run with findings — the acceptance record), `docs/architecture.md` (runtime map with `Source/…:line` and
decomp citations), `docs/design.md` (superseded snapshot; engine notes), `docs/plans/0001-quadrunner.md`
(decision record incl. the harness construction), `docs/plans/0003-network-dispatch.md`,
`docs/handoff/` (14 hand-offs + `TEMPLATE.md`).

**One-line lessons not already in [gotchas.md](../gotchas.md)**

- The harness protocol is three files — `heartbeat` (liveness), append-only `events.jsonl` (structured
  state), polled `command` (one at a time, delete-after-read) — and the driver is a MonoBehaviour so it can
  never end up in a save. Plain files beat daemons and sockets.
- Test only beside, never on, the user's game: prove isolation by snapshotting their profile's mtimes before
  and after runs.
- In-game restart via `GenCommandLine.Restart()` relaunches with the same quoted command line — argument
  values must contain no `"` or spaces.
- An independent observer's verdict must not be able to turn a bad result good: self-test the failure paths
  (new errors despite a PASS verdict, failed load, cleanup).
- Long "Run all" sequences accumulate state and flake (80k+ ticks, starving colonists): reproduce a failure
  alone before suspecting the code; three identical failures without new information ⇒ report.
- Never fire a session-lifecycle hook's Stop path from a live session (it SIGTERMs the nearest `claude`
  ancestor): test such tooling on a separate tmux server with a fake binary first.
- Keep time zones straight: `ls` shows local, `find -printf %T@` and the harness use UTC.
- Inspect screenshots and captures yourself before claiming done; programmatic checks passed clipped labels,
  clamped pointer moves and badly framed demos that a look caught.
- Prefer background runs and then actually read their result — one run's output was never seen because the
  session stopped first.
