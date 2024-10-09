// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MathIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using System;

namespace ILGPU.Intrinsic;

/// <summary>
/// Marks math methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MathIntrinsicAttribute(
    MathIntrinsicKind intrinsicKind,
    ArithmeticFlags intrinsicFlags) : IntrinsicAttribute(IntrinsicType.Math)
{
    /// <summary>
    /// Marks a math method with no flags.
    /// </summary>
    public MathIntrinsicAttribute(MathIntrinsicKind intrinsicKind)
        : this(intrinsicKind, ArithmeticFlags.None)
    { }

    /// <summary>
    /// Returns the associated intrinsic kind.
    /// </summary>
    public MathIntrinsicKind IntrinsicKind { get; } = intrinsicKind;

    /// <summary>
    /// Returns the associated intrinsic flags.
    /// </summary>
    public ArithmeticFlags IntrinsicFlags { get; } = intrinsicFlags;
}
