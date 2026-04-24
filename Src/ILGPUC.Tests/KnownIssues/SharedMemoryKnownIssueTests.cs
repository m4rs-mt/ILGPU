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
/// Regression tests pinned to shared-memory code-path bugs that previously
/// produced opaque internal-compiler-errors. Layered at the cheapest
/// pipeline stage that actually catches each bug:
///
///  * <see cref="SourceGeneration_SharedMemory1DTiled"/> — the previously
///    crashing grouped <see cref="KernelIndex"/> + shared-memory tiled
///    pattern now compiles end-to-end once the extent is a
///    compile-time constant (see <c>KnownIssueKernels.TileSize</c>).
///
///  * <see cref="SourceGeneration_SharedMemory1DTiled_RuntimeExtent_Rejected"/>
///    — the same pattern with a parameter-dependent extent is explicitly
///    unsupported by the backends; the frontend must report it as a clear
///    <see cref="NotSupportedException"/> rather than silently crashing.
///
///  * <see cref="SourceGeneration_SharedMemory2D"/> — Bug A's cheaper
///    sibling: confirms the frontend now emits a concrete
///    <see cref="IR.ModuleValues.StructureType"/> for
///    <c>Group.GetSharedMemory2D&lt;T, TStride&gt;</c> instead of leaving
///    behind a dangling <c>method_GetSharedMemory2D_*</c> call.
///
///  * <see cref="SourceGeneration_SharedMemory2D_RuntimeExtent_Rejected"/>
///    — negative form: asserts a parameter-dependent 2D extent raises a
///    clear frontend diagnostic at the intrinsic boundary.
///
///  * <see cref="Build_SharedMemory2D_Succeeds"/> — Bug A's end-to-end
///    lock: the emitted <c>*_CompiledKernel.cs</c> must compile under the
///    real ILGPU MSBuild integration. Used to fail with CS0103 on the
///    undefined helper; now green.
///
/// Runs on the CPU backend because all four bugs live in the shared
/// frontend / IR / C# emission stages.
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
    /// Grouped <see cref="KernelIndex"/> kernel with a compile-time-constant
    /// shared-memory extent compiles to non-empty backend source.
    /// </summary>
    [Fact]
    public void SourceGeneration_SharedMemory1DTiled() =>
        AssertSourceGenerationSucceeds(
            GetKernel(
                typeof(KnownIssueKernels),
                nameof(KnownIssueKernels.SharedMemory1DTiledKernel)));

    /// <summary>
    /// Same kernel shape but with a parameter-dependent shared-memory extent
    /// — backends cannot emit fixed-size shared arrays for this, so the
    /// frontend raises a clear <see cref="NotSupportedException"/> at the
    /// intrinsic boundary instead of producing an opaque internal-compiler
    /// error downstream.
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
    /// Source generation for the 2D shared-memory kernel — cheaper than the
    /// end-to-end build and catches regressions in the frontend intrinsic
    /// handler before they reach MSBuild.
    /// </summary>
    [Fact]
    public void SourceGeneration_SharedMemory2D() =>
        AssertSourceGenerationSucceeds(
            GetKernel(
                typeof(KnownIssueKernels),
                nameof(KnownIssueKernels.SharedMemory2DKernel)));

    /// <summary>
    /// Negative form: passing an <c>Index2D</c> derived from a kernel
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
