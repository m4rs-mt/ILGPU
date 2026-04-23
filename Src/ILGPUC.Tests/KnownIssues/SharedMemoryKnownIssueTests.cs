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

using System.Threading.Tasks;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.KnownIssues;

/// <summary>
/// Regression tests pinned to open ILGPUC codegen bugs in the shared-memory
/// code path. Each test is currently skipped — remove the <c>Skip</c>
/// argument when the corresponding bug is fixed. Both tests assert the
/// end state (successful source generation / successful build), so the
/// skipped test flips to green the moment the bug is resolved.
///
/// Layered at the cheapest pipeline stage that actually catches each bug:
///
///  * Bug B (<c>Group.GetSharedMemory&lt;T&gt;(int)</c> + 2D-index arithmetic
///    in a grouped <see cref="KernelIndex"/> launch) crashes the frontend,
///    so an in-process source-generation check on the CPU backend catches it.
///
///  * Bug A (<c>Group.GetSharedMemory2D</c> codegen) emits a string of C#
///    that parses but references an undefined helper; the failure only
///    surfaces when the emitted file is compiled. The CPU backend has no
///    native compile step, so this one lives at the MSBuild-integration
///    layer — materialize <c>IntegrationProjects/SharedMemory2D</c>, run
///    <c>dotnet build</c> as a subprocess, assert success. Same pattern as
///    <see cref="IntegrationTests.MsBuildIntegrationTests"/>.
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
    /// <see cref="KnownIssueKernels.SharedMemory1DTiledKernel"/> — currently
    /// throws <c>ILGPUC.InternalCompilerException</c> wrapping an
    /// <c>InvalidOperationException</c> from
    /// <c>PureValueBuilder.CreateConvert</c>
    /// (<c>targetType.BasicValueType == BasicValueType.None</c>) while
    /// storing into a local during IL → IR lowering. When fixed, source
    /// generation should complete and produce non-empty output.
    /// </summary>
    [Fact(Skip =
        "Pending ILGPUC fix: Group.GetSharedMemory<T>(int) indexed with int "
        + "arithmetic (`row * tileSize + col`) inside a grouped KernelIndex "
        + "launch crashes the frontend at PureValueBuilder.CreateConvert — "
        + "targetType.BasicValueType == BasicValueType.None.")]
    public void SourceGeneration_SharedMemory1DTiled() =>
        AssertSourceGenerationSucceeds(
            GetKernel(
                typeof(KnownIssueKernels),
                nameof(KnownIssueKernels.SharedMemory1DTiledKernel)));

    /// <summary>
    /// Drives an end-to-end <c>dotnet build</c> of
    /// <c>IntegrationProjects/SharedMemory2D</c>, whose kernel calls
    /// <c>Group.GetSharedMemory2D&lt;int, Stride2D.DenseX&gt;</c>. Source
    /// generation currently succeeds (so the in-process Stage 2 check can't
    /// catch this) but C# compilation of the emitted
    /// <c>*_CompiledKernel.cs</c> fails with <c>CS0103
    /// method_GetSharedMemory2D_*</c> (undefined helper reference), plus
    /// <c>CPURuntimeView&lt;T&gt;</c> conversion / struct-field errors.
    /// First observed when compiling the tiled variant of
    /// <c>Samples/MatrixMultiply</c>.
    /// </summary>
    [Fact(Skip =
        "Pending ILGPUC fix: Group.GetSharedMemory2D<T, TStride> codegen "
        + "emits an undefined 'method_GetSharedMemory2D_*' helper and invalid "
        + "CPURuntimeView<T> conversions in the generated *_CompiledKernel.cs. "
        + "Surfaces at C# compile time, not at source emit — tested via an "
        + "end-to-end dotnet build of IntegrationProjects/SharedMemory2D.")]
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
