#!/usr/bin/env bash
# Builds the files that get installed into games -> artifacts/payload/
# and a ready-to-ship folder per platform -> artifacts/release/<rid>/
#   EvectionHook(.exe)   the desktop app
#   evection(.exe)       the command-line tool
#   payload/             files copied into games
#   tools/               Cpp2IL (for inspecting IL2CPP games)
#
#   tools/build-payload.sh            # for this machine
#   tools/build-payload.sh --release  # win-x64 and linux-x64
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts"
PAYLOAD="$OUT/payload"
CONFIG=Release

[[ -d "$ROOT/deps/doorstop" ]] || "$ROOT/tools/fetch-deps.sh"

rm -rf "$PAYLOAD"
mkdir -p "$PAYLOAD/doorstop/x64" "$PAYLOAD/doorstop/x86"

echo "==> Doorstop"
cp "$ROOT/deps/doorstop/x64/winhttp.dll" "$PAYLOAD/doorstop/x64/"
cp "$ROOT/deps/doorstop/x86/winhttp.dll" "$PAYLOAD/doorstop/x86/"

echo "==> Mono runtime"
dotnet publish "$ROOT/src/Evection.Core.Mono" -c $CONFIG -o "$PAYLOAD/core-mono" --nologo -v quiet
# The game supplies Unity's own assemblies.
rm -f "$PAYLOAD"/core-mono/UnityEngine*.dll

# IL2CPP runtime: see src/Evection.Core.Il2Cpp/README.md (not built yet).

rids=("$(dotnet --info | awk '/RID:/ {print $2; exit}')")
[[ "${1:-}" == "--release" ]] && rids=(win-x64 linux-x64)
SINGLE_FILE=(--self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none --nologo -v quiet)
for rid in "${rids[@]}"; do
    DEST="$OUT/release/$rid"
    rm -rf "$DEST"
    echo "==> App ($rid)"
    dotnet publish "$ROOT/src/Evection.GUI" -c $CONFIG -r "$rid" "${SINGLE_FILE[@]}" -o "$DEST" ${SYNC_SERVER_URL:+-p:SyncServerUrl=$SYNC_SERVER_URL}
    echo "==> CLI ($rid)"
    dotnet publish "$ROOT/src/Evection.Cli" -c $CONFIG -r "$rid" "${SINGLE_FILE[@]}" -o "$DEST"
    cp -r "$PAYLOAD" "$DEST/payload"
    mkdir -p "$DEST/tools"
    case "$rid" in
        win-*) cp "$ROOT/deps/cpp2il/Cpp2IL-windows.exe" "$DEST/tools/" ;;
        *)     cp "$ROOT/deps/cpp2il/Cpp2IL-linux" "$DEST/tools/" ;;
    esac
    cp "$ROOT/README.md" "$ROOT/LICENSE" "$ROOT/THIRD-PARTY-NOTICES.md" "$DEST/"
done

echo "Done."
echo "  payload: $PAYLOAD"
echo "  release: $OUT/release/"
