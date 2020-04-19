// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SharedMemoryIntrinsics.cs
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
            in InvocationContext context,
            SharedMemoryIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            var genericArgs = context.GetMethodGenericArguments();
            var allocationType = builder.CreateType(genericArgs[0]);

            switch (attribute.IntrinsicKind)
            {
                case SharedMemoryIntrinsicKind.AllocateElement:
                    return builder.CreateAlloca(
                        allocationType,
                        MemoryAddressSpace.Shared);
                case SharedMemoryIntrinsicKind.Allocate:
                    return builder.CreateNewView(
                        builder.CreateStaticAllocaArray(
                            context[0],
                            allocationType,
                            MemoryAddressSpace.Shared),
                        context[0]);
                case SharedMemoryIntrinsicKind.AllocateDynamic:
                    var alloca = builder.CreateDynamicAllocaArray(
                        allocationType,
                        MemoryAddressSpace.Shared).ResolveAs<Alloca>();
                    return builder.CreateNewView(
                        alloca,
                        alloca.ArrayLength);
            }
            throw context.GetNotSupportedException(
                ErrorMessages.NotSupportedSharedMemoryIntrinsic,
                attribute.IntrinsicKind.ToString());
        }
    }
}
