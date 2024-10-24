// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.IO.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter>
    {
        /// <inheritdoc/>
        public void GenerateCode(Load load)
        {
            var mask = GetBlockMask(load.BasicBlock);
            var source = GetLocal(load.Source);
            CreateLoad(mask, source, load.Type);
            Store(load);
        }

        /// <summary>
        /// Creates code to load primitive values and pointers from memory while using
        /// the given mask to differentiate between active and inactive lanes.
        /// </summary>
        private void CreateNonStructureLoad(TypeNode typeNode)
        {
            switch (typeNode)
            {
                case PrimitiveType primitiveType:
                    Specializer.Load(Emitter, primitiveType.BasicValueType);
                    break;
                case PointerType _:
                    Specializer.Load(Emitter, BasicValueType.Int64);
                    break;
                default:
                    throw typeNode.GetNotSupportedException(
                        ErrorMessages.NotSupportedType);
            }
        }

        /// <summary>
        /// Creates a sequence of load instructions to load a vectorized value via
        /// specialized IO operations.
        /// </summary>
        private void CreateLoad(ILLocal mask, ILLocal source, TypeNode typeNode)
        {
            if (typeNode is StructureType structureType)
            {
                // Allocate a new temporary allocation to fill all fields
                var vectorizedType = TypeGenerator.GetVectorizedType(structureType);
                var temporary = Emitter.DeclareLocal(vectorizedType);
                Emitter.LoadNull(temporary);

                // Fill the temporary structure instance with values
                foreach (var (fieldType, fieldAccess) in structureType)
                {
                    // Load the variable address
                    Emitter.Emit(LocalOperation.LoadAddress, temporary);

                    // Load the local mask
                    Emitter.Emit(LocalOperation.Load, mask);

                    // Adjust the actual source address based on offsets in the type
                    // definition
                    // Adjust the target offset
                    long fieldOffset = structureType.GetOffset(fieldAccess);
                    Emitter.EmitConstant(fieldOffset);
                    Specializer.ConvertScalarTo64(Emitter, VelocityWarpOperationMode.I);
                    Emitter.Emit(LocalOperation.Load, source);
                    Specializer.BinaryOperation64(
                        Emitter,
                        BinaryArithmeticKind.Add,
                        VelocityWarpOperationMode.U);

                    // Load the converted field type
                    CreateNonStructureLoad(fieldType);

                    // Store it into out structure field
                    Emitter.StoreField(vectorizedType, fieldAccess.Index);
                }

                // Load local variable onto the stack containing all required information
                Emitter.Emit(LocalOperation.Load, temporary);
            }
            else
            {
                // Load the local mask
                Emitter.Emit(LocalOperation.Load, mask);

                // Load the type directly
                Emitter.Emit(LocalOperation.Load, source);

                CreateNonStructureLoad(typeNode);
            }
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

            Specializer.Store(Emitter, basicValueType);
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
                    // Load the current mask
                    Emitter.Emit(LocalOperation.Load, mask);

                    // Load target directly
                    Emitter.Emit(LocalOperation.Load, target);

                    // Get the source field offset in bytes
                    long fieldOffset = structureType.GetOffset(fieldAccess);
                    if (fieldOffset != 0L)
                    {
                        Emitter.EmitConstant(fieldOffset);
                        Specializer.ConvertScalarTo64(Emitter,
                            VelocityWarpOperationMode.U);

                        // Load target address and adjust offset
                        Specializer.BinaryOperation64(
                            Emitter,
                            BinaryArithmeticKind.Add,
                            VelocityWarpOperationMode.U);
                    }

                    // Load the current field value
                    Emitter.Emit(LocalOperation.Load, value);
                    Emitter.LoadField(vectorizedType, fieldAccess.Index);

                    // Store the field into memory
                    GenerateNonStructureStore(fieldType);
                }
            }
            else
            {
                Emitter.Emit(LocalOperation.Load, mask);
                Emitter.Emit(LocalOperation.Load, target);
                Emitter.Emit(LocalOperation.Load, value);

                GenerateNonStructureStore(type);
            }
        }
    }
}
