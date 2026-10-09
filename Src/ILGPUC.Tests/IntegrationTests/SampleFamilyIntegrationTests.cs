// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SampleFamilyIntegrationTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IntegrationTests;

/// <summary>
/// End-to-end MSBuild integration tests for sample-portability bug families.
/// Each test pins a specific bug family that was fixed on <c>temp5</c> by
/// driving a real <c>dotnet build</c> over an <c>IntegrationProjects/</c>
/// template that mirrors the corresponding sample under <c>Samples/</c>.
///
/// Complements the family-specific <c>BackendTests</c> + <c>ExecutionTests</c>
/// rows by exercising the full MSBuild round-trip — the path that
/// <c>build-samples</c> CI hits.
/// </summary>
public sealed class SampleFamilyIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public SampleFamilyIntegrationTests(ITestOutputHelper output)
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

    /// <summary>
    /// Family A.1 — launcher view-of-struct marshalling. Builds and runs a
    /// project whose kernel takes <c>ArrayView&lt;ComposedView&gt;</c> and
    /// reinterprets it via <c>Cast&lt;byte&gt;().SubView(...).Cast&lt;int&gt;()</c>.
    /// Pins the FlatStructLauncherEmitter regression.
    /// </summary>
    [SkippableTheory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    public async Task Build_AdvancedViews_PinsFamilyA1(BackendType backend)
    {
        await RequireBackendAsync(backend);

        using var project = new IntegrationProjectFixture(
            "AdvancedViews", backend, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build of AdvancedViews failed (exit {build.ExitCode})." +
                $" Stderr: {build.StdErr}");

            var run = await project.RunAsync();
            Assert.False(run.TimedOut, "Binary timed out");
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(run.StdOutLines, expected: ["1024"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    /// <summary>
    /// Family A.2 — generic-kernel struct-closure marshalling. Builds and
    /// runs a project whose kernel is generic over a closure type with a
    /// captured field. Pins the FlatStructLauncherEmitter regression for
    /// generic kernels.
    /// </summary>
    [SkippableTheory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    public async Task Build_GenericKernel_PinsFamilyA2(BackendType backend)
    {
        await RequireBackendAsync(backend);

        using var project = new IntegrationProjectFixture(
            "GenericKernel", backend, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build of GenericKernel failed (exit {build.ExitCode})." +
                $" Stderr: {build.StdErr}");

            var run = await project.RunAsync();
            Assert.False(run.TimedOut, "Binary timed out");
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(
                run.StdOutLines, expected: ["20", "21", "22", "23"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    /// <summary>
    /// Family B.1 — Float64 atomic add via <c>Atomic.MakeAtomic</c>. Pins
    /// the three emitter defects fixed on <c>temp5</c> (self-loop
    /// classification, FloatAsIntCast/IntAsFloatCast dispatch arms,
    /// element-type-aware pointer-cast emission).
    /// CPU only — Metal lacks Float64 atomics; OpenCL Float64 atomics need
    /// <c>cl_khr_int64_base_atomics</c> (pre-existing limitation).
    /// </summary>
    [SkippableFact]
    public async Task Build_AtomicsFloat64_PinsFamilyB1()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "AtomicsFloat64", BackendType.CPU, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build of AtomicsFloat64 failed (exit {build.ExitCode})." +
                $" Stderr: {build.StdErr}");

            var run = await project.RunAsync();
            Assert.False(run.TimedOut, "Binary timed out");
            Assert.Equal(0, run.ExitCode);
            OutputVerifier.Verify(run.StdOutLines, expected: ["10"]);
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }

    /// <summary>
    /// MatrixMultiply tiled — exercises grouped <see cref="KernelIndex"/> +
    /// <c>Group.GetSharedMemory2D</c> + <see cref="Group.Barrier"/> through
    /// the full MSBuild round-trip. Build-only assertion: the CPU backend
    /// cannot honour <see cref="Group.Barrier"/> semantics under its
    /// per-thread serial launcher, so the binary's stdout is intentionally
    /// not verified — the regression target is source-emission and the
    /// launcher signature, both validated at build time.
    /// </summary>
    [SkippableFact]
    public async Task Build_MatrixMultiplyTiled_PinsTiledShape()
    {
        await RequireBackendAsync(BackendType.CPU);

        using var project = new IntegrationProjectFixture(
            "MatrixMultiplyTiled", BackendType.CPU, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(build.Succeeded,
                $"dotnet build of MatrixMultiplyTiled failed (exit {build.ExitCode})." +
                $" Stderr: {build.StdErr}");
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }
}
