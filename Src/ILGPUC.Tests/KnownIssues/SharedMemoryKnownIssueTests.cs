// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryKnownIssueTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.KnownIssues;

/// <summary>
/// Regression tests pinned to shared-memory bugs whose failure shape does
/// NOT fit the auto-registered <c>Kernels/</c> + <c>BackendTests</c> flow:
///
///  * <see cref="SourceGeneration_SharedMemory1DTiled_RuntimeExtent_Rejected"/>
///    — the 1D shared-memory pattern with a parameter-dependent extent is
///    explicitly unsupported by the backends; the frontend must report it
///    as a clear <see cref="NotSupportedException"/> rather than silently
///    crashing.
///
///  * <see cref="SourceGeneration_SharedMemory2D_RuntimeExtent_Rejected"/>
///    — same negative form for the 2D path.
///
///  * <see cref="Build_SharedMemory2D_Succeeds"/> — end-to-end MSBuild
///    lock for the 2D shared-memory pattern. Used to fail with CS0103 on
///    an undefined helper; now green and pinned via the real ILGPU
///    MSBuild integration.
///
/// Positive-shape source-generation regressions (the tiled 1D and 2D
/// kernels) have been migrated to <c>Kernels/SharedMemoryKernels.cs</c>
/// so they pick up free coverage on all 5 backends via the auto-registered
/// <c>BackendTests</c> theory. This file retains only the negative tests
/// and the end-to-end build assertion.
///
/// Runs on the CPU backend because the frontend / IR / C# emission stages
/// being exercised are backend-invariant.
/// </summary>
public sealed class SharedMemoryKnownIssueTests : BackendTestBase
{
    private readonly ITestOutputHelper _output;

    public SharedMemoryKnownIssueTests(ITestOutputHelper output)
        : base(output, BackendType.CPU)
    {
        _output = output;
    }

    /// <summary>
    /// 1D shared-memory kernel with a parameter-dependent extent — backends
    /// cannot emit fixed-size shared arrays for this, so the frontend must
    /// raise a clear <see cref="NotSupportedException"/> at the intrinsic
    /// boundary instead of producing an opaque internal-compiler error
    /// downstream.
    /// </summary>
    [Fact]
    public void SourceGeneration_SharedMemory1DTiled_RuntimeExtent_Rejected()
    {
        var kernel = GetKernel(
            typeof(KnownIssueKernels),
            nameof(KnownIssueKernels.SharedMemory1DTiledKernel_RuntimeExtent));
        // location.GetNotSupportedException wraps the NotSupportedException
        // in an InternalCompilerException; assert on the wrapped inner message.
        var ex = Assert.ThrowsAny<Exception>(
            () => AssertSourceGenerationSucceeds(kernel));
        var inner = ex.InnerException ?? ex;
        Assert.IsType<NotSupportedException>(inner);
        Assert.Contains(
            "compile-time constant extent",
            inner.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Negative 2D form: passing an <c>Index2D</c> derived from a kernel
    /// parameter (not a <c>new Index2D(N, M)</c> literal) must raise a
    /// clear <see cref="NotSupportedException"/> at the intrinsic boundary
    /// instead of producing an opaque failure downstream.
    /// </summary>
    [Fact]
    public void SourceGeneration_SharedMemory2D_RuntimeExtent_Rejected()
    {
        var kernel = GetKernel(
            typeof(KnownIssueKernels),
            nameof(KnownIssueKernels.SharedMemory2DKernel_RuntimeExtent));
        var ex = Assert.ThrowsAny<Exception>(
            () => AssertSourceGenerationSucceeds(kernel));
        var inner = ex.InnerException ?? ex;
        Assert.IsType<NotSupportedException>(inner);
        Assert.Contains(
            "compile-time constant extent",
            inner.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Drives an end-to-end <c>dotnet build</c> of
    /// <c>IntegrationProjects/SharedMemory2D</c>, whose kernel calls
    /// <c>Group.GetSharedMemory2D&lt;int, Stride2D.DenseX&gt;</c>. Previously
    /// failed at C# compile time with <c>CS0103
    /// method_GetSharedMemory2D_*</c> (undefined helper reference) plus
    /// <c>CPURuntimeView&lt;T&gt;</c> conversion / struct-field errors;
    /// now emits a concrete <c>ArrayView2D</c> struct directly from the
    /// intrinsic and compiles cleanly.
    /// </summary>
    [Fact]
    public async Task Build_SharedMemory2D_Succeeds()
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(BackendType.CPU),
            "CPU backend is not available on this machine");

        using var project = new IntegrationProjectFixture(
            "SharedMemory2D", BackendType.CPU, _output);
        try
        {
            var build = await project.BuildAsync();
            Assert.True(
                build.Succeeded,
                $"dotnet build of SharedMemory2D failed (exit {build.ExitCode})."
                + $" Stderr: {build.StdErr}");
        }
        catch
        {
            project.MarkFailed();
            throw;
        }
    }
}
