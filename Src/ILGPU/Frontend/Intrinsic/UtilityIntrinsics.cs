// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: UtilityIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum UtilityIntrinsicKind
    {
        Select,
    }

    /// <summary>
    /// Marks intrinsic utility methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class UtilityIntrinsicAttribute : IntrinsicAttribute
    {
        public UtilityIntrinsicAttribute(UtilityIntrinsicKind kind)
        {
            IntrinsicKind = kind;
        }

        public override IntrinsicType Type => IntrinsicType.Utility;

        /// <summary>
        /// Returns the associated intrinsic kind.
        /// </summary>
        public UtilityIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles utility functions.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleUtilityOperation(
            ref InvocationContext context,
            UtilityIntrinsicAttribute attribute) =>
            context.Builder.CreatePredicate(
                context.Location,
                context[0],
                context[1],
                context[2]);
    }
}
