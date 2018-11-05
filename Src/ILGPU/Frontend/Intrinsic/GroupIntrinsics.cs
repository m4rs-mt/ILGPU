// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: GroupIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum GroupIntrinsicKind
    {
        BarrierPopCount = PredicateBarrierKind.PopCount,
        BarrierAnd = PredicateBarrierKind.And,
        BarrierOr = PredicateBarrierKind.Or,

        Barrier,
    }

    /// <summary>
    /// Marks group methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class GroupIntrinsicAttribute : IntrinsicAttribute
    {
        public GroupIntrinsicAttribute(GroupIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Group;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public GroupIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles group operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleGroupOperation(
            in InvocationContext context,
            GroupIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            var memory = context.PopMemory();
            if (attribute.IntrinsicKind == GroupIntrinsicKind.Barrier)
                return context.PushMemory(
                    builder.CreateBarrier(memory, BarrierKind.GroupLevel));
            var result = builder.CreateBarrier(
                memory,
                context[TopLevelFunction.ParametersOffset],
                (PredicateBarrierKind)attribute.IntrinsicKind);
            context.PushMemory(result);
            return result;
        }
    }
}
