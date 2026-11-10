#!/usr/bin/env bash
# Player.log helper: live follow, mark/since-mark around test steps, error baseline and diff.
set -euo pipefail
source "$(dirname "$0")/env.sh"

MARK="$DEVLOG/mark"
BASELINE="$DEVLOG/baseline-errors.txt"

usage() {
  cat <<EOF
usage: qblog.sh <command>
  follow          stream new $MOD_TAG lines + error blocks; baseline noise hidden (QBLOG_ALL=1 shows it)
  mark <label>    remember the current end of Player.log, before asking the user to do a test step
  since-mark      everything logged after the mark, including across a game restart
  baseline        record error signatures of Player.log + Player-prev.log (runs WITHOUT the mod)
  errors          error blocks in the current Player.log that are not in the baseline
EOF
  exit "${1:-0}"
}

filter() {
  local known=""
  [[ -f "$BASELINE" && -z "${QBLOG_ALL:-}" ]] && known="$BASELINE"
  LC_ALL=C awk -f "$HERE/qblog.awk" -v baseline="$known" -v tag="$MOD_TAG" "$@"
}

size() { stat -c %s "$1"; }
inode() { stat -c %i "$1"; }

[[ -f "$RW_LOG" ]] || die "no log at $RW_LOG"

case "${1:-}" in
  follow)
    # Read from the top so stack-trace refs logged earlier can still be resolved; print only new lines.
    tail -n +1 -F "$RW_LOG" 2>/dev/null | filter -v start="$(size "$RW_LOG")"
    ;;
  mark)
    [[ -n "${2:-}" ]] || usage 1
    printf '%s %s %s\n' "$(inode "$RW_LOG")" "$(size "$RW_LOG")" "${*:2}" > "$MARK"
    echo "marked: ${*:2}"
    ;;
  since-mark)
    [[ -f "$MARK" ]] || die "no mark yet; run: qblog.sh mark <label>"
    read -r ino off label < "$MARK"
    echo "--- since mark: $label"
    if [[ "$(inode "$RW_LOG")" == "$ino" ]]; then
      (( $(size "$RW_LOG") >= off )) || off=0   # truncated in place
      filter -v start="$off" "$RW_LOG"
    else
      echo "--- game restarted since the mark"
      if [[ -f "$RW_PREV_LOG" && "$(inode "$RW_PREV_LOG")" == "$ino" ]]; then
        filter -v start="$off" "$RW_PREV_LOG"
        echo "--- new run"
      fi
      filter "$RW_LOG"
    fi
    ;;
  baseline)
    logs=("$RW_LOG")
    [[ -f "$RW_PREV_LOG" ]] && logs+=("$RW_PREV_LOG")
    ! grep -qF "$MOD_TAG" "${logs[@]}" || die "a log contains $MOD_TAG; the baseline must come from runs without the mod"
    cat "${logs[@]}" | LC_ALL=C awk -f "$HERE/qblog.awk" -v tag="$MOD_TAG" -v mode=signatures | sort -u > "$BASELINE"
    echo "baseline: $(wc -l < "$BASELINE") error signatures → $BASELINE"
    ;;
  errors)
    [[ -f "$BASELINE" ]] || echo "warning: no baseline yet, showing every error" >&2
    filter -v errors_only=1 "$RW_LOG"
    ;;
  -h | --help) usage 0 ;;
  *) usage 1 ;;
esac
