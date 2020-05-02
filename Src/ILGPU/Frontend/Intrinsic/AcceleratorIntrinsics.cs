// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AcceleratorIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Diagnostics;

namespace ILGPU.Frontend.Intrinsic
{
    enum AcceleratorIntrinsicKind
    {
        CurrentType,
    }

    /// <summary>
    /// Marks accelerator methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class AcceleratorIntrinsicAttribute : IntrinsicAttribute
    {
        public AcceleratorIntrinsicAttribute(AcceleratorIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Accelerator;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public AcceleratorIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles accelerator operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleAcceleratorOperation(
            in InvocationContext context,
            AcceleratorIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            Debug.Assert(attribute.IntrinsicKind == AcceleratorIntrinsicKind.CurrentType);
            return builder.CreateAcceleratorTypeValue(context.Location);
        }
    }
}
