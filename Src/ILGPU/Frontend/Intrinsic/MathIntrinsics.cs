// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MathIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    /// <summary>
    /// Marks math methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class MathIntrinsicAttribute : IntrinsicAttribute
    {
        public MathIntrinsicAttribute(MathIntrinsicKind intrinsicKind)
            : this(intrinsicKind, ArithmeticFlags.None)
        { }

        public MathIntrinsicAttribute(
            MathIntrinsicKind intrinsicKind,
            ArithmeticFlags intrinsicFlags)
        {
            IntrinsicKind = intrinsicKind;
            IntrinsicFlags = intrinsicFlags;
        }

        public override IntrinsicType Type => IntrinsicType.Math;

        /// <summary>
        /// Returns the associated intrinsic kind.
        /// </summary>
        public MathIntrinsicKind IntrinsicKind { get; }

        /// <summary>
        /// Returns the associated intrinsic flags.
        /// </summary>
        public ArithmeticFlags IntrinsicFlags { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles math operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleMathOperation(
            ref InvocationContext context,
            MathIntrinsicAttribute attribute)
        {
            switch (context.NumArguments)
            {
                case 1:
                    return context.Builder.CreateArithmetic(
                        context.Location,
                        context[0],
                        (UnaryArithmeticKind)attribute.IntrinsicKind,
                        attribute.IntrinsicFlags);
                case 2:
                    var kindIndex = attribute.IntrinsicKind -
                        MathIntrinsicKind._BinaryFunctions - 1;
                    return context.Builder.CreateArithmetic(
                        context.Location,
                        context[0],
                        context[1],
                        (BinaryArithmeticKind)kindIndex,
                        attribute.IntrinsicFlags);
                default:
                    throw context.Location.GetNotSupportedException(
                        ErrorMessages.NotSupportedMathIntrinsic,
                        context.NumArguments.ToString());
            }
        }

    }
}
