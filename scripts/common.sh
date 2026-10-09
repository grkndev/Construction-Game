#!/usr/bin/env bash
# Shared helpers for compile.sh / test.sh / build.sh. Source this file, do not run it.

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Override with: UNITY=/path/to/Unity ./scripts/compile.sh
UNITY="${UNITY:-/Volumes/ExternalSSD/Unity/6000.6.5f1/Unity.app/Contents/MacOS/Unity}"
LOG_DIR="$PROJECT_DIR/Logs"
mkdir -p "$LOG_DIR"

die() { echo "error: $*" >&2; exit 1; }

[ -x "$UNITY" ] || die "Unity executable not found: $UNITY (set UNITY=...)"

# Unity refuses to open a project that another editor instance already has open.
# AssetImportWorker processes (-adb2) are children of an editor and ignored here.
ensure_editor_closed() {
  # Match only processes whose executable IS the Unity binary (line starts with it), so shells
  # whose command text merely mentions Unity are not mistaken for an editor.
  if ps -eo command | awk -v u="$UNITY" -v p="$PROJECT_DIR" \
       'index($0, u) == 1 && index($0, p) > 0 && index($0, "-adb2") == 0 { found = 1 } END { exit !found }'; then
    die "Unity editor has this project open. Close it first (batchmode cannot share a project)."
  fi
}

# Fails if the log contains C# compiler errors, even when Unity itself exited 0.
check_compile_errors() {
  local log="$1"
  if grep -E "error CS[0-9]+" "$log" | sort -u | head -50 | grep -q .; then
    echo "--- compile errors ($log) ---" >&2
    grep -E "error CS[0-9]+" "$log" | sort -u | head -50 >&2
    return 1
  fi
  return 0
}
