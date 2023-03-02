// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.IO.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime.Velocity;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter, TVerifier>
    {
        /// <inheritdoc/>
        public void GenerateCode(Load load)
        {
            var mask = blockMasks[load.BasicBlock];
            var source = GetLocal(load.Source);
            Instructions.CreateLoad(
                Emitter,
                mask,
                source,
                load.Type,
                TypeGenerator);
            Store(load);
        }

        /// <summary>
        /// Generates code to store primitive values and pointers from memory while using
        /// the given mask to differentiate between active and inactive lanes.
        /// </summary>
        private void GenerateNonStructureStore(TypeNode typeNode)
        {
            var basicValueType = typeNode switch
            {
                PrimitiveType primitiveType => primitiveType.BasicValueType,
                PaddingType paddingType => paddingType.BasicValueType,
                PointerType _ => BasicValueType.Int64,
                _ => throw typeNode.GetNotSupportedException(
                    ErrorMessages.NotSupportedType, typeNode)
            };

            Emitter.EmitCall(Instructions.GetIOOperation(
                basicValueType,
                WarpSize).Store);
        }

        /// <inheritdoc/>
        public void GenerateCode(Store store)
        {
            var mask = GetBlockMask(store.BasicBlock);
            var target = GetLocal(store.Target);
            var value = GetLocal(store.Value);
            var type = store.Value.Type;

            if (type is StructureType structureType)
            {
                // Iterate over all fields and store them
                var vectorizedType = GetVectorizedType(type);
                foreach (var (fieldType, fieldAccess) in structureType)
                {
                    // Load the current field value
                    Emitter.Emit(LocalOperation.LoadAddress, value);
                    Emitter.LoadField(vectorizedType, fieldAccess.Index);

                    // Adjust the target offset
                    long fieldOffset = structureType.GetOffset(fieldAccess);
                    Emitter.EmitConstant(fieldOffset);
                    Emitter.EmitCall(Instructions.GetConstValueOperation64(
                        VelocityWarpOperationMode.I));
                    Emitter.Emit(LocalOperation.Load, target);
                    Emitter.EmitCall(Instructions.GetBinaryOperation64(
                        BinaryArithmeticKind.Add,
                        VelocityWarpOperationMode.U));

                    // Store the field into memory
                    Emitter.Emit(LocalOperation.Load, mask);
                    GenerateNonStructureStore(fieldType);
                }
            }
            else
            {
                Emitter.Emit(LocalOperation.Load, value);
                Emitter.Emit(LocalOperation.Load, target);
                Emitter.Emit(LocalOperation.Load, mask);

                GenerateNonStructureStore(type);
            }
        }
    }
}
