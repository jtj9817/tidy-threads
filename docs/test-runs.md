# Test runs

The autotest loop's iteration log, newest first: one row per run. Each row lists the scenarios run and the failure mode
each one covers (AGENTS.md § Testing scope). Raw logs are in `.devlog/runs/<launch>/` (not committed).

| Date | Build | Scenarios (failure mode covered) | Result | Log showed | Next |
| --- | --- | --- | --- | --- | --- |
| 2026-11-10 | `95d5094-dirty.20261110T232126Z` | None: `autotest.py start` smoke start (does the mod load with ASF before it?) | Ready in 17 s, 0 new errors | `[TidyThreads] loaded build` line; ModsConfig Harmony → Core → ASF → Tidy Threads | Phase 1 |
| 2026-11-10 | — | None: `autotest.py baseline` (Harmony + Core + ASF, no mod) | 6 signatures | ASF in "Initializing new game with mods"; only allocator, breakpad and libcurl noise | Smoke start |
