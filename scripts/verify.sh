#!/usr/bin/env bash
# The one gate: build the mod in Release, then run the planner tests. WORLDBOX_DIR overrides the game path, as in deploy.sh.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

WORLDBOX_PROP=()
if [ -n "${WORLDBOX_DIR:-}" ]; then
  # dotnet needs a Windows path; an MSYS path such as /d/Games/worldbox must be converted first.
  WORLDBOX_DIR_WIN="$WORLDBOX_DIR"
  if command -v cygpath >/dev/null 2>&1; then
    WORLDBOX_DIR_WIN="$(cygpath -m "$WORLDBOX_DIR")"
  fi
  WORLDBOX_PROP=(-p:WorldBoxDir="$WORLDBOX_DIR_WIN")
fi

dotnet build "$ROOT/src/BoBox" -c Release "${WORLDBOX_PROP[@]}"
dotnet test "$ROOT/tests/BoBox.Tests"
