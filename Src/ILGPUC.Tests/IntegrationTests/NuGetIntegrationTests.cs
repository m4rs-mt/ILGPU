// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NuGetIntegrationTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
        Assert.True(File.Exists(ilgpucCsproj),
            $"ILGPUC.csproj not found at {ilgpucCsproj}");

        // Per-test temp feed and unique version so the global NuGet cache
        // never serves a stale package.
        var feedDir = Path.Combine(
            Path.GetTempPath(), $"ilgpuc_feed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(feedDir);
        var version = $"0.0.0-msbuildtest-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var stagingDir = Path.Combine(
            Path.GetTempPath(), $"ilgpuc_stage_{Guid.NewGuid():N}");
        var hostRid = RuntimeInformation.RuntimeIdentifier;
        // RuntimeIdentifier may be version-qualified (e.g. "osx.14-arm64").
        // Collapse to portable form; pack-ilgpuc.sh uses portable RIDs.
        hostRid = CollapsePortableRid(hostRid);
        _output.WriteLine($"NuGet feed: {feedDir}");
        _output.WriteLine($"Package version: {version}");
        _output.WriteLine($"Staging dir: {stagingDir}");
        _output.WriteLine($"Host RID: {hostRid}");

        try
        {
            // 1. Mirror the publish + pack flow that Src/scripts/pack-ilgpuc.sh
            //    runs in CI. Doing it inline here (instead of shelling out to
            //    the bash script) keeps this test cross-platform — Windows CI
            //    can't run a .sh file directly.
            //
            //    Step 1a: per-RID R2R publish into staging/tools/<tfm>/<rid>/.
            //    Single RID is fine for the test — its goal is to verify the
            //    targets-file resolution path, not to test cross-RID R2R.
            var ridStaging = Path.Combine(
                stagingDir, "tools", "net10.0", hostRid);
            var publishR2R = await MsBuildRunner.PublishAsync(
                ilgpucCsproj,
                outputDir: ridStaging,
                configuration: "Debug",
                framework: "net10.0",
                runtime: hostRid,
                selfContained: false,
                properties: new Dictionary<string, string>
                {
                    ["PublishReadyToRun"] = "true",
                    ["PublishReadyToRunComposite"] = "false",
                    ["DebugType"] = "none",
                    ["DebugSymbols"] = "false",
                });
            Assert.True(publishR2R.Succeeded,
                $"dotnet publish (R2R, {hostRid}) failed (exit {publishR2R.ExitCode}). " +
                $"Stderr: {publishR2R.StdErr}\nStdout:\n{publishR2R.FullStdOut}");

            // Step 1b: JIT fallback into staging/tools/<tfm>/.
            var jitStaging = Path.Combine(stagingDir, "tools", "net10.0");
            var publishJit = await MsBuildRunner.PublishAsync(
                ilgpucCsproj,
                outputDir: jitStaging,
                configuration: "Debug",
                framework: "net10.0",
                runtime: null,
                selfContained: false,
                properties: new Dictionary<string, string>
                {
                    ["DebugType"] = "none",
                    ["DebugSymbols"] = "false",
                });
            Assert.True(publishJit.Succeeded,
                $"dotnet publish (JIT) failed (exit {publishJit.ExitCode}). " +
                $"Stderr: {publishJit.StdErr}\nStdout:\n{publishJit.FullStdOut}");

            // Step 1c: pack with the staging dir feeding the tools/ glob.
            //   --no-build avoids re-running compile (publish already built).
            var packProps = new Dictionary<string, string>
            {
                ["Version"] = version,
                ["PackageVersion"] = version,
                ["IncludeSymbols"] = "false",
                ["ILGPUCPackStagingDir"] = stagingDir,
            };
            var pack = await MsBuildRunner.PackAsync(
                ilgpucCsproj,
                outputDir: feedDir,
                configuration: "Debug",
                noBuild: true,
                properties: packProps);
            Assert.True(pack.Succeeded,
                $"dotnet pack ILGPUC failed (exit {pack.ExitCode}). " +
                $"Stderr: {pack.StdErr}\nStdout:\n{pack.FullStdOut}");

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
            foreach (var dir in new[] { feedDir, stagingDir })
            {
                try
                {
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine(
                        $"Warning: failed to delete {dir}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Strips the OS-version qualifier from a runtime identifier
    /// (e.g. <c>osx.14-arm64</c> → <c>osx-arm64</c>). The package's tools/
    /// layout uses portable RIDs so the consumer's per-RID resolution can
    /// match across different OS versions.
    /// </summary>
    private static string CollapsePortableRid(string rid)
    {
        var dash = rid.LastIndexOf('-');
        if (dash <= 0) return rid;
        var os = rid[..dash];
        var arch = rid[(dash + 1)..];
        var dot = os.IndexOf('.');
        if (dot > 0) os = os[..dot];
        return $"{os}-{arch}";
    }
}
