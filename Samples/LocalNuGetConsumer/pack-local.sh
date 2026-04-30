#!/usr/bin/env bash
## ---------------------------------------------------------------------------------------
##                                    ILGPU Samples
##                           Copyright (c) 2026 ILGPU Project
##                                    www.ilgpu.net
##
## File: pack-local.sh
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

#
# Convenience helper: pack ILGPU + ILGPUC from your local clone
# into ./feed/ so Consumer/Consumer.csproj can restore against
# them via PackageReference.
#
# Run: ./pack-local.sh   (from this directory)
#
# Internally just calls Src/scripts/pack-ilgpuc.sh + a one-shot
# `dotnet pack` for ILGPU. Read the script — it's short and does
# exactly what the README walkthrough describes.

set -euo pipefail

SAMPLE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SAMPLE_DIR/../.." && pwd)"
FEED="$SAMPLE_DIR/feed"
VERSION="${VERSION:-0.0.0-local}"

host_rid() {
    case "$(uname -s)/$(uname -m)" in
        Darwin/arm64)              echo osx-arm64 ;;
        Darwin/x86_64)             echo osx-x64 ;;
        Linux/x86_64)              echo linux-x64 ;;
        Linux/aarch64|Linux/arm64) echo linux-arm64 ;;
        MINGW*/x86_64|MSYS*/x86_64|CYGWIN*/x86_64) echo win-x64 ;;
        *) echo "unsupported host: $(uname -s)/$(uname -m)" >&2; exit 2 ;;
    esac
}

# Wipe the feed and previous Consumer state so each run starts fresh.
rm -rf "$FEED" "$SAMPLE_DIR/Consumer/packages" \
       "$SAMPLE_DIR/Consumer/bin" "$SAMPLE_DIR/Consumer/obj"
mkdir -p "$FEED"

echo "==> packing ILGPU into $FEED ..."
dotnet pack "$REPO_ROOT/Src/ILGPU/ILGPU.csproj" \
    -c Release -o "$FEED" \
    -p:Version="$VERSION" -p:PackageVersion="$VERSION" \
    -p:IncludeSymbols=false \
    --nologo --verbosity minimal

echo "==> packing ILGPUC ($(host_rid)) into $FEED ..."
"$REPO_ROOT/Src/scripts/pack-ilgpuc.sh" \
    --rid "$(host_rid)" \
    --config Release \
    --output "$FEED" \
    --version "$VERSION"

echo
echo "Done. Now build the consumer:"
echo "    cd Consumer && dotnet build -p:ILGPUC_PACKAGE_VERSION=$VERSION"
echo "    dotnet run --project Consumer -p:ILGPUC_PACKAGE_VERSION=$VERSION"
