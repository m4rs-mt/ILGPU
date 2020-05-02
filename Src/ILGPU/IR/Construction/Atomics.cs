// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Atomics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new atomic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The target address.</param>
        /// <param name="value">The target value.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        /// <returns>A node that represents the atomic operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public MemoryValue CreateAtomic(
            Location location,
            Value target,
            Value value,
            AtomicKind kind,
            AtomicFlags flags)
        {
            location.Assert(
                target.Type is PointerType type &&
                type.ElementType == value.Type);

            return Append(new GenericAtomic(
                GetInitializer(location),
                target,
                value,
                kind,
                flags));
        }

        /// <summary>
        /// Creates a new atomic compare-and-swap operation
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The parent memory operation.</param>
        /// <param name="value">The target value.</param>
        /// <param name="compareValue">The comparison value.</param>
        /// <param name="flags">The operation flags.</param>
        /// <returns>
        /// A node that represents the atomic compare-and-swap operation.
        /// </returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public MemoryValue CreateAtomicCAS(
            Location location,
            Value target,
            Value value,
            Value compareValue,
            AtomicFlags flags)
        {
            location.Assert(
                target.Type is PointerType type &&
                type.ElementType == value.Type &&
                value.Type == compareValue.Type);

            return Append(new AtomicCAS(
                GetInitializer(location),
                target,
                value,
                compareValue,
                flags));
        }
    }
}
