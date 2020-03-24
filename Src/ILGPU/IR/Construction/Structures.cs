// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
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
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a primitive value if possible.
        /// </summary>
        /// <param name="instance">The instance value.</param>
        /// <param name="type">The resolved type.</param>
        /// <returns>The primitive value reference (if any).</returns>
        private ValueReference CreatePrimitiveObjectValue(object instance, out Type type)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            type = instance.GetType();
            if (type.IsPrimitive)
                return CreatePrimitiveValue(instance);
            if (type.IsEnum)
                return CreateEnumValue(instance);
            if (type.IsClass || type.IsArray)
                throw new NotSupportedException(
                    string.Format(ErrorMessages.NotSupportedClassType, type));

            return default;
        }

        /// <summary>
        /// Helper function to build new structure values.
        /// </summary>
        /// <param name="fields">The target field builder.</param>
        /// <param name="instance">The instance.</param>
        /// <param name="type">The instance type.</param>
        private void CreateStructureValue(
            ImmutableArray<ValueReference>.Builder fields,
            object instance,
            Type type)
        {
            var typeInfo = Context.TypeContext.GetTypeInfo(type);
            for (int i = 0, e = typeInfo.NumFields; i < e; ++i)
            {
                var rawFieldValue = typeInfo.Fields[i].GetValue(instance);
                var fieldValue = CreatePrimitiveObjectValue(rawFieldValue, out var fieldType);
                if (fieldValue.IsValid)
                    fields.Add(fieldValue);

                CreateStructureValue(
                    fields,
                    rawFieldValue,
                    fieldType);
            }
        }

        /// <summary>
        /// Creates a new object value.
        /// </summary>
        /// <param name="instance">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateObjectValue(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var primitiveValue = CreatePrimitiveObjectValue(instance, out var type);
            if (primitiveValue.IsValid)
                return primitiveValue;

            var fieldValues = ImmutableArray.CreateBuilder<ValueReference>();
            CreateStructureValue(
                fieldValues,
                instance,
                type);
            return CreateStructure(fieldValues.ToImmutable());
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
        /// <param name="fieldValues">The structure instance values.</param>
        /// <returns>The created structure instance value.</returns>
        public ValueReference CreateStructure(ImmutableArray<ValueReference> fieldValues)
        {
            if (fieldValues.Length < 1)
                return CreateNull(CreateEmptyStructureType());
            if (fieldValues.Length < 2)
                return fieldValues[0];

            // Construct structure type
            var fieldTypes = ImmutableArray.CreateBuilder<TypeNode>(fieldValues.Length);
            foreach (var value in fieldValues)
                fieldTypes.Add(value.Type);
            var structureType = CreateStructureType(fieldTypes.MoveToImmutable());
            return CreateStructure(structureType as StructureType, fieldValues);
        }

        /// <summary>
        /// Creates a new structure instance value.
        /// </summary>
        /// <param name="structureType">The structure type.</param>
        /// <param name="values">The structure instance values.</param>
        /// <returns>The created structure instance value.</returns>
        private ValueReference CreateStructure(
            StructureType structureType,
            ImmutableArray<ValueReference> values)
        {
            Debug.Assert(structureType != null, "Invalid structure type");
            Debug.Assert(values != null && values.Length > 0, "Invalid values");
            Debug.Assert(values.Length == structureType.NumFields, "Invalid values or structure type");

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
        public ValueReference CreateGetField(Value objectValue, FieldSpan fieldSpan)
        {
            Debug.Assert(objectValue != null, "Invalid object node");

            var structType = objectValue.Type as StructureType;
            if (structType == null && fieldSpan.Span < 2)
                return objectValue;

            Debug.Assert(structType != null, "Invalid object structure type");
            if (objectValue is StructureValue structureValue)
                return structureValue.Get(this, fieldSpan);
            if (objectValue is NullValue)
                return CreateNull(structType.Get(Context, fieldSpan));

            return Append(new GetField(
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

            var structType = objectValue.Type as StructureType;
            Debug.Assert(structType != null, "Invalid object structure type");
            Debug.Assert(structType.Get(Context, fieldSpan) == value.Type, "Incompatible value type");

            // Fold structure values
            if (objectValue is StructureValue structureValue)
            {
                var fieldValues = structType.CreateFieldBuilder();
                foreach (Value fieldValue in structureValue.Nodes)
                    fieldValues.Add(fieldValue);

                for (int i = 0; i < fieldSpan.Span; ++i)
                    fieldValues[fieldSpan.Index + i] = CreateGetField(value, new FieldSpan(i));

                return CreateStructure(structType, fieldValues.MoveToImmutable());
            }
            if (objectValue is NullValue && fieldSpan.Span == structType.NumFields)
                return value;

            return Append(new SetField(
                BasicBlock,
                objectValue,
                fieldSpan,
                value));
        }
    }
}
