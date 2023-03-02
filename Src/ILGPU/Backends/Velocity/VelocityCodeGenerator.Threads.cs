// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.Threads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter, TVerifier>
    {
        /// <inheritdoc />
        public void GenerateCode(GenericAtomic atomic)
        {
            // Load the target and the value
            Load(atomic.Target);
            Load(atomic.Value);
            Emitter.Emit(LocalOperation.Load, GetBlockMask(atomic.BasicBlock));

            // Get the appropriate atomic operation
            var warpMode = atomic.ArithmeticBasicValueType.GetWarpMode();
            var operation = atomic.IsTreatedAs32Bit()
                ? Instructions.GetAtomicOperation32(atomic.Kind, warpMode)
                : Instructions.GetAtomicOperation64(atomic.Kind, warpMode);

            // Call the operation implementation
            Emitter.EmitCall(operation);

            // Check whether we actually need the result
            if (!atomic.Uses.HasAny)
                Emitter.Emit(OpCodes.Pop);
            else
               Store(atomic);
        }

        /// <inheritdoc />
        public void GenerateCode(AtomicCAS atomicCAS)
        {
            // Load the target, the compare value and the value
            Load(atomicCAS.Target);
            Load(atomicCAS.Value);
            Load(atomicCAS.CompareValue);
            Emitter.Emit(LocalOperation.Load, GetBlockMask(atomicCAS.BasicBlock));

            // Get the appropriate atomic operation
            var operation = atomicCAS.IsTreatedAs32Bit()
                ? Instructions.AtomicCompareExchangeOperation32
                : Instructions.AtomicCompareExchangeOperation64;

            // Call the operation implementation
            Emitter.EmitCall(operation);
            Store(atomicCAS);
        }

        /// <inheritdoc />
        public void GenerateCode(GridIndexValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    Emitter.EmitCall(VelocityMultiprocessor.GetCurrentGridIdxMethodInfo);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(0);
                    Emitter.EmitCall(Instructions.GetConstValueOperation32(
                        VelocityWarpOperationMode.I));
                    break;
            }
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(GroupIndexValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    Emitter.EmitCall(Instructions.LaneIndexVectorOperation32);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(0);
                    ToWarpValue(is32Bit: true, VelocityWarpOperationMode.I);
                    break;
            }
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(GridDimensionValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    Emitter.EmitCall(VelocityMultiprocessor.GetCurrentGridDimMethodInfo);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(1);
                    ToWarpValue(is32Bit: true, VelocityWarpOperationMode.I);
                    break;
            }
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(GroupDimensionValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    Emitter.EmitCall(VelocityMultiprocessor.GetCurrentGroupDimMethodInfo);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(1);
                    ToWarpValue(is32Bit: true, VelocityWarpOperationMode.I);
                    break;
            }
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(WarpSizeValue value)
        {
            Emitter.EmitConstant(WarpSize);
            Emitter.EmitCall(
                Instructions.GetConstValueOperation32(VelocityWarpOperationMode.I));
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(LaneIdxValue value)
        {
            Emitter.EmitCall(Instructions.LaneIndexVectorOperation32);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(PredicateBarrier barrier)
        {
            // Load predicate
            Load(barrier.Predicate);
            Emitter.Emit(LocalOperation.Load, GetBlockMask(barrier.BasicBlock));

            // Load and call predicate operation
            var operation =
                Instructions.GetGroupPredicateBarrierOperation32(barrier.Kind);
            Emitter.EmitCall(operation);
            Store(barrier);
        }

        /// <inheritdoc />
        public void GenerateCode(Barrier barrier) =>
            Instructions.CallMemoryBarrier(Emitter);

        /// <inheritdoc />
        public void GenerateCode(Broadcast broadcast)
        {
            // Load the source variable
            Load(broadcast.Variable);
            Load(broadcast.Origin);

            // Get the appropriate broadcast operation
            var operation = broadcast.Kind == BroadcastKind.WarpLevel
                ? Instructions.GetWarpBroadcastOperation32(VerifierType)
                : Instructions.GetGroupBroadcastOperation32(VerifierType);

            // Emit the warp or group operation
            Emitter.EmitCall(operation);
            Store(broadcast);
        }

        /// <inheritdoc />
        public void GenerateCode(WarpShuffle shuffle)
        {
            // Load the source variable and the origin
            Load(shuffle.Variable);
            Load(shuffle.Origin);

            // Get the appropriate broadcast operation
            var operation = Instructions.GetWarpShuffleOperation32(
                shuffle.Kind,
                VerifierType);

            // Emit the shuffle operation
            Emitter.EmitCall(operation);
            Store(shuffle);
        }

        /// <inheritdoc />
        public void GenerateCode(SubWarpShuffle shuffle)
        {
            // Load the source variable, the origin, and the sub-warp width
            Load(shuffle.Variable);
            Load(shuffle.Origin);
            Load(shuffle.Width);

            // Get the appropriate broadcast operation
            var operation = Instructions.GetSubWarpShuffleOperation32(
                shuffle.Kind,
                VerifierType);

            // Emit the shuffle operation
            Emitter.EmitCall(operation);
            Store(shuffle);
        }
    }
}
