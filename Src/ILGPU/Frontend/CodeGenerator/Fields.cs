// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Fields.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Reflection;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Loads the value of a field specified by the given metadata token.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadField(
            Block block,
            IRBuilder builder,
            FieldInfo field)
        {
            if (field == null)
                throw this.GetInvalidILCodeException();

            var fieldValue = block.Pop();
            if (fieldValue.Type.IsPointerType)
            {
                // Load field from address
                block.Push(fieldValue);
                MakeLoadFieldAddress(block, builder, field);
                var fieldAddress = block.Pop();
                var fieldType = builder.CreateType(field.FieldType);
                block.Push(CreateLoad(
                    builder,
                    fieldAddress,
                    fieldType,
                    field.FieldType.ToTargetUnsignedFlags()));
            }
            else
            {
                // Load field from value
                var typeInfo = Context.TypeContext.GetTypeInfo(field.DeclaringType);
                if (!typeInfo.TryResolveIndex(field, out int fieldIndex))
                    throw this.GetInvalidILCodeException();
                block.Push(builder.CreateGetField(
                    fieldValue,
                    fieldIndex));
            }
        }

        /// <summary>
        /// Loads the address of a field specified by the given metadata token.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadFieldAddress(
            Block block,
            IRBuilder builder,
            FieldInfo field)
        {
            if (field == null)
                throw this.GetInvalidILCodeException();
            var targetType = field.DeclaringType;
            var targetPointerType = builder.CreatePointerType(
                builder.CreateType(targetType),
                MemoryAddressSpace.Generic);
            var address = block.Pop(targetPointerType, ConvertFlags.None);

            var typeInfo = Context.TypeContext.GetTypeInfo(targetType);
            if (!typeInfo.TryResolveIndex(field, out int fieldIndex))
                throw this.GetInvalidILCodeException();
            var fieldAddress = builder.CreateLoadFieldAddress(address, fieldIndex);

            block.Push(fieldAddress);
        }

        /// <summary>
        /// Loads a static field value and returns the created IR node.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="field">The field.</param>
        /// <returns>The loaded field value.</returns>
        private ValueReference CreateLoadStaticFieldValue(
            IRBuilder builder,
            FieldInfo field)
        {
            if (field == null)
                throw this.GetInvalidILCodeException();
            VerifyStaticFieldLoad(field);

            var fieldValue = field.GetValue(null);
            return fieldValue == null ?
                builder.CreateObjectValue(field.FieldType) :
                builder.CreateObjectValue(fieldValue);
        }

        /// <summary>
        /// Loads a static field value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadStaticField(
            Block block,
            IRBuilder builder,
            FieldInfo field)
        {
            block.Push(CreateLoadStaticFieldValue(builder, field));
        }

        /// <summary>
        /// Loads the address of a static field specified by the given metadata token.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadStaticFieldAddress(
            Block block,
            IRBuilder builder,
            FieldInfo field)
        {
            var fieldValue = CreateLoadStaticFieldValue(builder, field);
            var tempAlloca = CreateTempAlloca(fieldValue.Type);
            builder.CreateStore(tempAlloca, fieldValue);
            block.Push(tempAlloca);
        }

        /// <summary>
        /// Stores a value to a field.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="field">The field.</param>
        private void MakeStoreField(
            Block block,
            IRBuilder builder,
            FieldInfo field)
        {
            var fieldType = block.Builder.CreateType(field.FieldType);
            var value = block.Pop(fieldType, field.FieldType.ToTargetUnsignedFlags());
            MakeLoadFieldAddress(block, builder, field);
            var address = block.Pop();
            CreateStore(builder, address, value);
        }

        /// <summary>
        /// Stores a value to a static field.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="field">The field.</param>
        private void MakeStoreStaticField(Block block, FieldInfo field)
        {
            VerifyStaticFieldStore(field);

            // Consume the current value from the stack but do not emit a global store,
            // since we dont have any valid target address.
            // TODO: Stores to static fields could be automatically propagated to the .Net
            // runtime after kernel invocation. However, this remains as a future feature.
            block.Pop();
        }
    }
}
