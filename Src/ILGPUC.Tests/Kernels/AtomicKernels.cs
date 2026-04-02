// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Atomic operation kernels using ILGPU.Atomic.
/// </summary>
static class AtomicKernels
{
    public static void AtomicAddKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Add(ref data[0], value);
    }

    public static void AtomicMaxKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Max(ref data[0], value);
    }

    public static void AtomicMinKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Min(ref data[0], value);
    }

    public static void AtomicExchangeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Exchange(ref data[0], value);
    }
}
