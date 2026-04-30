# ILGPUC

ILGPU's AOT compiler toolchain. Compiles GPU kernels at MSBuild time and emits real `CompiledKernel` classes the C# compiler sees instead of your `stream.Launch(...)` lambdas.

## Install

```xml
<ItemGroup>
  <PackageReference Include="ILGPU" Version="2.0.0-beta1" />
  <PackageReference Include="ILGPUC" Version="2.0.0-beta1" />
</ItemGroup>
```

NuGet auto-imports `build/ILGPUC.targets`. No additional `<Import>` is needed. The bundled MSBuild integration runs before `CoreCompile` and:

1. Scans `@(Compile)` for `stream.Launch(...)` call sites
2. Compiles each kernel through ILGPUC's IL frontend → IR → optimizer → backend
3. Rewrites the launch site to call the compiled kernel directly
4. Hands `csc` the rewritten sources

Original source files on disk are never modified. `#line` directives map debugger steps back to the original code.

## Configuration

Set in any `<PropertyGroup>` in the consumer's csproj. All optional.

| Property                  | Default | Description                                                                                  |
|---------------------------|---------|----------------------------------------------------------------------------------------------|
| `ILGPUBackend`            | `CPU`   | Target backend(s). Examples: `CPU`, `Metal`, `Cuda`, `CPU Metal`                             |
| `ILGPUBackendOptions`     | *(empty)* | Extra flags. Example: `--simd-width 16`                                                    |
| `ILGPUCompilationOptions` | *(empty)* | Compiler flags. Example: `-opt O2`                                                         |
| `ILGPUCompile`            | `false` | When `true`, ILGPUC invokes the native toolchain (nvcc/hipcc/ocloc/xcrun) and embeds binaries |
| `ILGPUCompilerServices`   | *(empty)* | Semicolon-delimited list of remote `ILGPUC.CompilerService` URLs                           |
| `ILGPUCompilerTool`       | *(auto)* | Override the resolved tool path. Auto-resolves to a per-RID R2R native exe or a JIT fallback |

## Performance

ILGPUC ships with ReadyToRun-precompiled native binaries for `osx-arm64`, `linux-x64`, and `win-x64`. The MSBuild targets file picks the right binary for your host RID automatically. Other RIDs fall back to the framework-dependent JIT DLL — same correctness, ~200 ms slower per build. To override:

```xml
<PropertyGroup>
  <ILGPUCompilerTool>dotnet "/path/to/your/ILGPUC.dll" </ILGPUCompilerTool>
</PropertyGroup>
```

## More

- Project page: <http://www.ilgpu.net>
- Samples: <https://github.com/m4rs-mt/ILGPU/tree/master/Samples>
- License: University of Illinois Open Source License (see `LICENSE.txt`)
