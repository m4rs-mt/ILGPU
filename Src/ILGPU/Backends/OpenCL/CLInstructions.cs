// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLInstructions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Runtime.OpenCL;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Contains general OpenCL instructions.
    /// </summary>
    public static partial class CLInstructions
    {
        /// <summary>
        /// Resolves a compare operation.
        /// </summary>
        /// <param name="kind">The compare kind.</param>
        /// <returns>The resolved compare operation.</returns>
        public static string GetCompareOperation(CompareKind kind) =>
            CompareOperations[(int)kind];

        /// <summary>
        /// Resolves an address-space-cast prefix.
        /// </summary>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The resolved address-space prefix.</returns>
        public static string GetAddressSpacePrefix(MemoryAddressSpace addressSpace) =>
            AddressSpacePrefixes[(int)addressSpace];

        /// <summary>
        /// Trues to resolve an address-space-cast operation.
        /// </summary>
        /// <param name="addressSpace">The target address space to convert to.</param>
        /// <param name="operation">The resolved address-space-cast operation.</param>
        /// <returns>True, if an operation could be resolved.</returns>
        public static bool TryGetAddressSpaceCast(
            MemoryAddressSpace addressSpace,
            out string operation)
        {
            operation = AddressSpaceCastOperations[(int)addressSpace];
            return operation != null;
        }

        /// <summary>
        /// Resolves an unary arithmetic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="basicValueType">The arithmetic basic value type.</param>
        /// <param name="isFunction">
        /// True, if the resolved operation is a function call.
        /// </param>
        /// <returns>The resolved arithmetic operation.</returns>
        public static string GetArithmeticOperation(
            UnaryArithmeticKind kind,
            ArithmeticBasicValueType basicValueType,
            out bool isFunction)
        {
            if (!UnaryCategoryLookup.TryGetValue(basicValueType, out var category) ||
                !UnaryArithmeticOperations.TryGetValue(
                    (kind, category),
                    out var operation))
            {
                throw new NotSupportedIntrinsicException(kind.ToString());
            }

            isFunction = operation.Item2;
            return operation.Item1;
        }

        /// <summary>
        /// Resolves a binary arithmetic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="isFloat">True, if this is a floating-point operation.</param>
        /// <param name="isFunction">
        /// True, if the resolved operation is a function call.
        /// </param>
        /// <returns>The resolved arithmetic operation.</returns>
        public static string GetArithmeticOperation(
            BinaryArithmeticKind kind,
            bool isFloat,
            out bool isFunction)
        {
            if (!BinaryArithmeticOperations.TryGetValue(
                (kind, isFloat),
                out var operation))
            {
                throw new NotSupportedIntrinsicException(kind.ToString());
            }

            isFunction = operation.Item2;
            return operation.Item1;
        }

        /// <summary>
        /// Tries to resolve a ternary arithmetic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="isFloat">True, if this is a floating-point operation.</param>
        /// <param name="operation">The resolved operation.</param>
        /// <returns>True, if the operation could be resolved.</returns>
        public static bool TryGetArithmeticOperation(
            TernaryArithmeticKind kind,
            bool isFloat,
            out string operation) =>
            TernaryArithmeticOperations.TryGetValue((kind, isFloat), out operation);

        /// <summary>
        /// Resolves an atomic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <returns>The resolved atomic operation.</returns>
        public static string GetAtomicOperation(AtomicKind kind) =>
            AtomicOperations[kind];

        /// <summary>
        /// Resolves a barrier operation.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>The resolved barrier operation.</returns>
        public static string GetBarrier(BarrierKind kind) =>
            BarrierOperations[(int)kind];

        /// <summary>
        /// Tries to resolve a predicate-barrier operation.
        /// </summary>
        /// <param name="operation">The resolved memory-barrier operation.</param>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>True, if the operation could be resolved.</returns>
        public static bool TryGetPredicateBarrier(
            PredicateBarrierKind kind,
            out string operation)
        {
            operation = PredicateBarrierOperations[(int)kind];
            return operation != null;
        }

        /// <summary>
        /// Tries to resolve a memory-barrier operation.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <param name="memoryScope">The resolved memory-barrier scope.</param>
        /// <returns>True, if the operation could be resolved.</returns>
        public static string GetMemoryBarrier(
            MemoryBarrierKind kind,
            out string memoryScope)
        {
            memoryScope = MemoryScopes[(int)kind];
            return GetBarrier(BarrierKind.GroupLevel);
        }

        /// <summary>
        /// Resolves memory-fence flags.
        /// </summary>
        /// <param name="isGlobal">True, if the flags represent global memory.</param>
        /// <returns>The resolved fence flags.</returns>
        public static string GetMemoryFenceFlags(bool isGlobal) =>
            MemoryFenceFlags[isGlobal ? 0 : 1];

        /// <summary>
        /// Tries to resolve a shuffle operation.
        /// </summary>
        /// <param name="vendor">The accelerator vendor.</param>
        /// <param name="kind">The shuffle kind.</param>
        /// <param name="operation">The resolved shuffle operation.</param>
        /// <returns>True, if the operation could be resolved.</returns>
        public static bool TryGetShuffleOperation(
            CLDeviceVendor vendor,
            ShuffleKind kind,
            out string operation) =>
            ShuffleOperations.TryGetValue((vendor, kind), out operation);

        /// <summary>
        /// Resolves a broadcast operation.
        /// </summary>
        /// <param name="kind">The broadcast kind.</param>
        /// <returns>The resolved broadcast operation.</returns>
        public static string GetBroadcastOperation(BroadcastKind kind) =>
            BroadcastOperations[(int)kind];
    }
}
