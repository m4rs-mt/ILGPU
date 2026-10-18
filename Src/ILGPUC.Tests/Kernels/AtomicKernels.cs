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

    /// <summary>
    /// Unsigned 32-bit and float atomics: native where the target has them,
    /// compare-and-swap loops otherwise.
    /// </summary>
    public static void AtomicUnsignedFloatKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> u,
        ArrayView1D<float, Stride1D.Dense> f)
    {
        uint value = (uint)index.X;
        Atomic.Add(ref u[0], value);
        Atomic.Max(ref u[1], value);
        Atomic.Min(ref u[2], value);
        Atomic.And(ref u[3], value);
        Atomic.Or(ref u[4], value);
        Atomic.Xor(ref u[5], value);
        Atomic.Exchange(ref u[6], value);
        Atomic.CompareExchange(ref u[7], 0u, value);
        float floatValue = index.X * 0.5f;
        Atomic.Add(ref f[0], floatValue);
        f[1] = Atomic.Max(ref f[1], floatValue);
        Atomic.Min(ref f[2], floatValue);
        Atomic.Exchange(ref f[3], floatValue);
        Atomic.CompareExchange(ref f[4], 0.0f, floatValue);
    }

    /// <summary>
    /// Signed and unsigned 64-bit integer atomics.
    /// </summary>
    [RequiresCapability(BackendCapability.Int64Atomics)]
    public static void AtomicInt64Kernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> l,
        ArrayView1D<ulong, Stride1D.Dense> u)
    {
        long value = index.X;
        Atomic.Add(ref l[0], value);
        Atomic.Max(ref l[1], value);
        Atomic.Min(ref l[2], value);
        Atomic.And(ref l[3], value);
        Atomic.Or(ref l[4], value);
        Atomic.Xor(ref l[5], value);
        Atomic.Exchange(ref l[6], value);
        Atomic.CompareExchange(ref l[7], 0L, value);
        ulong unsignedValue = (ulong)index.X;
        Atomic.Add(ref u[0], unsignedValue);
        Atomic.Max(ref u[1], unsignedValue);
        Atomic.Min(ref u[2], unsignedValue);
        Atomic.And(ref u[3], unsignedValue);
        Atomic.Exchange(ref u[4], unsignedValue);
        Atomic.CompareExchange(ref u[5], 0UL, unsignedValue);
    }

    /// <summary>
    /// Float64 atomics besides add: max, min, exchange and compare-exchange.
    /// </summary>
    [RequiresCapability(BackendCapability.Float64Atomics)]
    public static void AtomicFloat64Kernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> d)
    {
        double value = index.X * 0.5;
        Atomic.Max(ref d[0], value);
        Atomic.Min(ref d[1], value);
        Atomic.Exchange(ref d[2], value);
        d[3] = Atomic.CompareExchange(ref d[4], 0.0, value);
    }
}
