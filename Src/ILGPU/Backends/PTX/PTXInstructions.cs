// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXInstructions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Contains general PTX instructions.
    /// </summary>
    public static partial class PTXInstructions
    {
        /// <summary>
        /// Resolves a LEA operation.
        /// </summary>
        /// <param name="pointerType">The pointer type.</param>
        /// <returns>The resolved LEA operation.</returns>
        public static string GetLEAMulOperation(ArithmeticBasicValueType pointerType) =>
            LEAMulOperations[pointerType];

        /// <summary>
        /// Resolves a select-value operation.
        /// </summary>
        /// <param name="type">The basic value type.</param>
        /// <returns>The resolved select-value operation.</returns>
        public static string GetSelectValueOperation(BasicValueType type) =>
            SelectValueOperations[type];

        /// <summary>
        /// Resolves a compare operation.
        /// </summary>
        /// <param name="kind">The compare kind.</param>
        /// <param name="flags">The compare flags.</param>
        /// <param name="type">The type to compare.</param>
        /// <returns>The resolved compare operation.</returns>
        public static string GetCompareOperation(
            CompareKind kind,
            CompareFlags flags,
            ArithmeticBasicValueType type)
        {
            var unorderedFloatComparison = type.IsFloat()
                                           && flags.HasFlag(CompareFlags
                                               .UnsignedOrUnordered);
            if (unorderedFloatComparison)
            {
                if (CompareUnorderedFloatOperations.TryGetValue(
                    (kind, type),
                    out string operation))
                {
                    return operation;
                }
            }
            else
            {
                if (CompareOperations.TryGetValue((kind, type), out string operation))
                    return operation;
            }

            throw new NotSupportedIntrinsicException(kind.ToString());
        }

        /// <summary>
        /// Resolves a convert operation.
        /// </summary>
        /// <param name="source">The source type to convert from.</param>
        /// <param name="target">The target type to convert to.</param>
        /// <returns>The resolved convert operation.</returns>
        public static string GetConvertOperation(
            ArithmeticBasicValueType source,
            ArithmeticBasicValueType target) =>
            ConvertOperations.TryGetValue((source, target), out string operation)
                ? operation
                : throw new NotSupportedIntrinsicException($"{source} -> {target}");

        /// <summary>
        /// Resolves an unary arithmetic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="type">The operation type.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="fastMath">True, to use a fast-math operation.</param>
        /// <returns>The resolved arithmetic operation.</returns>
        public static string GetArithmeticOperation(
            UnaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            CudaCapabilityContext capabilities,
            bool fastMath)
        {
            if (kind == UnaryArithmeticKind.TanhF &&
                type == ArithmeticBasicValueType.Float32 &&
                !capabilities.Float32_TanH)
            {
                throw CudaCapabilityContext.GetNotSupportedFloat32_TanHException();
            }
            else if (kind == UnaryArithmeticKind.TanhF &&
                     type == ArithmeticBasicValueType.Float16 &&
                     !capabilities.Float16_TanH)
            {
                throw CudaCapabilityContext.GetNotSupportedFloat16_TanHException();
            }

            var key = (kind, type);
            return fastMath &&
                   UnaryArithmeticOperationsFastMath.TryGetValue(
                       key,
                       out string operation) ||
                   UnaryArithmeticOperations.TryGetValue(key, out operation)
                ? operation
                : throw new NotSupportedIntrinsicException(kind.ToString());
        }

        /// <summary>
        /// Resolves a binary arithmetic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="type">The operation type.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="fastMath">True, to use a fast-math operation.</param>
        /// <returns>The resolved arithmetic operation.</returns>
        public static string GetArithmeticOperation(
            BinaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            CudaCapabilityContext capabilities,
            bool fastMath)
        {
            if (kind == BinaryArithmeticKind.Min &&
                type == ArithmeticBasicValueType.Float16 &&
                !capabilities.Float16_Min)
            {
                throw CudaCapabilityContext.GetNotSupportedFloat16_MinException();
            }
            else if (kind == BinaryArithmeticKind.Max &&
                     type == ArithmeticBasicValueType.Float16 &&
                     !capabilities.Float16_Max)
            {
                throw CudaCapabilityContext.GetNotSupportedFloat16_MaxException();
            }

            var key = (kind, type);
            return fastMath &&
                   BinaryArithmeticOperationsFastMath.TryGetValue(
                       key,
                       out string operation) ||
                   BinaryArithmeticOperations.TryGetValue(key, out operation)
                ? operation
                : throw new NotSupportedIntrinsicException(kind.ToString());
        }

        /// <summary>
        /// Resolves a ternary arithmetic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="type">The operation type.</param>
        /// <returns>The resolved arithmetic operation.</returns>
        public static string GetArithmeticOperation(
            TernaryArithmeticKind kind,
            ArithmeticBasicValueType type) =>
            TernaryArithmeticOperations.TryGetValue((kind, type), out string operation)
                ? operation
                : throw new NotSupportedIntrinsicException(kind.ToString());

        /// <summary>
        /// Resolves an atomic operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="requireResult">True, if the return value is required.</param>
        /// <returns>The resolved atomic operation.</returns>
        public static string GetAtomicOperation(AtomicKind kind, bool requireResult) =>
            AtomicOperations.TryGetValue((kind, requireResult), out string operation)
                ? operation
                : throw new NotSupportedIntrinsicException(kind.ToString());

        /// <summary>
        /// Resolves an atomic-operation suffix.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="type">The operation type.</param>
        /// <returns>The resolved atomic-operation suffix.</returns>
        public static string GetAtomicOperationSuffix(
            AtomicKind kind,
            ArithmeticBasicValueType type) =>
            AtomicOperationsTypes.TryGetValue((kind, type), out string operation)
                ? operation
                : throw new NotSupportedIntrinsicException(kind.ToString());

        /// <summary>
        /// Resolves an address-space-cast operation.
        /// </summary>
        /// <param name="convertToGeneric">
        /// True, to convert to the generic address space.
        /// </param>
        /// <returns>The resolved address-space-cast operation.</returns>
        public static string GetAddressSpaceCast(bool convertToGeneric) =>
            AddressSpaceCastOperations[convertToGeneric ? 0 : 1];

        /// <summary>
        /// Resolves an address-space-cast suffix.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <returns>The resolved address-space-cast suffix.</returns>
        public static string GetAddressSpaceCastSuffix(Backend backend) =>
            AddressSpaceCastOperationSuffix[
                backend.PointerBasicValueType == BasicValueType.Int32 ? 0 : 1];

        /// <summary>
        /// Resolves a barrier operation.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>The resolved barrier operation.</returns>
        public static string GetBarrier(BarrierKind kind) =>
            BarrierOperations[(int)kind];

        /// <summary>
        /// Resolves a predicate-barrier operation.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>The resolved predicate-barrier operation.</returns>
        public static string GetPredicateBarrier(PredicateBarrierKind kind) =>
            PredicateBarrierOperations[(int)kind];

        /// <summary>
        /// Resolves a memory-barrier operation.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>The resolved memory-barrier operation.</returns>
        public static string GetMemoryBarrier(MemoryBarrierKind kind) =>
            MemoryBarrierOperations[(int)kind];

        /// <summary>
        /// Resolves a shuffle operation.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>The resolved shuffle operation.</returns>
        public static string GetShuffleOperation(ShuffleKind kind) =>
            ShuffleOperations[(int)kind];

        /// <summary>
        /// Resolves a vector operation suffix.
        /// </summary>
        /// <param name="numElements">The number of elements.</param>
        /// <returns>The vector operation suffix.</returns>
        public static string GetVectorOperationSuffix(int numElements) =>
            VectorSuffixes.TryGetValue(numElements, out string operation)
                ? operation
                : throw new NotSupportedIntrinsicException("v" + numElements.ToString());
    }
}
