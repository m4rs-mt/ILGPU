#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SNAPSHOTS_DIR="$REPO_ROOT/Src/ILGPUC.Tests/Snapshots"

# 1. Regenerate snapshots
ILGPU_UPDATE_IR=1 dotnet test "$REPO_ROOT/Src/ILGPUC.Tests/ILGPUC.Tests.csproj" --no-build "$@"

# 2. Commit inside submodule
cd "$SNAPSHOTS_DIR"
if git diff --quiet && git diff --cached --quiet; then
    echo "No snapshot changes to commit."
else
    git add -A
    git commit -m "snapshots: update $(date -u +%Y-%m-%d)"
    git push
fi

# 3. Update parent pointer
cd "$REPO_ROOT"
git add Src/ILGPUC.Tests/Snapshots
git commit -m "chore: update snapshot submodule ref"
