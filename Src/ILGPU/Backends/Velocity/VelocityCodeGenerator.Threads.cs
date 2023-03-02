// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.Threads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Values;
using System;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter>
    {
        /// <inheritdoc />
        public void GenerateCode(GenericAtomic atomic)
        {
            // Load the target and the value
            Emitter.Emit(LocalOperation.Load, GetBlockMask(atomic.BasicBlock));
            Load(atomic.Target);
            Load(atomic.Value);

            // Get the appropriate atomic operation
            var warpMode = atomic.ArithmeticBasicValueType.GetWarpMode();
            if (atomic.IsTreatedAs32Bit())
                Specializer.Atomic32(Emitter, atomic.Kind, warpMode);
            else
                Specializer.Atomic64(Emitter, atomic.Kind, warpMode);

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
            Emitter.Emit(LocalOperation.Load, GetBlockMask(atomicCAS.BasicBlock));
            Load(atomicCAS.Target);
            Load(atomicCAS.Value);
            Load(atomicCAS.CompareValue);

            // Get the appropriate atomic operation
            if (atomicCAS.IsTreatedAs32Bit())
                Specializer.AtomicCompareExchange32(Emitter);
            else
                Specializer.AtomicCompareExchange64(Emitter);

            // Store the result
            Store(atomicCAS);
        }

        /// <inheritdoc />
        public void GenerateCode(GridIndexValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    // Load the first context argument and query the grid index
                    VelocityTargetSpecializer.GetGridIndex(Emitter);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(0);
                    break;
            }
            Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(GroupIndexValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    Specializer.LoadLaneIndexVector32(Emitter);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(0);
                    Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
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
                    VelocityTargetSpecializer.GetGridDim(Emitter);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(1);
                    break;
            }
            Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(GroupDimensionValue value)
        {
            switch (value.Dimension)
            {
                case DeviceConstantDimension3D.X:
                    VelocityTargetSpecializer.GetGroupDim(Emitter);
                    break;
                case DeviceConstantDimension3D.Y:
                case DeviceConstantDimension3D.Z:
                    Emitter.LoadIntegerConstant(1);
                    break;
            }
            Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(WarpSizeValue value)
        {
            Specializer.LoadWarpSize32(Emitter);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(LaneIdxValue value)
        {
            Specializer.LoadLaneIndexVector32(Emitter);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(PredicateBarrier barrier)
        {
            // Load predicate
            Emitter.Emit(LocalOperation.Load, GetBlockMask(barrier.BasicBlock));
            Load(barrier.Predicate);

            // Load and call predicate operation
            bool is32Bit = barrier.IsTreatedAs32Bit();
            switch (barrier.Kind)
            {
                case PredicateBarrierKind.PopCount:
                    if (is32Bit)
                        Specializer.BarrierPopCount32(Emitter);
                    else
                        Specializer.BarrierPopCount64(Emitter);
                    break;
                case PredicateBarrierKind.And:
                    if (is32Bit)
                        Specializer.BarrierAnd32(Emitter);
                    else
                        Specializer.BarrierAnd64(Emitter);
                    break;
                case PredicateBarrierKind.Or:
                    if (is32Bit)
                        Specializer.BarrierOr32(Emitter);
                    else
                        Specializer.BarrierOr64(Emitter);
                    break;
                default:
                    throw new NotSupportedException();
            }

            Store(barrier);
        }

        /// <inheritdoc />
        public void GenerateCode(Barrier barrier) =>
            Specializer.Barrier(Emitter);

        /// <inheritdoc />
        public void GenerateCode(Broadcast broadcast)
        {
            // Load the source variable
            Emitter.Emit(LocalOperation.Load, GetBlockMask(broadcast.BasicBlock));
            Load(broadcast.Variable);
            Load(broadcast.Origin);

            // Get the appropriate broadcast operation
            if (broadcast.IsTreatedAs32Bit())
                Specializer.Broadcast32(Emitter);
            else
                Specializer.Broadcast64(Emitter);

            Store(broadcast);
        }

        /// <inheritdoc />
        public void GenerateCode(WarpShuffle shuffle)
        {
            // Load the source variable and the origin
            Emitter.Emit(LocalOperation.Load, GetBlockMask(shuffle.BasicBlock));
            Load(shuffle.Variable);
            Load(shuffle.Origin);

            // Get the appropriate broadcast operation
            bool is32Bit = shuffle.IsTreatedAs32Bit();
            switch (shuffle.Kind)
            {
                case ShuffleKind.Generic:
                    if (is32Bit)
                        Specializer.Shuffle32(Emitter);
                    else
                        Specializer.Shuffle64(Emitter);
                    break;
                case ShuffleKind.Up:
                    if (is32Bit)
                        Specializer.ShuffleUp32(Emitter);
                    else
                        Specializer.ShuffleUp64(Emitter);
                    break;
                case ShuffleKind.Down:
                    if (is32Bit)
                        Specializer.ShuffleDown32(Emitter);
                    else
                        Specializer.ShuffleDown64(Emitter);
                    break;
                case ShuffleKind.Xor:
                    if (is32Bit)
                        Specializer.ShuffleXor32(Emitter);
                    else
                        Specializer.ShuffleXor64(Emitter);
                    break;
                default:
                    throw new NotSupportedException();
            }

            // Store the shuffle result
            Store(shuffle);
        }

        /// <inheritdoc />
        public void GenerateCode(SubWarpShuffle shuffle)
        {
            // Load the source variable, the origin, and the sub-warp width
            Emitter.Emit(LocalOperation.Load, GetBlockMask(shuffle.BasicBlock));
            Load(shuffle.Variable);
            Load(shuffle.Origin);
            Load(shuffle.Width);

            // Get the appropriate broadcast operation
            bool is32Bit = shuffle.IsTreatedAs32Bit();
            switch (shuffle.Kind)
            {
                case ShuffleKind.Up:
                    if (is32Bit)
                        Specializer.SubShuffleUp32(Emitter);
                    else
                        Specializer.SubShuffleUp64(Emitter);
                    break;
                case ShuffleKind.Down:
                    if (is32Bit)
                        Specializer.SubShuffleDown32(Emitter);
                    else
                        Specializer.SubShuffleDown64(Emitter);
                    break;
                case ShuffleKind.Xor:
                    if (is32Bit)
                        Specializer.SubShuffleXor32(Emitter);
                    else
                        Specializer.SubShuffleXor64(Emitter);
                    break;
                default:
                    throw new NotSupportedException();
            }

            // Store the shuffle result
            Store(shuffle);
        }
    }
}
