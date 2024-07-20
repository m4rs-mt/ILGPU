// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vec128.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;
using System;
using System.Reflection.Emit;

#if NET7_0_OR_GREATER

namespace ILGPU.Backends.Velocity.Vec128
{
    sealed class Vec128 : VelocityTargetSpecializer
    {
        #region Instance & General Methods

        public Vec128()
            : base(
                Vec128Operations.WarpSize,
                Vec128Operations.WarpType32,
                Vec128Operations.WarpType64)
        { }

        public override VelocityTypeGenerator CreateTypeGenerator(
            VelocityCapabilityContext capabilityContext,
            RuntimeSystem runtimeSystem) =>
            new Vec128TypeGenerator(capabilityContext, runtimeSystem);

        #endregion

        #region General

        public override void LoadLaneIndexVector32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.LoadLaneIndexVector32Method);

        public override void LoadLaneIndexVector64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.LoadLaneIndexVector64Method);

        public override void LoadWarpSizeVector32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.LoadVectorLengthVector32Method);

        public override void LoadWarpSizeVector64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.LoadVectorLengthVector64Method);

        #endregion

        #region Masks

        public override void PushAllLanesMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.LoadAllLanesMask32Method);

        public override void PushNoLanesMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.LoadNoLanesMask32Method);

        public override void ConvertMask32To64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetConvert32To64Operation(
                VelocityWarpOperationMode.I));

        public override void ConvertMask64To32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetConvert64To32Operation(
                VelocityWarpOperationMode.I));

        public override void IntersectMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetBinaryOperation32(
                BinaryArithmeticKind.And,
                VelocityWarpOperationMode.U));

        public override void IntersectMask64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetBinaryOperation64(
                BinaryArithmeticKind.And,
                VelocityWarpOperationMode.U));

        public override void UnifyMask32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetBinaryOperation32(
                BinaryArithmeticKind.Or,
                VelocityWarpOperationMode.U));

        public override void UnifyMask64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetBinaryOperation64(
                BinaryArithmeticKind.Or,
                VelocityWarpOperationMode.U));

        public override void NegateMask32<TILEmitter>(TILEmitter emitter)
        {
            PushAllLanesMask32(emitter);
            BinaryOperation32(
                emitter,
                BinaryArithmeticKind.Xor,
                VelocityWarpOperationMode.U);
        }

        public override void NegateMask64<TILEmitter>(TILEmitter emitter)
        {
            PushAllLanesMask32(emitter);
            ConvertMask32To64(emitter);
            BinaryOperation64(
                emitter,
                BinaryArithmeticKind.Xor,
                VelocityWarpOperationMode.U);
        }

        public override void CheckForAnyActiveLaneMask<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.CheckForAnyActiveLaneMethod);

        public override void CheckForNoActiveLaneMask<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.CheckForNoActiveLaneMethod);

        public override void CheckForEqualMasks<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.CheckForEqualMasksMethod);

        public override void GetNumberOfActiveLanes<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.GetNumberOfActiveLanesMethod);

        public override void ConditionalSelect32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Select32Method);

        public override void ConditionalSelect64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Select64Method);

        #endregion

        #region Scalar Values

        public override void LoadWarpSize32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitConstant(WarpSize);

        public override void LoadWarpSize64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitConstant((long)WarpSize);

        public override void ConvertBoolScalar<TILEmitter>(TILEmitter emitter, bool value)
        {
            emitter.Emit(value ? OpCodes.Ldc_I4_M1 : OpCodes.Ldc_I4_0);
            ConvertScalarTo32(emitter, VelocityWarpOperationMode.I);
        }

        public override void ConvertScalarTo32<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode)
        {
            switch (mode)
            {
                case VelocityWarpOperationMode.I:
                    emitter.EmitCall(Vec128Operations.FromScalarI32Method);
                    break;
                case VelocityWarpOperationMode.U:
                    emitter.EmitCall(Vec128Operations.FromScalarU32Method);
                    return;
                case VelocityWarpOperationMode.F:
                    emitter.EmitCall(Vec128Operations.FromScalarF32Method);
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
                    emitter.EmitCall(Vec128Operations.FromScalarI64Method);
                    break;
                case VelocityWarpOperationMode.U:
                    emitter.EmitCall(Vec128Operations.FromScalarU64Method);
                    return;
                case VelocityWarpOperationMode.F:
                    emitter.EmitCall(Vec128Operations.FromScalarF64Method);
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
            emitter.EmitCall(Vec128Operations.GetCompareOperation32(kind, mode));

        public override void Compare64<TILEmitter>(
            TILEmitter emitter,
            CompareKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetCompareOperation64(kind, mode));

        #endregion

        #region Conversions

        public override void ConvertSoftware32<TILEmitter>(
            TILEmitter emitter,
            ArithmeticBasicValueType sourceType,
            ArithmeticBasicValueType targetType) =>
            emitter.EmitCall(Vec128Operations.GetConvertOperation32(
                sourceType,
                targetType));

        public override void ConvertSoftware64<TILEmitter>(
            TILEmitter emitter,
            ArithmeticBasicValueType sourceType,
            ArithmeticBasicValueType targetType) =>
            emitter.EmitCall(Vec128Operations.GetConvertOperation64(
                sourceType,
                targetType));

        public override void Convert32<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode source,
            VelocityWarpOperationMode target) =>
            emitter.EmitCall(Vec128Operations.GetConvertOperation32(
                source.GetArithmeticBasicValueType(is64Bit: false),
                target.GetArithmeticBasicValueType(is64Bit: false)));

        public override void Convert64<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode source,
            VelocityWarpOperationMode target) =>
            emitter.EmitCall(Vec128Operations.GetConvertOperation64(
                source.GetArithmeticBasicValueType(is64Bit: true),
                target.GetArithmeticBasicValueType(is64Bit: true)));

        public override void Convert32To64<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetConvert32To64Operation(mode));

        public override void Convert64To32<TILEmitter>(
            TILEmitter emitter,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetConvert64To32Operation(mode));

        #endregion

        #region Arithmetics

        public override void UnaryOperation32<TILEmitter>(
            TILEmitter emitter,
            UnaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetUnaryOperation32(kind, mode));

        public override void UnaryOperation64<TILEmitter>(
            TILEmitter emitter,
            UnaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetUnaryOperation64(kind, mode));

        public override void BinaryOperation32<TILEmitter>(
            TILEmitter emitter,
            BinaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetBinaryOperation32(kind, mode));

        public override void BinaryOperation64<TILEmitter>(
            TILEmitter emitter,
            BinaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetBinaryOperation64(kind, mode));

        public override void TernaryOperation32<TILEmitter>(
            TILEmitter emitter,
            TernaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetTernaryOperation32(kind, mode));

        public override void TernaryOperation64<TILEmitter>(
            TILEmitter emitter,
            TernaryArithmeticKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetTernaryOperation64(kind, mode));

        #endregion

        #region Atomics

        public override void AtomicCompareExchange32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.AtomicCompareExchange32Method);

        public override void AtomicCompareExchange64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.AtomicCompareExchange64Method);

        public override void Atomic32<TILEmitter>(
            TILEmitter emitter,
            AtomicKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetAtomicOperation32(kind, mode));

        public override void Atomic64<TILEmitter>(
            TILEmitter emitter,
            AtomicKind kind,
            VelocityWarpOperationMode mode) =>
            emitter.EmitCall(Vec128Operations.GetAtomicOperation64(kind, mode));

        #endregion

        #region Threads

        public override void BarrierPopCount32<TILEmitter>(TILEmitter emitter)
        {
            emitter.Emit(OpCodes.Pop);
            emitter.EmitCall(Vec128Operations.BarrierPopCount32Method);
        }

        public override void BarrierAnd32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.BarrierAnd32Method);

        public override void BarrierOr32<TILEmitter>(TILEmitter emitter)
        {
            emitter.Emit(OpCodes.Pop);
            emitter.EmitCall(Vec128Operations.BarrierOr32Method);
        }

        public override void Shuffle32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Shuffle32Method);

        public override void ShuffleUp32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.ShuffleUp32Method);

        public override void SubShuffleUp32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.SubShuffleUp32Method);

        public override void ShuffleDown32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.ShuffleDown32Method);

        public override void SubShuffleDown32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.SubShuffleDown32Method);

        public override void ShuffleXor32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.ShuffleXor32Method);

        public override void SubShuffleXor32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.SubShuffleXor32Method);

        #endregion

        #region IO

        public override void Load8<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Load8Method);

        public override void Load16<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Load16Method);

        public override void Load32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Load32Method);

        public override void Load64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Load64Method);

        public override void Store8<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Store8Method);

        public override void Store16<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Store16Method);

        public override void Store32<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Store32Method);

        public override void Store64<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.Store64Method);

        #endregion

        #region Misc

        public override void DebugAssertFailed<TILEmitter>(TILEmitter emitter) =>
            emitter.EmitCall(Vec128Operations.DebugAssertFailedMethod);

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
            emitter.EmitCall(Vec128Operations.DumpWarp32Method);
        }

        public override void DumpWarp64<TILEmitter>(
            TILEmitter emitter,
            string? label = null)
        {
            if (string.IsNullOrEmpty(label))
                emitter.EmitConstant(string.Empty);
            else
                emitter.EmitConstant(label + ": ");
            emitter.EmitCall(Vec128Operations.DumpWarp64Method);
        }

        #endregion
    }
}

#endif
