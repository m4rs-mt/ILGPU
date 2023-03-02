// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime.Velocity;
using System.Runtime.InteropServices;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter, TVerifier>
    {
        /// <inheritdoc />
        public void GenerateCode(IntAsPointerCast cast)
        {
            // Load the integer information
            Load(cast.Value);

            // Check whether we have to convert it to a 64bit value
            if (cast.SourceType.BasicValueType.IsTreatedAs32Bit())
            {
                // Convert it to a 64bit pointer
                Emitter.EmitCall(Instructions.GetConvertWidenOperation32(
                    VelocityWarpOperationMode.U));
            }

            // The integer can now be interpreted as pointer
            Store(cast);
        }

        /// <inheritdoc />
        public void GenerateCode(PointerAsIntCast cast) =>
            Alias(cast, GetLocal(cast.Value));

        /// <inheritdoc />
        public void GenerateCode(PointerCast cast) =>
            Alias(cast, GetLocal(cast.Value));

        /// <inheritdoc />
        public void GenerateCode(AddressSpaceCast value) =>
            Alias(value, GetLocal(value.Value));

        /// <inheritdoc />
        public void GenerateCode(LoadElementAddress value)
        {
            // Load the raw element offset to multiply
            Load(value.Offset);

            // Widen the source address if necessary
            if (value.Is32BitAccess)
            {
                Emitter.EmitCall(
                    Instructions.GetConvertWidenOperation32(VelocityWarpOperationMode.I));
            }

            // Load the source type information and the element size to multiply
            var sourceType = value.Source.Type.As<AddressSpaceType>(value);
            Emitter.EmitConstant((long)sourceType.ElementType.Size);
            ToWarpValue(is32Bit: false, VelocityWarpOperationMode.U);

            // Load the source vector to add
            Load(value.Source);

            // Perform the actual offset computation
            var madOperation = Instructions.GetTernaryOperation64(
                TernaryArithmeticKind.MultiplyAdd,
                VelocityWarpOperationMode.U);
            Emitter.EmitCall(madOperation);
            Store(value);
        }

        /// <inheritdoc />
        public void GenerateCode(LoadFieldAddress value)
        {
            // Compute the actual field offset based on the vectorized type
            long offset = value.StructureType.GetOffset(value.FieldSpan.Access.Index);

            // If this results in an actual byte offset... add it
            if (offset != 0L)
            {
                // Load the source addresses
                Load(value.Source);

                // Load constant
                Emitter.EmitConstant(offset);
                ToWarpValue(is32Bit: false, VelocityWarpOperationMode.U);

                // Adjust address
                Emitter.EmitCall(Instructions.GetBinaryOperation64(
                    BinaryArithmeticKind.Add,
                    VelocityWarpOperationMode.U));

                // Store the newly computed offset
                Store(value);
            }
            else
            {
                Alias(value, GetLocal(value.Source));
            }
        }

        /// <inheritdoc />
        public void GenerateCode(AlignTo value) =>
            // Not implemented at the moment as we do not make use of bulk-vector loads
            // and stores at the moment
            Alias(value, GetLocal(value.Source));

        /// <inheritdoc />
        public void GenerateCode(AsAligned value) =>
            // Not implemented at the moment as we do not make use of bulk-vector loads
            // and stores at the moment
            Alias(value, GetLocal(value.Source));

        /// <inheritdoc />
        public void GenerateCode(DynamicMemoryLengthValue value) =>
            throw value.GetNotSupportedException(ErrorMessages
                .NotSupportedDynamicSharedMemoryAllocations);

        /// <inheritdoc />
        public void GenerateCode(LanguageEmitValue value) { }
    }
}
