#!/usr/bin/env bash
# Runs the EditMode (default) or PlayMode tests in batchmode. Exit code != 0 if anything fails.
# Usage: scripts/test.sh [EditMode|PlayMode]
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
ensure_editor_closed

PLATFORM="${1:-EditMode}"
LOG="$LOG_DIR/test-$PLATFORM.log"
RESULTS="$PROJECT_DIR/TestResults/$PLATFORM.xml"
mkdir -p "$(dirname "$RESULTS")"
rm -f "$RESULTS"   # a stale result file must never make a failed run look green

echo "Running $PLATFORM tests (log: $LOG)"
# No -quit: the test runner exits the editor itself (exit code 2 = test failures).
"$UNITY" -batchmode -nographics -projectPath "$PROJECT_DIR" \
  -runTests -testPlatform "$PLATFORM" -testResults "$RESULTS" -logFile "$LOG"
code=$?

check_compile_errors "$LOG" || exit 1
[ -f "$RESULTS" ] || die "No test result file produced (exit $code), see $LOG"

summary=$(grep -m1 "<test-run " "$RESULTS")
total=$(sed -n 's/.* total="\([0-9]*\)".*/\1/p' <<<"$summary")
passed=$(sed -n 's/.* passed="\([0-9]*\)".*/\1/p' <<<"$summary")
failed=$(sed -n 's/.* failed="\([0-9]*\)".*/\1/p' <<<"$summary")
echo "Tests: total=${total:-?} passed=${passed:-?} failed=${failed:-?}"

[ "${total:-0}" -gt 0 ] || die "0 tests were run"
[ $code -eq 0 ] && [ "${failed:-1}" -eq 0 ] || die "tests failed (exit $code), see $RESULTS"
echo "Tests OK"
