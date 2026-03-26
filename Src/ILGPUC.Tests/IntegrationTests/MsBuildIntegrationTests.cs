// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MsBuildIntegrationTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IntegrationTests;

/// <summary>
/// End-to-end MSBuild integration tests. Each test materializes a real
/// standalone .csproj from the IntegrationProjects/ templates, runs
/// <c>dotnet build</c> against it, inspects the artifacts produced by
/// <c>ILGPU.Kernels.targets</c>, and (where applicable) runs the resulting
/// binary and verifies its stdout.
///
/// These tests cover the .targets-file glue path that the existing in-process
/// test layers (IRTests/, BackendTests/, ExecutionTests/) never exercise.
///
/// Backend coverage: CPU runs everywhere; Metal is gated by both the
/// compiler and runtime availability checks (so it runs on macOS dev
/// machines and skips on Linux CI).
/// </summary>
public sealed class MsBuildIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public MsBuildIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static async Task RequireBackendAsync(BackendType backend)
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(backend).ConfigureAwait(false),
            $"Native compiler for {backend} is not available on this machine");
        Skip.IfNot(
            Availability.IsRuntimeAvailable(backend),
            $"No {backend} runtime device available on this machine");
    }

    // -----------------------------------------------------------------------
    // Test 1: HelloKernel — happy path. Build produces all expected artifacts
    // AND the resulting binary runs and prints the expected output.
    // -----------------------------------------------------------------------

    [SkippableTheory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    public async Task Build_HelloKernel_ProducesArtifactsAndRuns(BackendType backend)
    {
        await RequireBackendAsync(backend);

        using var project = new IntegrationProjectFixture(
            "HelloKernel", backend, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build failed (exit {build.ExitCode}). " +
                $"Stderr: {build.StdErr}");

            // .targets must have produced manifest, rewritten, generated.
            Assert.True(File.Exists(project.ManifestPath),
                $"manifest.txt not found at {project.ManifestPath}");
            Assert.True(File.Exists(project.ResponseFilePath),
                $"args.rsp not found at {project.ResponseFilePath}");

            var manifest = project.ReadManifest();
            Assert.Contains("Program.cs", manifest);

            var rewritten = project.ListRewritten();
            Assert.Contains(rewritten,
                p => p.EndsWith("Program.cs", StringComparison.Ordinal));

            var generated = project.ListGenerated();
            Assert.Contains(generated,
                p => p.EndsWith("_CompiledKernel.cs", StringComparison.Ordinal));
            Assert.Contains(generated,
                p => p.Equals(
                    $"{ExpectedAcceleratorTypeName(backend)}KernelRegistrar.cs",
                    StringComparison.Ordinal));

            // Run the resulting binary and verify stdout.
            var run = await project.RunAsync();
            Assert.False(run.TimedOut, "Binary timed out");
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(
                run.StdOutLines,
                expected: ["0", "2", "4", "6"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 2: NoKernels — project with no stream.Launch() calls. The build
    // must still succeed, the manifest must be empty, and no source files
    // should be swapped.
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Build_NoKernels_ProducesEmptyManifestAndRuns()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "NoKernels", BackendType.CPU, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build failed (exit {build.ExitCode}). " +
                $"Stderr: {build.StdErr}");

            // Manifest exists but is empty (BuildCommand.Execute writes "" when
            // no launch sites are found).
            Assert.True(File.Exists(project.ManifestPath));
            Assert.True(string.IsNullOrWhiteSpace(project.ReadManifest()),
                $"Expected empty manifest, got: {project.ReadManifest()}");

            // No rewritten or generated files for a project with no kernels.
            Assert.Empty(project.ListRewritten());
            Assert.Empty(project.ListGenerated());

            var run = await project.RunAsync();
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(
                run.StdOutLines,
                expected: ["hello no kernels"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 3: MultiKernel — two distinct kernels in one project.
    // -----------------------------------------------------------------------

    [SkippableTheory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    public async Task Build_MultiKernel_GeneratesAllKernelsAndRuns(
        BackendType backend)
    {
        await RequireBackendAsync(backend);

        using var project = new IntegrationProjectFixture(
            "MultiKernel", backend, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build failed (exit {build.ExitCode}). " +
                $"Stderr: {build.StdErr}");

            var generated = project.ListGenerated();
            var compiledKernelFiles = generated
                .Where(p => p.EndsWith(
                    "_CompiledKernel.cs", StringComparison.Ordinal))
                .ToArray();

            // Both kernels must produce a CompiledKernel class file.
            Assert.True(compiledKernelFiles.Length >= 2,
                $"Expected >=2 _CompiledKernel.cs files, got " +
                $"{compiledKernelFiles.Length}: {string.Join(", ", compiledKernelFiles)}");

            // Both kernel names should appear among the generated files.
            Assert.Contains(compiledKernelFiles,
                p => p.Contains("TenXKernel", StringComparison.Ordinal));
            Assert.Contains(compiledKernelFiles,
                p => p.Contains("PlusOneKernel", StringComparison.Ordinal));

            var run = await project.RunAsync();
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(
                run.StdOutLines,
                expected: ["A: 0 10 20 30", "B: 1 2 3 4"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 4: Override -p:ILGPUBackend at the command line, verify the
    // override propagates to args.rsp and is reflected in the registrar.
    //
    // This test deliberately stops at artifact verification (no run step).
    // The PREFER_CPU substitution in the .cs template is fixed at fixture-
    // construction time, so a runtime backend that differs from the source's
    // hard-coded preferCPU value would mismatch the accelerator. The point of
    // the test is to prove the property override path through the .targets
    // file, not the runtime — that's covered by tests 1 and 3.
    // -----------------------------------------------------------------------

    [SkippableTheory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    public async Task Build_BackendPropertyOverride_HonoredInArgsRspAndRegistrar(
        BackendType backend)
    {
        await RequireBackendAsync(backend);

        // Materialize the project with a *different* default backend than
        // what we override on the command line, to prove the -p override is
        // what's honored.
        var defaultBackend = backend == BackendType.CPU
            ? BackendType.Metal
            : BackendType.CPU;
        using var project = new IntegrationProjectFixture(
            "HelloKernel", defaultBackend, _output);
        try
        {
            var build = await project.BuildAsync(
                new Dictionary<string, string>
                {
                    ["ILGPUBackend"] = backend.ToString(),
                });
            Assert.True(build.Succeeded,
                $"dotnet build failed (exit {build.ExitCode}). " +
                $"Stderr: {build.StdErr}");

            // Response file must reflect the overridden backend.
            var rsp = project.ReadResponseFile();
            Assert.Contains("-b", rsp);
            Assert.Contains(backend.ToString(), rsp);

            // Generated registrar must match the overridden backend.
            var generated = project.ListGenerated();
            var expectedRegistrar =
                $"{ExpectedAcceleratorTypeName(backend)}KernelRegistrar.cs";
            Assert.Contains(generated,
                p => p.Equals(expectedRegistrar, StringComparison.Ordinal));
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 5: CompileError — kernel uses an unsupported pattern; the build
    // must fail with a non-zero exit code and surface the diagnostic.
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Build_CompileError_FailsWithDiagnostic()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "CompileError", BackendType.CPU, _output);
        try
        {
            var build = await project.BuildAsync();

            Assert.False(build.Succeeded,
                "Expected dotnet build to FAIL for the unsupported kernel " +
                "pattern, but it succeeded.");
            Assert.NotEqual(0, build.ExitCode);

            // The NotSupportedException from LowerGCInit should appear in
            // build output (combined stdout + stderr).
            var combined = build.FullStdOut + "\n" + build.StdErr;
            Assert.True(
                combined.Contains("NotSupportedException", StringComparison.Ordinal)
                || combined.Contains("not supported", StringComparison.OrdinalIgnoreCase),
                $"Expected NotSupportedException diagnostic in build output, " +
                $"got:\n{combined}");
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 6: Incremental — building twice with no source changes must NOT
    // re-run ILGPUKernelCompile. We use the manifest's mtime as the signal:
    // a skipped target leaves the file untouched.
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Build_Incremental_SkipsWhenSourcesUnchanged()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "HelloKernel", BackendType.CPU, _output);
        try
        {
            var firstBuild = await project.BuildAsync();
            Assert.True(firstBuild.Succeeded);
            var firstMtime = File.GetLastWriteTimeUtc(project.ManifestPath);

            // Build again with no source changes.
            var secondBuild = await project.BuildAsync();
            Assert.True(secondBuild.Succeeded);
            var secondMtime = File.GetLastWriteTimeUtc(project.ManifestPath);

            Assert.Equal(firstMtime, secondMtime);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 7: Incremental — touching a source file forces ILGPUKernelCompile
    // to re-run, regenerating the manifest. The binary still produces the
    // expected output after the second build.
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Build_Incremental_RebuildsWhenSourceChanges()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "HelloKernel", BackendType.CPU, _output);
        try
        {
            var firstBuild = await project.BuildAsync();
            Assert.True(firstBuild.Succeeded);
            var firstMtime = File.GetLastWriteTimeUtc(project.ManifestPath);

            // Touch Program.cs forward in time. Use +5s to clear FS mtime
            // resolution on common filesystems (HFS+, FAT, etc.).
            var programCs = Path.Combine(project.WorkingDir, "Program.cs");
            Assert.True(File.Exists(programCs));
            File.SetLastWriteTimeUtc(programCs, DateTime.UtcNow.AddSeconds(5));

            var secondBuild = await project.BuildAsync();
            Assert.True(secondBuild.Succeeded);
            var secondMtime = File.GetLastWriteTimeUtc(project.ManifestPath);

            Assert.True(secondMtime > firstMtime,
                $"Expected manifest mtime to advance after touching " +
                $"Program.cs. First={firstMtime:O}, Second={secondMtime:O}");

            var run = await project.RunAsync();
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(
                run.StdOutLines,
                expected: ["0", "2", "4", "6"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Test 8: dotnet clean removes the obj/.../ilgpu/ output directory via
    // the ILGPUClean target.
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Clean_RemovesIlgpuOutputDir()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "HelloKernel", BackendType.CPU, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded);
            Assert.True(Directory.Exists(project.GetIlgpuOutputDir()),
                $"Expected {project.GetIlgpuOutputDir()} to exist after build");

            var clean = await project.CleanAsync();
            Assert.True(clean.Succeeded,
                $"dotnet clean failed (exit {clean.ExitCode}). " +
                $"Stderr: {clean.StdErr}");

            Assert.False(Directory.Exists(project.GetIlgpuOutputDir()),
                $"Expected {project.GetIlgpuOutputDir()} to be removed by " +
                $"the ILGPUClean target, but it still exists");
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Maps a <see cref="BackendType"/> to the AcceleratorType name used by
    /// <see cref="ILGPUC.Backends.KernelRegistrarGenerator"/> when emitting
    /// the <c>{AcceleratorType}KernelRegistrar.cs</c> file.
    /// </summary>
    private static string ExpectedAcceleratorTypeName(BackendType backend) =>
        backend switch
        {
            BackendType.CPU => "CPU",
            BackendType.Metal => "Metal",
            BackendType.Cuda => "Cuda",
            BackendType.ROCm => "ROCm",
            BackendType.OpenCL => "OpenCL",
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };
}
