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
| `Dockerfile.cuda` | `ubuntu:24.04` + CUDA 13.2 toolkit from NVIDIA's apt repository (`cuda-minimal-build-13-2`) | `linux/amd64`, `linux/arm64` (SBSA) | `nvcc`, `ptxas` at `/usr/local/cuda/bin/` |
| `Dockerfile.rocm` | `rocm/dev-ubuntu-24.04:7.1.1-complete`             | `linux/amd64` only   | `hipcc`, `amdclang++` at `/opt/rocm/bin/` |
| `Dockerfile.opencl` | `ubuntu:24.04` + `intel-ocloc` from Intel's apt repository | `linux/amd64` only | `ocloc` |

All three images:

- Use a multi-stage build (`mcr.microsoft.com/dotnet/sdk:10.0-noble` for the
  build stage, the toolchain base for the runtime stage). The full **.NET 10
  SDK** is installed on the toolchain base via `dotnet-install.sh` — not just the
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

## Licenses

Each image bundles a third-party compiler toolchain under its vendor's
license:

- **CUDA** (`Dockerfile.cuda`): the CUDA toolkit packages from NVIDIA's apt
  repository, under the
  [CUDA Toolkit EULA](https://docs.nvidia.com/cuda/eula/index.html).
- **ROCm** (`Dockerfile.rocm`): AMD's ROCm components (MIT, Apache-2.0 with
  LLVM exception, NCSA).
- **OpenCL** (`Dockerfile.opencl`): Intel's `ocloc` and Graphics Compiler
  (MIT), installed from Intel's apt repository.

Each image lists the licenses of everything it contains at
`/usr/share/doc/ilgpuc/NOTICES`.

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

This builds natively for the Docker host's architecture (amd64, or arm64 on
Apple Silicon / arm64 Linux) and works with the legacy builder too.

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
`rocm/dev-ubuntu-24.04` base. On an arm64 host add `--platform linux/amd64`,
which needs BuildKit (see [Apple Silicon](#running-on-apple-silicon-m1m2m3m4-macs)).

### OpenCL (amd64 only)

```bash
docker build \
    -f Src/docker/Dockerfile.opencl \
    -t ilgpuc-compiler-service-opencl:latest \
    .
```

Intel publishes no arm64 build of `ocloc`; the same `--platform linux/amd64`
and BuildKit note as for ROCm applies on arm64 hosts.

## Quick start: build and launch with `run.sh`

[`run.sh`](./run.sh) builds the images and launches the containers on
**different host ports** so they coexist:

```bash
Src/docker/run.sh                    # build all three, run all three
Src/docker/run.sh --backend cuda     # only CUDA (fastest; native on arm64)
Src/docker/run.sh --backend cuda,opencl
Src/docker/run.sh --no-build         # skip rebuild, just (re)launch
Src/docker/run.sh --stop             # stop and remove the containers
```

Defaults: CUDA on host port `5001`, ROCm on `5002`, OpenCL on `5003` (all
still listen on `5000` *inside* the container — see [Running containers
simultaneously](#running-containers-simultaneously) below). Override with
`CUDA_HOST_PORT=... ROCM_HOST_PORT=... OPENCL_HOST_PORT=...`.

Platforms are chosen per backend: CUDA builds for the Docker host's own
architecture, ROCm and OpenCL for `linux/amd64` (the only platform AMD and
Intel publish packages for). `PLATFORM=...` forces one platform for every
selected backend. Building an amd64 image on an arm64 host needs BuildKit
(the `docker buildx` plugin, shipped with Docker Desktop); without it
`run.sh` stops early and says so — `--backend cuda` still works.

`--pull` fetches the project's prebuilt images from GHCR instead of building.
They are private (they serve this repository's CI), so this needs
`docker login ghcr.io` with read access; otherwise build locally.

After it returns, every selected `/api/v1/status` endpoint has been polled
and you'll see, for example:

```
✓ CUDA healthy on http://localhost:5001
✓ ROCm healthy on http://localhost:5002
✓ OpenCL healthy on http://localhost:5003
```

## Running

The service only *compiles* kernels, so a plain `docker run` is enough —
no GPU, drivers or device access needed (this is also what `run.sh` does).
The GPU flags below are only needed when something inside the container
must *execute* kernels on real hardware (for example `dotnet test` of the
execution tests).

### CUDA

```bash
docker run --rm -d \
    -p 5000:5000 \
    --name ilgpuc-cuda \
    ilgpuc-compiler-service-cuda:13.2.0
```

To execute kernels on an NVIDIA GPU from inside the container, install the
[NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
on the host and add `--gpus all`.

### ROCm

```bash
docker run --rm -d \
    -p 5000:5000 \
    --name ilgpuc-rocm \
    ilgpuc-compiler-service-rocm:7.1.1
```

To execute kernels on an AMD GPU from inside the container, expose
`/dev/kfd` and `/dev/dri` and the host's `video` group
(`--device=/dev/kfd --device=/dev/dri --group-add video`; on newer kernels
also `--group-add render`).

### OpenCL

```bash
docker run --rm -d \
    -p 5000:5000 \
    --name ilgpuc-opencl \
    ilgpuc-compiler-service-opencl:latest
```

### Running containers simultaneously

All images bind to **port 5000 inside the container**. To run them at the
same time, map them to different *host* ports — that's what `-p HOST:5000`
does:

```bash
docker run --rm -d -p 5001:5000 --name ilgpuc-cuda   ilgpuc-compiler-service-cuda:13.2.0
docker run --rm -d -p 5002:5000 --name ilgpuc-rocm   ilgpuc-compiler-service-rocm:7.1.1
docker run --rm -d -p 5003:5000 --name ilgpuc-opencl ilgpuc-compiler-service-opencl:latest

# Each backend points at its own host port (optional: these are the
# defaults CompilerManagerFactory probes when no variable is set)
ILGPU_CUDA_SERVICE_URL=http://localhost:5001 \
ILGPU_ROCM_SERVICE_URL=http://localhost:5002 \
ILGPU_OPENCL_SERVICE_URL=http://localhost:5003 \
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s
```

Keeping the *internal* port fixed at 5000 across all images is the design
choice that makes this work without rebuilding anything. `run.sh` does
exactly this end-to-end.

## Running on Apple Silicon (M1/M2/M3/M4 Macs)

Docker Desktop on Apple Silicon can run these images directly — no separate
VM needed.

- **CUDA image**: NVIDIA's apt repository ships `linux/arm64` (SBSA)
  packages, so on Apple Silicon the image builds natively for `linux/arm64`.
  No emulation involved.
- **ROCm and OpenCL images**: AMD and Intel only publish `linux/amd64`,
  so these run under emulation. Add `--platform linux/amd64` to
  `docker build` and `docker run` (`run.sh` does this for you). Building
  for a foreign platform needs **BuildKit** (`docker buildx`, included in
  Docker Desktop); the legacy builder fails with "failed to get destination
  image". **Enable Rosetta** in *Docker Desktop → Settings → General →
  "Use Rosetta for x86_64/amd64 emulation on Apple Silicon"* — it is
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
| `ubuntu:24.04` + `cuda-minimal-build-13-2` | CUDA 13.2 compiler toolchain from NVIDIA's apt repository (`x86_64` and `sbsa` repos) — only the packages the compiler service needs. |
| `rocm/dev-ubuntu-24.04:7.1.1-complete`    | Last release with a documented `-complete` tag bundling `hipcc`; ROCm 7.2+ deprecates `hipcc` in favour of `amdclang++`. |
| `ubuntu:24.04` + `intel-ocloc`            | Intel's compute-runtime apt repository (`noble unified`); MIT-licensed. |
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
