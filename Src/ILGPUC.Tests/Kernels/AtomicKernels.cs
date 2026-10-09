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
using ILGPUC.Tests.Framework;

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

    /// <summary>
    /// Built-in <see cref="Atomic.Add"/> for <c>double</c>. Family B.1
    /// regression — exercises the Float64 atomic add path through the
    /// emitter on every backend that supports Float64 atomics
    /// (CPU + CUDA + ROCm; Metal/OpenCL skip via capability gate).
    /// </summary>
    [RequiresCapability(BackendCapability.Float64Atomics)]
    public static void AtomicAddDoubleKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> data,
        double value)
    {
        Atomic.Add(ref data[0], value);
    }

    /// <summary>
    /// Custom <c>double</c> atomic add via <see cref="Atomic.MakeAtomic"/>
    /// with a delegate-driven CAS loop. Family B.1 regression — exercises
    /// the three emitter defects fixed on <c>temp5</c>:
    /// self-loop classification (<c>do/while</c> vs <c>while</c>),
    /// <see cref="ILGPU.IR.Values.FloatAsIntCast"/> /
    /// <see cref="ILGPU.IR.Values.IntAsFloatCast"/> dispatch arms in
    /// <c>ExpressionEmitter</c>, and element-type-aware pointer-cast
    /// emission.
    /// </summary>
    [RequiresCapability(BackendCapability.Float64Atomics)]
    public static void AtomicMakeAtomicAddDoubleKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> data,
        double value)
    {
        Atomic.MakeAtomic(
            ref data[0],
            value,
            (current, val) => current + val,
            (ref double target, double compare, double val) =>
                Atomic.CompareExchange(ref target, compare, val));
    }

    /// <summary>
    /// Atomic on shared (threadgroup) memory. Mirrors the
    /// <c>Samples/SharedMemory</c> pattern that surfaced the Metal
    /// codegen bug where <c>atomic_*_explicit</c> hard-coded a
    /// <c>(device atomic_int*)</c> cast — Metal rejects the cross
    /// address-space cast from <c>threadgroup int *</c>.
    /// </summary>
    public static void AtomicMaxOnSharedMemoryKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> source,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        var shared = Group.GetSharedMemory<int>(1);
        if (Group.IsFirstThread)
            shared[0] = 0;
        Group.Barrier();

        if (index < source.Length)
            Atomic.Max(ref shared[0], source[index]);
        Group.Barrier();

        if (Group.IsFirstThread && index < result.Length)
            result[index] = shared[0];
    }
}
