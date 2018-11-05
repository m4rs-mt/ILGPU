// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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

        public static bool TryGetArithmeticOperation(
            UnaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            bool fastMath,
            out string operation)
        {
            var key = (kind, type);
            if (fastMath &&
                UnaryArithmeticOperationsFastMath.TryGetValue(key, out operation))
                return true;
            return UnaryArithmeticOperations.TryGetValue(key, out operation);
        }

        public static bool TryGetArithmeticOperation(
            BinaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            bool fastMath,
            out string operation)
        {
            var key = (kind, type);
            if (fastMath &&
                BinaryArithmeticOperationsFastMath.TryGetValue(key, out operation))
                return true;
            return BinaryArithmeticOperations.TryGetValue(key, out operation);
        }

        public static bool TryGetArithmeticOperation(
            TernaryArithmeticKind kind,
            ArithmeticBasicValueType type,
            out string operation)
        {
            var key = (kind, type);
            return TernaryArithmeticOperations.TryGetValue(key, out operation);
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
