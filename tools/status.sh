#!/usr/bin/env bash
# Environment check, then live state.
# user loop:     is the user's running game up to date with the repo, or does it need a restart? (exit 2)
# autotest loop: tools/autotest.py status (the test instance, its build, the user's game).
set -euo pipefail
source "$(dirname "$0")/env.sh"

if [[ "${1:-}" == -h* || "${1:-}" == --help ]]; then
  echo "usage: status.sh    (exit 1 = environment problem, 2 = RESTART NEEDED; the latter only in the user loop)"
  exit 0
fi

problems=0
restart=()
ok()   { echo "  ok    $*"; }
warn() { echo "  WARN  $*"; problems=1; }

echo "environment ($MOD_LOOP loop)"
version="$(cat "$RW_DIR/Version.txt" 2>/dev/null || echo missing)"
[[ "$version" == "$RW_EXPECTED_VERSION"* ]] && ok "game $version" \
  || warn "game version is '$version', expected $RW_EXPECTED_VERSION* (Steam update? stop and ask the user)"
bins="dotnet python3 inkscape git"
[[ "$MOD_LOOP" == autotest ]] && bins="$bins Xvfb xdotool import"
for bin in $bins; do
  command -v "$bin" > /dev/null && ok "$bin" || warn "$bin not found"
done
if [[ "$MOD_LOOP" == autotest ]]; then
  pgrep -x steam > /dev/null && ok "Steam running" || warn "Steam not running (workshop Harmony won't load)"
  [[ -d "$HARMONY_DIR/1.6" ]] && ok "Harmony (workshop 2009463077)" || warn "Harmony not found at $HARMONY_DIR"
  link="$RW_DIR/Mods/$MOD_NAME"
  if [[ -L "$link" && "$(readlink -f "$link")" == "$REPO" ]]; then ok "Mods/$MOD_NAME → repo"
  elif [[ -e "$link" ]]; then warn "Mods/$MOD_NAME is not the dev symlink to this repo"
  else echo "  info  Mods/$MOD_NAME not linked yet (autotest.py creates it)"; fi
else
  # The game loads either the dev symlink (repo files) or a release copy (tools/install.sh); compare against that one.
  installed="$("$HERE/install.sh" status)"
  case "$installed" in
    dev:*) ok "$installed"; src="$REPO"; last="$(cat "$DEVLOG/last-build" 2>/dev/null || true)" ;;
    release:*)
      ok "$installed"; src="$RW_DIR/Mods/$MOD_NAME"; last="$(cat "$src/BUILD")"
      packaged="$(cat "$REPO/dist/$MOD_NAME/BUILD" 2>/dev/null || true)"
      [[ -z "$packaged" || "$packaged" == "$last" ]] \
        || echo "  info  newer package $packaged in dist/ (close the game, tools/install.sh release)" ;;
    *) warn "Mods/$MOD_NAME is neither the dev symlink nor a release copy (tools/install.sh dev|release)"; src="$REPO"; last="" ;;
  esac
  mods="$RW_DATA/Config/ModsConfig.xml"
  for id in brrainz.harmony "$MOD_ID"; do
    grep -qi "<li>$id</li>" "$mods" && ok "$id active" || echo "  info  $id not active in ModsConfig.xml"
  done
fi
[[ -f "$LOCAL_TOOLS/decomp/.version" ]] && cmp -s "$LOCAL_TOOLS/decomp/.version" "$RW_DIR/Version.txt" \
  && ok "decompiled source current" || warn "decompiled source missing or stale (tools/vanilla.sh init)"
[[ -f "$DEVLOG/baseline-errors.txt" ]] && ok "error baseline present" \
  || warn "no error baseline ($([[ "$MOD_LOOP" == autotest ]] && echo 'tools/autotest.py baseline' || echo 'tools/qblog.sh baseline'))"

echo "live"
if [[ "$MOD_LOOP" == autotest ]]; then
  python3 -I "$HERE/autotest.py" status | sed 's/^/  /'
  (( problems == 0 )) || exit 1
  echo "environment ok"
  exit 0
fi

# user loop: does the running game still match the repo?
echo "  built   ${last:-none (tools/build.sh or tools/package.sh)}"
pid="$(game_pid)"
if [[ -z "$pid" ]]; then
  echo "  game    not running (next launch loads the latest build and files)"
else
  started=$(( $(date +%s) - $(ps -o etimes= -p "$pid" | tr -d ' ') ))
  echo "  game    running, pid $pid, started $(date -d "@$started" '+%F %T')"
  loaded="$(grep -o "$MOD_TAG loaded build [^ ]*" "$RW_LOG" | tail -1 | sed 's/.* //' || true)"
  echo "  loaded  ${loaded:-none}"
  if grep -qi "<li>$MOD_ID</li>" "$RW_DATA/Config/ModsConfig.xml"; then
    if [[ -z "$loaded" ]]; then
      restart+=("$MOD_NAME is enabled but not loaded (enabled after launch, or it failed to load: check qblog.sh errors)")
    elif [[ -n "$last" && "${loaded#*+}" != "$last" ]]; then
      restart+=("running build ${loaded#*+} != latest build $last")
    fi
  fi
  changed="$(cd "$src" && find About Defs Textures Patches Languages Sounds 1.6 -type f -newermt "@$started" 2>/dev/null || true)"
  [[ -z "$changed" ]] || restart+=("changed since launch: $(echo "$changed" | head -5 | tr '\n' ' ')")
fi

if (( ${#restart[@]} )); then
  echo "RESTART NEEDED"
  printf '  - %s\n' "${restart[@]}"
  exit 2
fi
(( problems == 0 )) || exit 1
echo "up to date"
