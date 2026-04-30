# EndToEndTest

End-to-end consumer smoke check for the published ILGPU + ILGPUC NuGet packages.

## What it does

1. Packs `Src/ILGPU/ILGPU.csproj` and `Src/ILGPUC/ILGPUC.csproj` (the latter via `Src/scripts/pack-ilgpuc.sh`) into a local feed under `EndToEndTest/feed/`.
2. Renders `NuGet.config` for `EndToEndTest/HelloKernel/` pointing at that feed.
3. Restores, builds, and runs `HelloKernel/`, which contains a single-file CPU kernel `result[i] = b[i] * c[i]` over hard-coded inputs.
4. Asserts the program's stdout matches the known-good expected output line-for-line.

## When to run it

- **Locally** before a release tag: `EndToEndTest/run.sh`. Verifies the package ships in a state a downstream consumer can actually build against, on your dev box.
- **In CI**: the `e2e-test` job runs immediately after `test-cpu`. The four `test-*-compile` jobs (Metal / CUDA / ROCm / OpenCL) gate on it, so a packaging breakage fails fast — no expensive macOS or Docker-pull runners spin up.

## Why it's a real project, not a template

The csproj and `Program.cs` open in an IDE without token expansion. A contributor cloning the repo can `cat HelloKernel.csproj` and see exactly what a downstream consumer's csproj looks like — a single `<PackageReference Include="ILGPUC"/>`, no local-path workarounds. Inspectable post-success: after a run, `HelloKernel/{packages,bin,obj}/` are left on disk for poking around.

## Failure modes it catches

- ILGPUC nuspec missing the transitive ILGPU dep (consumer can't `using ILGPU.Runtime;`)
- `build/ILGPUC.targets` not auto-imported by NuGet
- `$(ILGPUCompilerTool)` resolves to nothing (no per-RID R2R exe, no JIT fallback)
- R2R native exe in `tools/<rid>/` missing required runtime DLLs
- Kernel walk / IR / codegen / launcher rewriting broken end-to-end
- CPU runtime execution producing wrong output

## Troubleshooting

When `run.sh` fails, the leftover state is on disk:

```bash
ls EndToEndTest/HelloKernel/packages/ilgpuc/0.0.0-e2e-*/
#  → tools/net10.0/<rid>/ILGPUC, build/ILGPU.Kernels.targets, ...

cat EndToEndTest/HelloKernel/obj/Release/net10.0/ilgpu/manifest.txt
#  → list of original .cs files that ilgpuc rewrote

cat EndToEndTest/HelloKernel/obj/Release/net10.0/ilgpu/args.rsp
#  → exact arguments passed to ilgpuc

ls EndToEndTest/HelloKernel/obj/Release/net10.0/ilgpu/generated/
#  → *_CompiledKernel.cs, CPUKernelRegistrar.cs
```

The `feed/` directory contains the packed nupkgs — `unzip -l` to inspect layout.

## Configuration

| Env var   | Default                              | Effect                                  |
|-----------|--------------------------------------|-----------------------------------------|
| `CONFIG`  | `Release`                            | Build configuration for both packs + consumer build |
| `VERSION` | `0.0.0-e2e-<utc-timestamp>`          | Package version stamped into the local feed |

## Relationship to other tests

- **`Src/ILGPUC.Tests/IntegrationTests/MsBuildIntegrationTests`** — drives `dotnet build` against templated csprojs that `<Import>` the local repo's `ILGPU.Kernels.targets` directly (no NuGet involvement). Catches local-dev path regressions. Lives in xUnit because it parametrises over many backends and scenarios.
- **EndToEndTest (this dir)** — drives the published-NuGet consumption path against a real persistent project. One CPU scenario, deterministic stdout assertion, dev-runnable.

There used to be an `NuGetIntegrationTests` xUnit test that exercised this path against a templated project under `$TMPDIR`. EndToEndTest replaces it with the persistent equivalent — see commit history for context.
