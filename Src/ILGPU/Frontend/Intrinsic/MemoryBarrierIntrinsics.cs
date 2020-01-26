// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryBarrierIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    /// <summary>
    /// Marks memory-barrier methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class MemoryBarrierIntrinsicAttribute : IntrinsicAttribute
    {
        public MemoryBarrierIntrinsicAttribute(MemoryBarrierKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.MemoryFence;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public MemoryBarrierKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles memory barriers.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleMemoryBarrierOperation(
            in InvocationContext context,
            MemoryBarrierIntrinsicAttribute attribute)
        {
            return context.Builder.CreateMemoryBarrier(
                attribute.IntrinsicKind);
        }
    }
}
