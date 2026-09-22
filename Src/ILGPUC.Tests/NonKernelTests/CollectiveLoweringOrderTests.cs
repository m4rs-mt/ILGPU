// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CollectiveLoweringOrderTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that a group broadcast lowered to shared memory keeps its barriers around
/// the load of its result: the barrier after the load keeps the next broadcast from
/// the same site, such as in the next iteration of a loop, from overwriting shared
/// memory before every thread read it.
/// </summary>
public sealed class CollectiveLoweringOrderTests
{
    #region Kernels

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry
    private static class Kernels
    {
        public static void BroadcastInLoopKernel(ArrayView1D<int, Stride1D.Dense> data)
        {
            int sum = 0;
            for (int i = 0; i < data.IntLength; ++i)
                sum += Group.Broadcast(data[i] + Group.Index, i);
            data[Group.Index] = sum;
        }
    }

    /// <summary>
    /// Returns the lines of the IR of the given kernel after the transformations of
    /// the given backend.
    /// </summary>
    private static string[] GetBackendIR(string kernelName, BackendType backend)
    {
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);
        return helper
            .GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms, backend: backend)
            .Split('\n');
    }

    /// <summary>
    /// Returns true if the given IR line is a group barrier.
    /// </summary>
    private static bool IsBarrier(string line) =>
        line.Contains("@ilgpu.barrier.group", StringComparison.Ordinal);

    /// <summary>
    /// Returns true if the given IR line is a load.
    /// </summary>
    private static bool IsLoad(string line) =>
        line.Contains("= load ", StringComparison.Ordinal);

    #endregion

    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.Metal)]
    public void GroupBroadcast_LoadsItsResultBetweenItsBarriers(BackendType backend)
    {
        var lines = GetBackendIR(nameof(Kernels.BroadcastInLoopKernel), backend);

        var barriers = Enumerable.Range(0, lines.Length)
            .Where(i => IsBarrier(lines[i]))
            .ToArray();
        Assert.Equal(2, barriers.Length);
        Assert.Contains(
            Enumerable.Range(barriers[0] + 1, barriers[1] - barriers[0] - 1),
            i => IsLoad(lines[i]));
    }
}
