# Queen Bee — learnings

**Project:** RimWorld 1.6 mod (Core + Harmony only). A Queen Bee building launches flying Scout/Explo/
Electri/Venom bees at hostiles. Repo: `/mnt/0B8533211952FCF2/queen-bee-rw-mod` (`josh9817.queenbee`, released
1.0.0, now played in a real colony). Tested user-in-the-loop: the user drove every in-game step, the agent
built/linted/marked/parsed.

**What it contributed to this center:** the user-loop testing method (`qblog.sh` byte-offset marks across
restarts, baseline error diffing), `vanilla.sh`/`rwdefs.py` offline lint, the build-id + atomic-install build,
`package.sh`/`install.sh` release switching, `export-textures.sh` (SVG `part-*` layers), window-only
screenshots, and the restart-discipline rules behind `status.sh`'s user mode. Quadrunner then automated the
same loop.

**Design stance worth copying**

- All state on the building itself (`Scribe_*` on the Thing); a mid-flight save works because the map saves
  spawned bees and the hive re-binds by reference on load. No managers, no GameComponents.
- Flying units as `Ethereal` `IAttackTarget` things (not pawns): shootable, explosion-immune, exact-position
  steering, layered draw — no Harmony for any of it. One Harmony postfix total (per-bill gating).
- Self-checking scenarios driven from the building's own tick, one click each, "Run all" as the release gate;
  dev actions kept in release builds.

**Where the evidence lives** (in the source repo): `TODO.md` (the gotcha log, lines ~44–107), `docs/
architecture/overview.md` (component map, startup order, save table), `docs/domain/queen-bee-rules.md`
(target validity, scanning, explosions, lifecycle), `docs/plans/0001-queen-bee.md` (design + T0 tooling
decisions), `docs/plans/0002-personal-release.md` (package/install/version/tagging decisions),
`docs/CHANGELOG.md`, `docs/handoff/` (T8, playthrough: evidence capture, patch-release procedure,
save-compatibility rules).

**One-line lessons not already in [gotchas.md](../gotchas.md)**

- Read `MoveSpeed` via `def.GetStatValueAbstract`; other mods' stat parts may assume a pawn exists. Convert
  cells/second with `GenTicks.TicksPerRealSecond`.
- Vanilla's starting-tile chooser can pick terrain that can't host fixtures — force a sane quicktest tile via
  a test-only patch, keep the seed random.
- Scenario setup is destructive (destroys previous fixtures and every other instance of your building on the
  map): document it as dev-only, never run it in a colony you care about. Starting a scenario by hand cancels
  a queued "Run all" — start the next only after the previous paused.
- A failed scenario may be a *setup* failure (three pirates clustered into one blast reported only two): the
  fixture geometry decides what the scenario measures.
- Record accepted-risk test gaps explicitly (e.g. "save/load accepted from a prior run"), with the user's
  reasoning, instead of silently ticking boxes.
- Release regression = "Run all" PASS on the release copy, then a save/load with live entities, then a
  zero-new-error diff; tag only the commit that passed it.
- Before a patch that touches save data: back up saves with the game closed, bump the version, keep
  `packageId`; document the uninstall hazard in the README, not just the changelog.
