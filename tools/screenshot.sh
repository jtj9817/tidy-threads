#!/usr/bin/env bash
# Capture ONLY the game window this repo's loop interacts with (never the whole desktop) to
# .devlog/screens/<timestamp>[-label].png.
# autotest loop: the test instance's window, on its own private Xvfb display.
# user loop:     the user's RimWorld window, on the game's own display (XAUTHORITY_GAME if needed).
set -euo pipefail
source "$(dirname "$0")/env.sh"

if [[ "${1:-}" == -h* || "${1:-}" == --help ]]; then
  echo "usage: screenshot.sh [label]    (prints the PNG path; fails if the target game window isn't running)"
  exit 0
fi

pid="$(game_pid)"
[[ -n "$pid" ]] || die "$([[ "$MOD_LOOP" == autotest ]] && echo 'the test instance is not running (tools/autotest.py start)' || echo 'RimWorld is not running')"
# The game's own display; the agent's shell may sit on a different one.
DISPLAY="$(tr '\0' '\n' < "/proc/$pid/environ" | sed -n 's/^DISPLAY=//p')"
export DISPLAY
[[ -n "$DISPLAY" ]] || die "no DISPLAY in the game's environment"
[[ "$MOD_LOOP" == autotest ]] || export XAUTHORITY="${XAUTHORITY_GAME:-$HOME/.Xauthority}"

# Match the exact window class: a title match also hits browser tabs about RimWorld, and
# _NET_WM_PID doesn't match pgrep inside Steam's pressure-vessel container.
mapfile -t wins < <(xdotool search --classname '^RimWorldLinux$' 2>/dev/null || true)
(( ${#wins[@]} == 1 )) || die "expected exactly one RimWorldLinux window on $DISPLAY, found ${#wins[@]}"

mkdir -p "$DEVLOG/screens"
out="$DEVLOG/screens/$(date +%Y%m%d-%H%M%S)${1:+-$1}.png"
import -window "${wins[0]}" "$out"
echo "$out"
