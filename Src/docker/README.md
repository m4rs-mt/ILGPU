# Docker images for `ILGPUC.CompilerService`

This directory holds Dockerfiles for packaging
[`ILGPUC.CompilerService`](../ILGPUC.CompilerService) together with the native
GPU toolchains, so a `docker run` is enough to spin up a remote builder for
ILGPU's CUDA / ROCm backends. The same images plug straight into the
`ILGPU_*_SERVICE_URL` test plumbing described in `Src/CLAUDE.md`.

> **No prebuilt images are published.** Build them yourself with the commands
> below.

## What's in the box

| Dockerfile        | Runtime base                                       | Architectures        | Bundled compilers              |
|-------------------|----------------------------------------------------|----------------------|--------------------------------|
| `Dockerfile.cuda` | `nvidia/cuda:13.2.0-devel-ubuntu24.04`             | `linux/amd64`, `linux/arm64` (SBSA) | `nvcc`, `ptxas` at `/usr/local/cuda/bin/` |
| `Dockerfile.rocm` | `rocm/dev-ubuntu-24.04:7.1.1-complete`             | `linux/amd64` only   | `hipcc`, `amdclang++` at `/opt/rocm/bin/` |

Both images:

- Use a multi-stage build (`mcr.microsoft.com/dotnet/sdk:10.0-noble` for the
  build stage, the GPU base for the runtime stage). The full **.NET 10 SDK**
  is installed on the GPU base via `dotnet-install.sh` — not just the
  ASP.NET Core runtime. The SDK is a strict superset of the runtime, so the
  CompilerService still launches via the same `dotnet ILGPUC.CompilerService.dll`
  default command. The reason for the larger install (~700 MB extra) is that
  the same images double as **CI test runners**: the GitHub Actions test
  workflow does `docker run image dotnet test ...` against bind-mounted test
  binaries, which requires the SDK.
- Use **`CMD`** (not `ENTRYPOINT`) for the service launch line, so
  `docker run image` (no args, what `Src/docker/run.sh` does) starts the
  CompilerService exactly like before, but `docker run image dotnet test ...`
  cleanly overrides the default for CI use.
- Run as a non-root `ilgpuc` user (uid 1000). Ubuntu 24.04's default
  `ubuntu` user is removed first to free that uid.
- **Listen on port `5000` *inside the container*** (`EXPOSE 5000`,
  `ASPNETCORE_URLS=http://+:5000`). The internal port is fixed across all
  three images so the same `ILGPU_*_SERVICE_URL` plumbing works uniformly.
- Define a `HEALTHCHECK` against `GET /api/v1/status`.
- Carry OCI labels (`org.opencontainers.image.source`,
  `org.opencontainers.image.licenses`, `org.opencontainers.image.description`)
  so the published GHCR packages auto-link back to the source repo and show
  up under the repo's Packages tab.

## Building

> **Build context is the repo root**, not `Src/`. The build needs `global.json`
> at the repo root and `Tools/CheckStyles/ILGPU.CheckStyles.targets` (imported
> by `Src/Directory.Build.props`). Always run `docker build` from the repo
> root.

### CUDA (single-arch, current host)

```bash
# From the repo root
docker build \
    -f Src/docker/Dockerfile.cuda \
    -t ilgpuc-compiler-service-cuda:13.2.0 \
    .
```

### CUDA (multi-arch, amd64 + arm64)

```bash
# One-time builder setup
docker buildx create --name ilgpuc-builder --driver docker-container --use
docker buildx inspect --bootstrap

# Build for both architectures
docker buildx build \
    --platform linux/amd64,linux/arm64 \
    -f Src/docker/Dockerfile.cuda \
    -t ilgpuc-compiler-service-cuda:13.2.0 \
    .
```

Notes:

- `--load` only works for single-platform builds. To export a multi-arch
  build locally, use `--output type=oci,dest=cuda.tar` (or `--push` if you
  have a registry).
- arm64 builds via QEMU are slow (15–30 min easily). On native arm64
  hardware they finish in normal time.

### ROCm (amd64 only)

```bash
docker build \
    -f Src/docker/Dockerfile.rocm \
    -t ilgpuc-compiler-service-rocm:7.1.1 \
    .
```

There is no ARM64 build for ROCm — AMD does not publish a multi-arch
`rocm/dev-ubuntu-24.04` base.

## Quick start: build and launch both side-by-side

[`run.sh`](./run.sh) builds both Dockerfiles and launches the CUDA + ROCm
containers on **different host ports** so they coexist:

```bash
Src/docker/run.sh                    # build both, run both
Src/docker/run.sh --no-build         # skip rebuild, just (re)launch
Src/docker/run.sh --stop             # stop and remove the containers
```

Defaults: CUDA on host port `5001`, ROCm on host port `5002` (both still
listen on `5000` *inside* the container — see [Running both
simultaneously](#running-both-containers-simultaneously) below).

Override with `CUDA_HOST_PORT=... ROCM_HOST_PORT=... PLATFORM=...`.

After it returns, both `/api/v1/status` endpoints have been polled and
you'll see:

```
✓ CUDA healthy on http://localhost:5001
✓ ROCm healthy on http://localhost:5002
```

## Running

### CUDA

Requires the
[NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
on the host.

```bash
docker run --rm -d \
    --gpus all \
    -p 5000:5000 \
    --name ilgpuc-cuda \
    ilgpuc-compiler-service-cuda:13.2.0
```

### ROCm

Needs `/dev/kfd` and `/dev/dri` exposed, plus the host's `video` group:

```bash
docker run --rm -d \
    --device=/dev/kfd \
    --device=/dev/dri \
    --group-add video \
    -p 5000:5000 \
    --name ilgpuc-rocm \
    ilgpuc-compiler-service-rocm:7.1.1
```

On newer kernels you may also need `--group-add render`.

### Running both containers simultaneously

Both images bind to **port 5000 inside the container**. To run them at the
same time, map them to different *host* ports — that's what `-p HOST:5000`
does:

```bash
# CUDA on host port 5001
docker run --rm -d --gpus all \
    -p 5001:5000 --name ilgpuc-cuda \
    ilgpuc-compiler-service-cuda:13.2.0

# ROCm on host port 5002
docker run --rm -d \
    --device=/dev/kfd --device=/dev/dri --group-add video \
    -p 5002:5000 --name ilgpuc-rocm \
    ilgpuc-compiler-service-rocm:7.1.1

# Each backend points at its own host port
ILGPU_CUDA_SERVICE_URL=http://localhost:5001 \
ILGPU_ROCM_SERVICE_URL=http://localhost:5002 \
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s
```

Keeping the *internal* port fixed at 5000 across both images is the design
choice that makes this work without rebuilding anything. `run.sh` does
exactly this end-to-end.

## Running on Apple Silicon (M1/M2/M3/M4 Macs)

Docker Desktop on Apple Silicon can run these images directly — no separate
VM needed.

- **CUDA image**: the `nvidia/cuda:13.2.0-devel-ubuntu24.04` base ships a
  multi-arch manifest, so on Apple Silicon Docker pulls the native
  `linux/arm64` (SBSA) layer. No emulation involved.
- **ROCm image**: AMD only publishes `linux/amd64`, so you must run it
  under emulation. Add `--platform linux/amd64` to `docker build` and
  `docker run`. **Enable Rosetta** in *Docker Desktop → Settings → General
  → "Use Rosetta for x86_64/amd64 emulation on Apple Silicon"* — it is
  dramatically faster than the QEMU fallback.

### "But there's no NVIDIA/AMD GPU on a Mac, what's the point?"

`nvcc`, `ptxas`, `hipcc`, and `amdclang++` are all *compilers*. They
translate source to PTX/binary without ever touching a GPU. The ILGPUC
compiler service only invokes them as compilers — it never executes the
generated code itself. **You can run a fully functional remote builder for
CUDA or HIP code on a Mac**; you just can't `dotnet test --filter Cuda`
against it (the test runner needs to *execute* the compiled kernels, which
requires real GPU hardware on the remote machine).

## Smoke-testing the container

The `/api/v1/status` endpoint always returns `{"status":"healthy",...}` and
does **not** require GPU access — useful for confirming the image runs at all
even on a host without an NVIDIA / AMD GPU:

```bash
docker run --rm -d -p 5000:5000 --name ilgpuc-test ilgpuc-compiler-service-cuda:13.2.0
curl -fsS http://localhost:5000/api/v1/status
docker stop ilgpuc-test
```

To verify that the bundled compilers are actually present and detected
(this *does* need GPU runtime flags):

```bash
curl -fsS http://localhost:5000/api/v1/capabilities | jq .
```

The response lists each backend (`cuda`, `hip`, ...) with `available: true`
and a `version` string when the compiler responds.

## Pointing ILGPUC tests at the container

```bash
# Single all-in-one CUDA service
ILGPU_CUDA_SERVICE_URL=http://localhost:5000 \
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

# ROCm running on a remote host
ILGPU_ROCM_SERVICE_URL=http://gpu-host:5000 \
    dotnet test --filter ROCm ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s
```

The available env vars (`ILGPU_COMPILER_SERVICE_URL`, `ILGPU_CUDA_SERVICE_URL`,
`ILGPU_ROCM_SERVICE_URL`, `ILGPU_METAL_SERVICE_URL`,
`ILGPU_OPENCL_SERVICE_URL`) are documented in `Src/CLAUDE.md`. Because both
images expose port 5000, you can swap the running image without touching the
test command.

## Pinning rationale

| Pin                                       | Why                                                                 |
|-------------------------------------------|---------------------------------------------------------------------|
| `nvidia/cuda:13.2.0-devel-ubuntu24.04`    | Latest CUDA SDK with multi-arch (`amd64` + `arm64/SBSA`) manifests. |
| `rocm/dev-ubuntu-24.04:7.1.1-complete`    | Last release with a documented `-complete` tag bundling `hipcc`; ROCm 7.2+ deprecates `hipcc` in favour of `amdclang++`. |
| `mcr.microsoft.com/dotnet/sdk:10.0-noble` | .NET 10 GA, Ubuntu 24.04, multi-arch.                                |

Bump these in the Dockerfiles when newer base images have been validated
against `ILGPUC.Compilers`.

## Known quirks

- **`OpenCLAmd` reports unavailable in the ROCm image.**
  `ILGPUC.Compilers/CompilerOptions.cs` looks for clang at `/usr/bin/clang`,
  but the ROCm `*-complete` base ships clang at `/opt/rocm/llvm/bin/clang`
  and the wrapper as `/opt/rocm/bin/amdclang++`. The `Hip` backend itself
  is fully operational; only the OpenCL-via-clang detection misses. Fix
  later by either symlinking or making `ClangPath` configurable from an
  env var inside the service.

## Related

- `Src/scripts/deploy-compiler-service.sh` — non-Docker, systemd-based
  deployment to a remote Linux host.
- `Src/CLAUDE.md` — "Remote Compiler Service Configuration" section
  documents the test-time env vars and the exact compiler-detection paths.
