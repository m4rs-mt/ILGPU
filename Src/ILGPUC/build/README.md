# ILGPU Kernel Build Integration

This directory contains the MSBuild targets file that integrates ILGPUC's Roslyn-based kernel compiler into the standard `dotnet build` pipeline.

## How It Works

When a project imports `ILGPU.Kernels.targets`, the build pipeline gains three additional targets:

1. **ILGPUKernelCompile** — Runs `ilgpuc build` before `CoreCompile`. Scans all `@(Compile)` source files for `stream.Launch()` call sites, compiles the kernels through ILGPUC's pipeline (IL frontend -> IR -> optimizer -> backend codegen), and produces real `CompiledKernel` classes.

2. **ILGPUSwapSources** — Reads the manifest produced by step 1, removes the original source files that were rewritten from `@(Compile)`, and adds the rewritten + generated files. The C# compiler (csc) sees the swapped files instead of the originals.

3. **ILGPUClean** — Removes the `obj/ilgpu/` output directory on `dotnet clean`.

User source files are **never modified on disk**. Rewritten files include `#line` directives so debugging maps back to the original source locations.

## Output Directory Structure

After a build, `$(IntermediateOutputPath)ilgpu/` (typically `obj/Debug/net10.0/ilgpu/`) contains:

```
ilgpu/
  manifest.txt                    # List of original files that were rewritten
  args.rsp                        # Response file passed to ilgpuc
  rewritten/
    Program.cs                    # Rewritten source with CompiledKernel.Launch() calls
    Shaders.cs                    # (one file per rewritten original)
  generated/
    MainKernel_CompiledKernel.cs  # Real compiled kernel classes (per kernel per backend)
    CPUKernelRegistrar.cs         # Kernel registrar (per backend)
```

## Setup

### Option A: NuGet Package (Production)

When ILGPUC is published as a NuGet package, the targets file is automatically imported by any project that references it:

```xml
<PackageReference Include="ILGPUC" Version="1.0.0" />
```

MSBuild auto-imports `build/ILGPUC.targets` (a thin wrapper that imports
`build/ILGPU.Kernels.targets`) from the package. No additional configuration
needed. The wrapper exists because NuGet only auto-imports `build/<PackageId>
.targets` — files with other names live in the package but stay dormant
until explicitly `<Import>`-ed.

### Option B: Local Development

For local development or testing without publishing a NuGet package:

1. **Import the targets file** in your consuming project's `.csproj`:

```xml
<Import Project="/path/to/Src/ILGPUC/build/ILGPU.Kernels.targets" />
```

2. **Set the tool path** to point to your local ILGPUC build:

```xml
<PropertyGroup>
  <ILGPUCompilerTool>dotnet run --project /path/to/Src/ILGPUC -- </ILGPUCompilerTool>
</PropertyGroup>
```

Or, if you've built `ilgpuc` as a standalone tool:

```xml
<PropertyGroup>
  <ILGPUCompilerTool>/path/to/Src/ILGPUC/bin/Debug/net10.0/ilgpuc</ILGPUCompilerTool>
</PropertyGroup>
```

## Configuration

All properties are optional. Set them in a `<PropertyGroup>` in your `.csproj`:

| Property | Default | Description |
|----------|---------|-------------|
| `ILGPUBackend` | `CPU` | Target backend(s). Examples: `CPU`, `Metal`, `Cuda`, `CPU Metal` |
| `ILGPUBackendOptions` | *(empty)* | Extra backend flags passed to `ilgpuc`. Example: `--simd-width 16` |
| `ILGPUCompilationOptions` | *(empty)* | Compilation flags passed to `ilgpuc`. Example: `-opt O2` |
| `ILGPUCompilerTool` | `ilgpuc` | Path to the ILGPUC tool. Override for local dev builds. |
| `ILGPUOutputDir` | `$(IntermediateOutputPath)ilgpu/` | Where to write rewritten + generated files |
| `ILGPUMaxGenericTypeDepth` | `8` | Maximum nesting depth of a type argument when finding the instantiations of generic kernels (`Box<Box<int>>` has depth 3) |
| `ILGPUMaxGenericInstantiationsPerDefinition` | `256` | Maximum number of instantiations of one generic method or type when finding the instantiations of generic kernels |
| `ILGPUMaxGenericInstantiations` | `16384` | Maximum number of generic instantiations in total when finding the instantiations of generic kernels |
| `ILGPUWarnOnOpenPublicEntryPoints` | `false` | Warn (`ILGPU1010`) for every public or protected method whose type arguments reach a kernel launch, since other assemblies may call it with type arguments that have no compiled kernel. Meant for libraries; executables have no such methods |

### Example: Metal backend with optimization

```xml
<PropertyGroup>
  <ILGPUBackend>Metal</ILGPUBackend>
  <ILGPUCompilationOptions>-opt O2</ILGPUCompilationOptions>
</PropertyGroup>
```

### Example: Multiple backends

```xml
<PropertyGroup>
  <ILGPUBackend>CPU Metal</ILGPUBackend>
</PropertyGroup>
```

This generates separate `CompiledKernel` classes and registrars for each backend.

### Example: Local development setup

```xml
<PropertyGroup>
  <ILGPUBackend>CPU</ILGPUBackend>
  <ILGPUCompilerTool>dotnet run --project ../ILGPUC -- </ILGPUCompilerTool>
</PropertyGroup>

<Import Project="../ILGPUC/build/ILGPU.Kernels.targets" />
```

## Generic Kernels

A generic kernel can be launched with type arguments that are themselves type
parameters, for example from a generic helper:

```csharp
static void Scale<T>(Index1D index, ArrayView1D<T, Stride1D.Dense> data, T factor)
    where T : unmanaged, INumber<T> =>
    data[index] = data[index] * factor;

static void Run<T>(AcceleratorStream stream, ArrayView1D<T, Stride1D.Dense> data, T factor)
    where T : unmanaged, INumber<T>
{
    stream.Launch(data.IntExtent, i => Scale(i, data, factor));
}
```

`ilgpuc build` finds every closed type argument that reaches such a launch site in
the project and compiles one kernel per instantiation. It follows calls through any
depth of generic methods, methods of generic classes (`Runner<T>.Run()`), local
functions and lambdas, permuted or partially applied type arguments
(`Run<int, T>`), constructed type arguments (`Run<Pair<T>>`), method groups,
user-defined operators, property accessors and partial methods. Calls through
interfaces, virtual methods, constrained calls and static abstract interface
members reach the implementations of every type the project creates
(`new Runner<float>()`, `new T()` or boxing). Recursion terminates; polymorphic
recursion such as `Run<T>` calling `Run<Box<T>>` stops at the limits above with a
warning (`ILGPU1002` to `ILGPU1004`), and launching an instantiation beyond the
limits throws a `NotSupportedException`. The limits only count type arguments
that reach a kernel launch. The build passes the project's `DefineConstants` and
`LangVersion` on, so `#if` blocks select the same launch sites as the C# build.

Inline lambda kernels and local functions used as kernels in generic methods and
classes are compiled the same way, once for each instantiation of the code that
contains them, and may use its type parameters:

```csharp
static void Fill<T>(AcceleratorStream stream, ArrayView1D<T, Stride1D.Dense> data, T value)
    where T : unmanaged
{
    stream.Launch(data.IntExtent, i => data[i] = value);
}
```

The calls that C# makes implicitly are followed as well: `foreach` enumerators,
`using` and `await using` disposal, deconstruction, `await`, implicit index and
range indexers, collection expressions, event accessors, field initializers (as
part of the constructors that run them) and static constructors and static field
initializers of generic types (whenever a member of the type may run).

The types of kernel parameters and the type arguments of generic kernels must be
`internal` or `public`, because the generated launcher classes are not nested in
your types. A private or protected type is error `ILGPU1011`.

### Source generators

`ilgpuc build` runs the project's source generators (the `Analyzer` items, with the
project's `AdditionalFiles` and `.editorconfig`/`.globalconfig` options), so code
that uses generated types and members builds as it does with `csc`, and generic
wrappers called from generated code are followed. A kernel launch inside generated
code cannot be rewritten, because `csc` generates that code again; it produces
warning `ILGPU1008`. A generator that cannot be loaded or throws produces warning
`ILGPU1009`, and the build continues without its output.

The rewritten launch site calls a generated dispatch class that selects the compiled
kernel for the actual type arguments. One dispatch class serves all launch sites of
a kernel.

### Generic kernels across assemblies

A library can launch kernels from public generic methods, and applications can call
them with their own type arguments:

```csharp
// Library
public static T[] Run<T>(AcceleratorStream stream, T[] values, T factor)
    where T : unmanaged, INumber<T> { /* ... stream.Launch(..., Kernels.Scale(...)) */ }

// Application
Scaler.Run(stream, values, 2f);
```

The library's build describes, in its assembly, which kernels each public generic
method launches with which type arguments. The build of an application that
references it compiles the kernels for the type arguments the application passes and
registers them; the library's dispatch code launches them. This works through chains
of generic methods and libraries, as long as every assembly is built with ILGPU.
Kernels that other assemblies compile must be methods: in a library, an inline lambda
or a local function used as a kernel with the type arguments of a method that other
assemblies can call is error `ILGPU1012`. Such methods are public methods of public
types, protected methods of types that other assemblies can derive from, and any
non-private method when the assembly declares `InternalsVisibleTo`.
Only type arguments cross assembly boundaries: delegates passed by other assemblies
cannot be traced, so in a library a launching method that captures a delegate
parameter, or a method forwarding one to it, must not be visible to other assemblies
(error `ILGPU1111`; see the kernel documentation on lambdas and delegates).
Executables (an `OutputType` of `Exe` or `WinExe`) are not called by other
assemblies: their builds describe no methods, and their public methods are treated
like internal ones.

A launch site that no closed type argument reaches and that no public method of a
library leads to, for example in an internal method nothing calls, produces warning
`ILGPU1001`.
Launching it throws a `NotSupportedException` that names the missing type arguments,
as does calling a library with type arguments from an assembly that is not built
with ILGPU. Calls through reflection or `dynamic` are not visible to the build.
Libraries can set `ILGPUWarnOnOpenPublicEntryPoints` to list every public method
through which other assemblies could reach a kernel launch with their own type
arguments.

## CLI Equivalent

The MSBuild integration invokes the same command you can run manually:

```bash
ilgpuc build \
  --compile-items src/Program.cs src/Shaders.cs \
  --references /path/to/ILGPU.dll ... \
  --output-dir obj/Debug/net10.0/ilgpu/ \
  --project-dir /path/to/project/ \
  --output-type Exe \
  -b CPU
```

`--output-type` takes the project's `OutputType`. Executables (`Exe` or `WinExe`) are
not called by other assemblies, so their public methods are treated like internal
ones; any other output type, or none, is a library.

Use `ilgpuc build --help` for all available options.

## Troubleshooting

- **No kernels found**: If your source files have no `stream.Launch()` calls marked with `[ReplaceWithLauncher]`, the tool writes an empty manifest and exits cleanly. No files are swapped.
- **Build errors in rewritten code**: Check `obj/.../ilgpu/rewritten/` to see the rewritten source. The `#line` directives map errors back to original source locations.
- **Response file contents**: Inspect `obj/.../ilgpu/args.rsp` to see exactly what arguments are passed to `ilgpuc`.
- **Incremental builds**: The `ILGPUKernelCompile` target is skipped when no source file, reference, generator input, project file or ILGPUC binary changed and the response file is unchanged. The response file is rewritten only when its contents change, so changing a property that `ilgpuc` reads, such as `DefineConstants` or `ILGPUBackend`, reruns the tool.
- **`ILGPU1007`, the project does not compile**: `ilgpuc` compiles the project itself to find its kernels. The first errors are reported with their locations. If `csc` builds the project without them, `ilgpuc` sees it differently, for example because the project uses language features newer than the C# version `ilgpuc` supports, or because a source generator could not be run (`ILGPU1009`).
