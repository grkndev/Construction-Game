#!/usr/bin/env bash
# Builds the Xcode project for the iOS Simulator, installs it and launches it. Everything after the device name reaches the game as launch
# flags (through the CONSTRUCTION_ARGS environment variable), so the dev flags work (e.g. -fps -setup -enter 4 -finish 4 -coins 99999999999 -workers 7 -decorate 6).
# Usage: scripts/run-ios-sim.sh [--no-unity-build] ["iPhone 16 Pro"] [game flags...]
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

BUILD_UNITY=1
[ "${1:-}" = "--no-unity-build" ] && { BUILD_UNITY=0; shift; }
DEVICE="${1:-iPhone 16 Pro}"; [ $# -gt 0 ] && shift
BUNDLE_ID="com.gweep.construction"
IOS_DIR="$PROJECT_DIR/Builds/iOS"
DERIVED="$PROJECT_DIR/Builds/iOS-derived"

[ $BUILD_UNITY -eq 1 ] && { "$(dirname "${BASH_SOURCE[0]}")/build.sh" ios sim || exit 1; }
[ -d "$IOS_DIR/Unity-iPhone.xcodeproj" ] || die "no Xcode project in $IOS_DIR (run scripts/build.sh ios sim)"

UDID=$(xcrun simctl list devices available | grep -F "$DEVICE (" | head -1 | sed -E 's/.*\(([0-9A-F-]{36})\).*/\1/')
[ -n "$UDID" ] || die "no simulator named '$DEVICE' (see: xcrun simctl list devices available)"
echo "Simulator: $DEVICE ($UDID)"
xcrun simctl boot "$UDID" 2>/dev/null || true
open -a Simulator 2>/dev/null || open "$(xcode-select -p)/Applications/Simulator.app" 2>/dev/null || true

echo "xcodebuild (log: $LOG_DIR/xcodebuild-ios.log)"
xcodebuild -project "$IOS_DIR/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Debug \
  -sdk iphonesimulator -destination "id=$UDID" -derivedDataPath "$DERIVED" \
  CODE_SIGNING_ALLOWED=NO build > "$LOG_DIR/xcodebuild-ios.log" 2>&1 || { tail -30 "$LOG_DIR/xcodebuild-ios.log"; die "xcodebuild failed"; }

APP=$(find "$DERIVED/Build/Products" -maxdepth 2 -name '*.app' | head -1)
[ -n "$APP" ] || die "no .app produced"
xcrun simctl terminate "$UDID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl install "$UDID" "$APP" || die "install failed"
# A phone build has no command line; the flags travel in the environment (see LaunchArgs).
SIMCTL_CHILD_CONSTRUCTION_ARGS="$*" xcrun simctl launch --console-pty "$UDID" "$BUNDLE_ID" > "$LOG_DIR/ios-sim.log" 2>&1 &
echo "Launched $BUNDLE_ID; game log: $LOG_DIR/ios-sim.log; screenshot: xcrun simctl io $UDID screenshot out.png"
