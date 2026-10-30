// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NativeCompilationExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Compilers;
using ILGPUC.Tests.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Tests that a kernel the native compiler rejects fails the build of a test program
/// with the message of the native compiler.
/// </summary>
public abstract class NativeCompilationExecutionTests : ExecutionTestBase
{
    /// <summary>
    /// The error the native compiler reports for every kernel compiled by
    /// <see cref="RejectingCompilerManager"/>.
    /// </summary>
    private const string RejectionMessage = "kernel rejected by the native compiler";

    /// <summary>
    /// Compiles every kernel with an error directive appended to its source, so that
    /// the native compiler of the backend rejects it.
    /// </summary>
    private sealed class RejectingCompilerManager(ICompilerManager manager)
        : ICompilerManager
    {
        public Task<CompilationResult> CompileAsync(
            CompileRequest request,
            CancellationToken ct) =>
            manager.CompileAsync(
                new CompileRequest
                {
                    SourceCode = request.SourceCode + Environment.NewLine +
                        $"#error {RejectionMessage}" + Environment.NewLine,
                    Target = request.Target,
                    OutputType = request.OutputType,
                    OptimizationLevel = request.OptimizationLevel,
                    IncludePaths = request.IncludePaths,
                    CompilerDirectives = request.CompilerDirectives,
                    ExtraFlags = request.ExtraFlags,
                },
                ct);

        public Task<CompilationCapabilities> GetCapabilitiesAsync(
            CancellationToken ct) =>
            manager.GetCapabilitiesAsync(ct);
    }

    private readonly ITestOutputHelper _output;

    protected NativeCompilationExecutionTests(
        ITestOutputHelper output,
        BackendType backend)
        : base(output, backend)
    {
        _output = output;
    }

    /// <summary>
    /// A kernel the native compiler rejects fails the build instead of embedding its
    /// source, which the driver would compile again when the program runs.
    /// </summary>
    [SkippableFact]
    public async Task RejectedKernel_FailsBuildWithCompilerMessage()
    {
        var target = Availability.MapToTarget(Backend);
        Skip.If(target is null, $"{Backend} kernels are not compiled natively");
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(Backend),
            $"Native compiler for {Backend} is not available on this machine");

        var manager = await CompilerManagerFactory.ResolveAsync(target.Value);
        using var builder = new ProgramBuilder(
            Backend, _output, new RejectingCompilerManager(manager));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.BuildExecutable(
                [GetTestProgramPath("TestPrograms/BasicIf/IfTrue.cs")],
                ["Kernels.IfTrueKernel"]));
        Assert.Contains(RejectionMessage, exception.Message);
    }
}
