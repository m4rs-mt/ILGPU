# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build everything
dotnet build ILGPU.sln

# Build a single project
dotnet build ILGPUC/ILGPUC.csproj

# Run all tests (always use --blame-hang-timeout 360s to prevent endless loops in compiler tests)
dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

# Run tests by layer
dotnet test --filter IRTests --blame-hang-timeout 360s
dotnet test --filter BackendTests --blame-hang-timeout 360s
dotnet test --filter ExecutionTests --blame-hang-timeout 360s
dotnet test --filter IntegrationTests --blame-hang-timeout 360s

# Run a single test class or method
dotnet test --filter BasicIfIRTests --blame-hang-timeout 360s
dotnet test --filter "BasicIfExecutionTests.IfTrue_ProducesCorrectOutput" --blame-hang-timeout 360s

# Update IR snapshots (writes .il files, then review with git diff)
ILGPU_UPDATE_IR=1 dotnet test --filter IRTests --blame-hang-timeout 360s

# Update snapshots + commit submodule
scripts/update-snapshots.sh

# Compile-time perf regression tests — deterministic counter budgets
# (methods discovered, assemblies scanned, cache reuse, walkability).
# Runs as part of the regular suite; this just filters to that layer.
dotnet test --filter PerfTests --blame-hang-timeout 360s

# Compile-time profiling bench — opt-in only, writes trace.csv +
# summary.json to ILGPUC.Tests' bench-output/. Same workload as the
# old ILGPUC.CompileBench exe (a[i] = b[i] * c[i] across all five
# backends). Optional: ILGPU_BENCH_WARMUP / ILGPU_BENCH_ITERATIONS /
# ILGPU_BENCH_OUTPUT.
ILGPU_RUN_BENCH=1 dotnet test --filter CompileBench --blame-hang-timeout 360s
```

Target framework is **net10.0** (C# 13). `AllowUnsafeBlocks` is enabled.

## Project Structure

| Project | Type | Purpose |
|---------|------|---------|
| `ILGPU/` | Library | Runtime library (accelerators, buffers, intrinsics) |
| `ILGPUC/` | Exe | AOT compiler CLI (`compile` and `build` commands) |
| `ILGPUC.Compilers/` | Library | Native compiler bindings (Metal, NVCC, Hipcc, clang) |
| `ILGPUC.CompilerService/` | Exe | Remote compilation service |
| `ILGPUC.Tests/` | Tests | xUnit test suite (IR snapshots, backend codegen, execution) |

## Compilation Pipeline

```
IL bytecode ──→ ILFrontend ──→ ModuleBuilder.Seal() ──→ Optimizer ──→ Backend.GenerateCode()
                (Frontend/)     (IR/ModuleValues/)       (IR/Transformations/)  (Backends/)
                                                                                    │
                                                               CompiledKernelGenerator
                                                               (C# wrapper source)
```

- **`KernelCompiler.cs`** — Facade: `MethodInfo` + `Backend` → `CompiledKernel` C# source
- **`ILFrontend.cs`** — Disassembles IL into SSA IR via `ModuleBuilder`
- **`ModuleBuilder.Seal()`** — Finalizes IR graph, computes uses, orders methods
- **Optimizer** — Runs transformation passes (address space inference, inlining, etc.)
- **`Backend.GenerateCode()`** — IR → target language source (Metal/CUDA/OpenCL/ROCm/CPU C#)

### Roslyn-based `build` command (alternative pipeline)

`ILGPUC/RoslynFrontend/` — Parses C# source directly via Roslyn, rewrites launch sites, injects compiled kernels. Namespaces: `ILGPUC.RoslynFrontend.{Analysis,Rewriting,Emit,Generation}`.

## IR Architecture

**Type hierarchy:** `TypeValue` → `PrimitiveType`, `ViewType`, `PointerType`, `StructureType`, `ArrayType`, `VoidType`

**Value hierarchy:** `Value` → `GlobalValue` (Module-level: `Method`, `Global`, `TypeValue`) and `BasicBlockValue` (block-local SSA values: `PhiValue`, `MethodCall`, loads/stores, arithmetic, etc.)

**Key types:**
- `Module` — Container for methods, globals, types
- `Method` — Has `Parameters`, `Blocks`, `EntryBlock`, `ExitBlock`, `CalledMethods`
- `BasicBlock` — SSA block with phi values and regular values
- `Parameter` — Method parameter with `Name`, `Index`, `Type`

**Transformation framework** (`IR/Transformations/Transformation/`):
- `ModuleTransform` — Rewrites at module level (globals, types, methods)
- `MethodTransform` — Rewrites within a single method
- `BasicBlockTransform` — Rewrites within a single basic block
- `PureValueTransform` — Rewrites pure (side-effect-free) values

**Backends** (`Backends/`): CPU, Cuda, Metal, OpenCL, ROCm — each has a `Backend`, `LanguageConfiguration`, `IntrinsicEmitter`, `LauncherEmitter`, and `CompiledKernelEmitter`.

## Test Infrastructure

Four test layers in `ILGPUC.Tests/`:

1. **IR Snapshot Tests** (`IRTests/`) — Compare IR text at pipeline stages (`AfterFrontend`, `AfterGlobalOpt`, `AfterBackendTransforms/`) against `.il` files in `Snapshots/` submodule. Set `ILGPU_UPDATE_IR=1` to regenerate.

2. **Backend Tests** (`BackendTests/`) — Verify every kernel produces non-empty source on all 5 backends. `KernelRegistry` auto-discovers kernels from `Kernels/` classes ending with `Kernels`.

3. **Execution Tests** (`ExecutionTests/`) — Build standalone programs from `TestPrograms/`, run as subprocess with 30s timeout, verify stdout line-by-line.

4. **Integration Tests** (`IntegrationTests/`) — End-to-end MSBuild integration. Materialize a real standalone `.csproj` from a template under `IntegrationProjects/`, run `dotnet build` against it as a subprocess, inspect the artifacts produced by `Src/ILGPUC/build/ILGPU.Kernels.targets` (`obj/.../ilgpu/{manifest.txt,args.rsp,rewritten/,generated/}`), and execute the resulting binary verifying stdout. The closest analog to "what a downstream user gets" — exercises the `ilgpuc build` CLI, the `.targets` file, the `<Compile Remove>` source-swapping, and the rewritten launch sites end-to-end. Two test classes:
   - `MsBuildIntegrationTests` — 8 tests covering happy path, no-kernel projects, multi-kernel projects, backend property override, compile errors, incremental skip / rebuild, `dotnet clean`. Most tests are `[SkippableTheory]` over `BackendType.{CPU,Metal}` with the existing `Availability` skip gates handling missing GPU hardware.
   - `NuGetIntegrationTests` — 1 NuGet pack/restore smoke test that packs `ILGPU + ILGPUC.Compilers + ILGPUC` into a per-test temp feed, then verifies a consumer project picks up `build/ILGPUC.targets` via auto-import. Run with `dotnet test --filter NuGetIntegrationTests`.

   The framework helpers live at `ILGPUC.Tests/Framework/{MsBuildRunner.cs,IntegrationProjectFixture.cs}` — a subprocess wrapper for `dotnet build/clean/pack/restore` and a fixture that materializes templates with token substitution. The fixture leaves the temp working dir in place on test failure so you can inspect `obj/.../ilgpu/` directly.

### Remote Compiler Service Configuration

Backend and execution tests resolve native compilers via `CompilerManagerFactory` (`ILGPUC.Tests/Framework/CompilerManagerFactory.cs`). By default it uses a local `CompilerManager`, but environment variables can redirect any backend to a remote `ILGPUC.CompilerService` instance — useful when developing on a machine that lacks one or more native toolchains (e.g. macOS without nvcc/hipcc/ocloc).

| Variable | Scope |
|----------|-------|
| `ILGPU_COMPILER_SERVICE_URL` | Fallback for all GPU backends |
| `ILGPU_CUDA_SERVICE_URL` | Cuda only |
| `ILGPU_ROCM_SERVICE_URL` | ROCm only |
| `ILGPU_METAL_SERVICE_URL` | Metal only |
| `ILGPU_OPENCL_SERVICE_URL` | OpenCL only |

Resolution precedence per backend: per-backend variable > `ILGPU_COMPILER_SERVICE_URL` > **per-backend Docker default URL (CUDA: `http://localhost:5001`; ROCm: `http://localhost:5002`; OpenCL: `http://localhost:5003`)** > local `CompilerManager`. CPU is always local. Both `Availability.IsCompilerAvailableAsync` (skip gating) and the actual compile sites (`CompilationHelper.CompileToNativeAsync`, `ProgramBuilder.GenerateWrapper`) go through the factory, so a remote service that reports a compiler available will also handle the compile. If the remote service is unreachable, `RemoteCompilerManager` returns empty capabilities and tests skip — they never fail with a network error.

`Availability.IsRuntimeAvailable(BackendType)` is a *separate* gate from the compiler check. It probes for an actual local device via `XxxDevice.GetDevices()` and is used by `ExecutionTestBase.RunProgramAsync` (after the compiler check). The compiler check can be satisfied by a remote / Docker service even on a host without GPU hardware; execution tests additionally need a real device. Without this gate, GPU execution tests on hardware-less hosts (e.g. macOS running the CUDA / ROCm Docker containers) crashed with an opaque exit-code-134 process abort. CPU is always available, Metal returns true on macOS, CUDA / ROCm / OpenCL return true only when the corresponding driver is loaded.

When **no** env var is set, `CompilerManagerFactory.GetCompilerManagerAsync` probes the per-backend Docker default URL once per backend per `dotnet test` invocation (3 s timeout, near-instant on connection-refused). If the container is up and reports the backend as available, all CUDA / ROCm / OpenCL compilation routes through it automatically — no env vars needed after `Src/docker/run.sh`. If the probe fails, it falls back to the local `CompilerManager` (today's behaviour). Defaults match `Src/docker/run.sh` `CUDA_HOST_PORT` / `ROCM_HOST_PORT` / `OPENCL_HOST_PORT`; bump both files together when reassigning. Metal has no Docker default and stays env-var-only.

```bash
# Run all tests with Cuda/ROCm/OpenCL via a remote builder, Metal local:
ILGPU_CUDA_SERVICE_URL=http://gpu-builder:5000 \
ILGPU_ROCM_SERVICE_URL=http://gpu-builder:5000 \
ILGPU_OPENCL_SERVICE_URL=http://gpu-builder:5000 \
dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

# Single all-in-one service for every GPU backend:
ILGPU_COMPILER_SERVICE_URL=http://gpu-builder:5000 \
dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

# Start a local CompilerService for round-trip testing:
dotnet run --project ILGPUC.CompilerService/ILGPUC.CompilerService.csproj
```

The `ILGPUC.CompilerService.Tests` project validates the service in isolation against a `MockCompilerManager`, so it has no native toolchain dependency.

#### Deploying the CompilerService to Linux

`scripts/deploy-compiler-service.sh` builds the service locally, rsyncs it to a remote host, installs a systemd unit, and verifies the deployment via `/api/v1/status`. Idempotent — re-run to upgrade. Requires `dotnet` ASP.NET Core 10 runtime and sudo access on the target host.

```bash
# Deploy to a GPU build host (default port 5000)
scripts/deploy-compiler-service.sh build@cuda-host

# Custom port, then point tests at it
scripts/deploy-compiler-service.sh build@cuda-host --port 8080
ILGPU_CUDA_SERVICE_URL=http://cuda-host:8080 \
    dotnet test --filter Cuda ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s

# Status / logs of an existing deployment
scripts/deploy-compiler-service.sh build@cuda-host --status
scripts/deploy-compiler-service.sh build@cuda-host --logs
```

The script does **not** install GPU compilers (nvcc/hipcc/ocloc/clang) — those must already exist at their default paths on the target. It warns if none are detected and reminds you about firewall rules when `ufw`/`firewalld` is active.

#### Docker images for the CompilerService

`docker/Dockerfile.{cuda,rocm,opencl}` package the service together with their respective native toolchains. CUDA pins `nvidia/cuda:13.2.0-devel-ubuntu24.04` (multi-arch amd64+arm64/SBSA); ROCm pins `rocm/dev-ubuntu-24.04:7.1.1-complete` (amd64 only — AMD has no upstream ARM64 dev image); OpenCL uses `ubuntu:24.04` + `intel-ocloc` from `repositories.intel.com/gpu/ubuntu noble unified` (amd64 only — Intel does not publish ARM64 builds of `ocloc`). All three images install the **full .NET 10 SDK** via `dotnet-install.sh` on top of their base (the SDK is a strict superset of the ASP.NET Core runtime — needed so `dotnet test` can run inside the container in the CI test workflow), run as a non-root `ilgpuc` user, and **listen on port 5000 inside the container**. Build context must be the repo root (the build needs `global.json`, `Tools/CheckStyles/...`, and `Src/ILGPUC/Static/*.ttinclude` which ILGPU's T4 templates pull in cross-project).

The service launch command is `CMD` (not `ENTRYPOINT`), so `docker run image` with no extra args starts the CompilerService exactly as before, but `docker run image dotnet test ...` cleanly overrides the default for CI use.

`docker/run.sh` builds OR pulls all three images and launches them side-by-side: CUDA on host port `5001`, ROCm on host port `5002`, OpenCL on host port `5003` (all still listen on `5000` inside the container). This is the only way to run them at once — same internal port, different host mappings.

```bash
Src/docker/run.sh                    # build all locally, run all (~50 min cold)
Src/docker/run.sh --pull             # pull from ghcr.io (~5 min), run all
Src/docker/run.sh --no-build         # skip rebuild, reuse existing local images
Src/docker/run.sh --stop             # stop and remove

# Then point tests at each backend's host port (env vars are optional —
# CompilerManagerFactory probes 5001/5002/5003 automatically when no
# env var is set):
ILGPU_CUDA_SERVICE_URL=http://localhost:5001 \
ILGPU_ROCM_SERVICE_URL=http://localhost:5002 \
ILGPU_OPENCL_SERVICE_URL=http://localhost:5003 \
    dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --blame-hang-timeout 360s
```

`--pull` mode pulls from `ghcr.io/m4rs-mt/ilgpuc-compiler-service-{cuda,rocm,opencl}:latest` (override the namespace via `GHCR_OWNER=my-fork`). The published images are produced by `.github/workflows/docker-publish.yml` on a monthly cron + manual trigger; old `:sha-*` tags are pruned by `.github/workflows/cleanup-ghcr.yml` on a monthly cron. Use `--pull` when you just want the service running locally without iterating on the Dockerfiles. Use the default mode when you're changing the Dockerfiles or verifying a local source change end-to-end.

On Apple Silicon, enable Rosetta in Docker Desktop (`Settings → General`) for fast amd64 emulation — required for ROCm. The CUDA image runs natively (arm64/SBSA). nvcc/hipcc/amdclang++ are *compilers* and don't need a real GPU at compile time, so the containers are fully usable as remote builders even on a Mac. See `docker/README.md` for the full story.

#### CI uses these images without the HTTP service tier

`.github/workflows/ci.yml` is the canonical CI pipeline (one workflow, sixteen jobs). For the GPU compile-only test jobs, instead of running the CompilerService and reaching it over HTTP, the CI workflow `docker run`s the GHCR-published image directly with the prebuilt test binaries bind-mounted in, and executes `dotnet test --no-build` **inside** the container — where the local `nvcc`/`hipcc`/`ocloc` is on PATH. The test framework's `CompilerManagerFactory` falls back to `LocalCompilerManager`, which finds the compiler at the path `Src/ILGPUC.Compilers/CompilerOptions.cs` expects. No HTTP, no probe, no service tier active in CI — the same images that serve as a remote builder for dev also serve as test runners for CI. See `plans/ci_redesign.md` for the design rationale.

## Critical Pitfalls

**Always pass `--blame-hang-timeout 360s` when running tests** — compiler tests can produce infinite loops (e.g. in optimization passes), which will hang the test runner indefinitely. This flag kills and reports the offending test after 6 minutes. Never run `dotnet test` without it.


**`InlineList<T>` is a struct** (`ILGPU/Util/InlineList.cs`) — never mark as `readonly` field if you mutate it. `readonly` causes defensive copies and silent data loss.

**BasicBlock value iteration:**
- `block.Values` — starts at `FirstValue`, does NOT include phi values
- `block.PhiValues` — only phi values
- `block.BasicBlockValues` / `foreach (var v in block)` — includes both phis and regular values

**`PhiValue` layout:** `_values = [arg0, arg1, ..., src0, src1, ...]`. `Count = 2 * NumArguments`. Use `NumArguments` for the number of SSA arguments, not `Count`.

**`MethodCall` layout:** `_values[0]` is the target method, `[1..]` are arguments. Use `call.Arguments` not `call.Values` for argument iteration.

**`Module.GetPrimitiveType` broken with readonly struct** — `Module._basicValueTypes` is a readonly struct, calling methods creates defensive copies. Use `transform.GetPrimitiveType()` inside transforms instead.

**After editing files, `touch` them before `dotnet build`** — the Edit tool doesn't update mtime, so MSBuild may skip recompilation.

## Commit Messages

- One-line summary of the main contribution, past tense (e.g. "Added GPU execution tests after the compile-only stage.", "Bumped requests from 2.32.0 to 2.33.0."). Match the style of existing commits in `git log`.
- Commits with Claude involvement must be marked with a trailer:

  ```
  Co-Authored-By: Claude <noreply@anthropic.com>
  ```

## Code Style

- 4-space indentation, CRLF line endings
- No `ImplicitUsings` in ILGPUC — all files need explicit `using` statements
- `Backend` class is `internal` — public APIs cannot expose it
- `.editorconfig` enforces naming conventions (PascalCase, `I` prefix for interfaces)
