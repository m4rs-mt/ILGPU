// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Instructions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Stores general PTX instructions.
    /// </summary>
    static partial class Instructions
    {
        public static string GetLEAMulOperation(ArithmeticBasicValueType pointerType) =>
            LEAMulOperations[pointerType];

        public static string GetSelectValueOperation(BasicValueType type) =>
            SelectValueOperations[type];

        public static string GetCompareOperation(CompareKind kind, ArithmeticBasicValueType type) =>
            CompareOperations[(kind, type)];

        public static string GetConvertOperation(ArithmeticBasicValueType source, ArithmeticBasicValueType target) =>
            ConvertOperations[(source, target)];

        public static string GetArithmeticOperation(
            UnaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            bool fastMath)
        {
            var key = (kind, type);
            if (fastMath &&
                UnaryArithmeticOperationsFastMath.TryGetValue(key, out string operation))
                return operation;
            return UnaryArithmeticOperations[key];
        }

        public static string GetArithmeticOperation(
            BinaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            bool fastMath)
        {
            var key = (kind, type);
            if (fastMath &&
                BinaryArithmeticOperationsFastMath.TryGetValue(key, out string operation))
                return operation;
            return BinaryArithmeticOperations[key];
        }

        public static string GetArithmeticOperation(
            TernaryArithmeticKind kind,
            ArithmeticBasicValueType type)
        {
            var key = (kind, type);
            return TernaryArithmeticOperations[key];
        }

        public static string GetAtomicOperation(AtomicKind kind, bool requireResult) =>
            AtomicOperations[(kind, requireResult)];

        public static string GetAtomicOperationPostfix(AtomicKind kind, ArithmeticBasicValueType type) =>
            AtomicOperationsTypes[(kind, type)];

        public static string GetAddressSpaceCast(bool convertToGeneric) =>
            AddressSpaceCastOperations[convertToGeneric ? 0 : 1];

        public static string GetBarrier(BarrierKind kind) =>
            BarrierOperations[(int)kind];

        public static string GetPredicateBarrier(PredicateBarrierKind kind) =>
            PredicateBarrierOperations[(int)kind];

        public static string GetMemoryBarrier(MemoryBarrierKind kind) =>
            MemoryBarrierOperations[(int)kind];

        public static string GetShuffleOperation(ShuffleKind kind) =>
            ShuffleOperations[(int)kind];
    }
}
