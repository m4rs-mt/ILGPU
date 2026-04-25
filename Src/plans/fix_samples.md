# Sample Compatibility — Findings and Fix Plan

## Context

The `build-samples` CI job historically built the ILGPU sample suite only
with the default CPU backend, which is the most permissive code path
(IR → C# wrapper, no native compile, no GPU runtime). Three things
exposed gaps in cross-backend sample portability over the past iteration:

1. **Tiled-MatrixMultiply restoration** turned up frontend SSA-construction
   and CPU-launcher KernelIndex bugs that were latent because no in-tree
   sample exercised the relevant patterns. Fixed in `temp5`'s recent
   commits (this branch).
2. **Sample-build matrix expansion** (Phase 3 of the remote-service work)
   added per-backend sample-build CI jobs. Running them locally on this
   Mac surfaced two `FlatStructLauncherEmitter` bugs that ship broken C#
   launchers on all three of CUDA / ROCm / OpenCL.
3. **End-to-end `ILGPUCompile=true` validation** against the running
   Docker compiler services (CUDA on 5001, ROCm on 5002, OpenCL on 5003)
   surfaced two more pre-existing IR→source emission bugs that only fire
   when the native toolchain (nvcc / hipcc / ocloc) actually runs.

Net: across CUDA, ROCm, OpenCL — three different backends, two different
remote services per backend — **30 of the 34 samples build cleanly all
the way through native binary embedding**. The four failures are
identical on every backend and split into two distinct bug families.

## Failure inventory

### Family A — `FlatStructLauncherEmitter` (two samples)

The C# launcher emission for kernels with non-trivial parameter shapes
produces a wrapper file that doesn't compile. Visible at source-emit
time (no `--compile`, no service required) — these are pure CLI codegen
bugs. Affects all backends that go through `FlatStructLauncherEmitter`:
**CUDA, ROCm, OpenCL**. Unaffected: CPU (`CPULauncherEmitter`), Metal
(`MetalLauncherEmitter`).

#### A.1 `AdvancedViews` — `error CS0246: The type or namespace name 'struct_613' could not be found`

The kernel takes an `ArrayView1D<ComposedStructure, Stride1D.Dense>`
where `ComposedStructure` is a user-defined struct. The emitted launcher
contains:

```csharp
private unsafe struct KernelArgs
{
    public ViewImplementation<int> param_elements;
    public ViewImplementation<struct_613> param_view;   // ← undefined
    public int param_comparisonValue;
}
```

`struct_613` is the IR-flattened name for `ComposedStructure`. The IR
backend emits it inside the kernel-side source string, but the C# wrapper
never declares an equivalent. The wrapper either needs to (a) use the
user-facing `ComposedStructure` type directly (it has the same memory
layout under `[StructLayout(LayoutKind.Sequential)]`) or (b) emit a
matching `internal struct struct_613` declaration alongside `KernelArgs`.

Critical files:
- `Src/ILGPUC/Backends/FlatStructLauncherEmitter.cs` — emits `KernelArgs`
- `Src/ILGPUC/Backends/CompiledKernelGenerator.cs` — owns the launcher
  emission pipeline; already has the user-facing `LaunchTypeName` per
  parameter (`ParameterInfo.LaunchTypeName`)
- `Src/ILGPUC/Backends/CodeGenerator.cs` — `ParameterInfo` definition

Recommended fix: the existing `EmitStructMarshalingCode` callback knows
the user-facing type; same callback can produce the field type for the
`KernelArgs` struct. For struct-element views, emit
`ViewImplementation<UserType>` and let `[StructLayout(Sequential)]` plus
`Unsafe.As<>` carry the layout across — exactly the pattern already used
in `CPULauncherEmitter.EmitStructMarshal:228-253` for the same case.

#### A.2 `GenericKernel` — `error CS0030: Cannot convert type 'GenericKernel.LambdaClosure' to 'long'`

The kernel takes a lambda parameter (a delegate) which compiles down to
a closure object. The emitted launcher contains:

```csharp
args.param_function = (long)param_function;   // LambdaClosure → long
```

The launcher is treating the closure as a scalar and casting it to its
flat-IR type (`long`, since closures lower to a pointer-shaped IR
value). What it should do is flatten the closure's captured fields the
same way struct-with-views parameters are handled — or refuse closure
parameters at the boundary with a clear diagnostic if cross-machine
closure marshaling is out of scope.

Critical files:
- `Src/ILGPUC/Backends/FlatStructLauncherEmitter.cs` — same emitter
- `Src/ILGPUC/Backends/CodeGenerator.cs` — `AnalyzeParameter` classifies
  parameter shapes; closure parameters likely fall through to
  `ParameterKind.StructPlain` and get the wrong codegen
- `Src/ILGPUC/IR/Transformations/ClosureElimination.cs` — already
  decomposes closure allocations during global opt; investigate why
  the kernel-entry parameter survives this transform

Recommended fix: `ClosureElimination` already runs in the global
optimizer pipeline (Optimizer.cs:183). Either (a) extend it to
flatten closure entry-parameters into their captured fields, or (b)
add a `ParameterKind.Closure` and a per-backend marshaling path for it.
(a) is the cleaner answer — by the time the launcher emits, the
parameter list should already be primitives + views.

### Family B — IR → native source emission (two samples)

The IR-to-CUDA / IR-to-HIP / IR-to-OpenCL source emitters produce a
source string that compiles to invalid C++/OpenCL-C and gets rejected
by the native toolchain. **Only visible with `ILGPUCompile=true`** —
without that, the broken source string is embedded as a string constant
and the runtime JIT would fail at launch time instead.

#### B.1 `AdvancedAtomics` — `double *` assigned to `long long *`

```
input.cu(20): error : a value of type "double *" cannot be assigned to
                      an entity of type "long long *"
input.cu(23): error : identifier "tmp_5" is undefined
```

Float64 atomic intrinsic emission. Atomics on `double` lower in IR to
the same shape as `long long` atomics (because CUDA's atomic primitives
operate on bit-equivalent integer types), but the emitter loses the
type info on the pointer cast. The undefined `tmp_5` is a downstream
effect of the type mismatch breaking SSA name resolution.

Critical files:
- `Src/ILGPUC/Backends/CUDA/CUDAIntrinsicEmitter.cs` — atomic intrinsics
- `Src/ILGPUC/Backends/ROCm/ROCmIntrinsicEmitter.cs` — same family
- `Src/ILGPUC/Backends/OpenCL/CLIntrinsicEmitter.cs` — same family
- `Src/ILGPUC/Backends/ExpressionEmitter.cs` — pointer cast formatting
- `Src/ILGPUC/IR/PureValues/Atomics.cs` (or wherever `Atomic.Add<double>`
  lowers) — the IR shape of double atomics

Recommended fix: emit the bitwise reinterpret cast that CUDA expects
(`(unsigned long long*)addr`) as part of the atomic emitter, and keep
the expression-emitter's pointer arithmetic shape untouched. Equivalent
fixes apply per-backend.

#### B.2 `InterleaveFields` — `no operator "=" matches these operands`

```
input.cu(37-40): error : no operator "=" matches these operands
```

Struct-field assignments where the LHS and RHS have non-identical
struct types. Most likely this is a struct shape that's reinterpreted
across an interleaving / SoA-AoS transform without the corresponding
type annotations being carried through to the source emitter.

Critical files:
- `Src/ILGPUC/Backends/ExpressionEmitter.cs` — produces assignments
- `Src/ILGPUC/IR/Transformations/Vectorization/` (if interleave runs
  there) — investigate which pass produces the cross-type assignment
- `Src/ILGPUC/Backends/CodeGenerator.cs` — type-name resolution that
  the assignment lhs/rhs eventually reach

Recommended fix: needs investigation of which IR transform produces the
cross-type struct values. Likely an interleave transform that doesn't
emit a proper bitcast / reinterpret around the assignment.

## Improvements already shipped (this branch, `temp5`)

The work that exposed all four failures plus a separate Metal
`KernelIndex` issue. Already implemented end-to-end across four phases:

### Phase 0 — Lift the compiler-manager factory to `ILGPUC.Compilers`

- New `Src/ILGPUC.Compilers/CompilerManagerFactory.cs` (lifted from
  `Src/ILGPUC.Tests/Framework/CompilerManagerFactory.cs`, retyped from
  `BackendType` to `CompilationTarget` so the project dependency graph
  works).
- New `Src/ILGPUC.Compilers/CompositeCompilerManager.cs` — backend-typed
  routing-table dispatcher; 5 unit tests in
  `Src/ILGPUC.CompilerService.Tests/CompositeCompilerManagerTests.cs`.
- `Src/ILGPUC/Backends/RemoteCompilerManager.cs` →
  `Src/ILGPUC.Compilers/RemoteCompilerManager.cs`, made `public` so the
  CLI and tests can construct it without a hop through `ILGPUC`.
- Test framework retargeted to the new factory; full
  `ILGPUC.Tests` suite green (5707 passed, 938 skipped per
  the local run, no regressions).

### Phase 1 — CLI multi-URI

- `--compiler-service` is now repeatable on both the `build` and
  `compile` subcommands (`Option<string[]>` + `Uri` parsing in code,
  because System.CommandLine has no default `Uri[]` binder).
- New `ilgpuc probe-services` subcommand walks the same resolution chain
  the build path uses and prints the resolved per-target routing table.
  Validated end-to-end against the running Docker services on
  5001/5002/5003.
- Resolution chain (shared between CLI and tests via
  `CompilerManagerFactory`):
  1. Each `--compiler-service <uri>` URI's `/api/v1/capabilities`.
     First URI to advertise a target wins.
  2. Per-target env vars (`ILGPU_CUDA_SERVICE_URL`,
     `ILGPU_ROCM_SERVICE_URL`, `ILGPU_METAL_SERVICE_URL`,
     `ILGPU_OPENCL_SERVICE_URL`) — trusted, no probe.
  3. Global `ILGPU_COMPILER_SERVICE_URL` — trusted, no probe.
  4. Per-target Docker default ports (5001 / 5002 / 5003) — probed.
  5. Local `CompilerManager` fallback (skippable via `--allow-local
     false` on the probe command).

### Phase 2 — MSBuild surface

- Two new MSBuild properties in `Src/ILGPUC/build/ILGPU.Kernels.targets`:
  - `ILGPUCompile` (bool, default `false`) — passes `--compile` to
    ilgpuc, triggering native-binary embedding instead of source.
  - `ILGPUCompilerServices` (semicolon-delimited URI list) — passes
    one `--compiler-service` per URI to ilgpuc.
- `Samples/Directory.Build.props` documents both env-var and
  property-based invocation styles.

### Phase 3 — CI matrix

- `build-samples` is now a CPU + Metal matrix; per-sample loop with a
  per-backend `excludes` list. Currently excludes `AdvancedAtomics`
  on Metal (Float64 atomics are a Metal limitation, not a regression).
- Three new advisory `build-samples-{cuda,rocm,opencl}` jobs `docker
  run` the existing GHCR images with `ILGPUCompile=true` to validate
  the full source-emit + native-compile path. Intentionally NOT
  required in the `checks-completed` aggregator yet — they will turn
  red on the four pre-existing bugs above and need to stay advisory
  until those land.

### Tiled-MatrixMultiply restoration (separate, on this branch)

- Restored `Samples/MatrixMultiply/Program.cs` tiled variant after
  fixing the underlying frontend / CPU launcher bugs:
  - **CPU backend**: launcher mishandled `KernelIndex` first-parameter
    grouped kernels — `EntryPointParamTypeNames` was off by one because
    `IsIndexStructType` rejected `KernelIndex` (mixed i64/i32 fields).
    Added `IsKernelIndexStructType` helper and a new
    `EntryPointIndexTypeName` field on `CodeGenerationResult`.
    Launcher iterates one thread at a time for KernelIndex (instead of
    `simdWidth`) since the SIMD vectorizer broadcasts the index struct.
  - **Metal backend**: `MetalLanguageConfiguration.EmitIndexComputation`
    treated any struct index as a multi-dim Index2D/3D, mapping
    threadgroup × thread positions onto Field0/Field1. For
    KernelIndex, Field0 must be `threadgroup_position_in_grid.x` and
    Field1 must be `thread_position_in_threadgroup.x`. Added the
    KernelIndex-shape detection and the correct mapping.
  - **CPU backend caveat (documented in the sample)**: shared-memory
    tiles inside grouped kernels still don't synchronize correctly on
    CPU — `Group.Barrier()` is a no-op when the runtime invokes each
    thread serially with a fresh per-call `stackalloc`'d tile. The
    sample skips correctness assertions on CPU and notes the limit.
    True fix is per-group heap-allocated shared memory in the CPU
    runtime, deferred.

## What ships in the next PR (proposed)

Order the four launcher / codegen bugs by ROI:

1. **A.1 `AdvancedViews`** (FlatStructLauncherEmitter — view over user
   struct). Highest leverage: pattern is general (any `ArrayView<T>`
   where `T` is a user struct), single emitter, fix probably <50 lines.
   Picked first by user request.
2. **A.2 `GenericKernel`** (closure as kernel parameter). Same emitter,
   but requires either an IR-pass change (`ClosureElimination` for
   parameters) or a per-backend marshaling change. Larger blast radius.
3. **B.1 `AdvancedAtomics`** (Float64 atomic emission across all three
   backends). Three intrinsic-emitter changes, one IR-side question.
4. **B.2 `InterleaveFields`** (cross-type struct assignment). Requires
   investigation first to identify which transform produces the
   mismatched assignment.

Each fix is its own self-contained PR with a regression sample-build
test added to the matrix. Once all four are green, tighten
`checks-completed` to require the three `build-samples-{cuda,rocm,opencl}`
jobs.

## Verification approach (per fix)

1. Add the failing sample to a per-backend test matrix (`build-samples`
   already covers the CI side — local dev re-runs the `for csproj in
   Samples/*/*.csproj; dotnet build $csproj -p:ILGPUBackend=$BACKEND
   [-p:ILGPUCompile=true]; done` loop).
2. Inspect the actual generated launcher / emitted source under
   `obj/Debug/net10.0/ilgpu/generated/` to see the exact bad output.
3. Fix in the relevant emitter, regenerate, confirm the local sample
   build is green, and confirm the existing 5707-test
   `ILGPUC.Tests` regression suite stays green.
4. Re-run the full sample loop with `ILGPUCompile=true` against the
   docker-running services on 5001/5002/5003 to confirm no other
   sample regressed and that Cuda + ROCm + OpenCL all turn from 30/34
   towards 34/34.
