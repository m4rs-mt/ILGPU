// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Structures.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new object value.
        /// </summary>
        /// <param name="instance">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateObjectValue(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = instance.GetType();
            if (type.IsPrimitive)
                return CreatePrimitiveValue(instance);
            if (type.IsEnum)
                return CreateEnumValue(instance);
            if (type.IsClass)
                throw new ArgumentOutOfRangeException(nameof(instance));
            var typeInfo = Context.TypeInformationManager.GetTypeInfo(type);

            var result = CreateNull(CreateType(type));
            for (int i = 0, e = typeInfo.NumFields; i < e; ++i)
            {
                var rawFieldValue = typeInfo.Fields[i].GetValue(instance);
                var fieldValue = CreateValue(rawFieldValue, typeInfo.FieldTypes[i]);
                result = CreateSetField(result, i, fieldValue);
            }
            return result;
        }

        /// <summary>
        /// Creates a load operation of an object field.
        /// </summary>
        /// <param name="objectValue">The object value.</param>
        /// <param name="fieldIndex">The field index to load.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetField(
            Value objectValue,
            int fieldIndex)
        {
            Debug.Assert(objectValue != null, "Invalid object node");

            var structType = objectValue.Type as StructureType;
            Debug.Assert(structType != null, "Invalid object structure type");
            Debug.Assert(fieldIndex >= 0 && fieldIndex < structType.NumChildren, "Invalid field index");

            if (objectValue is NullValue)
                return CreateNull(structType.Children[fieldIndex]);
            else if (objectValue is SetField setField && setField.FieldIndex == fieldIndex)
                return setField.Value;

            return CreateUnifiedValue(new GetField(
                Generation,
                objectValue,
                fieldIndex));
        }

        /// <summary>
        /// Creates a load operation of an object field using
        /// the given access chain. If the access chain is empty,
        /// the source value is returned.
        /// </summary>
        /// <param name="objectValue">The object value.</param>
        /// <param name="accessChain">The field index chain.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetField<TAccessChain>(
            Value objectValue,
            in TAccessChain accessChain)
            where TAccessChain : IReadOnlyList<int>
        {
            for (int i = 0, e = accessChain.Count; i < e; ++i)
                objectValue = CreateGetField(objectValue, accessChain[i]);
            return objectValue;
        }

        /// <summary>
        /// Creates a store operation of an object field.
        /// </summary>
        /// <param name="objectValue">The object value.</param>
        /// <param name="fieldIndex">The field index to store.</param>
        /// <param name="value">The field value to store.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSetField(
            Value objectValue,
            int fieldIndex,
            Value value)
        {
            Debug.Assert(objectValue != null, "Invalid object node");
            Debug.Assert(value != null, "Invalid value node");

            var structType = objectValue.Type as StructureType;
            Debug.Assert(structType != null, "Invalid object structure type");
            Debug.Assert(fieldIndex >= 0 && fieldIndex < structType.NumChildren, "Invalid field index");
            Debug.Assert(structType.Children[fieldIndex] == value.Type, "Incompatible value type");

            return CreateUnifiedValue(new SetField(
                Generation,
                objectValue,
                fieldIndex,
                value));
        }

        /// <summary>
        /// Creates a store operation of an object field using
        /// the given access chain. If the access chain is empty,
        /// the target value to set is returned.
        /// </summary>
        /// <param name="objectValue">The object value.</param>
        /// <param name="accessChain">The field index chain.</param>
        /// <param name="value">The field value to store.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSetField(
            Value objectValue,
            ImmutableArray<int> accessChain,
            Value value)
        {
            Debug.Assert(objectValue != null, "Invalid object node");

            var chainLength = accessChain.Length;

            if (chainLength == 0)
                return value;
            if (chainLength < 2)
                return CreateSetField(objectValue, accessChain[0], value);

            var chainElements = new Value[chainLength];
            chainElements[0] = objectValue;
            for (int i = 0, e = chainLength - 1; i < e; ++i)
                chainElements[i + 1] = CreateGetField(chainElements[i], accessChain[i]);

            // Insert element
            value = CreateSetField(chainElements[chainLength - 1], accessChain[chainLength - 1], value);

            // Build target chain
            for (int i = chainLength - 2; i >= 0; --i)
                value = CreateSetField(chainElements[i], accessChain[i], value);

            return value;
        }
    }
}
