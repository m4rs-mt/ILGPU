# Fix B.2 — Cross-type struct assignment (CUDA / ROCm / OpenCL native compile)

## Status

**RESOLVED on CUDA; ROCm and OpenCL delegated to CI.** Phases 1, 3, and 4 of
the original plan have landed in this branch; Phase 2 was a contingency for
per-backend failures that didn't materialise on the verified path. See
`fix_samples.md` "B.2 fix detail" for the canonical write-up.

## Original empirical finding

During investigation for this plan, I removed the `[KnownFailingOn]`
attributes from both kernels currently gated for B.2 and ran
`BackendTests.NativeCompilation` against the local Cuda Docker compiler
service (`ghcr.io/m4rs-mt/ilgpuc-compiler-service-cuda` on port 5001). Both
tests **passed**:

| Test | Backend | Result |
|---|---|---|
| `InterleaveFieldsKernels.InterleaveFieldsKernel` | Cuda | ✅ Passed |
| `MatrixMultiplyKernels.MatrixMultiplyTiledKernel` | Cuda | ✅ Passed |

The most likely cause of the resolution is the `EmitGetField` multi-field span
fix in `aa5598c0` (`Emit struct literal for multi-field GetField span`) — the
prior emitter produced a single-field access for `Span > 1` GetFields, which
landed an int into a multi-int struct slot. `aa5598c0` rewrites those to a
struct literal copying each constituent flat field, which both type-checks
under nvcc and gives the optimizer a coherent IR. The `LauncherStubGenerator`
flattening fix in `1d5207eb` also touches the same family (parameter struct
flattening must propagate consistently to the marshaled struct), so either or
both could be doing the work.

ROCm and OpenCL Docker services are running locally but report the underlying
toolchains (`hipcc`, `ocloc`) as unavailable on this machine, so the matching
verification couldn't run during planning. **Pending toolchain availability,
B.2 is plausibly fully resolved across the three GPU backends — the plan
below is ordered around proving that, not around shipping a new fix.**

## What the original issue described

Per `Src/plans/fix_samples.md` family B.2:

```
input.cu(37-40): error : no operator "=" matches these operands
```

The plan attributed it to "struct-field assignments where the LHS and RHS have
non-identical struct types … most likely … a struct shape that's reinterpreted
across an interleaving / SoA-AoS transform without the corresponding type
annotations being carried through to the source emitter."

That hypothesis lines up with the multi-field GetField bug `aa5598c0` fixed:
the IR produced a `GetField` with `FieldSpan.Span > 1` (extracting a sub-struct
from a flattened parent), the emitter dereferenced one primitive slot, and the
resulting expression had a primitive type while the destination variable was
declared with the sub-struct type. nvcc / hipcc / clang would all reject that
as `no operator = matches` between the int value and the struct slot — same
shape, same root cause.

## Inspecting the current generated source

A snapshot of the CUDA source ILGPUC currently emits for `InterleaveFieldsKernel`
(captured via `dotnet test --filter '~CudaBackendTests~InterleaveFields~SourceGeneration_Release'`
with verbose console output) shows clean pointer-arithmetic stores, no struct
copies:

```c
struct struct_X { int Field0; int Field1; int Field2; int Field3; };  // IntInline4
struct struct_P { int Field0; ... int Field7; };                       // InterleavedPoint4
struct struct_V { struct_P* Field0; long long Field1; long long Field2; char Field3; };  // ArrayView

__global__ void method_InterleaveFieldsKernel(int idx, struct_V data) {
  *(int*)(struct_X*)(data.Field0 + idx) = idx;
  *((int*)(struct_X*)(data.Field0 + idx) + 1) = idx + 1;
  // …six more writes, one per field …
  *(int*)((struct_X*)(data.Field0 + idx) + 4) = idx + 4;       // .Y[0]
  *((int*)((struct_X*)(data.Field0 + idx) + 4) + 1) = idx + 5; // .Y[1]
}
```

The Y-channel writes use `(struct_X*)(...) + 4` (advance four `IntInline4`
strides past the start of the parent), then cast to `int*` and offset within.
This compiles cleanly under nvcc 13.x; whether it's *correct* (i.e. lands in
the Y field, not 64 bytes past the parent) is a separate question that the
sample-level test in `Samples/InterleaveFields` already verifies.

## Plan

### Phase 1 — verify on ROCm and OpenCL

Bring the docker compiler services up with their native toolchains and re-run
the same probe used for Cuda:

```bash
Src/docker/run.sh --pull           # pull cuda + rocm + opencl images
# ROCm requires Rosetta on Apple Silicon; CUDA runs native arm64
ILGPU_CUDA_SERVICE_URL=http://localhost:5001 \
ILGPU_ROCM_SERVICE_URL=http://localhost:5002 \
ILGPU_OPENCL_SERVICE_URL=http://localhost:5003 \
  dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj --no-build \
  --filter "FullyQualifiedName~BackendTests&DisplayName~InterleaveFields&DisplayName~NativeCompilation" \
  --blame-hang-timeout 360s
```

Repeat with the `KnownFailingOn` attribute temporarily commented out so the
runner attempts native compile on each backend. There are exactly two attribute
sites to flip:

- `Src/ILGPUC.Tests/Kernels/InterleaveFieldsKernels.cs:52`
- `Src/ILGPUC.Tests/Kernels/MatrixMultiplyKernels.cs:43`

Possible outcomes, with the corresponding next step:

1. **All three backends green.** Skip to Phase 3.
2. **ROCm or OpenCL fail with a different error than the Cuda case.** The error
   message tells you exactly which family — see Phase 2.
3. **CUDA regresses (unlikely, but possible if the Docker image is updated
   between runs).** Capture the actual nvcc output and compare against the
   known-good source above; this would be a different bug than B.2.

### Phase 2 — address per-backend deltas (only if Phase 1 surfaces failures)

The most likely sources of per-backend asymmetry, in decreasing order of
probability:

1. **OpenCL address-space strictness.** OpenCL C requires explicit `__global` /
   `__local` / `__private` qualifiers on pointers; CUDA C++ doesn't.
   `ExpressionEmitter.EmitPointerCast` already handles "empty-keyword" backends
   (CUDA returns `""` for global) but the cast-around-arithmetic shape used in
   the InterleaveFields kernel may need explicit address-space prefixing on
   OpenCL. Check `EmitLoadElementAddress` / `EmitLoadFieldAddress` for the
   cast-target type and ensure the address-space qualifier rides through.

2. **ROCm / Hipcc lambda / template differences.** hipcc is clang-based and
   stricter about implicit conversions than nvcc. If the failure is "cannot
   convert int to struct_X" it's the same bug shape `aa5598c0` already fixed —
   look for any other `Emit*` site that emits `dest = src` where the IR Value
   types don't match. The two main suspects:
   - `MethodEmitter.EmitVariableAssignment` (line 380) — a strict line-by-line
     `var = expr` write; the type comes from `analysis.GetTypeName(value, …)`.
     If `expr` is a primitive and `value.Type` is a StructureType, you're back
     in B.2 territory but for a different IR Value class than GetField.
   - `EmitStructure` for vectorized lanes — currently emits per-lane object
     initializers; if any field source is a wider type than the destination
     slot, hipcc rejects.

3. **OpenCL int64 atomics.** Already a known limitation tracked separately
   (`AdvancedAtomics` plan); not B.2. If the failure mentions `cl_khr_int64_*`
   or `Atomic CAS not supported for type Int64 in OpenCL`, leave alone — it's
   B.1 / B.3 territory.

For each failure, capture the diagnostic with `--logger
"console;verbosity=detailed"` and dump the generated source (already in test
stdout). Compare the offending line against the corresponding CUDA emission;
the diff usually points to one of the three families above.

### Phase 3 — close out

When all three GPU backends compile both kernels cleanly:

1. **Drop the `[KnownFailingOn]` attributes** at the two sites. Both kernels
   become required-green on `BackendTests.NativeCompilation`.
2. **Delete `Src/ILGPUC.Tests/KnownIssues/InterleaveFieldsKnownFailureTests.cs`** —
   it's the negative test that asserted the gate; with the gate gone and the
   real test green, it has no purpose.
3. **Update `Src/plans/fix_samples.md`**:
   - Move the B.2 row from "Open" to "Fully fixed", with the resolving commit
     hash (`aa5598c0` is the leading candidate; bisect if you want certainty).
   - Tighten `checks-completed` in `.github/workflows/ci.yml` to require the
     `build-samples-{cuda,rocm,opencl}` jobs once they're all green for the
     two affected samples.
4. **Verify `Samples/MatrixMultiply` and `Samples/InterleaveFields` build with
   `-p:ILGPUCompile=true`** against the Docker services for each backend, e.g.:
   ```bash
   dotnet build Samples/MatrixMultiply -c Debug \
     -p:ILGPUBackend=Cuda -p:ILGPUCompile=true \
     '-p:ILGPUCompilerServices="http://localhost:5001"'
   ```
   These are the user-visible samples; passing them is the closure criterion.

### Phase 4 — guard against regression

The two new gates that protect against B.2 reappearing:

1. **`LauncherFieldAccessorFlatteningTests`** (added in `1d5207eb`) — pins the
   accessor flattening contract that the launcher relies on.
2. **`ArrayView2DExecutionTests` / `ArrayView3DExecutionTests`** (added in
   `1d5207eb` + `aa5598c0`) — exercise the multi-field GetField path that B.2
   was a downstream symptom of.

Neither directly compiles to native binaries, but both fail loudly the moment
the IR shape regresses. Run them in CI alongside the GPU compile-only jobs.
Add a one-line note pointing to this file in
`Src/ILGPUC.Tests/Kernels/InterleaveFieldsKernels.cs` once Phase 3 lands so
future readers can find the trail.

## Files touched (predicted)

If Phase 1 confirms green across all three backends, the closing PR is:

- `Src/ILGPUC.Tests/Kernels/InterleaveFieldsKernels.cs` — drop attribute,
  optionally rewrite the docstring.
- `Src/ILGPUC.Tests/Kernels/MatrixMultiplyKernels.cs` — drop attribute, drop
  the "B.2" note in the docstring above the tiled kernel.
- `Src/ILGPUC.Tests/KnownIssues/InterleaveFieldsKnownFailureTests.cs` — delete.
- `Src/plans/fix_samples.md` — update the table, add a B.2 fix-detail
  paragraph similar to B.1's.
- `.github/workflows/ci.yml` — flip `build-samples-{cuda,rocm,opencl}` from
  advisory to required.

If Phase 1 surfaces a real failure on ROCm or OpenCL, scope expands to
whichever emitter site matches the diagnostic — most likely
`Src/ILGPUC/Backends/ExpressionEmitter.cs`, possibly per-backend
`*LanguageConfiguration.cs` for address-space qualifiers.

## Effort estimate

- Phase 1: ~30 min (start docker stack, run two filtered tests).
- Phase 2: 0 if Phase 1 is clean; otherwise scoped per-failure, typically a
  day per backend-emit family.
- Phase 3: ~1 hour (paperwork — delete a file, drop two attributes, write the
  fix-detail paragraph).
- Phase 4: covered by the existing test suite; no extra work.
