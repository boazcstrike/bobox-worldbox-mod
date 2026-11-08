#!/usr/bin/env bash
# Build BoBox in Release and copy it into the game's mods folder.
# Override the install location with WORLDBOX_DIR, e.g. WORLDBOX_DIR=/d/Games/worldbox scripts/deploy.sh
#
# The game's ModLoader (decompiled/ModLoader.cs) loads <name>_memload.dll from memory when it exists, with
# <name>_memload.pdb as its symbols, and ignores the plain <name>.dll beyond needing it to be listed.
# So the build goes out as BoBox.dll, BoBox_memload.dll and BoBox_memload.pdb: the game never locks a file,
# and a running game does not stop a redeploy. A stale BoBox_memload.dll would silently beat a new BoBox.dll,
# which is why all three are copied together.
#
# The rules file goes to mods/bobox-rules.json only when it is missing there, so edits made in the game
# folder survive a redeploy. Pass --force-rules to overwrite it with rules/bobox-rules.json. A kept file
# that differs from rules/bobox-rules.json gets a notice, so new defaults are not missed silently.
set -euo pipefail

FORCE_RULES=0
for arg in "$@"; do
  case "$arg" in
    --force-rules) FORCE_RULES=1 ;;
    *) echo "unknown argument: $arg (valid: --force-rules)" >&2; exit 2 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/BoBox"
RULES_SRC="$ROOT/rules/bobox-rules.json"
WORLDBOX_DIR="${WORLDBOX_DIR:-D:/SteamLibrary/steamapps/common/worldbox}"
# dotnet needs a Windows path; an MSYS path such as /d/Games/worldbox must be converted first.
if command -v cygpath >/dev/null 2>&1; then
  WORLDBOX_DIR="$(cygpath -m "$WORLDBOX_DIR")"
fi
MODS_DIR="$WORLDBOX_DIR/worldbox_Data/StreamingAssets/mods"
OUT_DIR="$PROJECT/bin/Release/net472"

if [ ! -f "$RULES_SRC" ]; then
  echo "rules file not found: $RULES_SRC" >&2
  exit 1
fi

dotnet build "$PROJECT" -c Release -p:WorldBoxDir="$WORLDBOX_DIR"

if [ ! -d "$MODS_DIR" ]; then
  echo "mods folder not found: $MODS_DIR" >&2
  echo "create it or set WORLDBOX_DIR" >&2
  exit 1
fi

cp -f "$OUT_DIR/BoBox.dll" "$MODS_DIR/BoBox.dll"
cp -f "$OUT_DIR/BoBox.dll" "$MODS_DIR/BoBox_memload.dll"
cp -f "$OUT_DIR/BoBox.pdb" "$MODS_DIR/BoBox_memload.pdb"
# Left over from older deploys; the loader only reads BoBox_memload.pdb.
rm -f "$MODS_DIR/BoBox.pdb"
echo "deployed BoBox.dll, BoBox_memload.dll and BoBox_memload.pdb to $MODS_DIR"

RULES_DST="$MODS_DIR/bobox-rules.json"
if [ ! -f "$RULES_DST" ] || [ "$FORCE_RULES" -eq 1 ]; then
  cp -f "$RULES_SRC" "$RULES_DST"
  echo "deployed bobox-rules.json to $MODS_DIR"
else
  echo "kept existing $RULES_DST (pass --force-rules to overwrite)"
  if ! cmp -s "$RULES_SRC" "$RULES_DST"; then
    echo "notice: the kept rules differ from rules/bobox-rules.json; new defaults are not applied until you merge them or pass --force-rules"
  fi
fi
