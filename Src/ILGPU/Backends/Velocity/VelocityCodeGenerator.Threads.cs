// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
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
                    // Load global index and compute the actual grid index
                    LoadGlobalIndexScalar();
                    LoadGroupDimScalar();
                    Emitter.Emit(OpCodes.Div);
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
                    LoadGridDimScalar();
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
                    LoadGroupDimScalar();
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
            if (!barrier.IsTreatedAs32Bit())
                throw new InternalCompilerException();
            LoadGroupDimScalar();
            switch (barrier.Kind)
            {
                case PredicateBarrierKind.PopCount:
                    Specializer.BarrierPopCount32(Emitter);
                    break;
                case PredicateBarrierKind.And:
                    Specializer.BarrierAnd32(Emitter);
                    break;
                case PredicateBarrierKind.Or:
                    Specializer.BarrierOr32(Emitter);
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
        public void GenerateCode(Broadcast broadcast) =>
            throw new InternalCompilerException();

        /// <inheritdoc />
        public void GenerateCode(WarpShuffle shuffle)
        {
            // Load the source variable and the origin
            Emitter.Emit(LocalOperation.Load, GetBlockMask(shuffle.BasicBlock));
            Load(shuffle.Variable);
            Load(shuffle.Origin);

            // Make sure we are compiling 32bit versions only
            if (!shuffle.IsTreatedAs32Bit())
                throw new InternalCompilerException();

            switch (shuffle.Kind)
            {
                case ShuffleKind.Generic:
                    Specializer.Shuffle32(Emitter);
                    break;
                case ShuffleKind.Up:
                    Specializer.ShuffleUp32(Emitter);
                    break;
                case ShuffleKind.Down:
                    Specializer.ShuffleDown32(Emitter);
                    break;
                case ShuffleKind.Xor:
                    Specializer.ShuffleXor32(Emitter);
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

            // Make sure we are compiling 32bit versions only
            if (!shuffle.IsTreatedAs32Bit())
                throw new InternalCompilerException();

            // Get the appropriate broadcast operation
            switch (shuffle.Kind)
            {
                case ShuffleKind.Up:
                    Specializer.SubShuffleUp32(Emitter);
                    break;
                case ShuffleKind.Down:
                    Specializer.SubShuffleDown32(Emitter);
                    break;
                case ShuffleKind.Xor:
                    Specializer.SubShuffleXor32(Emitter);
                    break;
                default:
                    throw new NotSupportedException();
            }

            // Store the shuffle result
            Store(shuffle);
        }
    }
}
