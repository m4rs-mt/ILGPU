// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MsBuildIntegrationTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
///
/// Every scenario is a nested class of its own, since xUnit runs the tests of a
/// class one after another and different classes in parallel. Each test builds a
/// fresh project in its own temporary directory, and <see cref="MsBuildRunner"/>
/// disables MSBuild node reuse and the shared compiler server, so concurrent
/// builds share no state.
/// </summary>
public static class MsBuildIntegrationTests
{
    // -----------------------------------------------------------------------
    // Test 1: HelloKernel — happy path. Build produces all expected artifacts
    // AND the resulting binary runs and prints the expected output.
    // -----------------------------------------------------------------------

    public sealed class HelloKernel(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_HelloKernel_ProducesArtifactsAndRuns(BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "HelloKernel", backend, output);
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
    }

    // -----------------------------------------------------------------------
    // Test 2: NoKernels — project with no stream.Launch() calls. The build
    // must still succeed, the manifest must be empty, and no source files
    // should be swapped.
    // -----------------------------------------------------------------------

    public sealed class NoKernels(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_NoKernels_ProducesEmptyManifestAndRuns()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "NoKernels", BackendType.CPU, output);
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
    }

    // -----------------------------------------------------------------------
    // Test 3: MultiKernel — two distinct kernels in one project.
    // -----------------------------------------------------------------------

    public sealed class MultiKernel(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_MultiKernel_GeneratesAllKernelsAndRuns(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "MultiKernel", backend, output);
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
                    $"{compiledKernelFiles.Length}: " +
                    $"{string.Join(", ", compiledKernelFiles)}");

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

    public sealed class BackendPropertyOverride(ITestOutputHelper output)
    {
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
                "HelloKernel", defaultBackend, output);
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
    }

    // -----------------------------------------------------------------------
    // Test 5: CompileError — kernel uses an unsupported pattern; the build
    // must fail with a non-zero exit code and surface the diagnostic.
    // -----------------------------------------------------------------------

    public sealed class CompileError(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_CompileError_FailsWithDiagnostic()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "CompileError", BackendType.CPU, output);
            try
            {
                var build = await project.BuildAsync();

                Assert.False(build.Succeeded,
                    "Expected dotnet build to FAIL for the unsupported kernel " +
                    "pattern, but it succeeded.");
                Assert.NotEqual(0, build.ExitCode);

                // The rejection from LowerGCInit is reported in the canonical
                // error format at the loop, without an unhandled exception
                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.True(
                    combined.Contains(
                        "Program.cs(21,14): error ILGPU1208: A lambda or local " +
                        "function in method 'LambdaInLoopKernel' allocates its " +
                        "closure inside a loop",
                        StringComparison.Ordinal),
                    $"Expected an ILGPU1208 error at Program.cs(21,14), " +
                    $"got:\n{combined}");
                Assert.DoesNotContain("Unhandled exception", combined);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 6: Incremental — building twice with no source changes must NOT
    // re-run ILGPUKernelCompile. We use the manifest's mtime as the signal:
    // a skipped target leaves the file untouched.
    // -----------------------------------------------------------------------

    public sealed class IncrementalSkip(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_Incremental_SkipsWhenSourcesUnchanged()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "HelloKernel", BackendType.CPU, output);
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
    }

    // -----------------------------------------------------------------------
    // Test 7: Incremental — touching a source file forces ILGPUKernelCompile
    // to re-run, regenerating the manifest. The binary still produces the
    // expected output after the second build.
    // -----------------------------------------------------------------------

    public sealed class IncrementalRebuild(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_Incremental_RebuildsWhenSourceChanges()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "HelloKernel", BackendType.CPU, output);
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
    }

    // -----------------------------------------------------------------------
    // Test 8: dotnet clean removes the obj/.../ilgpu/ output directory via
    // the ILGPUClean target.
    // -----------------------------------------------------------------------

    public sealed class Clean(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Clean_RemovesIlgpuOutputDir()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "HelloKernel", BackendType.CPU, output);
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
    }

    // -----------------------------------------------------------------------
    // Test 9: InlineLambdaKernels — kernels written inline as lambdas at the
    // launch site are compiled into real kernels. The lambdas capture views,
    // buffers, scalars, structs and parameters, and cover 1D, 2D and grouped
    // launches from static and instance methods across three source files,
    // including a delegate parameter bound to the targets of its callers.
    // -----------------------------------------------------------------------

    public sealed class InlineLambdaKernels(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_InlineLambdaKernels_CompilesAndRuns(BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "InlineLambdaKernels", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stdout: {build.FullStdOut}\nStderr: {build.StdErr}");

                // Comments in front of a launch stay in front of the rewritten
                // launch and are not repeated inside its argument list.
                var rewrittenProgram = File.ReadAllText(
                    Path.Combine(project.RewrittenDir, "Program.cs"));
                Assert.Single(
                    rewrittenProgram.Split('\n'),
                    line => line.Contains(
                        "// A single captured view.", StringComparison.Ordinal));

                // Every inline kernel must be compiled, none may fall back to a
                // launcher stub that throws at runtime.
                foreach (var file in Directory.GetFiles(
                    project.GeneratedDir, "*_CompiledKernel.cs"))
                {
                    Assert.DoesNotContain(
                        "No Kernel", File.ReadAllText(file), StringComparison.Ordinal);
                }

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.True(run.ExitCode == 0,
                    $"Binary failed (exit {run.ExitCode}). Stderr: {run.StdErr}");
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected:
                    [
                        "A: done",
                        "B: 0 3 6 9",
                        "C: 10 11 12 13",
                        "D: 0 1 4 9",
                        "E: 5 6 7 8",
                        "F: 1 3 5 7",
                        "G: 10 110 210 310",
                        "H: 0 7 14 21",
                        "I: 0 1 10 11",
                        "J: 10 12 14 16",
                        "K: -1 2 5 8",
                        "L: 0 1 8 27",
                        "M: -20 -19 -12 7",
                        "N: 400 361 144 49",
                    ]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 10: InlineLambdaCaptureError — an inline lambda kernel that
    // captures 'this' fails the build with an ILGPUC diagnostic at the
    // offending source location instead of producing a binary.
    // -----------------------------------------------------------------------

    public sealed class InlineLambdaCaptureError(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_InlineLambdaCaptureError_FailsWithDiagnostic()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "InlineLambdaCaptureError", BackendType.CPU, output);
            try
            {
                var build = await project.BuildAsync();

                Assert.False(build.Succeeded,
                    "Expected dotnet build to FAIL for the inline kernel capturing " +
                    "'this', but it succeeded.");
                Assert.NotEqual(0, build.ExitCode);

                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.True(
                    combined.Contains(
                        "Program.cs(18,70): error ILGPU1101", StringComparison.Ordinal),
                    $"Expected an ILGPU1101 error at Program.cs(18,70), " +
                    $"got:\n{combined}");
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 11: LaunchSiteShapes — a public launching method capturing its
    // delegate parameter builds in an executable and runs the delegate passed
    // from another class, launches sharing their lines with other code keep the
    // rewritten source compilable, and a grouped launch passing values computed
    // from its index runs correctly.
    // -----------------------------------------------------------------------

    public sealed class LaunchSiteShapes(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_LaunchSiteShapes_CompilesAndRuns(BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "LaunchSiteShapes", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stdout: {build.FullStdOut}\nStderr: {build.StdErr}");

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.True(run.ExitCode == 0,
                    $"Binary failed (exit {run.ExitCode}). Stderr: {run.StdErr}");
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected:
                    [
                        "A: 5 6 7 8 9 10 11 12",
                        "B: 9 11 13 15 17 19 21 23",
                        "C: 0 1 2 3 10 11 12 13",
                    ]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 12: LibraryAlgorithms — the app kernel runs, and an ILGPU library
    // algorithm launching its kernel from inside ILGPU.dll reports that its
    // kernel was not compiled for the accelerator.
    // -----------------------------------------------------------------------

    public sealed class LibraryAlgorithms(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_LibraryAlgorithms_ReportsNotCompiledKernel(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "LibraryAlgorithms", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);

                var lines = run.StdOutLines;
                Assert.Equal(3, lines.Length);
                Assert.Equal("0 2 4 6", lines[0]);
                Assert.Equal("KernelNotCompiledException", lines[1]);
                Assert.StartsWith(
                    "Kernel 'ILGPU.Initialization.Initializer.Initialize' was not " +
                    $"compiled for {ExpectedAcceleratorTypeName(backend)}: ILGPUC " +
                    "did not compile its launch site.",
                    lines[2],
                    StringComparison.Ordinal);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 13: NestedGenericLaunch — generic kernels launched through generic
    // wrappers and from a generic class are compiled for every closed
    // instantiation, and a launch site that no closed instantiation reaches
    // produces warning ILGPU1001 as a real MSBuild warning.
    // -----------------------------------------------------------------------

    public sealed class NestedGenericLaunch(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_NestedGenericLaunch_WarnsForOpenSiteAndRuns(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "NestedGenericLaunch", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");

                // MSBuild's Exec task turns the canonical diagnostic line into a
                // build warning located at the launch site in Library.Run<T>.
                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.Contains(
                    "Program.cs(60,9): warning ILGPU1001",
                    combined,
                    StringComparison.Ordinal);
                Assert.Contains("Library.Run<T>", combined, StringComparison.Ordinal);

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected:
                    [
                        "2", "4", "6", "8",
                        "30", "60", "90", "120",
                        "2", "1", "4", "3",
                    ]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 14: SourceGenerated — the project only compiles with the code its
    // source generators produce. ILGPUC runs the generators, so it finds the
    // generic launch and the build and the binary succeed.
    // -----------------------------------------------------------------------

    public sealed class SourceGenerated(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_SourceGenerated_RunsGeneratorsAndRuns()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "SourceGenerated", BackendType.CPU, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");
                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.DoesNotContain("ILGPU1009", combined, StringComparison.Ordinal);
                Assert.Contains(
                    "--analyzers",
                    File.ReadAllText(project.ResponseFilePath),
                    StringComparison.Ordinal);

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(run.StdOutLines, expected: ["2", "4", "6", "8"]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 15: Incremental — changing an MSBuild property that ILGPUC reads
    // reruns ILGPUKernelCompile even though no source changed.
    // -----------------------------------------------------------------------

    public sealed class IncrementalPropertyChange(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_Incremental_RebuildsWhenPropertiesChange()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "HelloKernel", BackendType.CPU, output);
            try
            {
                var firstBuild = await project.BuildAsync();
                Assert.True(firstBuild.Succeeded);
                var firstMtime = File.GetLastWriteTimeUtc(project.ManifestPath);

                // Clear the filesystem's timestamp resolution
                await Task.Delay(TimeSpan.FromSeconds(2));
                var secondBuild = await project.BuildAsync(
                    new Dictionary<string, string>
                    {
                        ["ILGPUWarnOnOpenPublicEntryPoints"] = "true",
                    });
                Assert.True(secondBuild.Succeeded);
                var secondMtime = File.GetLastWriteTimeUtc(project.ManifestPath);

                Assert.True(secondMtime > firstMtime,
                    $"Expected manifest mtime to advance after changing a property. " +
                    $"First={firstMtime:O}, Second={secondMtime:O}");
                Assert.Contains(
                    "--warn-open-public-entry-points",
                    File.ReadAllText(project.ResponseFilePath),
                    StringComparison.Ordinal);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 16: TypeSpecialization — the type-specialized helper pattern from
    // issue #1427, built in Debug through the targets. The int instantiation
    // reached through a generic wrapper throws, so it is skipped with warning
    // ILGPU1006 and launching it throws at run time.
    // -----------------------------------------------------------------------

    public sealed class TypeSpecialization(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_TypeSpecialization_FoldsTypeTestsAndRuns(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "TypeSpecialization", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");

                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.Contains("warning ILGPU1006", combined, StringComparison.Ordinal);
                Assert.Contains("Kernel2<int>", combined, StringComparison.Ordinal);

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected:
                    [
                        "0.4794", "0.8415",
                        "0.4794", "0.8415",
                        "KernelNotCompiledException",
                    ]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 17: TypeSpecializationError — a direct launch of an instantiation
    // whose helper throws must fail the build with a named error.
    // -----------------------------------------------------------------------

    public sealed class TypeSpecializationError(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_TypeSpecializationError_FailsWithNamedError()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "TypeSpecializationError", BackendType.CPU, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.False(build.Succeeded,
                    "Expected dotnet build to fail for the throwing instantiation.");

                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.Contains("GenericSin<Int32>", combined, StringComparison.Ordinal);
                Assert.Contains(
                    "Exceptions are not supported in kernels",
                    combined,
                    StringComparison.Ordinal);
                Assert.Contains(
                    "'NotSupportedException'",
                    combined,
                    StringComparison.Ordinal);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 18: CrossAssemblyLaunch — an application calls a library's public
    // generic method with type arguments the library does not use itself. The
    // application's build compiles those kernels and registers them; the
    // library's dispatch code launches them.
    // -----------------------------------------------------------------------

    public sealed class CrossAssemblyLaunch(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_CrossAssemblyLaunch_RunsKernelsCompiledByTheApplication(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "CrossAssemblyLaunch", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");
                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.DoesNotContain("ILGPU1001", combined, StringComparison.Ordinal);

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected: ["3", "6", "3", "4", "10", "12"]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 19: CrossAssemblyLambdaError — a library launches an inline lambda
    // kernel from a public generic method, which applications cannot compile
    // for their own type arguments. The build fails with error ILGPU1012 at
    // the lambda.
    // -----------------------------------------------------------------------

    public sealed class CrossAssemblyLambdaError(ITestOutputHelper output)
    {
        [SkippableFact]
        public async Task Build_CrossAssemblyLambdaError_FailsWithDiagnostic()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "CrossAssemblyLambdaError", BackendType.CPU, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.False(build.Succeeded,
                    "Expected dotnet build to fail for the inline kernel in a " +
                    "public generic method.");
                Assert.NotEqual(0, build.ExitCode);

                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.True(
                    combined.Contains(
                        "Library.cs(16,39): error ILGPU1012", StringComparison.Ordinal),
                    $"Expected an ILGPU1012 error at Library.cs(16,39), " +
                    $"got:\n{combined}");
                Assert.Contains(
                    "Filler.Fill<T>",
                    combined,
                    StringComparison.Ordinal);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 20: OptimizedKernels — with Optimize=true, as in Release builds,
    // kernels are compiled from optimized IL, which emits 0/1 selections as
    // comparisons. Without it, the response file does not request optimized IL.
    // -----------------------------------------------------------------------

    public sealed class OptimizedKernels(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_Optimized_CompilesKernelsFromOptimizedILAndRuns(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "OptimizedKernels", backend, output);
            try
            {
                var build = await project.BuildAsync(
                    new Dictionary<string, string> { ["Optimize"] = "true" });
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");
                Assert.Contains("--optimize", project.ReadResponseFile());

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected: ["2", "2", "1", "0", "0", "1", "0", "1"]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }

        [SkippableFact]
        public async Task Build_NotOptimized_DoesNotRequestOptimizedIL()
        {
            await RequireBackendAsync(BackendType.CPU);

            using var project = new IntegrationProjectFixture(
                "OptimizedKernels", BackendType.CPU, output);
            try
            {
                var build = await project.BuildAsync(
                    new Dictionary<string, string> { ["Optimize"] = "false" });
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");
                Assert.DoesNotContain("--optimize", project.ReadResponseFile());
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Test 21: CrossAssemblyTypeSpecialization — the type-specialized helper
    // from issue #1427 lives in a referenced library without ILGPU. Kernels of
    // the application and of a library launching from a public generic method
    // call it; the int instantiation is skipped with warning ILGPU1006.
    // -----------------------------------------------------------------------

    public sealed class CrossAssemblyTypeSpecialization(ITestOutputHelper output)
    {
        [SkippableTheory]
        [InlineData(BackendType.CPU)]
        [InlineData(BackendType.Metal)]
        public async Task Build_CrossAssemblyTypeSpecialization_FoldsExternalTypeTests(
            BackendType backend)
        {
            await RequireBackendAsync(backend);

            using var project = new IntegrationProjectFixture(
                "CrossAssemblyTypeSpecialization", backend, output);
            try
            {
                var build = await project.BuildAsync();
                Assert.True(build.Succeeded,
                    $"dotnet build failed (exit {build.ExitCode}). " +
                    $"Stderr: {build.StdErr}");

                var combined = build.FullStdOut + "\n" + build.StdErr;
                Assert.Contains("warning ILGPU1006", combined, StringComparison.Ordinal);
                Assert.Contains("Kernels.Sin<int>", combined, StringComparison.Ordinal);
                Assert.DoesNotContain("ILGPU1001", combined, StringComparison.Ordinal);

                var run = await project.RunAsync();
                Assert.False(run.TimedOut, "Binary timed out");
                Assert.Equal(0, run.ExitCode);
                OutputVerifier.Verify(
                    run.StdOutLines,
                    expected:
                    [
                        "0.4794", "0.8415",
                        "0.4794", "0.8415",
                        "0.4794",
                        "KernelNotCompiledException",
                    ]);
            }
            catch
            {
                project.MarkFailed();
                throw;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task RequireBackendAsync(BackendType backend)
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(backend).ConfigureAwait(false),
            $"Native compiler for {backend} is not available on this machine");
        Skip.IfNot(
            Availability.IsRuntimeAvailable(backend),
            $"No {backend} runtime device available on this machine");
    }

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
