#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

APP_NAME="MirrorCast"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' Resources/Info.plist)"
APP_PATH=".build/bundle/$APP_NAME.app"
DIST_DIR="dist"
DMG_NAME="$APP_NAME-v$VERSION-macOS-arm64.dmg"
DMG_PATH="$DIST_DIR/$DMG_NAME"
CHECKSUM_PATH="$DMG_PATH.sha256"
STAGING_DIR="$(mktemp -d "${TMPDIR:-/tmp}/mirrorcast-dmg.XXXXXX")"

cleanup() {
    /bin/rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

echo "==> Building $APP_NAME.app"
./build.sh

echo "==> Verifying app bundle"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"
plutil -lint "$APP_PATH/Contents/Info.plist"

ARCHS="$(lipo -archs "$APP_PATH/Contents/MacOS/$APP_NAME")"
if [[ "$ARCHS" != "arm64" ]]; then
    echo "Expected an arm64 executable, found: $ARCHS" >&2
    exit 1
fi

echo "==> Staging DMG contents"
mkdir -p "$STAGING_DIR/root" "$DIST_DIR"
ditto "$APP_PATH" "$STAGING_DIR/root/$APP_NAME.app"
ln -s /Applications "$STAGING_DIR/root/Applications"

echo "==> Creating $DMG_NAME"
/bin/rm -f "$DMG_PATH" "$CHECKSUM_PATH"
hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$STAGING_DIR/root" \
    -format UDZO \
    -imagekey zlib-level=9 \
    -ov \
    "$DMG_PATH"

echo "==> Verifying disk image"
hdiutil verify "$DMG_PATH"
shasum -a 256 "$DMG_PATH" > "$CHECKSUM_PATH"

echo
echo "Built:"
echo "  $DMG_PATH"
echo "  $CHECKSUM_PATH"
