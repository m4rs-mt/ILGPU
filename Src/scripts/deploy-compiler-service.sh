#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# deploy-compiler-service.sh
#
# Build, push, and start ILGPUC.CompilerService on a remote Linux host as a
# systemd service. Idempotent — re-run to upgrade an existing install.
#
# Requires on local:  dotnet (>=10), rsync, ssh, curl
# Requires on remote: dotnet ASP.NET Core 10 runtime, sudo for the SSH user
# ---------------------------------------------------------------------------
set -euo pipefail

SERVICE_NAME="ilgpuc-compiler-service"
INSTALL_DIR="/opt/${SERVICE_NAME}"
SERVICE_USER="ilgpuc"
DEFAULT_PORT=5000
HEALTH_TIMEOUT=30

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="${REPO_ROOT}/Src/ILGPUC.CompilerService/ILGPUC.CompilerService.csproj"

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
${BOLD}Usage:${RESET}
  $(basename "$0") <user@host> [options]

${BOLD}Options:${RESET}
  --port N            Bind port on the remote (default: ${DEFAULT_PORT})
  --service-name N    Systemd unit name (default: ${SERVICE_NAME})
  --status            Show service status on the remote (no deploy)
  --logs              Tail journalctl on the remote (no deploy)
  -h, --help          This message

${BOLD}Examples:${RESET}
  # Deploy to a GPU build host
  $(basename "$0") build@cuda-host

  # Deploy on a non-default port
  $(basename "$0") build@cuda-host --port 8080

  # Check status of an existing deployment
  $(basename "$0") build@cuda-host --status

  # Tail logs (Ctrl-C to exit)
  $(basename "$0") build@cuda-host --logs

After deploy, configure tests to use the remote service:
  ILGPU_COMPILER_SERVICE_URL=http://<host>:${DEFAULT_PORT} \\
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s
EOF
}

# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------
TARGET=""
PORT="${DEFAULT_PORT}"
MODE="deploy"

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)        usage; exit 0 ;;
        --port)           PORT="$2"; shift 2 ;;
        --service-name)   SERVICE_NAME="$2"; INSTALL_DIR="/opt/${SERVICE_NAME}"; shift 2 ;;
        --status)         MODE="status"; shift ;;
        --logs)           MODE="logs"; shift ;;
        -*)               die "Unknown option: $1 (try --help)" ;;
        *)
            if [[ -z "${TARGET}" ]]; then
                TARGET="$1"
            else
                die "Unexpected argument: $1"
            fi
            shift
            ;;
    esac
done

[[ -n "${TARGET}" ]] || { usage; die "Missing <user@host>"; }

# Extract just the hostname for curl probes (strip user@)
REMOTE_HOST="${TARGET#*@}"

# ---------------------------------------------------------------------------
# Subcommands: status / logs
# ---------------------------------------------------------------------------
if [[ "${MODE}" == "status" ]]; then
    info "Remote service status (${TARGET})"
    ssh "${TARGET}" "sudo systemctl status ${SERVICE_NAME} --no-pager" || true
    echo
    info "Local health probe → http://${REMOTE_HOST}:${PORT}/api/v1/status"
    if curl -fsS --max-time 5 "http://${REMOTE_HOST}:${PORT}/api/v1/status"; then
        echo
        ok "Service is reachable"
    else
        err "Service is NOT reachable"
        exit 1
    fi
    exit 0
fi

if [[ "${MODE}" == "logs" ]]; then
    info "Tailing journalctl on ${TARGET} (Ctrl-C to exit)"
    exec ssh -t "${TARGET}" "sudo journalctl -u ${SERVICE_NAME} -f"
fi

# ---------------------------------------------------------------------------
# Deploy mode
# ---------------------------------------------------------------------------
info "Deploying ${SERVICE_NAME} to ${TARGET} (port ${PORT})"

# Local prerequisites
for tool in dotnet rsync ssh curl; do
    command -v "${tool}" >/dev/null 2>&1 \
        || die "Missing local tool: ${tool}"
done

DOTNET_MAJOR="$(dotnet --version | cut -d. -f1)"
[[ "${DOTNET_MAJOR}" -ge 10 ]] \
    || die "dotnet >= 10 required (found $(dotnet --version))"

[[ -f "${PROJECT}" ]] || die "Project not found: ${PROJECT}"

# Per-run staging dir, cleaned up on exit
STAGING="$(mktemp -d -t ilgpuc-publish-XXXXXX)"
trap 'rm -rf "${STAGING}"' EXIT

# 1. Build
info "Publishing (Release, linux-x64, framework-dependent)"
dotnet publish "${PROJECT}" \
    -c Release \
    -r linux-x64 \
    --no-self-contained \
    -o "${STAGING}" \
    --nologo \
    /clp:ErrorsOnly \
    >/dev/null
ok "Published $(du -sh "${STAGING}" | cut -f1) to staging"

[[ -f "${STAGING}/ILGPUC.CompilerService.dll" ]] \
    || die "Publish output missing ILGPUC.CompilerService.dll"

# 2. Remote prerequisites
info "Checking remote prerequisites"
ssh "${TARGET}" "command -v dotnet >/dev/null 2>&1" \
    || die "dotnet not found on ${TARGET}.
Install ASP.NET Core 10 runtime: https://learn.microsoft.com/dotnet/core/install/linux"

ssh "${TARGET}" "dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 10\\.'" \
    || die "Microsoft.AspNetCore.App 10.x not found on ${TARGET}.
Install ASP.NET Core 10 runtime: https://learn.microsoft.com/dotnet/core/install/linux"

ok "dotnet ASP.NET Core 10 runtime present"

# Compiler toolchain probe (warning only)
COMPILER_REPORT="$(ssh "${TARGET}" '
    found=""
    [[ -x /usr/local/cuda/bin/nvcc ]] && found="${found} nvcc"
    [[ -x /opt/rocm/bin/hipcc       ]] && found="${found} hipcc"
    [[ -x /usr/bin/ocloc            ]] && found="${found} ocloc"
    [[ -x /usr/bin/clang            ]] && found="${found} clang"
    echo "${found:- none}"
')"
if [[ "${COMPILER_REPORT}" == " none" ]]; then
    warn "No GPU compilers detected on ${TARGET} (looked for nvcc, hipcc, ocloc, clang)"
    warn "Service will run, but /api/v1/capabilities will report all targets unavailable"
else
    ok "Detected compilers on remote:${COMPILER_REPORT}"
fi

# Firewall reminder
if ssh "${TARGET}" "command -v ufw >/dev/null 2>&1 && sudo ufw status | grep -qi active" 2>/dev/null; then
    warn "ufw is active on ${TARGET} — make sure port ${PORT} is allowed: sudo ufw allow ${PORT}/tcp"
elif ssh "${TARGET}" "command -v firewall-cmd >/dev/null 2>&1 && sudo firewall-cmd --state 2>/dev/null | grep -q running" 2>/dev/null; then
    warn "firewalld is active on ${TARGET} — make sure port ${PORT} is allowed: sudo firewall-cmd --add-port=${PORT}/tcp --permanent && sudo firewall-cmd --reload"
fi

# 3. Create system user (idempotent)
info "Ensuring system user '${SERVICE_USER}' exists"
ssh "${TARGET}" "
    set -e
    if ! id ${SERVICE_USER} >/dev/null 2>&1; then
        sudo useradd --system --home-dir ${INSTALL_DIR} \
            --shell /usr/sbin/nologin ${SERVICE_USER}
    fi
"

# 4. Rsync to a staging dir on the remote, then sudo-rsync to /opt/...
REMOTE_STAGING="/tmp/${SERVICE_NAME}-staging"
info "Syncing artifacts to ${TARGET}:${REMOTE_STAGING}"
rsync -az --delete --info=stats0,progress0 \
    "${STAGING}/" "${TARGET}:${REMOTE_STAGING}/"

info "Installing to ${INSTALL_DIR}"
ssh "${TARGET}" "
    set -e
    sudo mkdir -p ${INSTALL_DIR}
    sudo rsync -a --delete ${REMOTE_STAGING}/ ${INSTALL_DIR}/
    sudo chown -R ${SERVICE_USER}:${SERVICE_USER} ${INSTALL_DIR}
    rm -rf ${REMOTE_STAGING}
"
ok "Installed to ${INSTALL_DIR}"

# 5. Install / update systemd unit
DOTNET_PATH="$(ssh "${TARGET}" "command -v dotnet")"
[[ -n "${DOTNET_PATH}" ]] || die "Could not resolve dotnet path on remote"

info "Installing systemd unit /etc/systemd/system/${SERVICE_NAME}.service"
# Note: Type=notify works with .NET >= 8 (sd_notify support).
# If startup hangs on a minimal system, change to Type=simple.
ssh "${TARGET}" "sudo tee /etc/systemd/system/${SERVICE_NAME}.service > /dev/null" <<EOF
[Unit]
Description=ILGPUC Compiler Service
Documentation=https://github.com/m4rs-mt/ILGPU
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
User=${SERVICE_USER}
Group=${SERVICE_USER}
WorkingDirectory=${INSTALL_DIR}
ExecStart=${DOTNET_PATH} ${INSTALL_DIR}/ILGPUC.CompilerService.dll
Environment=ASPNETCORE_URLS=http://0.0.0.0:${PORT}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Restart=on-failure
RestartSec=5

# Hardening
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
ReadWritePaths=/tmp

[Install]
WantedBy=multi-user.target
EOF

# 6. (Re)start
info "Reloading systemd and starting service"
ssh "${TARGET}" "
    set -e
    sudo systemctl daemon-reload
    sudo systemctl enable ${SERVICE_NAME} >/dev/null
    sudo systemctl restart ${SERVICE_NAME}
"
ok "systemctl restart issued"

# 7. Health check
info "Probing http://${REMOTE_HOST}:${PORT}/api/v1/status (timeout ${HEALTH_TIMEOUT}s)"
deadline=$(( $(date +%s) + HEALTH_TIMEOUT ))
status_body=""
while (( $(date +%s) < deadline )); do
    if status_body="$(curl -fsS --max-time 3 \
        "http://${REMOTE_HOST}:${PORT}/api/v1/status" 2>/dev/null)"; then
        break
    fi
    sleep 1
done

if [[ -z "${status_body}" ]]; then
    err "Health check failed — service is not reachable"
    err "Inspect logs with:  $(basename "$0") ${TARGET} --logs"
    exit 1
fi
ok "Service healthy: ${status_body}"

# Capabilities probe
info "Querying /api/v1/capabilities"
caps_body="$(curl -fsS --max-time 5 \
    "http://${REMOTE_HOST}:${PORT}/api/v1/capabilities" 2>/dev/null || true)"
if [[ -n "${caps_body}" ]]; then
    echo "${caps_body}"
fi

# 8. Followup
echo
ok "${BOLD}Deployment complete${RESET}"
cat <<EOF

Service is live at: ${BOLD}http://${REMOTE_HOST}:${PORT}${RESET}

Use in tests:
  ILGPU_COMPILER_SERVICE_URL=http://${REMOTE_HOST}:${PORT} \\
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

Or per-backend:
  ILGPU_CUDA_SERVICE_URL=http://${REMOTE_HOST}:${PORT} \\
    dotnet test --filter "Cuda" ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

Manage on the remote:
  sudo systemctl status   ${SERVICE_NAME}
  sudo systemctl restart  ${SERVICE_NAME}
  sudo journalctl -u ${SERVICE_NAME} -f

Or from this script:
  $(basename "$0") ${TARGET} --status
  $(basename "$0") ${TARGET} --logs
EOF
