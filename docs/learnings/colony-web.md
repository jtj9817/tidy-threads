# Colony Web — learnings

Repo: `/mnt/0B8533211952FCF2/social-graphs-mod-rw` (`josh9817.colonyweb`, prefix `CW_`). A social graph of the colony
and every outsider who dealt with it, in a main tab with a force-directed layout. Started 2026-10-22; in progress,
built phase by phase (the user's rule: TODO.md first, one phase at a time, stop for a go).

Evidence: the repo's `docs/plans/0001-colony-web.md` (decisions D1–D15, patch budget), `TODO.md` (verified API facts),
`docs/test-runs.md` (runs).

## Method

- **Exact scenarios, unit-test style** (user, 2026-10-22): every `autotest.py run` names the scenarios that prove the
  change; never all of them in sequence. The mod's `Scenarios.RunAll` matches a whole scenario name exactly, any other
  part as a substring; scenario names carry no commas. Rule in [testing.md](../testing.md#the-autotest-loop).
- **TODO.md before code, one phase at a time.** Drafting every phase ahead before TODO.md existed was corrected by the
  user.
- Reused Quadrunner's `.tools/decomp` (same game version: `vanilla.sh init` accepts it via `.version`) instead of
  decompiling again.

## Design patterns

- **History without a GameComponent:** a comp injected into every race in C#. Only colony-side pawns store data
  (vanilla's `WorldPawnGC` keeps ever-colonists forever), and outsiders are kept as name snapshots keyed by `ThingID`.
  The comp saves nothing for pawns without data, so removing the mod leaves inert XML only.
- **Events from vanilla logs:** postfixes on `PlayLog.Add`/`BattleLog.Add` cover social interactions, attacks, downs
  and kills. `LogEntry.LogID` de-duplicates owners and backfill from the 150-entry play log.

Engine facts are in [gotchas.md](../gotchas.md), marked (CW).
