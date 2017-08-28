// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Fields.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Reflection;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Loads the field address of the given field reference.
        /// </summary>
        /// <param name="field">The field to address.</param>
        /// <param name="value">The object value.</param>
        /// <returns>The computed field address.</returns>
        private LLVMValueRef LoadFieldAddress(FieldInfo field, Value value)
        {
            var name = string.Empty;
            do
            {
                Debug.Assert(value.ValueType.IsTreatedAsPtr());
                var mappedType = Unit.GetObjectType(value.ValueType.GetElementType());
                if (mappedType.TryResolveOffset(field, out int offset))
                {
                    value = new Value(field.FieldType, BuildStructGEP(Builder, value.LLVMValue, offset, name));
                    break;
                }
                var baseType = value.ValueType.BaseType;
                if (baseType == null)
                    throw new InvalidOperationException();
                value = new Value(baseType, BuildStructGEP(Builder, value.LLVMValue, 0, name));
            }
            while (value.ValueType.BaseType != null);
            return value.LLVMValue;
        }

        /// <summary>
        /// Loads the value of a field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void LoadField(FieldInfo field)
        {
            var value = CurrentBlock.Pop();
            var name = string.Empty;

            if (value.ValueType.IsTreatedAsPtr())
            {
                var address = LoadFieldAddress(field, value);
                CurrentBlock.Push(field.FieldType, BuildLoad(Builder, address, name));
            }
            else
            {
                // It is a value object
                do
                {
                    var mappedType = Unit.GetObjectType(value.ValueType);
                    if (mappedType.TryResolveOffset(field, out int offset))
                    {
                        value = new Value(field.FieldType, BuildExtractValue(Builder, value.LLVMValue, offset, name));
                        break;
                    }
                    var baseType = value.ValueType.BaseType;
                    if (baseType == null)
                        throw new InvalidOperationException();
                    value = new Value(baseType, BuildExtractValue(Builder, value.LLVMValue, 0, name));
                }
                while (value.ValueType.BaseType != null);
                CurrentBlock.Push(value);
            }
        }

        /// <summary>
        /// Loads the address of a field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void LoadFieldAddress(FieldInfo field)
        {
            var value = CurrentBlock.Pop();
            CurrentBlock.Push(field.FieldType.MakePointerType(), LoadFieldAddress(field, value));
        }

        /// <summary>
        /// Loads a static field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void LoadStaticField(FieldInfo field)
        {
            VerifyStaticFieldLoad(CompilationContext, Unit.Flags, field);

            var value = field.GetValue(null);
            CurrentBlock.Push(field.FieldType, Unit.GetValue(field.FieldType, value));
        }

        /// <summary>
        /// Loads the address of a static field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void LoadStaticFieldAddress(FieldInfo field)
        {
            throw CompilationContext.GetNotSupportedException(
                ErrorMessages.NotSupportedLoadOfStaticFieldAddress, field);
        }

        /// <summary>
        /// Stores a value to a field.
        /// </summary>
        /// <param name="field">The field.</param>
        private void StoreField(FieldInfo field)
        {
            var value = CurrentBlock.Pop(field.FieldType);
            var fieldValue = CurrentBlock.Pop();
            var fieldAddress = LoadFieldAddress(field, fieldValue);
            BuildStore(Builder, value.LLVMValue, fieldAddress);
        }

        /// <summary>
        /// Stores a value to a static field.
        /// </summary>
        /// <param name="field">The field.</param>
        private void StoreStaticField(FieldInfo field)
        {
            VerifyStaticFieldStore(CompilationContext, Unit.Flags, field);

            // Consume the current value from the stack but do not emit a global store,
            // since we dont have any valid target address.
            // TODO: Stores to static fields could be automatically propagated to the .Net
            // runtime after kernel invocation. However, this remains as a future feature.
            CurrentBlock.Pop();
        }
    }
}
