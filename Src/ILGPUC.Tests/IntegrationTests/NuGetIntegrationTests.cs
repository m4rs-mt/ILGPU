// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: NuGetIntegrationTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IntegrationTests;

/// <summary>
/// Smoke test for the NuGet consumption story: pack ILGPUC into a per-test
/// temp feed, materialize a consumer project that references it via
/// <c>&lt;PackageReference&gt;</c>, restore + build, and verify the .targets
/// file was auto-imported (manifest/rewritten/generated all present and the
/// resulting binary runs).
///
/// This complements <see cref="MsBuildIntegrationTests"/> which uses the
/// local <c>&lt;Import&gt;</c> path.
/// </summary>
public sealed class NuGetIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public NuGetIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public async Task Build_PackedNuGet_AutoImportsTargets()
    {
        // CPU is the only backend the NuGet smoke test exercises — its goal
        // is to verify the package's targets-import path, not retest backend
        // codegen. CPU compiler is always available so this never skips.
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(BackendType.CPU)
                .ConfigureAwait(false),
            "CPU compiler unexpectedly unavailable");

        var srcRoot = Path.Combine(IntegrationProjectFixture.RepoRoot, "Src");
        var ilgpucCsproj = Path.Combine(srcRoot, "ILGPUC", "ILGPUC.csproj");
        var ilgpuCsproj = Path.Combine(srcRoot, "ILGPU", "ILGPU.csproj");
        var compilersCsproj = Path.Combine(
            srcRoot, "ILGPUC.Compilers", "ILGPUC.Compilers.csproj");
        Assert.True(File.Exists(ilgpucCsproj),
            $"ILGPUC.csproj not found at {ilgpucCsproj}");

        // Per-test temp feed and unique version so the global NuGet cache
        // never serves a stale package.
        var feedDir = Path.Combine(
            Path.GetTempPath(), $"ilgpuc_feed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(feedDir);
        var version = $"0.0.0-msbuildtest-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        _output.WriteLine($"NuGet feed: {feedDir}");
        _output.WriteLine($"Package version: {version}");

        try
        {
            // 1. Pack ILGPUC AND its sibling project references (ILGPU,
            //    ILGPUC.Compilers) into the temp feed using the SAME version
            //    so the inter-package dependency constraints resolve. Without
            //    these companion packs, the consumer's restore fails with
            //    NU1101 because ILGPUC's .nuspec lists ILGPU/ILGPUC.Compilers
            //    as dependencies but they only exist as project references.
            //    Use --no-build because the test setup already built
            //    everything into Bin/Debug/net10.0/.
            var packProps = new Dictionary<string, string>
            {
                ["Version"] = version,
                ["PackageVersion"] = version,
                ["IncludeSymbols"] = "false",
            };

            foreach (var (label, csproj) in new[]
            {
                ("ILGPU", ilgpuCsproj),
                ("ILGPUC.Compilers", compilersCsproj),
                ("ILGPUC", ilgpucCsproj),
            })
            {
                var pack = await MsBuildRunner.PackAsync(
                    csproj,
                    outputDir: feedDir,
                    configuration: "Debug",
                    noBuild: true,
                    properties: packProps);
                Assert.True(pack.Succeeded,
                    $"dotnet pack {label} failed (exit {pack.ExitCode}). " +
                    $"Stderr: {pack.StdErr}\nStdout:\n{pack.FullStdOut}");
            }

            var nupkgPath = Path.Combine(feedDir, $"ILGPUC.{version}.nupkg");
            Assert.True(File.Exists(nupkgPath),
                $"Expected {nupkgPath} to exist after pack");

            // 2. Materialize the NuGetHello template, substituting the feed
            //    path and the per-test version into both the .csproj and the
            //    NuGet.config.
            using var project = new IntegrationProjectFixture(
                "NuGetHello",
                BackendType.CPU,
                _output,
                extraTokens: new Dictionary<string, string>
                {
                    ["$(ILGPUC_TEMP_FEED)"] = feedDir.Replace('\\', '/'),
                    ["$(ILGPUC_PACKAGE_VERSION)"] = version,
                });
            try
            {
                // 3. Restore using the temp feed.
                var restore = await project.RestoreAsync();
                Assert.True(restore.Succeeded,
                    $"dotnet restore failed (exit {restore.ExitCode}). " +
                    $"Stderr: {restore.StdErr}\nStdout:\n{restore.FullStdOut}");

                // 4. Build. The .targets file inside the NuGet package must
                //    be auto-imported via build/ILGPUC.targets.
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}\nStdout:\n{build.FullStdOut}");

                // 5. Auto-import worked: artifacts must be present.
                Assert.True(File.Exists(project.ManifestPath),
                    $"manifest.txt not found at {project.ManifestPath}. " +
                    $"This means the NuGet package's build/ILGPUC.targets " +
                    $"was NOT auto-imported by MSBuild.");
                Assert.True(File.Exists(project.ResponseFilePath),
                    $"args.rsp not found at {project.ResponseFilePath}");

                var generated = project.ListGenerated();
                Assert.Contains(generated,
                    p => p.EndsWith(
                        "_CompiledKernel.cs", StringComparison.Ordinal));
                Assert.Contains(generated,
                    p => p.Equals(
                        "CPUKernelRegistrar.cs", StringComparison.Ordinal));

                // 6. Run the binary, verify expected output.
                var run = await project.RunAsync();
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected: ["0", "3", "6", "9"]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(feedDir))
                    Directory.Delete(feedDir, recursive: true);
            }
            catch (Exception ex)
            {
                _output.WriteLine(
                    $"Warning: failed to delete feed dir {feedDir}: {ex.Message}");
            }
        }
    }
}
