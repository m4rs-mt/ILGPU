// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LocalMemoryIntrinsics.cs
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
    enum LocalMemoryIntrinsicKind
    {
        Allocate,
    }

    /// <summary>
    /// Marks local-memory methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class LocalMemoryIntrinsicAttribute : IntrinsicAttribute
    {
        public LocalMemoryIntrinsicAttribute(LocalMemoryIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.LocalMemory;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public LocalMemoryIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles local operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleLocalMemoryOperation(
            ref InvocationContext context,
            LocalMemoryIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            var location = context.Location;
            var genericArgs = context.GetMethodGenericArguments();
            var allocationType = builder.CreateType(genericArgs[0]);

            return attribute.IntrinsicKind == LocalMemoryIntrinsicKind.Allocate
                ? builder.CreateNewView(
                    location,
                    builder.CreateAlloca(
                        location,
                        allocationType,
                        MemoryAddressSpace.Local,
                        context[0]),
                    context[0])
                : throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedLocalMemoryIntrinsic,
                    attribute.IntrinsicKind.ToString());
        }
    }
}
