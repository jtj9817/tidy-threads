# Tooling catalog and adoption guide

The shared RimWorld mod toolchain, distilled from [Queen Bee](../README.md#source-projects) (user-in-the-loop
testing) and [Quadrunner](../README.md#source-projects) (fully agent-driven testing), and proven in both.
This repo is the canonical home: when a tool changes here, port the change to the mods that adopted it.

## Adoption (new or existing mod repo)

1. Copy this `tools/` directory into the mod repo. Keep only what the mod needs (see the *Loop* column; the
   `autotest` and `user` loops share `env.sh`, `vanilla.sh`, `rwdefs.py`, `build.sh`, `package.sh`,
   `export-textures.sh`, `backup-saves.sh`).
2. Edit `tools/env.sh` and set the mod identity — the toolkit refuses to run without it:

   ```sh
   MOD_NAME="Quadrunner"     # Source/ dir, assembly and DLL name, dist/ dir, Mods/ dir, log tag [Quadrunner]
   MOD_ID="josh9817.quadrunner"  # packageId from About.xml; never change it once saves exist
   MOD_LOOP=autotest         # autotest (agent drives an isolated instance) or user (user plays every step)
   MOD_DEF_PREFIX="QR_"      # your defNames' prefix; read by the removal probe (empty disables that check)
   ```

   Every value can also come from the environment; `env.sh` only fails when `MOD_NAME`/`MOD_ID` are unset.
3. Expect the conventional layout (both source mods use it): `About/`, `Defs/`, `Textures/`, `1.6/Assemblies/`
   (build output), `Source/<MOD_NAME>/<MOD_NAME>.csproj`, `Art/` (SVG sources), `Patches/` `Languages/` `Sounds/`
   (optional, packaged only when present).
4. Git-ignore the generated state: this repo's `.gitignore` plus, in a mod repo, the build output
   (`Source/**/bin/`, `Source/**/obj/`, `1.6/Assemblies/`). Never commit `.devlog/`, `.tools/`, `dist/`
   or build output.
5. For the `autotest` loop, also copy `tools/game-side/AutoTest.cs` into `Source/<MOD_NAME>/` (rename the
   namespace and its `Scenario`/mod-class references), and copy the scenario-runner pattern (see
   [testing.md](testing.md)). Copy `tools/game-side/Fixtures.cs` too (rename its namespace) for rooms, benches and
   power built by convention: [scenario-fixtures.md](scenario-fixtures.md). Register a `.devlog`-backed error baseline before the first `run`.
6. **Mandatory:** the mod repo's `AGENTS.md` (when created, or now if it exists) carries the section in
   [`templates/AGENTS-testing-rule.md`](../templates/AGENTS-testing-rule.md) verbatim: the strict testing-scope
   rule (named scenarios only, no full sequences, no scope creep, failure-mode scenarios with well-defined state).
7. Optional agent tooling wiring (Claude Code), in the mod repo's `.claude/settings.json`:

   ```json
   {
     "hooks": {
       "PostToolUse":  [{ "matcher": "*", "hooks": [{ "type": "command", "command": "python3 -I \"$CLAUDE_PROJECT_DIR/tools/context-guard.py\"", "timeout": 10 }] }],
       "Stop":         [{ "hooks": [{ "type": "command", "command": "python3 -I \"$CLAUDE_PROJECT_DIR/tools/context-guard.py\"", "timeout": 10 }] }],
       "UserPromptSubmit": [{ "hooks": [{ "type": "command", "command": "python3 -I \"$CLAUDE_PROJECT_DIR/tools/context-guard.py\"", "timeout": 10 }] }]
     }
   }
   ```

   and, when the user approves, a status line: `"statusLine": {"type": "command",
   "command": "python3 -I \"$CLAUDE_PROJECT_DIR/tools/statusline.py\""}`.

Rules the tools follow (keep them when editing): Bash with `#!/usr/bin/env bash` + `set -euo pipefail`,
executable, `-h` prints usage; Python 3 **stdlib only**, run with `python3 -I`; every shared path comes from
`tools/env.sh`, never redefined elsewhere; generated state lives in `.devlog/` or `.tools/`, both git-ignored;
plain files over daemons and sockets.

## Catalog

| Tool | Loop | Purpose |
| --- | --- | --- |
| `tools/env.sh` | both | Mod identity and shared paths/helpers (`REPO`, `RW_DIR`, `RW_DATA`, `DEVLOG`, `git_`, `game_pid`, `test_pid`, `user_game_pid`, `die`), sourced by every script |
| `tools/vanilla.sh` | both | `init` (decompile Assembly-CSharp once into `.tools/decomp/`, reruns when the game version changes) / `grep <regex>` (code + Core Defs) / `def <defName>` (a Core def plus its ParentName chain) |
| `tools/rwdefs.py` | both | Python behind `vanilla.sh def` and the lint step of `build.sh`. `lint` checks XML parsing, def references (`ParentName` etc. against Core + own Defs, implied `Corpse_<race>` included), defNames ending in a digit, texture paths (exact case, XML and `ContentFinder`/`MaterialPool` in C#; prefix from `MOD_TEX_PREFIX` or the sole `Textures/` subdir), PNG sizes (multiple of 4) and `About/Preview.png` under 1 MB |
| `tools/build.sh` | both | Lint, then `dotnet build` stamped with a build id, installed into `1.6/Assemblies` with an atomic rename. The id goes to `.devlog/last-build` |
| `tools/status.sh` | both | Environment check (game version, toolchain, Steam/Harmony, decomp, baseline) plus live state: `autotest` loop calls `autotest.py status`; `user` loop decides RESTART NEEDED (loaded vs built build id, files changed since launch, install mode). Exit `0` up to date, `1` environment problem, `2` RESTART NEEDED |
| `tools/package.sh` | both | From a clean tree: `build.sh`, then copies About/Defs/Textures/1.6 (and Patches/Languages/Sounds if present) into `dist/<MOD_NAME>` with a `BUILD` stamp, and lints the copy. Includes optional `README.md`, `LICENSE` / `LICENSE.md` / `LICENSE.txt`, `CHANGELOG.md`, and `docs/CHANGELOG.md` so player instructions and licensing travel with the package |
| `tools/install.sh` | user | `release` (copy `dist/<MOD_NAME>` to `Mods/<MOD_NAME>`) / `dev` (symlink the repo) / `status`. Switching refuses while the game runs, and only replaces an install it recognizes |
| `tools/export-textures.sh` | both | Exports `Art/**/*.svg` to PNG at the mirrored path via Inkscape, sized by the SVG's `width`/`height`; each `id="part-<name>"` element also goes to `<file>_<Name>.png` (facing suffix kept last for `Graphic_Multi`); `Art/About/` last |
| `tools/screenshot.sh` | both | Captures only the target game window (exact class `RimWorldLinux`, on the game's own `DISPLAY` read from `/proc/<pid>/environ`) to `.devlog/screens/<timestamp>[-label].png`. `autotest` loop: the test instance; `user` loop: the user's game. Never the whole desktop |
| `tools/backup-saves.sh` | both | Archives the **user's** profile (Saves, Config; logs excluded) to `~/RimWorldBackups/<stamp>.tar.gz`, verifies the archive, refuses while their game runs |
| `tools/qblog.sh` + `tools/qblog.awk` | user | `follow` / `mark <label>` / `since-mark` / `baseline` / `errors` on Player.log, with whole stack traces and `[Ref …]` duplicates resolved; error baseline from a run without the mod (`QBLOG_ALL=1` shows baseline noise). The awk matches the mod's `MOD_TAG` |
| `tools/autotest.py` | autotest | The harness: `run [filter]` (lint/build/deploy, restart in game, scenarios, error diff vs baseline; `--from-save NAME` loads a save instead of the quicktest map, `--premod SPEC` first builds that save without the mod) / `pkg-run` (cold-launch `MOD_PKG`, validate BUILD stamp and modRoot, restore the dev symlink) / `removal-check` (independent observer, no mod assembly: load/tick/save/reload/tick, new-error check) / `start` / `restart` / `stop` / `status` / `baseline` / `errors` / `follow` / `hang`. Uses only the private profile and Xvfb. See [testing.md](testing.md) |
| `tools/selftest.sh` + `tools/selftest/` | autotest | Exercises `autotest.py` against `fake_game.py` (speaks the game-side protocol): baseline, cold start, in-game restart, filters, failures, error diffing, hang watchdog, package runs, removal verdicts, cleanup. 19 cases |
| `tools/game-side/AutoTest.cs` | autotest | Reference implementation (from Quadrunner) of the game-side driver the harness talks to: a MonoBehaviour (never saved) with heartbeat, `events.jsonl`, one-command-at-a-time `command` file, `ready` on the quicktest map, and a test-only quicktest patch (flat temperate tile, `-modcenter-mapsize`). Copy into the mod's `Source/`, rename namespace and mod references |
| `tools/game-side/Fixtures.cs` | autotest | Scenario fixture kit: `Site`/`Room`/`Rooms` (walled, roofed steel rooms, door south, pawns moved out first, adjacent rooms share walls), `Bench` (north row, interaction cell inside, fueled benches filled), `RoomPower` (`Vanometric` cells added to cover demand, `Battery`, `None`), `CutPower`, `AllPowered`/`Unpowered`/`WhyUnreachable` diagnostics, `CleanUp` (unroof first). Compiled against 1.6 refs, not yet run in game. Conventions and recipes: [scenario-fixtures.md](scenario-fixtures.md) |
| `tools/premod-builder/` | autotest | Independent test-only mod built by `run --premod`: loaded without the mod, builds the SPEC's things on the quicktest map, writes `<dir>/manifest.txt`, saves, writes a verdict. Never in `dist/` |
| `tools/removal-probe/` | autotest | Independent test-only mod built by `removal-check`: no reference to the mod's assembly, nothing saved; verifies absent assembly/packageId/def-prefixed content (identity from `<dir>/mod.txt`), surviving map/caravan contents and battle-history IDs, 600 ticks, save/reload, 600 more ticks; writes a verdict and screenshot |
| `tools/context-guard.py` | agent | Claude Code hook (PostToolUse/Stop/UserPromptSubmit): past `MOD_HANDOFF_TOKENS` (default 250 000) the session must write a hand-off doc and run `handoff.sh`; the hook enforces it and ends the old session once the hand-off is launched. Disable: `MOD_HANDOFF_DISABLE=1` or `.devlog/handoff/disabled` |
| `tools/handoff.sh` | agent | `[--session <id>] [--dry-run] <doc>`: starts a new interactive Claude Code instance on a committed hand-off doc, replacing the old one (tmux same pane via `respawn-pane -k`, else an Alacritty window on `MOD_HANDOFF_DISPLAY`). Extra claude args: `MOD_HANDOFF_CLAUDE_ARGS` |
| `tools/statusline.py` | agent | Claude Code status line: appends `rate_limits` to `.devlog/usage.jsonl` (`--out` / `MOD_USAGE_LOG`) whenever they change, prints `user@host:cwd` |

Environment variables beyond the identity: `RW_DIR` (game install), `RW_DATA`/`USER_RW_DATA` (profiles),
`HARMONY_DIR`, `RW_EXPECTED_VERSION` (default `1.6.`), `MOD_DEVLOG`, `MOD_MAP_SIZE` (default 200),
`MOD_PKG`, `MOD_SCREEN` (default 640×360), `MOD_RENDER_THREADS` (default 4), `MOD_TEX_PREFIX`,
`MOD_EXTRA_MODS` (test-only packageIds loaded after the mod, comma-separated; the launched set is recorded in
`.devlog/autotest/extra-mods` and a run with a different set restarts the instance cold; scenarios needing one check
`ModsConfig.IsActive` and skip themselves without it), `MOD_DEPS` (required packageIds loaded before the mod in
every launch, baseline included; `preflight` refuses to start when one isn't installed),
`MOD_HANDOFF_TOKENS`, `MOD_HANDOFF_DISPLAY`, `MOD_HANDOFF_CLAUDE_ARGS`, `MOD_USAGE_LOG`, `QBLOG_ALL`,
`BACKUP_DIR`, `XAUTHORITY_GAME`.

## External dependencies

`dotnet` (SDK that can target `net472`), `python3`, `git`, `inkscape` (texture export), `ilspycmd` (installed
on demand into `.tools/ilspy` by `vanilla.sh init`), and for the autotest loop: the native Linux RimWorld,
Steam **running** (workshop Harmony only loads through the Steam API), `Xvfb`, `xdotool`, ImageMagick
(`import`). RimWorld 1.6 and workshop Harmony (2009463077) at the paths above.
