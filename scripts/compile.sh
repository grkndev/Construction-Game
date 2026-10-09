#!/usr/bin/env bash
# Imports the project and compiles all scripts in batchmode. Exit code != 0 on any error.
# Usage: scripts/compile.sh
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
ensure_editor_closed

LOG="$LOG_DIR/compile.log"
echo "Compiling $PROJECT_DIR (log: $LOG)"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT_DIR" -logFile "$LOG"
code=$?

check_compile_errors "$LOG" || exit 1
[ $code -eq 0 ] || die "Unity exited with code $code, see $LOG"
echo "Compile OK"
