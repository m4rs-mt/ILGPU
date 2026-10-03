// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryPerThreadSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that shared memory per thread and per warp of a group is declared as
/// an array sized for the largest group of the target.
/// </summary>
public sealed class SharedMemoryPerThreadSourceTests
{
    /// <summary>
    /// Allocates shared memory per thread, per thread with a multiplier and per
    /// warp of the group.
    /// </summary>
    internal static void PerThreadKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var perThread = Group.GetSharedMemoryPerThread<int>();
        var pairs = Group.GetSharedMemoryPerThread<long>(2);
        var perWarp = Group.GetSharedMemoryPerWarp<short>();
        perThread[Group.Index] = index;
        pairs[Group.Index * 2 + 1] = index;
        perWarp[Warp.Index] = (short)Warp.Index;
        Group.Barrier();
        data[index] = perThread[Group.Dimension - 1 - Group.Index] +
            (int)pairs[Group.Index * 2 + 1] + perWarp[Warp.Index];
    }

    /// <summary>
    /// Every allocation is declared as an array of the number of its elements
    /// for the largest group of the target, of 1024 threads in warps of 32
    /// threads (Metal, Cuda, ROCm gfx1100) or 16 threads (OpenCL on Intel).
    /// </summary>
    [Theory]
    [InlineData(BackendType.Metal, 32)]
    [InlineData(BackendType.Cuda, 32)]
    [InlineData(BackendType.ROCm, 32)]
    [InlineData(BackendType.OpenCL, 64)]
    public void PerThreadAndPerWarp_AreSizedForLargestGroup(
        BackendType backend,
        int perWarpLength)
    {
        var kernel = typeof(SharedMemoryPerThreadSourceTests).GetMethod(
            nameof(PerThreadKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);

        var source = helper.GenerateBackendCode(kernel, backend).SourceCode;
        Assert.Matches(@"\bint global_\d+\[1024\]", source);
        Assert.Matches(@"\blong global_\d+\[2048\]", source);
        Assert.Matches(@"\bshort global_\d+\[" + perWarpLength + @"\]", source);
    }
}
