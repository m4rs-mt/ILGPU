// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpBarrierSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System.Text.RegularExpressions;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that <c>Warp.Barrier()</c> synchronizes only the lanes of a warp and
/// <c>Group.Barrier()</c> the whole group on every backend.
/// </summary>
public sealed class WarpBarrierSourceTests
{
    private const string CpuWarpBarrier =
        "/* warp barrier - vectorized execution, no synchronization needed */";
    private const string CpuGroupBarrier =
        "/* barrier - vectorized execution, no synchronization needed */";
    private const string RocmWarpBarrier =
        "__builtin_amdgcn_fence(__ATOMIC_RELEASE, \"wavefront\"); " +
        "__builtin_amdgcn_wave_barrier(); " +
        "__builtin_amdgcn_fence(__ATOMIC_ACQUIRE, \"wavefront\")";

    /// <summary>
    /// The warp barrier and group barrier of each backend.
    /// </summary>
    public static TheoryData<BackendType, string, string> Barriers => new()
    {
        { BackendType.CPU, CpuWarpBarrier, CpuGroupBarrier },
        { BackendType.Cuda, "__syncwarp()", "__syncthreads()" },
        { BackendType.ROCm, RocmWarpBarrier, "__syncthreads()" },
        {
            BackendType.Metal,
            "simdgroup_barrier(mem_flags::mem_device | mem_flags::mem_threadgroup)",
            "threadgroup_barrier(mem_flags::mem_threadgroup)"
        },
        {
            BackendType.OpenCL,
            "sub_group_barrier(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)",
            "barrier(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)"
        },
    };

    /// <summary>
    /// <c>Warp.Barrier()</c> emits the warp barrier and no group barrier.
    /// </summary>
    [Theory]
    [MemberData(nameof(Barriers))]
    public void WarpBarrier_EmitsWarpScopedBarrier(
        BackendType backend,
        string warpBarrier,
        string groupBarrier)
    {
        var source = GenerateSource(backend, nameof(WarpKernels.WarpBarrierKernel));
        Assert.Contains(warpBarrier, source);
        Assert.False(
            ContainsBarrier(source, groupBarrier),
            $"Unexpected group barrier '{groupBarrier}' in:\n{source}");
    }

    /// <summary>
    /// <c>Group.Barrier()</c> emits the group barrier and no warp barrier.
    /// </summary>
    [Theory]
    [MemberData(nameof(Barriers))]
    public void GroupBarrier_EmitsGroupScopedBarrier(
        BackendType backend,
        string warpBarrier,
        string groupBarrier)
    {
        var source = GenerateSource(backend, nameof(GroupKernels.GroupBarrierKernel));
        Assert.True(
            ContainsBarrier(source, groupBarrier),
            $"Missing group barrier '{groupBarrier}' in:\n{source}");
        Assert.DoesNotContain(warpBarrier, source);
    }

    /// <summary>
    /// Returns true if the source contains the barrier as a call of its own, so
    /// that OpenCL's <c>barrier(</c> does not match <c>sub_group_barrier(</c>.
    /// </summary>
    private static bool ContainsBarrier(string source, string barrier) =>
        Regex.IsMatch(source, @"(?<!\w)" + Regex.Escape(barrier));

    /// <summary>
    /// Generates the source of a kernel of <see cref="WarpKernels"/> or
    /// <see cref="GroupKernels"/> for the given backend.
    /// </summary>
    private static string GenerateSource(BackendType backend, string kernelName)
    {
        var kernel = typeof(WarpKernels).GetMethod(kernelName)
            ?? typeof(GroupKernels).GetMethod(kernelName)!;
        using var helper = new CompilationHelper(backend);
        return helper.GenerateBackendCode(kernel, backend).SourceCode;
    }
}
