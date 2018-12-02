// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Atomics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new atomic operation.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The target value.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        /// <returns>A node that represents the atomic operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public MemoryValue CreateAtomic(
            Value target,
            Value value,
            AtomicKind kind,
            AtomicFlags flags)
        {
            Debug.Assert(target != null, "Invalid target node");
            Debug.Assert(value != null, "Invalid value node");
            Debug.Assert(
                target.Type is PointerType type && type.ElementType == value.Type,
                "Incompatible pointer and element types");

            return Append(new GenericAtomic(
                BasicBlock,
                target,
                value,
                kind,
                flags));
        }

        /// <summary>
        /// Creates a new atomic compare-and-swap operation
        /// </summary>
        /// <param name="target">The parent memory operation.</param>
        /// <param name="value">The target value.</param>
        /// <param name="compareValue">The comparison value.</param>
        /// <param name="flags">The operation flags.</param>
        /// <returns>A node that represents the atomic compare-and-swap operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public MemoryValue CreateAtomicCAS(
            Value target,
            Value value,
            Value compareValue,
            AtomicFlags flags)
        {
            Debug.Assert(target != null, "Invalid target node");
            Debug.Assert(value != null, "Invalid value node");
            Debug.Assert(compareValue != null, "Invalid compare value node");
            Debug.Assert(
                target.Type is PointerType type && type.ElementType == value.Type,
                "Incompatible pointer and element types");
            Debug.Assert(value.Type == compareValue.Type, "Incompatible value types");

            return Append(new AtomicCAS(
                BasicBlock,
                target,
                value,
                compareValue,
                flags));
        }
    }
}
