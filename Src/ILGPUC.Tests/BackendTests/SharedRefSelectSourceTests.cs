// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedRefSelectSourceTests.cs
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
/// Checks that a reference selected from two elements of the same shared memory
/// array keeps the shared address space on backends without a generic one.
/// </summary>
public sealed class SharedRefSelectSourceTests
{
    /// <summary>
    /// A reference selects one of two elements of shared memory per thread.
    /// </summary>
    internal static void SharedRefSelectSameArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx] = 10 * (groupIdx + 1);
            shared[groupIdx + 32] = groupIdx;
        }
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref shared[groupIdx + 32];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx] + shared[groupIdx + 32];
    }

    /// <summary>
    /// The selected reference is declared in the shared address space instead
    /// of the global one.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Metal, "threadgroup int* tmp_", "device int* tmp_")]
    [InlineData(BackendType.OpenCL, "local int* tmp_", "global int* tmp_")]
    public void SameArraySelect_KeepsSharedSpace(
        BackendType backend,
        string sharedPointer,
        string globalPointer)
    {
        var kernel = typeof(SharedRefSelectSourceTests).GetMethod(
            nameof(SharedRefSelectSameArrayKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);

        var source = helper.GenerateBackendCode(kernel, backend).SourceCode;
        Assert.Contains(sharedPointer, source);
        Assert.DoesNotContain(globalPointer, source);
    }
}
