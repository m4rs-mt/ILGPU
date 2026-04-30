#!/usr/bin/env bash
#
# Builds ReadyToRun-accelerated ILGPUC binaries for one or more RIDs plus a
# RID-agnostic JIT fallback, then packs the lot into a single ILGPUC NuGet
# package. The package layout the consumer's MSBuild integration expects:
#
#   tools/net10.0/ILGPUC.dll          ← JIT fallback for any RID
#   tools/net10.0/<rid>/ILGPUC[.exe]  ← R2R native exe per shipped RID
#   build/{ILGPUC,ILGPU.Kernels}.targets
#
# AOT note: NativeAOT is *not* viable for ILGPUC today. The IL frontend
# depends on MethodBase.MetadataToken and Module.Resolve*(int token), neither
# of which is exposed by the AOT reflection model — the binary builds but
# crashes on first use. R2R gets ~3x cold-start without that blocker.
#
# Usage:
#   pack-ilgpuc.sh [--rid <rid>]... [--config Release|Debug]
#                  [--output <dir>] [--version <ver>] [--no-pack]
#
# Defaults:
#   --rid     osx-arm64 linux-x64 win-x64
#   --config  Release
#   --output  $REPO_ROOT/Bin/Packages
#   --version <LibraryVersionPrefix> from Src/Directory.Build.props
#
# Examples:
#   # Full release pack (all default RIDs):
#   Src/scripts/pack-ilgpuc.sh
#
#   # Single host RID for a NuGet integration test:
#   Src/scripts/pack-ilgpuc.sh --rid osx-arm64 \
#       --output /tmp/feed --version 0.0.0-test
#
#   # Stage-only (skip pack), useful for inspecting the layout:
#   Src/scripts/pack-ilgpuc.sh --no-pack

set -euo pipefail

RIDS=()
CONFIG=Release
OUTPUT=""
VERSION=""
DO_PACK=1

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid)      RIDS+=("$2"); shift 2;;
        --config)   CONFIG="$2"; shift 2;;
        --output)   OUTPUT="$2"; shift 2;;
        --version)  VERSION="$2"; shift 2;;
        --no-pack)  DO_PACK=0; shift;;
        -h|--help)
            sed -n '3,/^set -euo/p' "$0" | sed 's/^# \?//;s/^set -euo.*$//'
            exit 0
            ;;
        *)
            echo "error: unknown argument '$1'" >&2
            exit 2
            ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CSPROJ="$REPO_ROOT/Src/ILGPUC/ILGPUC.csproj"
PROPS="$REPO_ROOT/Src/Directory.Build.props"

[[ ${#RIDS[@]} -eq 0 ]] && RIDS=(osx-arm64 linux-x64 win-x64)
[[ -z "$OUTPUT" ]]      && OUTPUT="$REPO_ROOT/Bin/Packages"

if [[ -z "$VERSION" ]]; then
    # Read <LibraryVersionPrefix> from Src/Directory.Build.props.
    VERSION=$(grep -oE '<LibraryVersionPrefix>[^<]+' "$PROPS" \
              | sed 's|<LibraryVersionPrefix>||')
    if [[ -z "$VERSION" ]]; then
        echo "error: failed to read LibraryVersionPrefix from $PROPS" >&2
        exit 1
    fi
fi

mkdir -p "$OUTPUT"

STAGING="$REPO_ROOT/Bin/Packaging/ILGPUC-staging"
rm -rf "$STAGING"
mkdir -p "$STAGING/tools/net10.0"

echo "==> ILGPUC pack"
echo "    repo:    $REPO_ROOT"
echo "    config:  $CONFIG"
echo "    version: $VERSION"
echo "    rids:    ${RIDS[*]}"
echo "    staging: $STAGING"
echo "    output:  $OUTPUT"
echo

# Per-RID R2R publish. --self-contained false keeps the output framework-
# dependent (~33 MB with R2R'd entry assembly, vs. ~180 MB self-contained).
# PublishReadyToRunComposite=false produces per-DLL R2R images so the
# Roslyn deps that R2R skips can be deduped against the JIT fallback dir
# at MSBuild resolution time.
for rid in "${RIDS[@]}"; do
    echo "==> publish R2R for $rid"
    dotnet publish "$CSPROJ" \
        --configuration "$CONFIG" \
        --framework net10.0 \
        --runtime "$rid" \
        --self-contained false \
        -p:PublishReadyToRun=true \
        -p:PublishReadyToRunComposite=false \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        --output "$STAGING/tools/net10.0/$rid" \
        --nologo \
        --verbosity minimal

    # Strip xml-doc + pdb noise from the per-RID dirs. Doc/symbols stay in
    # the JIT fallback dir; consumers only need them once.
    find "$STAGING/tools/net10.0/$rid" -maxdepth 1 \
         \( -name '*.xml' -o -name '*.pdb' \) -delete
done

echo "==> publish JIT fallback (RID-agnostic)"
dotnet publish "$CSPROJ" \
    --configuration "$CONFIG" \
    --framework net10.0 \
    --self-contained false \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    --output "$STAGING/tools/net10.0" \
    --nologo \
    --verbosity minimal

# XML doc files (~5–6 MB combined) help IDEs but bring nothing to a tool
# package whose only job is invoking ilgpuc build. Strip them.
find "$STAGING/tools/net10.0" -maxdepth 1 -name '*.xml' -delete

# Strip per-RID dirs of any DLL that's byte-identical to the JIT-fallback
# copy at tools/net10.0/. Roslyn deps that crossgen2 skipped fall through
# to the parent dir during normal MSBuild assembly resolution. Saves
# 5–15 MB per RID without changing observable behaviour.
echo "==> dedupe per-RID payload against JIT fallback"
JIT_DIR="$STAGING/tools/net10.0"
for rid in "${RIDS[@]}"; do
    rid_dir="$STAGING/tools/net10.0/$rid"
    while IFS= read -r -d '' f; do
        rel="${f#$rid_dir/}"
        twin="$JIT_DIR/$rel"
        if [[ -f "$twin" ]] && cmp -s "$f" "$twin"; then
            rm "$f"
        fi
    done < <(find "$rid_dir" -maxdepth 1 -name '*.dll' -print0)
done

echo "==> staged tree"
find "$STAGING" -type f -printf '%P (%s bytes)\n' 2>/dev/null \
    || find "$STAGING" -type f -exec ls -l {} \; | awk '{print $NF, "(" $5 " bytes)"}'

if [[ $DO_PACK -eq 0 ]]; then
    echo
    echo "==> --no-pack set, skipping pack. Staging at: $STAGING"
    exit 0
fi

# Pack. ILGPUC.csproj's IncludeBuildOutput=false suppresses the default
# lib/<tfm>/ payload (we don't need it — the package is consumed via
# build/ILGPU.Kernels.targets, not as an assembly reference). The staging
# dir contents land under tools/ via a <None> glob in the csproj that
# activates when ILGPUCPackStagingDir is set.
echo
echo "==> pack"
dotnet pack "$CSPROJ" \
    --configuration "$CONFIG" \
    --no-build \
    --output "$OUTPUT" \
    -p:PackageVersion="$VERSION" \
    -p:Version="$VERSION" \
    -p:ILGPUCPackStagingDir="$STAGING" \
    -p:IncludeSymbols=false \
    --nologo \
    --verbosity minimal

echo
echo "==> done"
ls -lh "$OUTPUT"/ILGPUC.*.nupkg
