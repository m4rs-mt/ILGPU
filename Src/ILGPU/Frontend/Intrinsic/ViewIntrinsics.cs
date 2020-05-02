// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ViewIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
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
    /// Marks view methods that are built in.
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
            var location = context.Location;
            // These methods are instance calls -> load instance value
            var paramOffset = 0;
            var instanceValue = builder.CreateLoad(
                location,
                context[paramOffset++]);
            return attribute.IntrinsicKind switch
            {
                ViewIntrinsicKind.GetViewLength => builder.CreateGetViewLength(
                    location,
                    instanceValue),
                ViewIntrinsicKind.GetViewLengthInBytes => builder.CreateArithmetic(
                    location,
                    builder.CreateGetViewLength(location, instanceValue),
                    builder.CreateSizeOf(
                        location,
                        (instanceValue.Type as AddressSpaceType).ElementType),
                    BinaryArithmeticKind.Mul),
                ViewIntrinsicKind.GetSubView => builder.CreateSubViewValue(
                    location,
                    instanceValue,
                    context[paramOffset++],
                    context[paramOffset++]),
                ViewIntrinsicKind.GetSubViewImplicitLength =>
                    builder.CreateSubViewValue(
                        location,
                        instanceValue,
                        context[paramOffset],
                        builder.CreateArithmetic(
                            location,
                            builder.CreateGetViewLength(location, instanceValue),
                            context[paramOffset],
                            BinaryArithmeticKind.Sub)),
                ViewIntrinsicKind.GetViewElementAddress =>
                    builder.CreateLoadElementAddress(
                        location,
                        instanceValue,
                        context[paramOffset++]),
                ViewIntrinsicKind.CastView => builder.CreateViewCast(
                    location,
                    instanceValue,
                    builder.CreateType(context.GetMethodGenericArguments()[0])),
                ViewIntrinsicKind.IsValidView => builder.CreateCompare(
                    location,
                    builder.CreateGetViewLength(location, instanceValue),
                    builder.CreatePrimitiveValue(location, 0),
                    CompareKind.GreaterThan),
                ViewIntrinsicKind.GetViewExtent => builder.CreateIndex(
                    location,
                    builder.CreateGetViewLength(location, instanceValue)),
                ViewIntrinsicKind.GetViewElementAddressByIndex =>
                    builder.CreateLoadElementAddress(
                        location,
                        instanceValue,
                        builder.CreateGetField(
                            location,
                            context[paramOffset++],
                            new FieldAccess(0))),
                ViewIntrinsicKind.AsLinearView => instanceValue,
                _ => throw context.GetNotSupportedException(
                    ErrorMessages.NotSupportedViewIntrinsic,
                    attribute.IntrinsicKind.ToString()),
            };
        }
    }
}
