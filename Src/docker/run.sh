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
#                minutes of cold-build time. The published packages are
#                private, so this only works with read access to them —
#                otherwise build locally with the default mode.
#   --no-build   Reuse whatever local images already exist and run. For
#                iterating on downstream code that hits the running services.
#
# Other:
#   --backend LIST  Only build/pull/run the listed backends (comma-separated,
#                   any of cuda,rocm,opencl; default: all three).
#   --stop          Stop and remove the containers, then exit.
#   -h, --help      Show usage.
#
# Platforms: CUDA builds for the Docker host's own architecture (native on
# Apple Silicon and arm64 Linux); ROCm and OpenCL are linux/amd64 only (AMD
# and Intel publish no arm64 packages) and run under emulation on arm64
# hosts. Building for a non-native platform needs BuildKit (the docker
# buildx plugin); the legacy builder cannot cross-build these images.
#
# Env overrides:
#   GHCR_OWNER       (default: m4rs-mt)         GHCR namespace for --pull
#   CUDA_HOST_PORT   (default: 5001)
#   ROCM_HOST_PORT   (default: 5002)
#   OPENCL_HOST_PORT (default: 5003)
#   PLATFORM         (default: per backend, see above) — forces one
#                    platform for every selected backend
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
#   Src/ILGPUC.Compilers/CompilerManagerFactory.cs
#   (DefaultCudaServiceUrl / DefaultRocmServiceUrl / DefaultOpenCLServiceUrl).
# Bump both files together when reassigning ports.
CUDA_HOST_PORT="${CUDA_HOST_PORT:-5001}"
ROCM_HOST_PORT="${ROCM_HOST_PORT:-5002}"
OPENCL_HOST_PORT="${OPENCL_HOST_PORT:-5003}"
# Empty = per-backend defaults (resolved after argument parsing).
PLATFORM="${PLATFORM:-}"
HEALTH_TIMEOUT=30

# Backends to build/pull/run (overridable via --backend).
BACKENDS="cuda,rocm,opencl"

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
                     time. The published packages are private, so this
                     needs read access to them (docker login ghcr.io);
                     without it, use the default mode. See GHCR_OWNER env
                     var to point at your own private namespace.
  --no-build         Skip docker build entirely; reuse whatever local
                     images already exist (useful when iterating only on
                     downstream code that hits the running services).

${BOLD}Other options:${RESET}
  --backend LIST     Only build/pull/run these backends (comma-separated:
                     cuda,rocm,opencl; default: all three)
  --stop             Stop and remove the containers, then exit
  -h, --help         Show this message

${BOLD}Platforms:${RESET}
  CUDA builds for the Docker host's own architecture (native on Apple
  Silicon). ROCm and OpenCL are linux/amd64 only and run under emulation
  on arm64 hosts — that needs BuildKit (docker buildx) for the build.

${BOLD}Environment overrides:${RESET}
  GHCR_OWNER         GHCR namespace for --pull mode    (default: m4rs-mt)
  CUDA_HOST_PORT     Host port for the CUDA service   (default: 5001)
  ROCM_HOST_PORT     Host port for the ROCm service   (default: 5002)
  OPENCL_HOST_PORT   Host port for the OpenCL service (default: 5003)
  PLATFORM           Force one platform for all backends (default: per
                     backend, see above)

${BOLD}Examples:${RESET}
  $(basename "$0")                    # build all locally then run
  $(basename "$0") --backend cuda     # only the CUDA service
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
        --backend)
            [[ $# -ge 2 ]] || die "--backend needs a value (try --help)"
            BACKENDS="$2"; shift 2 ;;
        --backend=*) BACKENDS="${1#--backend=}"; shift ;;
        -*)         die "Unknown option: $1 (try --help)" ;;
        *)          die "Unexpected argument: $1" ;;
    esac
done

if [[ "${SKIP_BUILD}" -eq 1 && "${PULL_GHCR}" -eq 1 ]]; then
    die "--no-build and --pull are mutually exclusive (try --help)"
fi

# Normalise the backend selection to a space-separated list in canonical
# order, rejecting unknown names.
SELECTED=""
IFS=',' read -r -a requested <<< "${BACKENDS// /}"
for backend in "${requested[@]}"; do
    case "${backend}" in
        cuda|rocm|opencl) ;;
        "") continue ;;
        *) die "Unknown backend: ${backend} (expected cuda, rocm or opencl)" ;;
    esac
done
for backend in cuda rocm opencl; do
    for wanted in "${requested[@]}"; do
        if [[ "${wanted}" == "${backend}" ]]; then
            SELECTED="${SELECTED} ${backend}"
            break
        fi
    done
done
SELECTED="${SELECTED# }"
[[ -n "${SELECTED}" ]] || die "No backend selected (try --help)"

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

# Per-backend lookups (bash 3 compatible — no associative arrays on macOS).
backend_label() {
    case "$1" in cuda) echo "CUDA" ;; rocm) echo "ROCm" ;; opencl) echo "OpenCL" ;; esac
}
backend_image() {
    case "$1" in cuda) echo "${CUDA_IMAGE}" ;; rocm) echo "${ROCM_IMAGE}" ;; opencl) echo "${OPENCL_IMAGE}" ;; esac
}
backend_name() {
    case "$1" in cuda) echo "${CUDA_NAME}" ;; rocm) echo "${ROCM_NAME}" ;; opencl) echo "${OPENCL_NAME}" ;; esac
}
backend_port() {
    case "$1" in cuda) echo "${CUDA_HOST_PORT}" ;; rocm) echo "${ROCM_HOST_PORT}" ;; opencl) echo "${OPENCL_HOST_PORT}" ;; esac
}

# The Docker daemon's own platform (what builds natively, without emulation).
native_platform() {
    case "$(docker info --format '{{.Architecture}}' 2>/dev/null)" in
        x86_64|amd64)  echo "linux/amd64" ;;
        aarch64|arm64) echo "linux/arm64" ;;
        *)             echo "" ;;
    esac
}

# The platform a backend is built and run for. CUDA has amd64 and arm64
# (SBSA) packages and follows the host; ROCm and OpenCL are amd64 only.
backend_platform() {
    if [[ -n "${PLATFORM}" ]]; then
        echo "${PLATFORM}"
    elif [[ "$1" == "cuda" ]]; then
        echo "${NATIVE_PLATFORM:-linux/amd64}"
    else
        echo "linux/amd64"
    fi
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

NATIVE_PLATFORM="$(native_platform)"
for backend in ${SELECTED}; do
    platform="$(backend_platform "${backend}")"
    if [[ "${backend}" != "cuda" && "${platform}" != "linux/amd64" ]]; then
        die "$(backend_label "${backend}") is linux/amd64 only (requested ${platform})"
    fi
    if [[ "${backend}" == "cuda" \
          && "${platform}" != "linux/amd64" && "${platform}" != "linux/arm64" ]]; then
        die "CUDA supports linux/amd64 and linux/arm64 only (requested ${platform})"
    fi
done

# Cross-platform builds (e.g. ROCm/OpenCL on an arm64 host) need BuildKit:
# the legacy builder fails with "failed to get destination image" and does
# not set TARGETARCH. Fail early with the fix instead.
if [[ "${PULL_GHCR}" -eq 0 && "${SKIP_BUILD}" -eq 0 ]] \
        && ! docker buildx version >/dev/null 2>&1; then
    for backend in ${SELECTED}; do
        platform="$(backend_platform "${backend}")"
        if [[ -n "${NATIVE_PLATFORM}" && "${platform}" != "${NATIVE_PLATFORM}" ]]; then
            die "Building the $(backend_label "${backend}") image for ${platform} on a ${NATIVE_PLATFORM} host needs BuildKit, but the docker buildx plugin is missing. Install it (Docker Desktop ships it; on Homebrew: brew install docker-buildx, then link it as described by 'brew info docker-buildx'), or build only native backends, e.g. --backend cuda."
        fi
    done
fi

# ---------------------------------------------------------------------------
# Acquire images: build locally, pull from GHCR, or reuse existing.
# ---------------------------------------------------------------------------
for backend in ${SELECTED}; do
    label="$(backend_label "${backend}")"
    image="$(backend_image "${backend}")"
    platform="$(backend_platform "${backend}")"
    if [[ "${PULL_GHCR}" -eq 1 ]]; then
        info "Pulling ${label} image (${image}, ${platform})"
        docker pull --platform "${platform}" "${image}" \
            || die "Pulling ${image} failed. The published images are private: run 'docker login ghcr.io' with an account that has read access, or build locally (omit --pull)."
        ok "${label} image pulled"
    elif [[ "${SKIP_BUILD}" -eq 0 ]]; then
        info "Building ${label} image (${image}, ${platform})"
        docker build --platform "${platform}" \
            -f "Src/docker/Dockerfile.${backend}" \
            -t "${image}" .
        ok "${label} image built"
    fi
done
if [[ "${PULL_GHCR}" -eq 0 && "${SKIP_BUILD}" -eq 1 ]]; then
    info "Skipping build (--no-build) — reusing existing local images"
fi

# ---------------------------------------------------------------------------
# Launch
# ---------------------------------------------------------------------------
for backend in ${SELECTED}; do
    stop_container "$(backend_name "${backend}")"
done

for backend in ${SELECTED}; do
    info "Launching $(backend_label "${backend}") container on host port $(backend_port "${backend}")"
    docker run -d \
        --platform "$(backend_platform "${backend}")" \
        -p "$(backend_port "${backend}"):5000" \
        --name "$(backend_name "${backend}")" \
        "$(backend_image "${backend}")" >/dev/null
done

# ---------------------------------------------------------------------------
# Health check
# ---------------------------------------------------------------------------
info "Waiting for services to become healthy"
for backend in ${SELECTED}; do
    wait_healthy "$(backend_label "${backend}")" "$(backend_port "${backend}")"
done

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
echo
ok "${BOLD}All selected services are running.${RESET}"
echo
for backend in ${SELECTED}; do
    printf '  %-6s → %shttp://localhost:%s%s\n' "$(backend_label "${backend}")" \
        "${BOLD}" "$(backend_port "${backend}")" "${RESET}"
done
echo
echo "Smoke test:"
for backend in ${SELECTED}; do
    echo "  curl -fsS http://localhost:$(backend_port "${backend}")/api/v1/capabilities | jq ."
done
cat <<EOF

Use in tests (env vars are optional — CompilerManagerFactory probes the
default ports 5001/5002/5003 automatically):
  ILGPU_CUDA_SERVICE_URL=http://localhost:${CUDA_HOST_PORT} \\
  ILGPU_ROCM_SERVICE_URL=http://localhost:${ROCM_HOST_PORT} \\
  ILGPU_OPENCL_SERVICE_URL=http://localhost:${OPENCL_HOST_PORT} \\
      dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

Stop with:
  $(basename "$0") --stop
EOF
