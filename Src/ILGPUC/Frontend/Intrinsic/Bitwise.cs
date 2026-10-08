// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Bitwise.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles Bitwise.And intrinsic (maps to BinaryArithmeticKind.And).
    /// </summary>
    private static Value? Bitwise_And(ref InvocationContext context)
    {
        var left = context.Pull();
        var right = context.Pull();
        return context.Builder.CreateArithmetic(
            context.Location,
            left,
            right,
            BinaryArithmeticKind.And);
    }

    /// <summary>
    /// Handles Bitwise.Or intrinsic (maps to BinaryArithmeticKind.Or).
    /// </summary>
    private static Value? Bitwise_Or(ref InvocationContext context)
    {
        var left = context.Pull();
        var right = context.Pull();
        return context.Builder.CreateArithmetic(
            context.Location,
            left,
            right,
            BinaryArithmeticKind.Or);
    }
}
