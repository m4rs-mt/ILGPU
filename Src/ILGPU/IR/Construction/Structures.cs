// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Structures.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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

            var managedType = instance.GetType();
            if (managedType.IsPrimitive)
                return CreatePrimitiveValue(instance);
            if (managedType.IsEnum)
                return CreateEnumValue(instance);
            if (managedType.IsClass || managedType.IsArray)
            {
                throw new NotSupportedException(
                    string.Format(ErrorMessages.NotSupportedClassType, managedType));
            }

            var typeInfo = Context.TypeContext.GetTypeInfo(managedType);
            var type = CreateType(managedType);
            if (!(type is StructureType structureType))
            {
                // This type has zero or one element and can be used without an
                // enclosing structure value
                return typeInfo.NumFields > 0
                    ? CreateObjectValue(typeInfo.Fields[0].GetValue(instance))
                    : CreateNull(type);
            }
                var instanceBuilder = CreateStructure(structureType);
                for (int i = 0, e = typeInfo.NumFields; i < e; ++i)
                {
                    var rawFieldValue = typeInfo.Fields[i].GetValue(instance);
                    Value fieldValue = CreateObjectValue(rawFieldValue);
                    if (fieldValue.Type is StructureType nestedStructureType)
                    {
                        // Extract all nested fields and insert them into the builder
                        foreach (var (_, access) in nestedStructureType)
                        {
                            instanceBuilder.Add(
                                CreateGetField(fieldValue, access));
                        }
                    }
                    else
                    {
                        instanceBuilder.Add(fieldValue);
                    }
                }
                return instanceBuilder.Seal();
        }

        /// <summary>
        /// Creates a new structure instance builder.
        /// </summary>
        /// <param name="structureType">The structure type.</param>
        /// <returns>The created structure instance builder.</returns>
        public StructureValue.Builder CreateStructure(StructureType structureType) =>
            new StructureValue.Builder(this, structureType);

        /// <summary>
        /// Creates a new dynamic structure instance builder.
        /// </summary>
        /// <returns>The created structure instance builder.</returns>
        public StructureValue.DynamicBuilder CreateDynamicStructure() =>
            CreateDynamicStructure(2);

        /// <summary>
        /// Creates a new dynamic structure instance builder.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <returns>The created structure instance builder.</returns>
        public StructureValue.DynamicBuilder CreateDynamicStructure(int capacity) =>
            new StructureValue.DynamicBuilder(this, capacity);

        /// <summary>
        /// Creates a new dynamic structure instance.
        /// </summary>
        /// <param name="item1">The first item.</param>
        /// <param name="item2">The second item.</param>
        /// <returns>The created structure instance value.</returns>
        public ValueReference CreateDynamicStructure(
            ValueReference item1,
            ValueReference item2)
        {
            var builder = CreateDynamicStructure(2);
            builder.Add(item1);
            builder.Add(item2);
            return builder.Seal();
        }

        /// <summary>
        /// Creates a new dynamic structure instance.
        /// </summary>
        /// <param name="item1">The first item.</param>
        /// <param name="item2">The second item.</param>
        /// <param name="item3">The third item.</param>
        /// <returns>The created structure instance value.</returns>
        public ValueReference CreateDynamicStructure(
            ValueReference item1,
            ValueReference item2,
            ValueReference item3)
        {
            var builder = CreateDynamicStructure(3);
            builder.Add(item1);
            builder.Add(item2);
            builder.Add(item3);
            return builder.Seal();
        }

        /// <summary>
        /// Creates a new dynamic structure instance.
        /// </summary>
        /// <param name="values">The list of all values to add.</param>
        /// <returns>The created structure instance value.</returns>
        public ValueReference CreateDynamicStructure<TList>(TList values)
            where TList : IReadOnlyList<ValueReference>
        {
            var builder = CreateDynamicStructure(values.Count);
            for (int i = 0, e = values.Count; i < e; ++i)
                builder.Add(values[i]);
            return builder.Seal();
        }

        /// <summary>
        /// Creates a new structure instance value.
        /// </summary>
        /// <param name="builder">The structure instance builder.</param>
        /// <returns>The created structure instance value.</returns>
        internal ValueReference FinishStructureBuilder<TBuilder>(in TBuilder builder)
            where TBuilder : struct, StructureValue.IInternalBuilder
        {
            if (builder.Count < 1)
                return CreateNull(this.CreateEmptyStructureType());
            if (builder.Count < 2)
                return builder[0];

            // Construct structure instance
            var values = builder.Seal(out var structureType);
            return Append(new StructureValue(
                BasicBlock,
                structureType,
                values));
        }

        /// <summary>
        /// Creates a load operation of an object field.
        /// </summary>
        /// <param name="objectValue">The object value.</param>
        /// <param name="fieldSpan">The field span.</param>
        /// <returns>A reference to the requested value.</returns>
        [SuppressMessage(
            "Style",
            "IDE0046:Convert to conditional expression",
            Justification = "Avoid nested if conditionals")]
        public ValueReference CreateGetField(Value objectValue, FieldSpan fieldSpan)
        {
            Debug.Assert(objectValue != null, "Invalid object node");

            var structType = objectValue.Type as StructureType;
            if (structType == null && fieldSpan.Span < 2)
                return objectValue;

            Debug.Assert(structType != null, "Invalid object structure type");
            if (objectValue is StructureValue structureValue)
                return structureValue.Get(this, fieldSpan);

            return objectValue is NullValue
                ? CreateNull(structType.Get(Context, fieldSpan))
                : Append(new GetField(
                    Context,
                    BasicBlock,
                    objectValue,
                    fieldSpan));
        }

        /// <summary>
        /// Creates a store operation of an object field using the given field access.
        /// </summary>
        /// <param name="objectValue">The object value.</param>
        /// <param name="fieldSpan">The field span.</param>
        /// <param name="value">The field value to store.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSetField(
            Value objectValue,
            FieldSpan fieldSpan,
            Value value)
        {
            Debug.Assert(objectValue != null, "Invalid object node");
            Debug.Assert(value != null, "Invalid value node");

            var structureType = objectValue.Type as StructureType;
            Debug.Assert(structureType != null, "Invalid object structure type");
            Debug.Assert(
                structureType.Get(Context, fieldSpan) == value.Type,
                "Incompatible value type");

            // Fold structure values
            if (objectValue is StructureValue structureValue)
            {
                var instance = CreateStructure(structureType);
                foreach (Value fieldValue in structureValue.Nodes)
                    instance.Add(fieldValue);

                for (int i = 0; i < fieldSpan.Span; ++i)
                {
                    instance[fieldSpan.Index + i] = CreateGetField(
                        value,
                        new FieldSpan(i));
                }

                return instance.Seal();
            }

            return objectValue is NullValue && fieldSpan.Span == structureType.NumFields
                ? (ValueReference)value
                : Append(new SetField(
                    BasicBlock,
                    objectValue,
                    fieldSpan,
                    value));
        }
    }
}
