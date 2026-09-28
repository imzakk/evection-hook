#!/usr/bin/env bash
# Downloads the third-party binaries Evection Hook ships with into ./deps/.
# See DEPENDENCIES.md for what each one is and its license.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT/tools/deps.env"
DEPS="$ROOT/deps"
mkdir -p "$DEPS"

fetch() { # url dest
    if [[ -f "$2" ]]; then echo "  cached  $(basename "$2")"; return; fi
    echo "  fetch   $1"
    curl -fsSL --retry 3 -o "$2.part" "$1"
    mv "$2.part" "$2"
}

echo "UnityDoorstop $DOORSTOP_VERSION"
fetch "https://github.com/NeighTools/UnityDoorstop/releases/download/v$DOORSTOP_VERSION/doorstop_win_release_$DOORSTOP_VERSION.zip" \
      "$DEPS/doorstop_win_release_$DOORSTOP_VERSION.zip"
rm -rf "$DEPS/doorstop" && mkdir -p "$DEPS/doorstop"
unzip -q -o "$DEPS/doorstop_win_release_$DOORSTOP_VERSION.zip" -d "$DEPS/doorstop"

echo "Cpp2IL $CPP2IL_VERSION"
mkdir -p "$DEPS/cpp2il"
CPP2IL_BASE="https://github.com/SamboyCoding/Cpp2IL/releases/download/$CPP2IL_VERSION"
fetch "$CPP2IL_BASE/Cpp2IL-$CPP2IL_VERSION-Linux" "$DEPS/cpp2il/Cpp2IL-linux"
fetch "$CPP2IL_BASE/Cpp2IL-$CPP2IL_VERSION-Windows.exe" "$DEPS/cpp2il/Cpp2IL-windows.exe"
chmod +x "$DEPS/cpp2il/Cpp2IL-linux"

echo "Done -> $DEPS"
