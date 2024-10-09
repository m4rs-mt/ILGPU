// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArithmeticFlags.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.IR;

/// <summary>
/// Represents flags of an arithmetic operation.
/// </summary>
[Flags]
public enum ArithmeticFlags
{
    /// <summary>
    /// No special flags (default).
    /// </summary>
    None = 0,

    /// <summary>
    /// The operation has overflow semantics.
    /// </summary>
    Overflow = 1,

    /// <summary>
    /// The operation has unsigned semantics.
    /// </summary>
    Unsigned = 2,

    /// <summary>
    /// The operation has overflow semantics and the
    /// overflow check is based on unsigned semantics.
    /// </summary>
    OverflowUnsigned = 3,
}
