# Test runs

The autotest loop's iteration log, newest first: one row per run. Each row lists the scenarios run and the failure mode
each one covers (AGENTS.md § Testing scope). Raw logs are in `.devlog/runs/<launch>/` (not committed).

| Date | Build | Scenarios (failure mode covered) | Result | Log showed | Next |
| --- | --- | --- | --- | --- | --- |
| 2026-11-10 | `15380bd-dirty.20261110T234109Z` | T2 Build a wardrobe VFE not active (rerun after dropping its bed-link check, decision 7: NoVFE not loaded, texture missing, wrong capacity) | 1/1 PASS, 0 new errors | Same as the first T2 run, without the bed-link step | Phase 3 |
| 2026-11-10 | `6e2179c-dirty.20261110T233235Z` | T2 Build a wardrobe VFE not active (NoVFE folder not loaded, texture missing in a rotation, wrong capacity, bed link missing or doubled) | 1/1 PASS, 0 new errors | Def from our packageId with our texPath; all four rotations draw `Wardrobe_*`; `30 / 30`; items hidden | Phase 3 |
| 2026-11-10 | `a599149-dirty.20261110T232913Z` | T1 Build a dresser (patch not applied; wrong filter, priority or capacity; items drawn), T3 Minified and reinstalled (settings lost through the minified thing), T4 Blueprint settings (no Blueprint_Storage: no tab or lost edits) | 3/3 PASS, 0 new errors | Every step passed on the first run; ASF `ShowContainedItems` false; inspect shows `20 / 20` | Phase 2 |
| 2026-11-10 | `95d5094-dirty.20261110T232126Z` | None: `autotest.py start` smoke start (does the mod load with ASF before it?) | Ready in 17 s, 0 new errors | `[TidyThreads] loaded build` line; ModsConfig Harmony → Core → ASF → Tidy Threads | Phase 1 |
| 2026-11-10 | — | None: `autotest.py baseline` (Harmony + Core + ASF, no mod) | 6 signatures | ASF in "Initializing new game with mods"; only allocator, breakpad and libcurl noise | Smoke start |
