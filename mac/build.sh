#!/bin/bash
#
# Builds MirrorCast.app.
#
# The .app wrapper is not optional: macOS ties Screen Recording permission to a bundle
# identity, so a bare executable from `swift build` can never be granted access.
#
#   ./build.sh          # build for this Mac
#   ./build.sh --run    # build, then launch
#
set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="MirrorCast"
CONFIG="release"
OUT_DIR=".build/bundle"
APP="$OUT_DIR/$APP_NAME.app"

echo "==> Compiling ($CONFIG)"
swift build -c "$CONFIG"

BIN_PATH="$(swift build -c "$CONFIG" --show-bin-path)/$APP_NAME"
if [[ ! -f "$BIN_PATH" ]]; then
    echo "Build produced no binary at: $BIN_PATH" >&2
    exit 1
fi

echo "==> Assembling $APP_NAME.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_PATH" "$APP/Contents/MacOS/$APP_NAME"
cp Resources/Info.plist "$APP/Contents/Info.plist"

# Ad-hoc signature. Without a paid Developer ID this is the best available, and it is
# what makes the bundle loadable at all on Apple Silicon.
echo "==> Ad-hoc signing"
codesign --force --deep --sign - "$APP"

echo
echo "Built: $(cd "$OUT_DIR" && pwd)/$APP_NAME.app"
echo
echo "NOTE: an ad-hoc signature changes on every rebuild, so macOS may treat each build"
echo "      as a new app and ask for Screen Recording permission again. If the app is"
echo "      listed but still blocked, remove the stale entry in"
echo "      System Settings > Privacy & Security > Screen Recording and re-add it."

if [[ "${1:-}" == "--run" ]]; then
    echo
    echo "==> Launching"
    open "$APP"
fi
