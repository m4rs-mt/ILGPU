#!/usr/bin/env bash
#
# End-to-end consumer smoke check for the published ILGPUC NuGet
# package. Packs ILGPU + ILGPUC into a local feed, restores +
# builds + runs HelloKernel against that feed, and asserts the
# stdout matches a known-good expected output.
#
# Goal: catch packaging breakage (nuspec deps wrong, targets
# auto-import broken, RID resolution wrong, R2R binary missing
# DLLs, kernel codegen broken, runtime execution broken) before
# any expensive backend / GPU / sample job spins up in CI.
#
# Usage:
#   EndToEndTest/run.sh
#
# Env vars (all optional):
#   CONFIG   Build configuration (default: Release)
#   VERSION  Package version stamp (default: 0.0.0-e2e-<utc-timestamp>)
#
# Exit code: 0 = pass, non-zero = fail with diagnostic on stderr.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DIR="$REPO_ROOT/EndToEndTest"
PROJECT_DIR="$TEST_DIR/HelloKernel"
FEED="$TEST_DIR/feed"
CONFIG="${CONFIG:-Release}"
VERSION="${VERSION:-0.0.0-e2e-$(date -u +%Y%m%d%H%M%S)}"

host_rid() {
    case "$(uname -s)/$(uname -m)" in
        Darwin/arm64)              echo osx-arm64 ;;
        Darwin/x86_64)             echo osx-x64 ;;
        Linux/x86_64)              echo linux-x64 ;;
        Linux/aarch64|Linux/arm64) echo linux-arm64 ;;
        MINGW*/x86_64|MSYS*/x86_64|CYGWIN*/x86_64) echo win-x64 ;;
        *)
            echo "unsupported host: $(uname -s)/$(uname -m)" >&2
            exit 2
            ;;
    esac
}
RID="$(host_rid)"

echo "==> EndToEndTest"
echo "    repo:    $REPO_ROOT"
echo "    config:  $CONFIG"
echo "    version: $VERSION"
echo "    rid:     $RID"
echo "    feed:    $FEED"
echo

# 1. Clean prior run state so each run starts from a known-empty slate.
rm -rf "$FEED" \
       "$PROJECT_DIR/packages" \
       "$PROJECT_DIR/bin" \
       "$PROJECT_DIR/obj" \
       "$PROJECT_DIR/NuGet.config"
mkdir -p "$FEED"

# 2. Pack ILGPU into the local feed. Plain `dotnet pack` is enough —
#    ILGPU is a normal class library; no staging-dir / tools/
#    machinery needed.
echo "==> pack ILGPU"
dotnet pack "$REPO_ROOT/Src/ILGPU/ILGPU.csproj" \
    --configuration "$CONFIG" \
    --output "$FEED" \
    -p:Version="$VERSION" \
    -p:PackageVersion="$VERSION" \
    -p:IncludeSymbols=false \
    --nologo \
    --verbosity minimal

# 3. Pack ILGPUC into the same feed via the existing pack script.
#    Single host RID is enough for the gate; cross-RID R2R is
#    verified separately by the script's CI-time multi-RID build.
echo "==> pack ILGPUC ($RID, R2R + JIT fallback)"
"$REPO_ROOT/Src/scripts/pack-ilgpuc.sh" \
    --rid "$RID" \
    --config "$CONFIG" \
    --output "$FEED" \
    --version "$VERSION"

# 4. Render NuGet.config from the template (absolute feed path).
echo "==> render NuGet.config"
sed "s|@@FEED@@|$FEED|g" "$TEST_DIR/NuGet.config.template" \
    > "$PROJECT_DIR/NuGet.config"

# 5. Restore + build the consumer project.
echo "==> restore + build HelloKernel"
cd "$PROJECT_DIR"
dotnet restore \
    -p:ILGPUC_PACKAGE_VERSION="$VERSION" \
    --nologo --verbosity minimal
dotnet build \
    --configuration "$CONFIG" \
    --no-restore \
    -p:ILGPUC_PACKAGE_VERSION="$VERSION" \
    --nologo --verbosity minimal

# 6. Run, capture stdout, compare against the known-good expected
#    output. The kernel computes result[i] = b[i] * c[i] for the
#    fixed inputs in Program.cs; the expected output below is what
#    those inputs produce.
echo "==> run + assert"
ACTUAL="$(dotnet run --configuration "$CONFIG" --no-build --no-restore)"
EXPECTED="0 = 50
1 = 27
2 = -4
3 = -15
4 = 48
5 = -8
6 = 0
7 = 0"

if [[ "$ACTUAL" != "$EXPECTED" ]]; then
    echo "FAIL: stdout mismatch" >&2
    diff <(printf '%s\n' "$ACTUAL") <(printf '%s\n' "$EXPECTED") >&2 || true
    exit 1
fi

# 7. Sanity-check the build/ILGPU.Kernels.targets pipeline produced the
#    artefacts it should. If the binary ran and produced correct output
#    these have to be present anyway, but checking explicitly gives a
#    more actionable failure message if a future regression breaks the
#    pipeline before the binary even gets built.
MANIFEST="$PROJECT_DIR/obj/$CONFIG/net10.0/ilgpu/manifest.txt"
GENERATED_DIR="$PROJECT_DIR/obj/$CONFIG/net10.0/ilgpu/generated"
[[ -f "$MANIFEST" ]] || {
    echo "FAIL: $MANIFEST missing — build/ILGPUC.targets did not auto-import" >&2
    exit 1
}
[[ -d "$GENERATED_DIR" ]] || {
    echo "FAIL: $GENERATED_DIR missing — kernel codegen did not run" >&2
    exit 1
}

echo
echo "OK"
