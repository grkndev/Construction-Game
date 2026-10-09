#!/usr/bin/env bash
# Generates scenes, materials, prefabs and fonts, and applies project settings. Idempotent.
# Usage: scripts/setup.sh [rebuild]     (rebuild = regenerate the scene from scratch)
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
ensure_editor_closed

run() {  # run <logname> <ExecuteMethod> [extra unity args, e.g. -quit]
  local log="$LOG_DIR/$1.log" method="$2"; shift 2
  "$UNITY" -batchmode -nographics -projectPath "$PROJECT_DIR" -executeMethod "$method" -logFile "$log" "$@"
  local code=$?
  check_compile_errors "$log" || exit 1
  [ $code -eq 0 ] || die "$method failed (exit $code), see $log"
}

echo "Setup phase 1: TextMesh Pro essentials"
# No -quit here: the method exits the editor itself once the async import is done.
run setup-tmp Game.Editor.ProjectSetup.ImportTmp

METHOD=Game.Editor.ProjectSetup.RunAll
[ "${1:-}" = "rebuild" ] && METHOD=Game.Editor.ProjectSetup.RebuildAll
echo "Setup phase 2: $METHOD"
run setup "$METHOD" -quit
echo "Setup OK"
