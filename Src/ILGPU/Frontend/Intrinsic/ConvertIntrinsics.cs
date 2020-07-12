// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ConvertIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    /// <summary>
    /// Marks compare intrinsics that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class ConvertIntriniscAttribute : IntrinsicAttribute
    {
        public ConvertIntriniscAttribute()
            : this(ConvertFlags.None)
        { }

        public ConvertIntriniscAttribute(ConvertFlags flags)
        {
            IntrinsicFlags = flags;
        }

        public override IntrinsicType Type => IntrinsicType.Convert;

        /// <summary>
        /// Returns the associated intrinsic flags.
        /// </summary>
        public ConvertFlags IntrinsicFlags { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles convert operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleConvertOperation(
            ref InvocationContext context,
            ConvertIntriniscAttribute attribute)
        {
            var returnType = context.Method.GetReturnType();
            var typeNode = context.Builder.CreateType(returnType);
            return context.Builder.CreateConvert(
                context.Location,
                context[0],
                typeNode,
                attribute.IntrinsicFlags);
        }
    }
}
