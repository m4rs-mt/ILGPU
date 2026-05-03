#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# run.sh — Build/pull and launch the ILGPUC.CompilerService Docker images
#          (CUDA + ROCm + OpenCL) side-by-side on different host ports.
#
# All three containers listen on port 5000 INTERNALLY (the design choice
# that lets `ILGPU_*_SERVICE_URL` env vars work uniformly across images).
# Host port mapping is what lets them coexist:
#
#   CUDA   → host port ${CUDA_HOST_PORT:-5001}    → container 5000
#   ROCm   → host port ${ROCM_HOST_PORT:-5002}    → container 5000
#   OpenCL → host port ${OPENCL_HOST_PORT:-5003}  → container 5000
#
# Modes (mutually exclusive — pick one):
#   default      Build the three images locally from Src/docker/ and run.
#                Iterating on the Dockerfiles? This mode.
#   --pull       Pull pre-built images from ghcr.io and run. Skips ~50
#                minutes of cold-build time. "I just want the service
#                running locally" — this mode. Requires the GHCR packages
#                to be public (see .github/workflows/docker-publish.yml).
#   --no-build   Reuse whatever local images already exist and run. For
#                iterating on downstream code that hits the running services.
#
# Other:
#   --stop       Stop and remove the containers, then exit.
#   -h, --help   Show usage.
#
# Env overrides:
#   GHCR_OWNER       (default: m4rs-mt)         GHCR namespace for --pull
#   CUDA_HOST_PORT   (default: 5001)
#   ROCM_HOST_PORT   (default: 5002)
#   OPENCL_HOST_PORT (default: 5003)
#   PLATFORM         (default: linux/amd64 — required for ROCm; CUDA also
#                    has a multi-arch base if you switch this to your
#                    native platform)
# ---------------------------------------------------------------------------
set -euo pipefail

CUDA_IMAGE_LOCAL="ilgpuc-compiler-service-cuda:13.2.0"
ROCM_IMAGE_LOCAL="ilgpuc-compiler-service-rocm:7.1.1"
OPENCL_IMAGE_LOCAL="ilgpuc-compiler-service-opencl:latest"

# GHCR images published by .github/workflows/docker-publish.yml. The default
# owner matches the upstream repo; override via GHCR_OWNER env var if you
# fork or pull from a different namespace.
GHCR_OWNER="${GHCR_OWNER:-m4rs-mt}"
CUDA_IMAGE_GHCR="ghcr.io/${GHCR_OWNER}/ilgpuc-compiler-service-cuda:latest"
ROCM_IMAGE_GHCR="ghcr.io/${GHCR_OWNER}/ilgpuc-compiler-service-rocm:latest"
OPENCL_IMAGE_GHCR="ghcr.io/${GHCR_OWNER}/ilgpuc-compiler-service-opencl:latest"

# Resolved at runtime based on --pull / --no-build / build mode.
CUDA_IMAGE="${CUDA_IMAGE_LOCAL}"
ROCM_IMAGE="${ROCM_IMAGE_LOCAL}"
OPENCL_IMAGE="${OPENCL_IMAGE_LOCAL}"

CUDA_NAME="ilgpuc-cuda"
ROCM_NAME="ilgpuc-rocm"
OPENCL_NAME="ilgpuc-opencl"
# NOTE: these defaults are duplicated in
#   Src/ILGPUC.Tests/Framework/CompilerManagerFactory.cs
#   (DefaultCudaServiceUrl / DefaultRocmServiceUrl / DefaultOpenCLServiceUrl).
# Bump both files together when reassigning ports.
CUDA_HOST_PORT="${CUDA_HOST_PORT:-5001}"
ROCM_HOST_PORT="${ROCM_HOST_PORT:-5002}"
OPENCL_HOST_PORT="${OPENCL_HOST_PORT:-5003}"
PLATFORM="${PLATFORM:-linux/amd64}"
HEALTH_TIMEOUT=30

# Mode flags. Mutually exclusive: at most one of --no-build / --pull may be
# set; default (none set) is to build locally from the in-tree Dockerfiles.
SKIP_BUILD=0
PULL_GHCR=0
STOP_ONLY=0

# This script lives in Src/docker/. Build context is the repo root.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# ---------------------------------------------------------------------------
# Output helpers
# ---------------------------------------------------------------------------
if [[ -t 1 ]]; then
    BOLD=$'\033[1m'; RED=$'\033[31m'; GREEN=$'\033[32m'
    YELLOW=$'\033[33m'; BLUE=$'\033[34m'; RESET=$'\033[0m'
else
    BOLD=""; RED=""; GREEN=""; YELLOW=""; BLUE=""; RESET=""
fi

info()  { echo "${BLUE}==>${RESET} ${BOLD}$*${RESET}"; }
ok()    { echo "${GREEN}✓${RESET} $*"; }
warn()  { echo "${YELLOW}!${RESET} $*" >&2; }
err()   { echo "${RED}✗${RESET} $*" >&2; }
die()   { err "$*"; exit 1; }

# ---------------------------------------------------------------------------
# Usage
# ---------------------------------------------------------------------------
usage() {
    cat <<EOF
${BOLD}Usage:${RESET} $(basename "$0") [options]

Build the CUDA, ROCm, and OpenCL images
(Src/docker/Dockerfile.{cuda,rocm,opencl}) and launch all three
containers side-by-side on different host ports.

${BOLD}Modes (mutually exclusive — pick at most one):${RESET}
  ${BOLD}default${RESET}            Build the three images locally from Src/docker/
                     and launch them. The most accurate dev mode — what
                     you want when iterating on the Dockerfiles or
                     verifying a local source change end-to-end.
  --pull             Pull pre-built images from ghcr.io instead of
                     building locally. Skips ~50 minutes of cold-build
                     time. Best for "I just want the service running
                     locally to test something" — see GHCR_OWNER env var
                     to point at a fork.
  --no-build         Skip docker build entirely; reuse whatever local
                     images already exist (useful when iterating only on
                     downstream code that hits the running services).

${BOLD}Other options:${RESET}
  --stop             Stop and remove the containers, then exit
  -h, --help         Show this message

${BOLD}Environment overrides:${RESET}
  GHCR_OWNER         GHCR namespace for --pull mode    (default: m4rs-mt)
  CUDA_HOST_PORT     Host port for the CUDA service   (default: 5001)
  ROCM_HOST_PORT     Host port for the ROCm service   (default: 5002)
  OPENCL_HOST_PORT   Host port for the OpenCL service (default: 5003)
  PLATFORM           Build/run platform               (default: linux/amd64)

${BOLD}Examples:${RESET}
  $(basename "$0")                    # build all locally then run
  $(basename "$0") --pull             # pull from GHCR then run
  $(basename "$0") --no-build         # reuse existing local images
  $(basename "$0") --stop             # stop everything
  GHCR_OWNER=my-fork $(basename "$0") --pull
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)  usage; exit 0 ;;
        --no-build) SKIP_BUILD=1; shift ;;
        --pull)     PULL_GHCR=1;  shift ;;
        --stop)     STOP_ONLY=1; shift ;;
        -*)         die "Unknown option: $1 (try --help)" ;;
        *)          die "Unexpected argument: $1" ;;
    esac
done

if [[ "${SKIP_BUILD}" -eq 1 && "${PULL_GHCR}" -eq 1 ]]; then
    die "--no-build and --pull are mutually exclusive (try --help)"
fi

# In --pull mode, point all the run-time tags at the GHCR refs so the
# rest of the script doesn't need to know which mode it's in.
if [[ "${PULL_GHCR}" -eq 1 ]]; then
    CUDA_IMAGE="${CUDA_IMAGE_GHCR}"
    ROCM_IMAGE="${ROCM_IMAGE_GHCR}"
    OPENCL_IMAGE="${OPENCL_IMAGE_GHCR}"
fi

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
stop_container() {
    local name="$1"
    if docker ps --format '{{.Names}}' 2>/dev/null | grep -qx "${name}"; then
        info "Stopping ${name}"
        docker stop "${name}" >/dev/null
    fi
    if docker ps -a --format '{{.Names}}' 2>/dev/null | grep -qx "${name}"; then
        docker rm "${name}" >/dev/null 2>&1 || true
    fi
}

wait_healthy() {
    local label="$1" port="$2" deadline=$(( $(date +%s) + HEALTH_TIMEOUT ))
    while (( $(date +%s) < deadline )); do
        if curl -fsS --max-time 2 \
            "http://localhost:${port}/api/v1/status" >/dev/null 2>&1; then
            ok "${label} healthy on http://localhost:${port}"
            return 0
        fi
        sleep 1
    done
    err "${label} did NOT become healthy within ${HEALTH_TIMEOUT}s"
    return 1
}

# ---------------------------------------------------------------------------
# Stop-only mode
# ---------------------------------------------------------------------------
if [[ "${STOP_ONLY}" -eq 1 ]]; then
    stop_container "${CUDA_NAME}"
    stop_container "${ROCM_NAME}"
    stop_container "${OPENCL_NAME}"
    ok "Stopped."
    exit 0
fi

# ---------------------------------------------------------------------------
# Local prerequisites
# ---------------------------------------------------------------------------
command -v docker >/dev/null 2>&1 || die "docker not found in PATH"
command -v curl   >/dev/null 2>&1 || die "curl not found in PATH"

cd "${REPO_ROOT}"

# ---------------------------------------------------------------------------
# Acquire images: build locally, pull from GHCR, or reuse existing.
# ---------------------------------------------------------------------------
if [[ "${PULL_GHCR}" -eq 1 ]]; then
    info "Pulling CUDA image (${CUDA_IMAGE}, ${PLATFORM})"
    docker pull --platform "${PLATFORM}" "${CUDA_IMAGE}"
    ok "CUDA image pulled"

    info "Pulling ROCm image (${ROCM_IMAGE}, ${PLATFORM})"
    docker pull --platform "${PLATFORM}" "${ROCM_IMAGE}"
    ok "ROCm image pulled"

    info "Pulling OpenCL image (${OPENCL_IMAGE}, ${PLATFORM})"
    docker pull --platform "${PLATFORM}" "${OPENCL_IMAGE}"
    ok "OpenCL image pulled"
elif [[ "${SKIP_BUILD}" -eq 0 ]]; then
    info "Building CUDA image (${CUDA_IMAGE}, ${PLATFORM})"
    docker build --platform "${PLATFORM}" \
        -f Src/docker/Dockerfile.cuda \
        -t "${CUDA_IMAGE}" .
    ok "CUDA image built"

    info "Building ROCm image (${ROCM_IMAGE}, ${PLATFORM})"
    docker build --platform "${PLATFORM}" \
        -f Src/docker/Dockerfile.rocm \
        -t "${ROCM_IMAGE}" .
    ok "ROCm image built"

    info "Building OpenCL image (${OPENCL_IMAGE}, ${PLATFORM})"
    docker build --platform "${PLATFORM}" \
        -f Src/docker/Dockerfile.opencl \
        -t "${OPENCL_IMAGE}" .
    ok "OpenCL image built"
else
    info "Skipping build (--no-build) — reusing existing local images"
fi

# ---------------------------------------------------------------------------
# Launch
# ---------------------------------------------------------------------------
stop_container "${CUDA_NAME}"
stop_container "${ROCM_NAME}"
stop_container "${OPENCL_NAME}"

info "Launching CUDA container on host port ${CUDA_HOST_PORT}"
docker run -d \
    --platform "${PLATFORM}" \
    -p "${CUDA_HOST_PORT}:5000" \
    --name "${CUDA_NAME}" \
    "${CUDA_IMAGE}" >/dev/null

info "Launching ROCm container on host port ${ROCM_HOST_PORT}"
docker run -d \
    --platform "${PLATFORM}" \
    -p "${ROCM_HOST_PORT}:5000" \
    --name "${ROCM_NAME}" \
    "${ROCM_IMAGE}" >/dev/null

info "Launching OpenCL container on host port ${OPENCL_HOST_PORT}"
docker run -d \
    --platform "${PLATFORM}" \
    -p "${OPENCL_HOST_PORT}:5000" \
    --name "${OPENCL_NAME}" \
    "${OPENCL_IMAGE}" >/dev/null

# ---------------------------------------------------------------------------
# Health check
# ---------------------------------------------------------------------------
info "Waiting for services to become healthy"
wait_healthy "CUDA"   "${CUDA_HOST_PORT}"
wait_healthy "ROCm"   "${ROCM_HOST_PORT}"
wait_healthy "OpenCL" "${OPENCL_HOST_PORT}"

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
echo
ok "${BOLD}All services are running.${RESET}"
cat <<EOF

  CUDA   → ${BOLD}http://localhost:${CUDA_HOST_PORT}${RESET}
  ROCm   → ${BOLD}http://localhost:${ROCM_HOST_PORT}${RESET}
  OpenCL → ${BOLD}http://localhost:${OPENCL_HOST_PORT}${RESET}

Smoke test:
  curl -fsS http://localhost:${CUDA_HOST_PORT}/api/v1/capabilities   | jq .
  curl -fsS http://localhost:${ROCM_HOST_PORT}/api/v1/capabilities   | jq .
  curl -fsS http://localhost:${OPENCL_HOST_PORT}/api/v1/capabilities | jq .

Use in tests (env vars are optional — the test framework's
CompilerManagerFactory probes these ports automatically):
  ILGPU_CUDA_SERVICE_URL=http://localhost:${CUDA_HOST_PORT} \\
  ILGPU_ROCM_SERVICE_URL=http://localhost:${ROCM_HOST_PORT} \\
  ILGPU_OPENCL_SERVICE_URL=http://localhost:${OPENCL_HOST_PORT} \\
      dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

Stop with:
  $(basename "$0") --stop
EOF
