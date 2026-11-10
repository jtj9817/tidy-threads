#!/usr/bin/env bash
# Build a clean release copy in dist/$MOD_NAME: game content plus player docs and license, linted.
set -euo pipefail
source "$(dirname "$0")/env.sh"

if [[ "${1:-}" == -h* || "${1:-}" == --help ]]; then
  echo "usage: package.sh    (build + copy game content, README, license and changelog to dist/$MOD_NAME)"
  exit 0
fi

[[ -z "$(git_ status --porcelain)" ]] || die "uncommitted changes: commit first, so the build id names a real commit"
"$HERE/build.sh"

DIST="$REPO/dist/$MOD_NAME"
NEW="$REPO/dist/.$MOD_NAME.new"
rm -rf "$NEW"
mkdir -p "$NEW/1.6/Assemblies"
for dir in About Defs Textures Patches Languages Sounds; do
  [[ ! -d "$REPO/$dir" ]] || cp -r "$REPO/$dir" "$NEW/"
done
cp "$REPO/1.6/Assemblies/$MOD_NAME.dll" "$NEW/1.6/Assemblies/"
# LoadFolders.xml and the extra load folders it names (e.g. NoVFE, NoVFE/1.6), with their game content only.
if [[ -f "$REPO/LoadFolders.xml" ]]; then
  cp "$REPO/LoadFolders.xml" "$NEW/"
  while read -r folder; do
    for dir in Defs Textures Patches Languages Sounds; do
      [[ ! -d "$REPO/$folder/$dir" ]] || { mkdir -p "$NEW/$folder"; cp -r "$REPO/$folder/$dir" "$NEW/$folder/"; }
    done
  done < <(python3 -I "$HERE/rwdefs.py" folders "$REPO" | grep -vxE '|1\.6')
fi
# The game ignores files at the mod root.
cp "$DEVLOG/last-build" "$NEW/BUILD"
for file in README.md LICENSE LICENSE.md LICENSE.txt CHANGELOG.md; do
  [[ ! -f "$REPO/$file" ]] || cp "$REPO/$file" "$NEW/"
done
if [[ -f "$REPO/docs/CHANGELOG.md" ]]; then
  mkdir -p "$NEW/docs"
  cp "$REPO/docs/CHANGELOG.md" "$NEW/docs/"
fi

python3 -I "$HERE/rwdefs.py" lint "$RW_DIR" "$NEW"
rm -rf "$DIST"
mv "$NEW" "$DIST"
(cd "$DIST" && find . -type f | sort | sed 's|^\./|  |')
echo "package: dist/$MOD_NAME, $(du -sh "$DIST" | cut -f1), build $(cat "$DIST/BUILD")"
