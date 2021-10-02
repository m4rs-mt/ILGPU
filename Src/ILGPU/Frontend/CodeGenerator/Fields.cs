// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Fields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Reflection;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Helper function to compute field refence for the specified field of a type.
        /// </summary>
        /// <param name="type">The type node.</param>
        /// <param name="field">The field.</param>
        /// <returns>The target field span.</returns>
        private FieldSpan ComputeFieldSpan(TypeNode type, FieldInfo field)
        {
            var typeInfo = TypeContext.GetTypeInfo(field.FieldType);
            var parentInfo = TypeContext.GetTypeInfo(field.DeclaringType);
            var fieldIndex = parentInfo.GetAbsoluteIndex(field);

            if (type.IsStructureType)
            {
                var structureType = type.As<StructureType>(Location);
                fieldIndex = structureType.RemapFieldIndex(fieldIndex);
            }
            return new FieldSpan(fieldIndex, typeInfo.NumFlattendedFields);
        }

        /// <summary>
        /// Loads the value of a field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeLoadField(FieldInfo field)
        {
            if (field == null)
                throw Location.GetInvalidOperationException();

            var fieldValue = Block.Pop();
            if (fieldValue.Type.IsPointerType)
            {
                // Load field from address
                Block.Push(fieldValue);
                MakeLoadFieldAddress(field);
                var fieldAddress = Block.Pop();
                var fieldType = Builder.CreateType(field.FieldType);
                Block.Push(CreateLoad(
                    fieldAddress,
                    fieldType,
                    field.FieldType.ToTargetUnsignedFlags()));
            }
            else
            {
                // Load field from value
                var fieldSpan = ComputeFieldSpan(fieldValue.Type, field);

                // Check whether we have to get multiple elements
                var getField = Builder.CreateGetField(
                    Location,
                    fieldValue,
                    fieldSpan);
                if (fieldSpan.Span == 1)
                {
                    Block.Push(LoadOntoEvaluationStack(
                        getField,
                        field.FieldType.ToTargetUnsignedFlags()));
                }
                else
                {
                    Block.Push(getField);
                }
            }
        }

        /// <summary>
        /// Loads the address of a field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeLoadFieldAddress(FieldInfo field)
        {
            if (field == null)
                throw Location.GetInvalidOperationException();
            var targetPointerType = Builder.CreatePointerType(
                Builder.CreateType(field.DeclaringType),
                MemoryAddressSpace.Generic);
            var address = Block.Pop(
                targetPointerType,
                ConvertFlags.None);

            var fieldSpan = ComputeFieldSpan(targetPointerType.ElementType, field);
            var fieldAddress = Builder.CreateLoadFieldAddress(
                Location,
                address,
                fieldSpan);
            Block.Push(fieldAddress);
        }

        /// <summary>
        /// Loads a static field value and returns the created IR node.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The loaded field value.</returns>
        private ValueReference CreateLoadStaticFieldValue(FieldInfo field)
        {
            if (field == null)
                throw Location.GetInvalidOperationException();
            VerifyStaticFieldLoad(field);

            var fieldValue = field.GetValue(null);
            return fieldValue == null ?
                Builder.CreateObjectValue(Location, field.FieldType) :
                Builder.CreateObjectValue(Location, fieldValue);
        }

        /// <summary>
        /// Loads a static field value.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeLoadStaticField(FieldInfo field) =>
            Block.Push(CreateLoadStaticFieldValue(field));

        /// <summary>
        /// Loads the address of a static field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeLoadStaticFieldAddress(FieldInfo field)
        {
            var fieldValue = CreateLoadStaticFieldValue(field);
            var tempAlloca = CreateTempAlloca(fieldValue.Type);
            Builder.CreateStore(Location, tempAlloca, fieldValue);
            Block.Push(tempAlloca);
        }

        /// <summary>
        /// Stores a value to a field.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeStoreField(FieldInfo field)
        {
            var fieldType = Builder.CreateType(field.FieldType);
            var value = Block.Pop(
                fieldType,
                field.FieldType.ToTargetUnsignedFlags());
            MakeLoadFieldAddress(field);
            var address = Block.Pop();
            CreateStore(address, value);
        }

        /// <summary>
        /// Stores a value to a static field.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeStoreStaticField(FieldInfo field)
        {
            VerifyStaticFieldStore(field);

            // Consume the current value from the stack but do not emit a global store,
            // since we don't have any valid target address.
            // TODO: Stores to static fields could be automatically propagated to the
            // .Net runtime after kernel invocation. However, this remains as a future
            // feature.
            Block.Pop();
        }
    }
}
