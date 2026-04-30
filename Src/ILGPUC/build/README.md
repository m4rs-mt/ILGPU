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

## CLI Equivalent

The MSBuild integration invokes the same command you can run manually:

```bash
ilgpuc build \
  --compile-items src/Program.cs src/Shaders.cs \
  --references /path/to/ILGPU.dll ... \
  --output-dir obj/Debug/net10.0/ilgpu/ \
  --project-dir /path/to/project/ \
  -b CPU
```

Use `ilgpuc build --help` for all available options.

## Troubleshooting

- **No kernels found**: If your source files have no `stream.Launch()` calls marked with `[ReplaceWithLauncher]`, the tool writes an empty manifest and exits cleanly. No files are swapped.
- **Build errors in rewritten code**: Check `obj/.../ilgpu/rewritten/` to see the rewritten source. The `#line` directives map errors back to original source locations.
- **Response file contents**: Inspect `obj/.../ilgpu/args.rsp` to see exactly what arguments are passed to `ilgpuc`.
- **Incremental builds**: The `ILGPUKernelCompile` target uses `Inputs="@(Compile)" Outputs="$(ILGPUManifest)"` for incremental build support. If source files haven't changed, the tool is skipped.
