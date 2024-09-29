// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum SharedMemoryIntrinsicKind
    {
        AllocateElement,
        Allocate,
        AllocateDynamic,
    }

    /// <summary>
    /// Marks shared-memory methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class SharedMemoryIntrinsicAttribute : IntrinsicAttribute
    {
        public SharedMemoryIntrinsicAttribute(SharedMemoryIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.SharedMemory;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public SharedMemoryIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles view operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleSharedMemoryOperation(
            ref InvocationContext context,
            SharedMemoryIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            var location = context.Location;
            var genericArgs = context.GetMethodGenericArguments();
            var allocationType = builder.CreateType(genericArgs[0]);

            switch (attribute.IntrinsicKind)
            {
                case SharedMemoryIntrinsicKind.AllocateElement:
                    return builder.CreateAlloca(
                        location,
                        allocationType,
                        MemoryAddressSpace.Shared);
                case SharedMemoryIntrinsicKind.Allocate:
                    return builder.CreateNewView(
                        location,
                        builder.CreateStaticAllocaArray(
                            location,
                            context[0],
                            allocationType,
                            MemoryAddressSpace.Shared),
                        context[0]);
                case SharedMemoryIntrinsicKind.AllocateDynamic:
                    var alloca = builder.CreateDynamicAllocaArray(
                        location,
                        allocationType,
                        MemoryAddressSpace.Shared)
                        .ResolveAs<Alloca>()
                        .AsNotNull();
                    return builder.CreateNewView(
                        location,
                        alloca,
                        alloca.ArrayLength);
            }
            throw context.Location.GetNotSupportedException(
                ErrorMessages.NotSupportedSharedMemoryIntrinsic,
                attribute.IntrinsicKind.ToString());
        }
    }
}
