# scripts/

Operational helpers. Run from the repository root or anywhere — they
resolve their own location.

| Script | Purpose |
|--------|---------|
| `deploy-compiler-service.sh` | Build, push, and start `ILGPUC.CompilerService` on a remote Linux host as a systemd service |
| `update-snapshots.sh` | Regenerate IR snapshots and commit them in the `Snapshots` submodule |

---

## deploy-compiler-service.sh

Builds `ILGPUC.CompilerService` locally, rsyncs the published bundle to a
remote Linux host, installs a systemd unit so the service auto-starts on
boot, and verifies the deployment by hitting `/api/v1/status`. Re-running
the script upgrades an existing install in place.

### When to use it

You're running tests on a machine that doesn't have all GPU toolchains
locally (e.g. macOS without `nvcc`/`hipcc`/`ocloc`) and want to redirect
backend tests through a remote build host. After deploying, set
`ILGPU_*_SERVICE_URL` env vars when running `dotnet test` and the test
framework will compile against the remote service. See the
"Remote Compiler Service Configuration" section in `Src/CLAUDE.md` for the
full list of env vars.

### Prerequisites

**Local machine:**
- `dotnet` SDK ≥ 10
- `rsync`, `ssh`, `curl`

**Remote host:**
- Linux with systemd
- ASP.NET Core 10 runtime (`Microsoft.AspNetCore.App 10.x`)
  — install: <https://learn.microsoft.com/dotnet/core/install/linux>
- The SSH user must have `sudo` (the script writes to `/opt`, creates a
  system user, and installs a systemd unit)
- One or more GPU compilers in their default locations:
  - `nvcc` at `/usr/local/cuda/bin/nvcc`
  - `hipcc` at `/opt/rocm/bin/hipcc`
  - `ocloc` at `/usr/bin/ocloc`
  - `clang` at `/usr/bin/clang`
  
  The script does **not** install these — it only checks them and warns
  if none are found. Without any compiler, the service runs but reports
  every target as unavailable.

### Usage

```bash
# Deploy on the default port (5000)
Src/scripts/deploy-compiler-service.sh build@cuda-host

# Custom port
Src/scripts/deploy-compiler-service.sh build@cuda-host --port 8080

# Custom systemd unit name (if you want multiple services on one host)
Src/scripts/deploy-compiler-service.sh build@cuda-host \
    --service-name ilgpuc-cuda-service --port 5001

# Check status of an existing deployment
Src/scripts/deploy-compiler-service.sh build@cuda-host --status

# Tail journalctl on the remote (Ctrl-C to exit)
Src/scripts/deploy-compiler-service.sh build@cuda-host --logs
```

### What it installs on the remote

| Path | Contents |
|------|----------|
| `/opt/ilgpuc-compiler-service/` | Published service binaries, owned by `ilgpuc` user |
| `/etc/systemd/system/ilgpuc-compiler-service.service` | Systemd unit (Type=notify, Restart=on-failure, hardened) |
| `ilgpuc` system user | Non-login user that owns the install dir and runs the service |

The systemd unit binds `ASPNETCORE_URLS=http://0.0.0.0:<port>` so the
service is reachable from outside the box. Process hardening:
`NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`,
`ReadWritePaths=/tmp` (the only writable location — needed because
compilers stage temp files under `Path.GetTempPath()`).

### Firewall

The script does **not** modify firewall rules. If `ufw` or `firewalld` is
active on the remote, it prints the exact command you need to run:

```bash
# ufw
sudo ufw allow 5000/tcp

# firewalld
sudo firewall-cmd --add-port=5000/tcp --permanent
sudo firewall-cmd --reload
```

### Verifying a deployment

After deploy, the script polls `/api/v1/status` for up to 30 seconds and
dumps `/api/v1/capabilities` to show which compilers the service detected.
You can probe manually any time:

```bash
curl http://cuda-host:5000/api/v1/status
curl http://cuda-host:5000/api/v1/capabilities
```

### Using the deployed service in tests

```bash
# Single host serving all GPU backends
ILGPU_COMPILER_SERVICE_URL=http://cuda-host:5000 \
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

# Different hosts per backend
ILGPU_CUDA_SERVICE_URL=http://cuda-host:5000 \
ILGPU_ROCM_SERVICE_URL=http://rocm-host:5000 \
ILGPU_OPENCL_SERVICE_URL=http://intel-host:5000 \
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s
```

Per-backend env vars take precedence over `ILGPU_COMPILER_SERVICE_URL`.

### Troubleshooting

| Symptom | Fix |
|---------|-----|
| `dotnet >= 10 required` | Install .NET 10 SDK locally |
| `Microsoft.AspNetCore.App 10.x not found on <host>` | Install ASP.NET Core 10 runtime on the remote |
| Health check times out | `--logs` to see what the service is complaining about; check firewall; confirm `ASPNETCORE_URLS` shows `0.0.0.0:<port>` in `systemctl show ilgpuc-compiler-service -p Environment` |
| `/api/v1/capabilities` reports all compilers unavailable | Install the toolchain at the expected default path, then `--status` (re-running deploy is unnecessary — capabilities are probed lazily) |
| `Type=notify` startup hangs | Edit the unit on the remote and switch to `Type=simple`, then `daemon-reload` + `restart`. Has not been observed on standard distros — only listed for completeness |

### Managing the service manually

```bash
sudo systemctl status   ilgpuc-compiler-service
sudo systemctl restart  ilgpuc-compiler-service
sudo systemctl stop     ilgpuc-compiler-service
sudo systemctl disable  ilgpuc-compiler-service     # don't start on boot
sudo journalctl -u ilgpuc-compiler-service -f
```

### Uninstalling

```bash
sudo systemctl disable --now ilgpuc-compiler-service
sudo rm /etc/systemd/system/ilgpuc-compiler-service.service
sudo systemctl daemon-reload
sudo rm -rf /opt/ilgpuc-compiler-service
sudo userdel ilgpuc
```

---

## update-snapshots.sh

Regenerates IR snapshot `.il` files for `ILGPUC.Tests/IRTests/*` and
commits the result inside the `Snapshots/` submodule, then bumps the
parent repo's submodule pointer. Use after any IR-affecting change. See
"Update IR snapshots" in `Src/CLAUDE.md` for the underlying mechanism.

```bash
Src/scripts/update-snapshots.sh
```

Pass through extra `dotnet test` arguments to filter which snapshots are
regenerated:

```bash
Src/scripts/update-snapshots.sh --filter BasicIfIRTests
```
