<!--
Mandatory section for every mod repo's AGENTS.md (user rule, 2026-10-28). Paste everything below this comment
verbatim into the mod's AGENTS.md; only the scenario names in the example may change. Source of truth:
rimworld-modding-center docs/testing.md § "Strict testing scope".
-->

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
