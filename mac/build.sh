#!/bin/bash
#
# Builds MirrorCast.app.
#
# The .app wrapper is not optional: macOS ties Screen Recording permission to a bundle
# identity, so a bare executable from `swift build` can never be granted access.
#
#   ./build.sh                         # build for this Mac
#   ./build.sh --arch x86_64           # build for Intel Macs
#   ./build.sh --arch arm64            # build for Apple Silicon Macs
#   ./build.sh --arch universal --run  # build a Universal 2 app, then launch
#
set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="MirrorCast"
CONFIG="release"
OUT_DIR=".build/bundle"
APP="$OUT_DIR/$APP_NAME.app"
ARCH="native"
RUN_APP=false

usage() {
    cat <<'EOF'
Usage: ./build.sh [--arch native|arm64|x86_64|universal] [--run]

  --arch native       Build for the current Mac (default)
  --arch arm64        Build for Apple Silicon Macs
  --arch x86_64       Build for Intel Macs
  --arch universal    Build a Universal 2 app containing both architectures
  --run               Launch the assembled app after building
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            [[ $# -ge 2 ]] || { echo "Missing value for --arch" >&2; exit 2; }
            ARCH="$2"
            shift 2
            ;;
        --run)
            RUN_APP=true
            shift
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

build_binary() {
    local target_arch="$1"
    local build_args=(-c "$CONFIG")

    if [[ "$target_arch" != "native" ]]; then
        build_args+=(--arch "$target_arch")
    fi

    echo "==> Compiling $target_arch ($CONFIG)" >&2
    swift build "${build_args[@]}" >&2

    local bin_dir
    bin_dir="$(swift build "${build_args[@]}" --show-bin-path)"
    local bin_path="$bin_dir/$APP_NAME"
    if [[ ! -f "$bin_path" ]]; then
        echo "Build produced no binary at: $bin_path" >&2
        exit 1
    fi

    printf '%s\n' "$bin_path"
}

echo "==> Assembling $APP_NAME.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

if [[ "$ARCH" == "universal" ]]; then
    ARM64_BIN="$(build_binary arm64)"
    X86_64_BIN="$(build_binary x86_64)"
    lipo -create \
        -arch arm64 "$ARM64_BIN" \
        -arch x86_64 "$X86_64_BIN" \
        -output "$APP/Contents/MacOS/$APP_NAME"
else
    BIN_PATH="$(build_binary "$ARCH")"
    cp "$BIN_PATH" "$APP/Contents/MacOS/$APP_NAME"
fi

cp Resources/Info.plist "$APP/Contents/Info.plist"

# Ad-hoc signing keeps the bundle identity stable enough for Screen Recording permission.
echo "==> Ad-hoc signing"
codesign --force --deep --sign - "$APP"

BUILT_ARCHS="$(lipo -archs "$APP/Contents/MacOS/$APP_NAME")"
echo "==> Architectures: $BUILT_ARCHS"

echo
echo "Built: $(cd "$OUT_DIR" && pwd)/$APP_NAME.app"
echo
echo "NOTE: an ad-hoc signature changes on every rebuild, so macOS may treat each build"
echo "      as a new app and ask for Screen Recording permission again. If the app is"
echo "      listed but still blocked, remove the stale entry in"
echo "      System Settings > Privacy & Security > Screen Recording and re-add it."

if [[ "$RUN_APP" == true ]]; then
    echo
    echo "==> Launching"
    open "$APP"
fi
