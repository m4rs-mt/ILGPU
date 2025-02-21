// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles accelerator type information.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Accelerator_CurrentType(
        ref InvocationContext context) =>
        context.Builder.CreateAcceleratorTypeValue(
            context.Location);
}
