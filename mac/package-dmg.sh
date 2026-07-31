#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

APP_NAME="MirrorCast"
ARCH="native"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' Resources/Info.plist)"
APP_PATH=".build/bundle/$APP_NAME.app"
DIST_DIR="dist"
STAGING_DIR="$(mktemp -d "${TMPDIR:-/tmp}/mirrorcast-dmg.XXXXXX")"

usage() {
    cat <<'EOF'
Usage: ./package-dmg.sh [--arch native|arm64|x86_64|universal]

Builds, verifies, and packages a macOS DMG for the selected architecture.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            [[ $# -ge 2 ]] || { echo "Missing value for --arch" >&2; exit 2; }
            ARCH="$2"
            shift 2
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

case "$ARCH" in
    native|arm64|x86_64|universal) ;;
    *)
        echo "Unsupported architecture: $ARCH" >&2
        usage >&2
        exit 2
        ;;
esac

cleanup() {
    /bin/rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

echo "==> Building $APP_NAME.app ($ARCH)"
./build.sh --arch "$ARCH"

echo "==> Verifying app bundle"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"
plutil -lint "$APP_PATH/Contents/Info.plist"

ARCHS="$(lipo -archs "$APP_PATH/Contents/MacOS/$APP_NAME")"
case "$ARCH" in
    arm64|x86_64)
        if [[ "$ARCHS" != "$ARCH" ]]; then
            echo "Expected a $ARCH executable, found: $ARCHS" >&2
            exit 1
        fi
        ARCH_LABEL="$ARCH"
        ;;
    universal)
        if ! lipo -verify_arch arm64 x86_64 "$APP_PATH/Contents/MacOS/$APP_NAME"; then
            echo "Expected a Universal 2 executable, found: $ARCHS" >&2
            exit 1
        fi
        ARCH_LABEL="universal"
        ;;
    native)
        if [[ "$ARCHS" == *" "* ]]; then
            echo "Native build unexpectedly contains multiple architectures: $ARCHS" >&2
            exit 1
        fi
        ARCH_LABEL="$ARCHS"
        ;;
esac

DMG_NAME="$APP_NAME-v$VERSION-macOS-$ARCH_LABEL.dmg"
DMG_PATH="$DIST_DIR/$DMG_NAME"
CHECKSUM_PATH="$DMG_PATH.sha256"
echo "==> Architectures: $ARCHS"

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
