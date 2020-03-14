// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ViewIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum ViewIntrinsicKind
    {
        GetViewLength,
        GetViewLengthInBytes,
        GetSubView,
        GetSubViewImplicitLength,
        GetViewElementAddress,
        CastView,

        IsValidView,
        GetViewExtent,
        GetViewElementAddressByIndex,
        AsLinearView,
    }

    /// <summary>
    /// Marks view methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class ViewIntrinsicAttribute : IntrinsicAttribute
    {
        public ViewIntrinsicAttribute(ViewIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.View;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public ViewIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles shared memory operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleViewOperation(
            in InvocationContext context,
            ViewIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            // These methods are instance calls -> load instance value
            var paramOffset = 0;
            var instanceValue = builder.CreateLoad(context[paramOffset++]);
            switch (attribute.IntrinsicKind)
            {
                case ViewIntrinsicKind.GetViewLength:
                    return builder.CreateGetViewLength(instanceValue);
                case ViewIntrinsicKind.GetViewLengthInBytes:
                    return builder.CreateArithmetic(
                        builder.CreateGetViewLength(instanceValue),
                        builder.CreateSizeOf(
                            (instanceValue.Type as AddressSpaceType).ElementType),
                        BinaryArithmeticKind.Mul);
                case ViewIntrinsicKind.GetSubView:
                    return builder.CreateSubViewValue(
                        instanceValue,
                        context[paramOffset++],
                        context[paramOffset++]);
                case ViewIntrinsicKind.GetSubViewImplicitLength:
                    return builder.CreateSubViewValue(
                        instanceValue,
                        context[paramOffset],
                        builder.CreateArithmetic(
                            builder.CreateGetViewLength(instanceValue),
                            context[paramOffset],
                            BinaryArithmeticKind.Sub));
                case ViewIntrinsicKind.GetViewElementAddress:
                    return builder.CreateLoadElementAddress(
                        instanceValue,
                        context[paramOffset++]);
                case ViewIntrinsicKind.CastView:
                    return builder.CreateViewCast(
                        instanceValue,
                        builder.CreateType(context.GetMethodGenericArguments()[0]));

                case ViewIntrinsicKind.IsValidView:
                    return builder.CreateCompare(
                        builder.CreateGetViewLength(instanceValue),
                        builder.CreatePrimitiveValue(0),
                        CompareKind.GreaterThan);
                case ViewIntrinsicKind.GetViewExtent:
                    return builder.CreateIndex(
                        builder.CreateGetViewLength(instanceValue));
                case ViewIntrinsicKind.GetViewElementAddressByIndex:
                    return builder.CreateLoadElementAddress(
                        instanceValue,
                        builder.CreateGetField(
                            context[paramOffset++],
                            new FieldAccess(0)));
                case ViewIntrinsicKind.AsLinearView:
                    return instanceValue;
                default:
                    throw context.GetNotSupportedException(
                        ErrorMessages.NotSupportedViewIntrinsic,
                        attribute.IntrinsicKind.ToString());
            }
        }
    }
}
