// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericKernelKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Closure interface for the generic kernel — exercises the
/// <c>FlatStructLauncherEmitter</c> path that handles user-defined struct
/// closures passed to a generic kernel as a struct-typed parameter.
/// Family A.2 regression.
/// </summary>
interface IClosure<T>
    where T : struct
{
    T Compute(Index1D index, int value);
}

/// <summary>
/// Concrete closure used to instantiate <see cref="GenericKernelKernels.LaunchClosure"/>.
/// Captures a long offset and combines it with the per-thread index.
/// </summary>
readonly struct AddOffsetClosure : IClosure<long>
{
    public AddOffsetClosure(long offset)
    {
        Offset = offset;
    }

    public long Offset { get; }

    public long Compute(Index1D index, int value) =>
        Offset + value * index;
}

/// <summary>
/// Generic kernel launchers exercising struct-closure marshalling.
/// </summary>
static class GenericKernelKernels
{
    /// <summary>
    /// Generic over the closure type and its result type. The launcher must
    /// marshal a struct value containing captured state (here:
    /// <see cref="AddOffsetClosure.Offset"/>) into the kernel without losing
    /// the field — the bug fixed on <c>temp5</c> dropped the captured field
    /// in the generated launcher.
    /// </summary>
    public static void LaunchClosure<TClosure, T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        int value,
        TClosure closure)
        where TClosure : struct, IClosure<T>
        where T : unmanaged
    {
        data[index] = closure.Compute(index, value);
    }
}
