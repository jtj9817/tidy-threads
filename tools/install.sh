#!/usr/bin/env bash
# Switch Mods/$MOD_NAME between the release copy (dist/$MOD_NAME, from tools/package.sh) and the dev symlink to the repo.
set -euo pipefail
source "$(dirname "$0")/env.sh"

usage() { echo "usage: install.sh release|dev|status    (release/dev refuse while RimWorld runs)"; }
[[ "${1:-}" == -h* || "${1:-}" == --help ]] && { usage; exit 0; }

MOD="$RW_DIR/Mods/$MOD_NAME"
DIST="$REPO/dist/$MOD_NAME"

mode() {
  if [[ -L "$MOD" ]]; then
    [[ "$(readlink -f "$MOD")" == "$REPO" ]] && echo dev || echo other
  elif [[ -f "$MOD/BUILD" ]]; then
    echo release
  elif [[ -e "$MOD" ]]; then
    echo other
  else
    echo none
  fi
}

# Removes the current install, but only one this script recognizes.
remove_current() {
  [[ -z "$(game_pid)" ]] || die "RimWorld is running; close it first"
  case "$(mode)" in
    dev) rm "$MOD" ;;
    release) rm -rf "$MOD" ;;
    none) ;;
    *) die "$MOD is neither the dev symlink nor a release copy; move it away by hand" ;;
  esac
}

case "${1:-}" in
  release)
    [[ -f "$DIST/BUILD" ]] || die "no package in dist/$MOD_NAME (tools/package.sh)"
    [[ -z "$(game_pid)" ]] || die "RimWorld is running; close it first"
    # Copy next to Mods/ (same filesystem, outside the folder the game scans), then swap.
    tmp="$RW_DIR/.$MOD_NAME.new"
    rm -rf "$tmp"
    cp -r "$DIST" "$tmp"
    remove_current
    mv "$tmp" "$MOD"
    echo "install: release $(cat "$MOD/BUILD") → Mods/$MOD_NAME"
    ;;
  dev)
    remove_current
    ln -s "$REPO" "$MOD"
    echo "install: dev symlink Mods/$MOD_NAME → $REPO"
    ;;
  status)
    case "$(mode)" in
      dev) echo "dev: Mods/$MOD_NAME → $REPO" ;;
      release)
        version="$(sed -n 's|.*<modVersion>\(.*\)</modVersion>.*|\1|p' "$MOD/About/About.xml")"
        echo "release: Mods/$MOD_NAME is version ${version:-?}, build $(cat "$MOD/BUILD")" ;;
      *) echo "$(mode): Mods/$MOD_NAME" ;;
    esac
    ;;
  *) usage; exit 1 ;;
esac
