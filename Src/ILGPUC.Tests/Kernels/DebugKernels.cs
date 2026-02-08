// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System.Diagnostics;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for debug/assert tests.
/// Uses System.Diagnostics.Debug.Assert which ILGPU recognizes.
/// </summary>
static class DebugKernels
{
    /// <summary>
    /// Assert with a constant true condition.
    /// </summary>
    public static void AssertTrueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Debug.Assert(true);
        data[index] = 42;
    }

    /// <summary>
    /// Assert with a runtime condition based on index.
    /// </summary>
    public static void AssertConditionKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Debug.Assert(index.X >= 0);
        data[index] = index.X + 1;
    }
}
