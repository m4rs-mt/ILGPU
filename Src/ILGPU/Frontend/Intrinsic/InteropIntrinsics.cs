// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: InteropIntrinsics.cs
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
    enum InteropIntrinsicKind
    {
        SizeOf,
        OffsetOf,

        FloatAsInt,
        IntAsFloat,
    }

    /// <summary>
    /// Marks intrinsic interop methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class InteropIntrinsicAttribute : IntrinsicAttribute
    {
        public InteropIntrinsicAttribute(InteropIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Interop;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public InteropIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles interop operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleInterop(
            ref InvocationContext context,
            InteropIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            return attribute.IntrinsicKind switch
            {
                InteropIntrinsicKind.SizeOf => builder.CreateSizeOf(
                    context.Location,
                    builder.CreateType(context.GetMethodGenericArguments()[0])),
                InteropIntrinsicKind.OffsetOf => CreateOffsetOf(ref context),
                InteropIntrinsicKind.FloatAsInt => builder.CreateFloatAsIntCast(
                    context.Location,
                    context[0]),
                InteropIntrinsicKind.IntAsFloat => builder.CreateIntAsFloatCast(
                    context.Location,
                    context[0]),
                _ => throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedInteropIntrinsic,
                    attribute.IntrinsicKind.ToString()),
            };
        }

        /// <summary>
        /// Creates a new offset-of computation.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateOffsetOf(ref InvocationContext context)
        {
            var builder = context.Builder;
            var typeInfo = builder.Context.TypeContext.GetTypeInfo(
                context.GetMethodGenericArguments()[0]);
            var fieldName = context[0].ResolveAs<StringValue>();
            int fieldIndex = 0;
            foreach (var field in typeInfo.Fields)
            {
                if (field.Name == fieldName.String)
                {
                    fieldIndex = typeInfo.GetAbsoluteIndex(field);
                    break;
                }
            }
            var irType = context.Builder.CreateType(typeInfo.ManagedType);
            return context.Builder.CreateOffsetOf(
                context.Location,
                irType,
                fieldIndex);
        }
    }
}
