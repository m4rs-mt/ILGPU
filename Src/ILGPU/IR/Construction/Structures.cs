﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Structures.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
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
            if (type.IsClass || type.IsArray)
                throw new NotSupportedException(
                    string.Format(ErrorMessages.NotSupportedClassType, type));
            var typeInfo = Context.TypeContext.GetTypeInfo(type);

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
        /// Creates a new structure value.
        /// </summary>
        /// <param name="structureType">The structure type.</param>
        /// <returns>The created empty structure value.</returns>
        public ValueReference CreateStructure(StructureType structureType) =>
            CreateNull(structureType);

        /// <summary>
        /// Creates a new structure instance value.
        /// </summary>
        /// <param name="values">The structure instance values.</param>
        /// <returns>The created structure instance value.</returns>
        public ValueReference CreateStructure(params ValueReference[] values)
        {
            Debug.Assert(values == null && values.Length > 0, "Invalid values");

            // Construct structure type
            var fieldTypes = ImmutableArray.CreateBuilder<TypeNode>(values.Length);
            foreach (var value in values)
                fieldTypes.Add(value.Type);
            var structureType = CreateStructureType(StructureType.Root, fieldTypes.MoveToImmutable());
            return CreateStructure(structureType, values);
        }

        /// <summary>
        /// Creates a new structure instance value.
        /// </summary>
        /// <param name="structureType">The structure type.</param>
        /// <param name="values">The structure instance values.</param>
        /// <returns>The created structure instance value.</returns>
        private ValueReference CreateStructure(StructureType structureType, params ValueReference[] values)
        {
            Debug.Assert(structureType != null, "Invalid structure type");
            Debug.Assert(values != null && values.Length > 0, "Invalid values");
            Debug.Assert(values.Length == structureType.NumFields, "Invalid values or structure type");

            // Create structure instance
            var instance = CreateStructure(structureType);
            for (int i = 0, e = values.Length; i < e; ++i)
                instance = CreateSetField(instance, i, values[i]);
            return instance;
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
            Debug.Assert(fieldIndex >= 0 && fieldIndex < structType.NumFields, "Invalid field index");

            // Try to combine different get and set operations on the same value
            var current = objectValue;
            for (; ; )
            {
                switch (current)
                {
                    case SetField setField:
                        if (setField.FieldIndex == fieldIndex)
                            return setField.Value;
                        current = setField.ObjectValue;
                        continue;
                    case NullValue _:
                        return CreateNull(structType.Fields[fieldIndex]);
                }

                // Value could not be resolved
                break;
            }

            return Append(new GetField(
                BasicBlock,
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
            Debug.Assert(fieldIndex >= 0 && fieldIndex < structType.NumFields, "Invalid field index");
            Debug.Assert(structType.Fields[fieldIndex] == value.Type, "Incompatible value type");

            return Append(new SetField(
                BasicBlock,
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
