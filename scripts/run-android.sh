#!/usr/bin/env bash
# Builds the APK, installs it on the connected phone over adb and launches it. Everything after the options reaches the game as launch flags
# (an intent extra named "unity"), so the dev flags work: scripts/run-android.sh -fps -setup -enter 4 -finish 4 -coins 99999999999 -workers 7
# Usage: scripts/run-android.sh [--no-unity-build] [--release] [game flags...]    (ANDROID_SERIAL=... picks a device when several are attached)
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

BUILD_UNITY=1; MODE=dev
while [ $# -gt 0 ]; do
  case "$1" in
    --no-unity-build) BUILD_UNITY=0; shift ;;
    --release) MODE=""; shift ;;
    *) break ;;
  esac
done
PACKAGE="com.gweep.construction"
APK="$PROJECT_DIR/Builds/Android/Construction.apk"
ADB="${ADB:-adb}"

"$ADB" get-state >/dev/null 2>&1 || die "no authorised device (adb devices -l; accept the USB debugging prompt on the phone)"
[ $BUILD_UNITY -eq 1 ] && { "$(dirname "${BASH_SOURCE[0]}")/build.sh" android $MODE || exit 1; }
[ -f "$APK" ] || die "no APK at $APK (run scripts/build.sh android dev)"

"$ADB" install -r "$APK" || die "install failed"
"$ADB" shell am force-stop "$PACKAGE"
"$ADB" logcat -c
"$ADB" shell input keyevent KEYCODE_WAKEUP
if [ $# -gt 0 ]; then
  "$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
  sleep 1; "$ADB" shell am force-stop "$PACKAGE"
  ACTIVITY=$("$ADB" shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER "$PACKAGE" | tail -1 | tr -d '\r')
  # One string for the phone's own shell: otherwise adb splits it and "-fps" is taken for an option of "am start".
  "$ADB" shell "am start -n $ACTIVITY -e unity '$*'" >/dev/null
else
  "$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
fi
echo "Launched $PACKAGE. Log: $ADB logcat -s Unity   Screenshot: $ADB exec-out screencap -p > out.png"
