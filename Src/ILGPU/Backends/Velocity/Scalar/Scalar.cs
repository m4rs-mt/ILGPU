// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Scalar.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;
using System;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity.Scalar
{
    /// <summary>
    /// A scalar 2-warp-wide sequential warp implementation.
    /// </summary>
    sealed class Scalar : VelocityTargetSpecializer
    {
        #region Instance & General Methods

        public Scalar()
            : base(
                ScalarOperations2.WarpSize,
                ScalarOperations2.WarpType32,
                ScalarOperations2.WarpType64)
        { }

        public override VelocityTypeGenerator CreateTypeGenerator(
            VelocityCapabilityContext capabilityContext,
            RuntimeSystem runtimeSystem) =>
            new ScalarTypeGenerator(capabilityContext, runtimeSystem);

        #endregion

        #region General

        public override void LoadLaneIndexVector32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.LoadLaneIndexVector32Method);

        public override void LoadLaneIndexVector64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.LoadLaneIndexVector64Method);

        public override void LoadWarpSizeVector32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.LoadVectorLengthVector32Method);

        public override void LoadWarpSizeVector64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.LoadVectorLengthVector64Method);

        #endregion

        #region Masks

        public override void PushAllLanesMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.LoadAllLanesMask32Method);

        public override void PushNoLanesMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.LoadNoLanesMask32Method);

        public override void ConvertMask32To64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetConvert32To64Operation(
                VelocityWarpOperationMode.I));

        public override void ConvertMask64To32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetConvert64To32Operation(
                VelocityWarpOperationMode.I));

        public override void IntersectMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetBinaryOperation32(
                BinaryArithmeticKind.And,
                VelocityWarpOperationMode.U));

        public override void IntersectMask64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetBinaryOperation64(
                BinaryArithmeticKind.And,
                VelocityWarpOperationMode.U));

        public override void UnifyMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetBinaryOperation32(
                BinaryArithmeticKind.Or,
                VelocityWarpOperationMode.U));

        public override void UnifyMask64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetBinaryOperation64(
                BinaryArithmeticKind.Or,
                VelocityWarpOperationMode.U));

        public override void NegateMask32<TILEmitter>(TILEmitter emitter)
        {
            // As an active lane is 1 and a non-active lane is 0...
            emitter.Emit(OpCodes.Ldc_I4_1);
            emitter.EmitCall(ScalarOperations2.FromScalarU32Method);
            BinaryOperation32(
                emitter,
                BinaryArithmeticKind.Xor,
                VelocityWarpOperationMode.U);
        }

        public override void NegateMask64<TILEmitter>(TILEmitter emitter)
        {
            emitter.Emit(OpCodes.Ldc_I4_1);
            emitter.Emit(OpCodes.Conv_U8);
            emitter.EmitCall(ScalarOperations2.FromScalarU64Method);
            BinaryOperation64(
                emitter,
                BinaryArithmeticKind.Xor,
                VelocityWarpOperationMode.U);
        }

        public override void CheckForAnyActiveLaneMask<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.CheckForAnyActiveLaneMethod);

        public override void CheckForNoActiveLaneMask<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.CheckForNoActiveLaneMethod);

        public override void CheckForEqualMasks<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.CheckForEqualMasksMethod);

        public override void GetNumberOfActiveLanes<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.GetNumberOfActiveLanesMethod);

        public override void ConditionalSelect32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Select32Method);

        public override void ConditionalSelect64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Select64Method);

        #endregion

        #region Scalar Values

        public override void LoadWarpSize32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitConstant(WarpSize);

        public override void LoadWarpSize64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitConstant((long)WarpSize);

        public override void ConvertBoolScalar<TILEmitter>(TILEmitter emitter) =>
            // As the initial bool value was already converted to an integer, we can
            // simply reuse the integer value
            ConvertScalarTo32(emitter, VelocityWarpOperationMode.I);

        public override void ConvertScalarTo32<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode)
        {
            switch (mode)
            {
                case VelocityWarpOperationMode.I:
                    emitter.EmitCall(ScalarOperations2.FromScalarI32Method);
                    break;
                case VelocityWarpOperationMode.U:
                    emitter.EmitCall(ScalarOperations2.FromScalarU32Method);
                    return;
                case VelocityWarpOperationMode.F:
                    emitter.EmitCall(ScalarOperations2.FromScalarF32Method);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void ConvertScalarTo64<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode)
        {
            switch (mode)
            {
                case VelocityWarpOperationMode.I:
                    emitter.EmitCall(ScalarOperations2.FromScalarI64Method);
                    break;
                case VelocityWarpOperationMode.U:
                    emitter.EmitCall(ScalarOperations2.FromScalarU64Method);
                    return;
                case VelocityWarpOperationMode.F:
                    emitter.EmitCall(ScalarOperations2.FromScalarF64Method);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        #endregion

        #region Comparisons

        public override void Compare32<TILEmitter>(
            TILEmitter emitter,
            CompareKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetCompareOperation32(kind, mode));

        public override void Compare64<TILEmitter>(
            TILEmitter emitter,
            CompareKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetCompareOperation64(kind, mode));

        #endregion

        #region Conversions

        public override void ConvertSoftware32<TILEmitter>(
            TILEmitter emitter,
            ArithmeticBasicValueType sourceType,
            ArithmeticBasicValueType targetType) =>
            emitter.EmitCall(ScalarOperations2.GetConvertOperation32(
                sourceType,
                targetType));

        public override void ConvertSoftware64<TILEmitter>(
            TILEmitter emitter,
            ArithmeticBasicValueType sourceType,
            ArithmeticBasicValueType targetType) =>
            emitter.EmitCall(ScalarOperations2.GetConvertOperation64(
                sourceType,
                targetType));

        public override void Convert32<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode source,
            VelocityWarpOperationMode target) =>
            emitter.EmitCall(ScalarOperations2.GetConvertOperation32(
                source.GetArithmeticBasicValueType(is64Bit: false),
                target.GetArithmeticBasicValueType(is64Bit: false)));

        public override void Convert64<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode source,
            VelocityWarpOperationMode target) =>
            emitter.EmitCall(ScalarOperations2.GetConvertOperation64(
                source.GetArithmeticBasicValueType(is64Bit: true),
                target.GetArithmeticBasicValueType(is64Bit: true)));

        public override void Convert32To64<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetConvert32To64Operation(mode));

        public override void Convert64To32<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetConvert64To32Operation(mode));

        #endregion

        #region Arithmetics

        public override void UnaryOperation32<TILEmitter>(
            TILEmitter emitter,
            UnaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetUnaryOperation32(kind, mode));

        public override void UnaryOperation64<TILEmitter>(
            TILEmitter emitter,
            UnaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetUnaryOperation64(kind, mode));

        public override void BinaryOperation32<TILEmitter>(
            TILEmitter emitter,
            BinaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetBinaryOperation32(kind, mode));

        public override void BinaryOperation64<TILEmitter>(
            TILEmitter emitter,
            BinaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetBinaryOperation64(kind, mode));

        public override void TernaryOperation32<TILEmitter>(
            TILEmitter emitter,
            TernaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetTernaryOperation32(kind, mode));

        public override void TernaryOperation64<TILEmitter>(
            TILEmitter emitter,
            TernaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetTernaryOperation64(kind, mode));

        #endregion

        #region Atomics

        public override void AtomicCompareExchange32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.AtomicCompareExchange32Method);

        public override void AtomicCompareExchange64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.AtomicCompareExchange64Method);

        public override void Atomic32<TILEmitter>(
            TILEmitter emitter,
            AtomicKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetAtomicOperation32(kind, mode));

        public override void Atomic64<TILEmitter>(
            TILEmitter emitter,
            AtomicKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(ScalarOperations2.GetAtomicOperation64(kind, mode));

        #endregion

        #region Threads


        public override void BarrierPopCount32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.BarrierPopCount32Method);

        public override void BarrierAnd32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.BarrierAnd32Method);

        public override void BarrierOr32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.BarrierOr32Method);

        public override void Shuffle32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Shuffle32Method);

        public override void ShuffleUp32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.ShuffleUp32Method);

        public override void SubShuffleUp32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.SubShuffleUp32Method);

        public override void ShuffleDown32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.ShuffleDown32Method);

        public override void SubShuffleDown32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.SubShuffleDown32Method);

        public override void ShuffleXor32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.ShuffleXor32Method);

        public override void SubShuffleXor32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.SubShuffleXor32Method);

        #endregion

        #region IO

        public override void Load8<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Load8Method);

        public override void Load16<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Load16Method);

        public override void Load32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Load32Method);

        public override void Load64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Load64Method);

        public override void Store8<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Store8Method);

        public override void Store16<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Store16Method);

        public override void Store32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Store32Method);

        public override void Store64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.Store64Method);

        #endregion

        #region Misc

        public override void DebugAssertFailed<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(ScalarOperations2.DebugAssertFailedMethod);

        public override void WriteToOutput<TILEmitter>(TILEmitter emitter) =>
            throw new NotSupportedException();

        public override void DumpWarp32<TILEmitter>(
            TILEmitter emitter,
            string? label = null)
        {
            if (string.IsNullOrEmpty(label))
                emitter.EmitConstant(string.Empty);
            else
                emitter.EmitConstant(label + ": ");
            emitter.EmitCall(ScalarOperations2.DumpWarp32Method);
        }

        public override void DumpWarp64<TILEmitter>(
            TILEmitter emitter,
            string? label = null)
        {
            if (string.IsNullOrEmpty(label))
                emitter.EmitConstant(string.Empty);
            else
                emitter.EmitConstant(label + ": ");
            emitter.EmitCall(ScalarOperations2.DumpWarp64Method);
        }

        #endregion
    }
}
