#!/usr/bin/env bash
# Lint Defs/textures, build $MOD_NAME.dll stamped with a build id, install it into 1.6/Assemblies atomically.
set -euo pipefail
source "$(dirname "$0")/env.sh"

if [[ "${1:-}" == -h* || "${1:-}" == --help ]]; then
  echo "usage: build.sh    (lint + dotnet build; writes the build id to .devlog/last-build)"
  exit 0
fi

PROJ="$REPO/Source/$MOD_NAME/$MOD_NAME.csproj"
DEST="$REPO/1.6/Assemblies/$MOD_NAME.dll"

python3 -I "$HERE/rwdefs.py" lint "$RW_DIR" "$REPO"

id="$(git_ rev-parse --short HEAD 2>/dev/null || echo nogit)"
[[ -z "$(git_ status --porcelain 2>/dev/null)" ]] || id+="-dirty"
id+=".$(date -u +%Y%m%dT%H%M%SZ)"

dotnet build "$PROJ" -c Release -nologo -v quiet -p:SourceRevisionId="$id"

# Rename instead of overwriting in place: a running game keeps its mapped copy untouched,
# and a game starting mid-install never sees a half-written DLL.
mkdir -p "$(dirname "$DEST")"
cp "$REPO/Source/$MOD_NAME/bin/$MOD_NAME.dll" "$DEST.tmp"
mv -f "$DEST.tmp" "$DEST"
echo "$id" > "$DEVLOG/last-build"
echo "build: $id → ${DEST#$REPO/}"
[[ -z "$(game_pid)" ]] || echo "build: the game still runs the previous build ($([[ "$MOD_LOOP" == autotest ]] && echo 'tools/autotest.py run restarts it' || echo 'see tools/status.sh'))"
