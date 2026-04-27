# ILGPUC.Tests — Test Suite

## Overview

The ILGPUC test suite verifies the AOT compiler across three layers, from IR correctness
through backend code generation to end-to-end execution. Each layer catches different
classes of bugs:

1. **IR Snapshot Tests** — verify the compiler's internal representation at key pipeline stages
2. **Backend Code Generation Tests** — verify every kernel produces valid source on every backend
3. **Execution Tests** — build and run standalone programs through the full ILGPUC pipeline, verify stdout

All tests use xUnit and live in `ILGPUC.Tests/`.

---

## Directory Structure

```
ILGPUC.Tests/
├── Framework/              # Shared test infrastructure
│   ├── CompilationHelper.cs      # Wraps ILGPUC pipeline (IL → IR → optimize → codegen)
│   ├── CompilationTestBase.cs    # Base for IR snapshot tests
│   ├── BackendTestBase.cs        # Base for backend code generation tests
│   ├── IRSnapshotVerifier.cs     # Snapshot comparison + update mode
│   ├── KernelRegistry.cs         # Auto-discovers all kernel methods via reflection
│   ├── ExecutionTestBase.cs      # Base for execution tests
│   ├── ProgramBuilder.cs         # Full pipeline: source → ILGPUC → executable
│   ├── RoslynCompiler.cs         # In-process Roslyn compilation (.cs → .dll)
│   ├── ProcessRunner.cs          # Runs executable as separate process with timeout
│   ├── OutputVerifier.cs         # Compares stdout lines to expected values
│   ├── FloatTolerance.cs         # Backend-specific float comparison precision
│   ├── Availability.cs           # Checks native compiler availability per backend
│   ├── TestTypes.cs              # Shared struct/enum types used by kernels
│   └── TestData.cs               # Test data generators
│
├── Kernels/                # Reference kernel definitions (28 files)
│   ├── BasicIfKernels.cs
│   ├── BasicLoopKernels.cs
│   ├── BinaryIntOpKernels.cs     # Generic: expanded with int, long
│   ├── UnaryIntOpKernels.cs      # Generic: expanded with int, long
│   ├── CompareIntKernels.cs      # Generic: expanded with int, long
│   ├── CompareFloatKernels.cs    # Generic: expanded with float, double
│   └── ...
│
├── IRTests/                # Layer 1: IR snapshot tests (28 files)
├── BackendTests/           # Layer 2: Backend code generation (5 files)
│   ├── CpuBackendTests.cs
│   ├── CudaBackendTests.cs
│   ├── MetalBackendTests.cs
│   ├── ROCmBackendTests.cs
│   └── OpenCLBackendTests.cs
│
├── ExecutionTests/         # Layer 3: Execution tests (26 files)
│   ├── BasicIfExecutionTests.cs
│   ├── BasicLoopExecutionTests.cs
│   └── ...
│
├── TestPrograms/           # Standalone .cs programs for execution tests (38 files)
│   ├── BasicIf/
│   │   ├── IfTrue.cs
│   │   ├── IfFalse.cs
│   │   └── IfSideEffects.cs
│   ├── BasicLoop/
│   │   ├── WhileLoop.cs
│   │   └── ForLoop.cs
│   └── ...
│
├── NonKernelTests/         # Tests for non-kernel compiler components
├── Snapshots/              # IR snapshot reference files (.il)
│   ├── AfterFrontend/
│   ├── AfterGlobalOpt/
│   └── AfterBackendTransforms/
│
└── ILGPUC.Tests.csproj
```

---

## Layer 1: IR Snapshot Tests

**Purpose:** Verify the normalized IR text at three pipeline stages.

**Pipeline stages:**
- `AfterFrontend` — raw IR from IL disassembly
- `AfterGlobalOpt` — after optimization passes
- `AfterBackendTransforms` — after backend-specific lowering

**How it works:**
1. `CompilationHelper` takes a kernel `MethodInfo` and runs the ILGPUC pipeline
2. At each dump point, `Module.Dump()` produces normalized IR text
3. `IRSnapshotVerifier` compares the text to a reference `.il` file in `Snapshots/`

**Snapshot storage:**
Reference files live in the source tree under `Snapshots/` and are copied to the build
output at build time (via `<None CopyToOutputDirectory="PreserveNewest" />` in the `.csproj`).

```
Snapshots/
├── AfterFrontend/
│   └── BasicIfKernels.IfTrueKernel.il
├── AfterGlobalOpt/
│   └── BasicIfKernels.IfTrueKernel.O1.il
└── AfterBackendTransforms/
    ├── CPU/
    │   └── BasicIfKernels.IfTrueKernel.O1.il
    ├── Cuda/
    ├── Metal/
    └── ...
```

**Missing snapshots:** If no reference `.il` file exists for a test, the test **fails** with:

> Snapshot file not found: `AfterFrontend/BasicIfKernels.IfTrueKernel.il`.
> Run with ILGPU_UPDATE_IR=1 to generate snapshots.

**Generate-review-verify workflow:**

```bash
# 1. Generate a snapshot for one specific test
ILGPU_UPDATE_IR=1 dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj \
  --filter "BasicIfIRTests.IfTrueKernel_AfterFrontend"

# 2. Review the generated IR
cat ILGPUC.Tests/Snapshots/AfterFrontend/BasicIfKernels.IfTrueKernel.il

# 3. Run the test without the env var to confirm it passes
dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj \
  --filter "BasicIfIRTests.IfTrueKernel_AfterFrontend"

# 4. Commit the snapshot once you're happy with it
```

**Generate all snapshots at once:**
```bash
ILGPU_UPDATE_IR=1 dotnet test --filter IRTests
```

Then review with `git diff` before committing — since the `.il` files are plain text,
every IR change is visible in your normal diff workflow.

**Re-generating after compiler changes:** If the compiler's IR output changes (e.g., new
optimization pass, renamed instructions), regenerate all snapshots and review the diff
to confirm the changes are intentional:
```bash
ILGPU_UPDATE_IR=1 dotnet test --filter IRTests
git diff ILGPUC.Tests/Snapshots/
```

---

## Layer 2: Backend Code Generation Tests

**Purpose:** Verify that every kernel produces valid source code on every backend.

**How it works:**
- `KernelRegistry` auto-discovers all public static methods from `Kernels/*.cs` via reflection
- Generic kernels (e.g., `BinaryIntOpKernels.AddKernel<T>`) are expanded with representative
  type arguments (int/long for integer ops, float/double for float ops)
- Each backend test file uses `[Theory] + [MemberData]` to test all kernels:

```csharp
public sealed class CpuBackendTests : BackendTestBase
{
    public CpuBackendTests(ITestOutputHelper output) : base(output, BackendType.CPU) { }

    [Theory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public void SourceGeneration(string kernelName) =>
        AssertSourceGenerationSucceeds(KernelRegistry.Resolve(kernelName));
}
```

**Coverage:** ~120 kernels × 5 backends = ~600 source generation tests.

**Assertions:**
- Source code string is non-empty
- Entry point name is non-empty

**Backends tested:** CPU, Cuda, Metal, ROCm, OpenCL.

---

## Layer 3: Execution Tests

**Purpose:** End-to-end verification that compiled kernels produce correct output.

### Compilation Flow

Each execution test drives this pipeline:

```
TestPrograms/BasicIf/IfTrue.cs              ① Test program source
         │
    Roslyn compile #1                        ② → temp/input.dll (library)
         │
    ILGPUC pipeline per kernel:              ③ ILFrontend → IR → optimize
         │                                      → Backend.GenerateCode
         │                                      → CompiledKernelGenerator → wrapper .cs
         │                                      → KernelRegistrarGenerator → registrar .cs
         │
    Roslyn compile #2                        ④ source + wrappers + registrar → program.dll
         │
    dotnet program.dll                       ⑤ Run as separate process (30s timeout)
         │
    OutputVerifier.Verify(stdout, expected)  ⑥ Line-by-line comparison
```

### Test Program Pattern

Each test program is a self-contained `.cs` file in `TestPrograms/`:

```csharp
// TestPrograms/BasicIf/IfTrue.cs
using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IfTrueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = true ? 42 : 23;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);

        var kernel = accelerator.LoadAutoGroupedStreamKernel
            <Index1D, ArrayView1D<int, Stride1D.Dense>>(
            Kernels.IfTrueKernel);

        using var buffer = accelerator.Allocate1D<int>(4);
        kernel(4, buffer.View);
        accelerator.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
```

Key conventions:
- Kernel methods go in `static class Kernels`
- `Main()` goes in `static class Program`
- Output one value per line via `Console.WriteLine()`
- Use `preferCPU: true` to ensure CPU accelerator selection
- Standard buffer size is 4 elements

### Execution Test Class Pattern

```csharp
public sealed class BasicIfExecutionTests : ExecutionTestBase
{
    public BasicIfExecutionTests(ITestOutputHelper output)
        : base(output, BackendType.CPU) { }

    [Fact]
    public async Task IfTrue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/IfTrue.cs",      // path relative to output dir
            ["Kernels.IfTrueKernel"],               // fully-qualified kernel method(s)
            ["42", "42", "42", "42"]);              // expected stdout lines
    }
}
```

**Process isolation:** Test programs run as separate `dotnet` processes. A segfault or
infinite loop in the compiled kernel cannot crash the test host — the process is killed
after 30 seconds and the test reports a timeout failure.

### Execution Test Coverage

| Test Class | Tests | Kernel Area |
|---|---|---|
| BasicIfExecutionTests | 3 | If/else branching |
| BasicLoopExecutionTests | 2 | While/for loops |
| BasicCallExecutionTests | 1 | Method calls |
| BasicSwitchExecutionTests | 1 | Switch statements |
| BasicJumpExecutionTests | 2 | Goto/jump |
| BasicPhiExecutionTests | 2 | Phi nodes (value merging from branches) |
| BasicMovementExecutionTests | 1 | Data copy between views |
| ArrayViewExecutionTests | 2 | Load/store, length |
| ArrayExecutionTests | 1 | Local array access |
| ConvertExecutionTests | 4 | Truncate, promote, float-to-int, int-to-float |
| StructureExecutionTests | 1 | Struct field access |
| EnumExecutionTests | 1 | Integer enums |
| BinaryIntOpExecutionTests | 2 | Add, multiply |
| UnaryIntOpExecutionTests | 2 | Negate, bitwise NOT |
| CompareExecutionTests | 2 | Integer and float comparisons |
| AtomicExecutionTests | 1 | Atomic add |
| MemoryBufferExecutionTests | 2 | Buffer copy, scale |
| ReinterpretCastExecutionTests | 1 | Float-to-UInt32 reinterpret |
| ValueTupleExecutionTests | 1 | Tuple create |
| FixedBufferExecutionTests | 1 | Fixed buffer write |
| DebugExecutionTests | 1 | Debug.Assert |
| KernelEntryPointExecutionTests | 1 | Index1D entry point |
| GridExecutionTests | 1 | Grid.Dimension intrinsic |
| GroupExecutionTests | 1 | Group.Barrier intrinsic |
| WarpExecutionTests | 1 | Warp.Barrier intrinsic |
| SharedMemoryExecutionTests | 1 | Shared memory allocation |

**Total: 26 test classes, 39 test methods, 38 test programs.**

---

## Running Tests

```bash
# All tests
dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj

# By layer
dotnet test --filter IRTests
dotnet test --filter BackendTests
dotnet test --filter ExecutionTests
dotnet test --filter NonKernelTests

# Single test class
dotnet test --filter BasicIfExecutionTests

# Single test method
dotnet test --filter "BasicIfExecutionTests.IfTrue_ProducesCorrectOutput"

# Single backend
dotnet test --filter CpuBackendTests

# Update IR snapshots
ILGPU_UPDATE_IR=1 dotnet test --filter IRTests
```

---

## Adding a New Execution Test

### Step 1: Write the test program

Create a `.cs` file in `TestPrograms/<Category>/<Name>.cs`:

```csharp
// TestPrograms/MyFeature/MyTest.cs
using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void MyKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int arg)
    {
        data[index] = arg * (index.X + 1);
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);

        var kernel = accelerator.LoadAutoGroupedStreamKernel
            <Index1D, ArrayView1D<int, Stride1D.Dense>, int>(
            Kernels.MyKernel);

        using var buffer = accelerator.Allocate1D<int>(4);
        kernel(4, buffer.View, 10);
        accelerator.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
```

The `.csproj` automatically picks up new files under `TestPrograms/` — they are excluded
from compilation (`<Compile Remove>`) and copied to the output directory (`<None CopyToOutputDirectory>`).

### Step 2: Write the execution test

Create or extend a test class in `ExecutionTests/`:

```csharp
// ExecutionTests/MyFeatureExecutionTests.cs
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public sealed class MyFeatureExecutionTests : ExecutionTestBase
{
    public MyFeatureExecutionTests(ITestOutputHelper output)
        : base(output, BackendType.CPU) { }

    [Fact]
    public async Task MyTest_ProducesCorrectOutput()
    {
        // arg=10: data[i] = 10 * (i+1) → [10, 20, 30, 40]
        await VerifyProgramOutputAsync(
            "TestPrograms/MyFeature/MyTest.cs",
            ["Kernels.MyKernel"],
            ["10", "20", "30", "40"]);
    }
}
```

### Step 3: Verify

```bash
dotnet test --filter MyFeatureExecutionTests
```

---

## Adding a New Kernel (for Backend Tests)

### Step 1: Add the kernel

Create or extend a file in `Kernels/`:

```csharp
// Kernels/MyFeatureKernels.cs
namespace ILGPUC.Tests.Kernels;

static class MyFeatureKernels
{
    public static void SomeKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X * 2;
    }
}
```

The class name must end with `Kernels` and be `static` — `KernelRegistry` auto-discovers
all public static methods in such classes. No manual registration needed.

### Step 2: For generic kernels

If the kernel is generic, add the class name to `KernelRegistry.GenericExpansion`:

```csharp
// In Framework/KernelRegistry.cs
private static readonly Dictionary<string, Type[]> GenericExpansion = new()
{
    ["BinaryIntOpKernels"] = IntegerTypeArgs,
    ["MyFeatureKernels"] = IntegerTypeArgs,  // add this
};
```

### Step 3: Verify

```bash
dotnet test --filter "CpuBackendTests.SourceGeneration"
```

The new kernel will automatically appear in all 5 backend test runs.

---

## Adding a New IR Snapshot Test

Add a test method in the appropriate `IRTests/` file:

```csharp
[Fact]
public void MyKernel_AfterFrontend()
{
    var kernel = GetKernel(typeof(MyFeatureKernels), nameof(MyFeatureKernels.SomeKernel));
    var ir = _helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);
    IRSnapshotVerifier.VerifyOrUpdate(ir,
        IRSnapshotVerifier.GetSnapshotPath(
            "MyFeatureKernels", "SomeKernel", IRDumpPoint.AfterFrontend));
}
```

Generate the initial snapshot:
```bash
ILGPU_UPDATE_IR=1 dotnet test --filter "MyFeatureIRTests.MyKernel_AfterFrontend"
```

---

## Framework Classes

### `ExecutionTestBase`

Base class for execution tests. Wraps `ProgramBuilder` + `ProcessRunner` + `OutputVerifier`.

Key methods:
- `VerifyProgramOutputAsync(source, kernels, expected)` — build, run, verify exact match
- `VerifyProgramOutputWithToleranceAsync(...)` — same but with float tolerance for GPU
- `RunProgramAsync(source, kernels)` — build and run, return `ProcessResult` for custom assertions

### `ProgramBuilder`

Orchestrates the full pipeline from `.cs` source to runnable `.dll`:
1. Roslyn compiles source into a library DLL
2. Loads the DLL to resolve kernel `MethodInfo`s
3. For each kernel: ILFrontend → ModuleBuilder → optimize → Backend.GenerateCode → CompiledKernelGenerator
4. Generates `KernelRegistrar` source
5. Roslyn compiles source + wrappers + registrar into a console DLL
6. Generates `.runtimeconfig.json`

### `ProcessRunner`

Runs a `.dll` via `dotnet` as a separate process:
- Captures stdout and stderr
- 30-second timeout (configurable)
- Kills entire process tree on timeout
- Returns `ProcessResult(ExitCode, StdOutLines, StdErr, TimedOut)`

### `OutputVerifier`

Two verification modes:
- `Verify()` — exact string match per line
- `VerifyWithTolerance()` — lines that parse as floats use backend-specific precision via `FloatTolerance`

### `KernelRegistry`

Auto-discovers kernels at test startup:
- Scans all `static` classes ending with `Kernels` in the `ILGPUC.Tests.Kernels` namespace
- Expands generic methods with type arguments from `GenericExpansion` dictionary
- Exposes `AllKernelNames` for xUnit `[MemberData]` (string-based for serialization)
- `Resolve(name)` returns the concrete `MethodInfo`

### `RoslynCompiler`

In-process C# compilation via `Microsoft.CodeAnalysis.CSharp`:
- `CompileToLibrary()` — produces a class library DLL
- `CompileToExecutable()` — produces a console application DLL
- References .NET BCL + ILGPU automatically
- Throws `InvalidOperationException` with diagnostics on failure

---

## Notes

- **Test programs are excluded from compilation.** The `.csproj` has
  `<Compile Remove="TestPrograms\**\*.cs" />` — they are only copied to the output directory
  as content files. ProgramBuilder reads and compiles them via Roslyn at test runtime.

- **Process isolation.** Execution tests run compiled programs as separate processes.
  A crash (segfault, stack overflow) or infinite loop in the compiled kernel does not
  affect the test host — the process is killed after timeout and the test reports failure.

- **GPU backend execution tests** can use `VerifyProgramOutputWithToleranceAsync()` for
  floating-point output where precision differs across backends.

- **Grid/Group/Warp/SharedMemory tests** have output that depends on the CPU runtime's
  threading model. Expected values may need adjustment if the runtime's grouping strategy
  changes.
