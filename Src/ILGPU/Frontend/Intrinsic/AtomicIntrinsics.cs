// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: AtomicIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum AtomicIntrinsicKind
    {
        Exchange = AtomicKind.Exchange,
        Add = AtomicKind.Add,
        And = AtomicKind.And,
        Or = AtomicKind.Or,
        Xor = AtomicKind.Xor,
        Max = AtomicKind.Max,
        Min = AtomicKind.Min,

        CompareExchange,
    }

    /// <summary>
    /// Marks intrinsic atomic methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class AtomicIntrinsicAttribute : IntrinsicAttribute
    {
        public AtomicIntrinsicAttribute(
            AtomicIntrinsicKind kind,
            AtomicFlags flags)
        {
            IntrinsicKind = kind;
            IntrinsicFlags = flags;
        }

        public override IntrinsicType Type => IntrinsicType.Atomic;

        /// <summary>
        /// Returns the associated intrinsic kind.
        /// </summary>
        public AtomicIntrinsicKind IntrinsicKind { get; }

        /// <summary>
        /// Returns the associated intrinsic flags.
        /// </summary>
        public AtomicFlags IntrinsicFlags { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles atomics.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleAtomicOperation(
            in InvocationContext context,
            AtomicIntrinsicAttribute attribute)
        {
            var memory = context.PopMemory();
            MemoryValue atomicValue;
            if (attribute.IntrinsicKind == AtomicIntrinsicKind.CompareExchange)
            {
                atomicValue = context.Builder.CreateAtomicCAS(
                    memory,
                    context[TopLevelFunction.ParametersOffset + 0],
                    context[TopLevelFunction.ParametersOffset + 1],
                    context[TopLevelFunction.ParametersOffset + 2],
                    attribute.IntrinsicFlags);
            }
            else
            {
                atomicValue = context.Builder.CreateAtomic(
                    memory,
                    context[TopLevelFunction.ParametersOffset + 0],
                    context[TopLevelFunction.ParametersOffset + 1],
                    (AtomicKind)attribute.IntrinsicKind,
                    attribute.IntrinsicFlags);
            }
            context.PushMemory(atomicValue);
            return atomicValue;
        }
    }
}
