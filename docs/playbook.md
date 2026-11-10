# Modding playbook

The working method both source mods converged on. It assumes the [toolkit](tooling.md); the two testing loops
are detailed in [testing.md](testing.md). Read [gotchas.md](gotchas.md) before writing Defs or C#.

## Look things up, never trust memory

- **Primary resource:** the [RimWorld Wiki: Modding Tutorials](https://rimworldwiki.com/wiki/Modding_Tutorials).
  Beware: it may describe a newer game version than the installed one (it documented 1.7 while both mods
  targeted 1.6) — pin to `Version.txt` and the matching `Krafs.Rimworld.Ref` package, and stop to ask the user
  if the game updated.
- **Offline wiki mirror** (this machine): `/mnt/0B8533211952FCF2/rimworld-wiki-local/data/site/pages/` — the
  whole wiki as Markdown; grep it instead of fetching the web.
- **Decompiled game source + Core Defs:** `tools/vanilla.sh init` once, then `vanilla.sh grep <regex>` and
  `vanilla.sh def <defName>`. Never `strings` the DLL (shared metadata suffixes give false negatives); never
  trust remembered API. Keep the decomp in `.tools/decomp/` so later sessions reuse it.

## How RimWorld loads a mod (why the loop looks like this)

At startup the game loads assemblies, then parses, inherits, patches and resolves Defs, then loads textures,
then runs `[StaticConstructorOnStartup]` classes. Consequences that shape everything:

- **Assemblies never reload while the game runs.** Every new DLL needs a new game process. "Hot reload Defs"
  reloads XML/textures/strings but not assemblies, and proved unreliable with a big mod list (third-party NREs,
  lost textures) — treat every change as needing a restart.
- The game reads only `About/`, `Defs/`, `Textures/`, `Patches/`, `Languages/`, `Sounds/` and the version
  folder (`1.6/`). `Source/`, `Art/`, `tools/`, `docs/` are ignored, so they live in the repo freely.
- A folder named for the game's minor version (`1.6/Assemblies`) loads for that version; root folders load
  always. No `LoadFolders.xml` needed for that layout.
- Folder names and `texPath`s are **case-sensitive** on Linux (only `About/About.xml`, `LoadFolders.xml` exempt).
- Restarts are the scarce resource: batch every change for a test round, lint and build before asking for one,
  never ask while `tools/build.sh` is still running (a half-written DLL), and let `tools/status.sh` decide
  whether a restart is needed rather than guessing.

## Roles and the user's game

- **The agent** writes code/assets, builds, tests, reads logs, notes and iterates. **The user** settles design
  questions and plays their own game.
- The user's game and profile (`~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios`: Saves, Config,
  Player.log) are off limits: never close, kill, focus, screenshot or send input to their game; never write
  their profile; the only tool that reads it is `tools/backup-saves.sh` (tar).
- In the autotest loop, test runs go ahead **while the user's game is open** (proven beside a live session);
  anything that needs their game closed (e.g. backups) is asked for in one plain line, and waits.
- Commands only the user can run (global git config, interactive logins): give the exact command; never work
  around a permission refusal.

## Session start

1. If started from a hand-off doc (`docs/handoff/`), read it first.
2. Read the mod's `TODO.md` (progress, current state, open questions, agent notes/gotchas) and the active
   plan's section.
3. `git -c safe.directory="$PWD" log --oneline -10` to see where the last session stopped.
4. `tools/status.sh`; if the environment check fails — above all if the game version changed — stop and tell
   the user.

## The work loop (per target)

1. Implement the smallest slice that moves an acceptance box. Add or extend the scenario that proves it in the
   same slice.
2. Build and test in one command: `tools/autotest.py run "<exact scenario names>"` (autotest loop): only the
   scenarios that prove this slice, each with a well-defined state and a named failure mode it catches; never
   all of them in sequence, never anything outside the slice (strict rule:
   [testing.md](testing.md#strict-testing-scope-user-rule-2026-10-28-extends-colony-web-2026-10-22)); or
   `tools/build.sh` plus numbered dev-mode steps (user loop).
3. Analyze: scenario verdicts, new errors diffed against the baseline, `RESULT:` line. When a result surprises
   you, read the raw `Player.log`, not only the filtered view.
4. Note: one row in `docs/test-runs.md` (autotest) or tick boxes + update `TODO.md`; anything that took more
   than one attempt becomes a one/two-line gotcha in `TODO.md` (and, if general, in this repo's
   [gotchas.md](gotchas.md)).
5. Commit. Then back to 1.

Stop and tell the user when: every acceptance box of the target is ticked; a decision the plan doesn't record
is needed (ask, then record it in the plan's Decisions table in the same commit); the same scenario failed
three runs in a row without new information (report what was tried and the log evidence); a step needs their
game closed; or they asked you to stand by (finish and verify the current task, commit, write a hand-off doc,
stop the test instance, then wait — start nothing new).

## Tracking progress

- **`TODO.md`** holds status: target checkboxes, "Current state" (next step, last run), open questions, agent
  notes. Keep it truthful; a stale state misleads the next session.
- **The plan** (`docs/plans/NNNN-<slug>.md`) holds Goal, Decisions, Targets with acceptance boxes, Open
  questions. Tick a box only after a run proved it, and name the run. Never tick on "should work". New scope
  gets a new plan in the same shape; no timelines.
- **`docs/test-runs.md`** (autotest loop) is the iteration log: one row per run, newest first — build id,
  scenarios, result with new-error count, what the log showed, what changes next.
- **Hand-off docs** (`docs/handoff/YYYYMMDD-HHMM-<slug>.md` from [the template](../templates/handoff-TEMPLATE.md))
  whenever a session ends early or hits the context limit; written for an instance that remembers nothing.

## Code conventions

- KISS and DRY. No abstraction, manager, interface or settings class the plan doesn't call for. Reuse vanilla
  systems first; check def/XML extension points (stat parts, render-tree workers, think-tree nodes,
  `FloatMenuOptionProvider`, vanilla job drivers) **before** reaching for Harmony. Keep a patch budget and
  justify each patch in the plan.
- C# namespace = mod name; defNames prefixed consistently (`QR_`/`QB_`-style); log only through the mod's own
  dev-mode wrapper (`[ModName]` prefix) with exactly one unconditional build-id line; never name a method
  `Log` (it shadows `Verse.Log`).
- Tunables go in XML (`DefModExtension` or Def fields), not C# constants, unless the plan fixes the value.
- Every new field on a saved object gets a `Scribe_*` line in `ExposeData`, with a default correct for old
  saves. Persistent state lives on the Thing/Building/comp — no `GameComponent`s, no singletons in saves.
  Saves key defNames, class names and namespaces by string: renaming any of them on an installed save is
  destructive, and `packageId` never changes once saves exist.
- Textures: edit the SVG in `Art/`, re-export (`tools/export-textures.sh`), commit both.
- Core-only discipline: with only Core installed, never touch DLC systems (`ModsConfig.BiotechActive` etc.) or
  reference DLC defs; don't claim DLC compatibility you can't test.

## Commits

- After every verified iteration or self-contained change; small commits; never leave work uncommitted at the
  end of a session.
- `git -c safe.directory="$PWD" ...` on this mount. Imperative subject, prefixed with the target when there is
  one (`T2: Scout Bee patrol and waypoint gizmos`). Never commit build output (`1.6/Assemblies/`), `dist/`,
  `.devlog/` or `.tools/`. Don't push unless asked.

## Context limit and hand-off (agent sessions)

At a fixed token limit (250 000 worked) a session hands off to a fresh one: stop at the next safe point
(`tools/autotest.py stop` if needed), commit, write the hand-off doc from the template, commit it, then
`tools/handoff.sh --session <id> <doc>` arranges a new interactive instance (in tmux it replaces the old
session in the same pane). `tools/context-guard.py` wired into hooks enforces this; see
[tooling.md](tooling.md#adoption-new-or-existing-mod-repo). A hand-off doc is also written on request without
launching anything.
