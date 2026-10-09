#!/usr/bin/env bash
# Builds a player in batchmode. Exit code != 0 on any failure.
# Usage: scripts/build.sh mac|web|win [fast]    (fast = mac only: Mono instead of IL2CPP, for quick checks)
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

case "${1:-}" in
  mac) METHOD="Game.Editor.BuildScript.BuildMac" ;;
  web) METHOD="Game.Editor.BuildScript.BuildWeb" ;;
  win) METHOD="Game.Editor.BuildScript.BuildWindows" ;;
  *)   die "usage: $0 mac|web|win" ;;
esac
ensure_editor_closed

EXTRA=()
[ "${2:-}" = "fast" ] && EXTRA=(-mono)
LOG="$LOG_DIR/build-$1.log"
echo "Building $1 (log: $LOG)"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT_DIR" \
  -executeMethod "$METHOD" -logFile "$LOG" ${EXTRA[@]+"${EXTRA[@]}"}
code=$?

check_compile_errors "$LOG" || exit 1
[ $code -eq 0 ] || die "build failed (exit $code), see $LOG"
echo "Build OK"
