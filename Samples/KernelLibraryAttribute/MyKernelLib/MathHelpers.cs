// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MathHelpers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using System;

namespace MyKernelLib;

/// <summary>
/// GPU-callable helpers shipped by this library. Any kernel that references
/// MyKernelLib can call these methods just like any other static method —
/// the ILGPUC frontend disassembles them and inlines them into the kernel's
/// IR. Math.Abs is remapped to ILGPU.XMath.Abs (and downstream to the
/// backend's native abs intrinsic) automatically by the intrinsic registry.
/// </summary>
public static class MathHelpers
{
    /// <summary>Returns |x|, expressed via the BCL <see cref="Math.Abs(int)"/>
    /// which the ILGPUC intrinsic registry remaps to a backend-native
    /// absolute-value instruction.</summary>
    public static int SafeAbs(int x) => Math.Abs(x);

    /// <summary>Sum of absolute differences, two layers deep, so it's
    /// obvious that the helper isn't a single trivial inline.</summary>
    public static int SumAbsDiff(int a, int b, int c) =>
        SafeAbs(a - b) + SafeAbs(b - c);
}
