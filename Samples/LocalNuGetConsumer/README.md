# LocalNuGetConsumer

This sample shows the workflow for **consuming a locally-built ILGPUC NuGet package** from a project of your own. Useful when you're working on the ILGPU compiler itself, want to verify what your downstream users will actually see, and want to iterate on the package end-to-end without touching `nuget.org`.

## When you'd want this

The other samples in this directory consume ILGPU via local DLL references (`<Reference Include="ILGPU"><HintPath>$(ILGPUBinDir)/ILGPU.dll</HintPath></Reference>`). That's perfect for "show me how to use the ILGPU API" but bypasses the NuGet packaging entirely. If you're touching `Src/ILGPUC/build/ILGPU.Kernels.targets`, the pack script, or anything that affects how `<PackageReference Include="ILGPUC"/>` behaves for downstream users, the local-DLL pattern will hide your bugs. Use this sample instead.

## Step 1 — pack ILGPU + ILGPUC into a local feed

On macOS / Linux (or Git Bash / WSL on Windows):

```bash
cd Samples/LocalNuGetConsumer
./pack-local.sh
```

On Windows with PowerShell 7+:

```powershell
cd Samples\LocalNuGetConsumer
.\pack-local.ps1
```

Both scripts are small and worth reading; they do the same thing in their respective shells:

1. **`dotnet pack Src/ILGPU/ILGPU.csproj`** — produces `feed/ILGPU.0.0.0-local.nupkg` (a normal class library, plain pack flow).
2. **Per-host-RID R2R publish + JIT-fallback publish + dedupe + `dotnet pack`** for `ILGPUC` — produces `feed/ILGPUC.0.0.0-local.nupkg`. The bash variant delegates this to `Src/scripts/pack-ilgpuc.sh`; the PowerShell variant inlines the same flow so it has no bash dependency. See `Src/scripts/pack-ilgpuc.sh` for the canonical publish + pack mechanics, including the multi-RID release flow.

Both packages get the same version (`0.0.0-local` by default). Override via `VERSION=... ./pack-local.sh` (bash) or `.\pack-local.ps1 -Version 0.0.1-local` (PowerShell); useful when you want to iterate on the compiler and bump the version each round so NuGet's package cache doesn't serve a stale copy.

## Step 2 — review what the consumer needs

Three files. Read them in this order:

### `Consumer/NuGet.config`

Tells NuGet *where* to look for packages. Two sources: the local `../feed/` directory (where `pack-local.sh` puts the nupkgs) and `nuget.org` (for transitive deps). The `<clear />` is important — it wipes any inherited config that might silently pull `ILGPUC` from a public feed instead of your local one.

### `Consumer/Consumer.csproj`

A normal SDK-style csproj with one `<PackageReference Include="ILGPUC" Version="$(ILGPUC_PACKAGE_VERSION)" />`. Three details worth noting:

- **No `<Reference Include="ILGPU">` with `HintPath`.** Other samples have one; this one doesn't. ILGPU comes via NuGet's transitive dep resolution — `ILGPUC` declares `ILGPU` as a dependency in its published `.nuspec`, so the consumer only references `ILGPUC`.
- **No `<Import Project=".../ILGPU.Kernels.targets"/>`.** NuGet auto-imports `build/ILGPUC.targets` from the package. That file in turn imports `ILGPU.Kernels.targets`, which is what runs the kernel compilation pipeline.
- **`<ILGPUCompilerTool></ILGPUCompilerTool>` (blank).** This sample lives under `Samples/`, so `Samples/Directory.Build.props` is in effect — it sets `ILGPUCompilerTool` to point at the local repo's `Bin/.../ILGPUC.dll`. Setting it blank here re-enables the package's auto-resolution against its bundled `tools/<rid>/` layout. In a project of your own (outside this repo), you wouldn't need this line — `Directory.Build.props` wouldn't be there to override.

### `Consumer/Program.cs`

A trivial CPU kernel `data[i] = i * 2`. The point is to demonstrate that the kernel compiles + runs end-to-end via the package, not to teach kernel patterns. Other samples show richer kernels.

## Step 3 — restore, build, run

```bash
cd Consumer
dotnet build -p:ILGPUC_PACKAGE_VERSION=0.0.0-local
dotnet run -p:ILGPUC_PACKAGE_VERSION=0.0.0-local
```

(Same on PowerShell — `dotnet` argument syntax is shell-agnostic.)

Expected output:

```
0 = 0
1 = 2
2 = 4
3 = 6
4 = 8
5 = 10
6 = 12
7 = 14
```

What happened during `dotnet build`:

- `dotnet restore` (run automatically) pulled `ILGPUC.0.0.0-local` from `../feed/` and `ILGPU.0.0.0-local` (transitive dep) from `../feed/`. Other transitive deps came from nuget.org.
- NuGet auto-imported `build/ILGPUC.targets` from the ILGPUC package; that imported `ILGPU.Kernels.targets`, which set up the `ILGPUKernelCompile` MSBuild target.
- `ILGPUKernelCompile` ran *before* `CoreCompile`. It invoked the `ilgpuc` tool from `packages/ilgpuc/0.0.0-local/tools/net10.0/<host-rid>/ILGPUC` (or the JIT DLL fallback for unsupported RIDs), scanning your source for `stream.Launch(...)` calls.
- `ilgpuc` walked the kernel, ran the IR + optimiser pipeline, generated a real `CompiledKernel` C# class, and rewrote your `stream.Launch(...)` site to invoke it directly.
- `csc` compiled the rewritten source. The original `Program.cs` on disk was never touched.
- `dotnet run` executed the binary. The CPU runtime invoked the compiled kernel.

## Inspect the artefacts

After a build:

```bash
# What got installed:
ls Consumer/packages/ilgpuc/0.0.0-local/
#   build/ILGPUC.targets  build/ILGPU.Kernels.targets
#   tools/net10.0/ILGPUC.dll                        ← JIT fallback
#   tools/net10.0/<host-rid>/ILGPUC                 ← R2R native exe (~3x cold-start over JIT)
#   ...

# What the targets-file pipeline produced:
cat Consumer/obj/Debug/net10.0/ilgpu/manifest.txt    # source files that got rewritten
cat Consumer/obj/Debug/net10.0/ilgpu/args.rsp        # exact ilgpuc invocation
ls  Consumer/obj/Debug/net10.0/ilgpu/generated/      # *_CompiledKernel.cs, CPUKernelRegistrar.cs
ls  Consumer/obj/Debug/net10.0/ilgpu/rewritten/      # rewritten copy of Program.cs
```

## Iterating

Make a change to the compiler. Re-run `pack-local.sh` (or `pack-local.ps1`) to rebuild the nupkgs. Re-run `dotnet build` in the consumer. Repeat. Bump the version if you need NuGet to forget the previous package version (`VERSION=0.0.1-local ./pack-local.sh` or `.\pack-local.ps1 -Version 0.0.1-local`).

For an automated end-to-end version of this same workflow with assertions, see `EndToEndTest/run.sh` at the repo root — same pack flow, but it runs the consumer and verifies stdout against a known-good expected output.
