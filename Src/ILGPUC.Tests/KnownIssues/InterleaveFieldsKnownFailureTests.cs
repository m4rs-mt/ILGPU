// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterleaveFieldsKnownFailureTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.KnownIssues;

/// <summary>
/// Holding pen for B.2 (open): cross-type struct assignment fails IR→native
/// source emission on CUDA / ROCm / OpenCL. Tracked in
/// <c>Src/plans/fix_samples.md</c> family B.2.
///
/// Two assertions:
///
///  * <see cref="NativeCompilation_InterleaveFields_PinsB2"/> — when the
///    fix lands, this test removes the <c>Skip</c> and the in-process
///    native-compile path proves the fix end-to-end. While the bug is
///    open, the kernel sits in
///    <c>Kernels/InterleaveFieldsKernels.cs</c> with a
///    <see cref="KnownFailingOnAttribute"/> so <c>BackendTests</c>'
///    <c>NativeCompilation</c> theory skips cleanly and CI stays green.
///
///  * <see cref="Build_InterleaveFieldsSample_PinsB2"/> — the
///    <c>dotnet build Samples/InterleaveFields ... -p:ILGPUCompile=true</c>
///    round-trip. Skip-flagged for the same reason — flips green when the
///    fix lands.
///
/// To prove a fix:
///   1. Remove the <c>[KnownFailingOn]</c> attribute on
///      <c>InterleaveFieldsKernels.InterleaveFieldsKernel</c>
///      (and on <c>MatrixMultiplyKernels.MatrixMultiplyTiledKernel</c>,
///      which exercises the same shape).
///   2. Remove the <c>Skip</c> on both tests below.
///   3. Re-run BackendTests + this file's tests; both must go green.
/// </summary>
public sealed class InterleaveFieldsKnownFailureTests : BackendTestBase
{
    private readonly ITestOutputHelper _output;

    public InterleaveFieldsKnownFailureTests(ITestOutputHelper output)
        : base(output, BackendType.CPU)
    {
        _output = output;
    }

    /// <summary>
    /// In-process native-compile assertion against the registered
    /// <see cref="InterleaveFieldsKernels.InterleaveFieldsKernel"/>. Targets
    /// CUDA because the open bug fires identically on Cuda / ROCm / OpenCL,
    /// and the local Docker compiler service routes CUDA on
    /// <c>http://localhost:5001</c> by default.
    /// </summary>
    [SkippableFact(Skip = "B.2 pending — see Src/plans/fix_samples.md family B.2")]
    public async Task NativeCompilation_InterleaveFields_PinsB2()
    {
        var kernel = GetKernel(
            typeof(InterleaveFieldsKernels),
            nameof(InterleaveFieldsKernels.InterleaveFieldsKernel));
        // When un-Skipped: this currently throws on Cuda / ROCm / OpenCL
        // with `error: no operator "=" matches these operands`. The fix-PR
        // makes this pass.
        var helper = new CompilationHelper(BackendType.Cuda);
        try
        {
            var available = await Availability.IsCompilerAvailableAsync(BackendType.Cuda);
            Skip.IfNot(available,
                "CUDA compiler unavailable — start the Docker service "
                + "(Src/docker/run.sh) or install nvcc locally.");

            var result = await helper.CompileToNativeAsync(kernel, BackendType.Cuda);
            Assert.NotNull(result);
            Assert.True(
                result!.Success,
                $"B.2 native compile of InterleaveFields failed: {result.StdErr}");
        }
        finally
        {
            helper.Dispose();
        }
    }

    /// <summary>
    /// End-to-end <c>dotnet build Samples/InterleaveFields</c> with
    /// <c>-p:ILGPUCompile=true</c>. Routes CUDA through the local Docker
    /// service (port 5001 — the same default <c>fix_samples.md</c>
    /// documents). When the fix lands, removing the <c>Skip</c> reveals a
    /// green build and an embedded <c>KernelBinary</c>.
    /// </summary>
    [SkippableFact(Skip = "B.2 pending — see Src/plans/fix_samples.md family B.2")]
    public async Task Build_InterleaveFieldsSample_PinsB2()
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(BackendType.Cuda),
            "CUDA compiler unavailable — start the Docker service "
            + "(Src/docker/run.sh) or install nvcc locally.");

        var samplePath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "Samples", "InterleaveFields");
        var fullPath = Path.GetFullPath(samplePath);
        Assert.True(
            Directory.Exists(fullPath),
            $"Samples/InterleaveFields not found at {fullPath}");

        var properties = new Dictionary<string, string>
        {
            ["ILGPUBackend"] = "Cuda",
            ["ILGPUCompile"] = "true",
            ["ILGPUCompilerServices"] = "http://localhost:5001",
        };
        var build = await MsBuildRunner.BuildAsync(fullPath, properties: properties);
        Assert.True(
            build.Succeeded,
            $"dotnet build Samples/InterleaveFields failed (exit {build.ExitCode})."
            + $" Stderr: {build.StdErr}");
    }
}
