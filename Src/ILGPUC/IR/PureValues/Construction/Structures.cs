// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Structures.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ILGPUC.IR.PureValues.Construction;

partial class PureValueBuilder
{
    /// <summary>
    /// Creates a new object value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="instance">The object value.</param>
    /// <returns>A reference to the requested value.</returns>
    public Value CreateObjectValue(Location location, object? instance)
    {
        if (instance is null)
            throw location.GetArgumentNullException(nameof(instance));

        var managedType = instance.GetType();
        if (managedType.IsILGPUPrimitiveType())
            return CreatePrimitiveValue(location, instance);
        if (managedType.IsEnum)
            return CreateEnumValue(location, instance);
        if (managedType.IsArray || managedType.IsImmutableArray(out _))
            throw location.GetArgumentException(nameof(instance));

        // Reject class types for now
        if (managedType.IsClass)
        {
            throw location.GetNotSupportedException(
                ErrorMessages.NotSupportedClassType,
                managedType);
        }

        // Get type information from the parent type context
        var typeInfo = ModuleBuilder.GetTypeInfo(managedType);
        var type = ModuleBuilder.CreateType(managedType);
        if (type is not StructureType structureType)
        {
            // This type has zero or one element and can be used without an
            // enclosing structure value
            return typeInfo.NumFields > 0
                ? CreateObjectValue(
                    location,
                    typeInfo.Fields[0].GetValue(instance).AsNotNull())
                : CreateNull(location, type);
        }
        var instanceBuilder = CreateStructure(location, structureType);
        for (int i = 0, e = typeInfo.NumFields; i < e; ++i)
        {
            var field = typeInfo.Fields[i];
            var rawFieldValue = field.GetValue(instance).AsNotNull();
            Value fieldValue = CreateObjectValue(
                location,
                rawFieldValue);
            if (fieldValue.Type is StructureType nestedStructureType)
            {
                // Extract all nested fields and insert them into the builder
                foreach (var (_, access) in nestedStructureType)
                {
                    instanceBuilder.Add(
                        CreateGetField(
                            location,
                            fieldValue,
                            access));
                }
            }
            else
            {
                instanceBuilder.Add(fieldValue);

                // If the field value we just added is not the full size of the field,
                // (e.g. it was the start of a fixed buffer) copy the remaining
                // elements.
                var fieldTypeInfo = typeInfo.GetFieldTypeInfo(i);
                var initialBytes = fieldValue.Type.Size;
                var numBytes = fieldTypeInfo.Size;
                if (initialBytes < numBytes)
                {
                    AddPaddingFields(
                        ref instanceBuilder,
                        location,
                        rawFieldValue,
                        initialBytes,
                        numBytes);
                }
            }
        }
        return instanceBuilder.Seal();
    }

    /// <summary>
    /// Copies the remaining bytes to fill the structure fields.
    /// </summary>
    /// <param name="instanceBuilder">The current structure builder.</param>
    /// <param name="location">The current location.</param>
    /// <param name="rawFieldValue">The field of the structure to copy.</param>
    /// <param name="initialBytes">The starting offset in bytes.</param>
    /// <param name="numBytes">The size of the structure in bytes.</param>
    private unsafe void AddPaddingFields(
        ref StructureValue.Builder instanceBuilder,
        Location location,
        object rawFieldValue,
        int initialBytes,
        int numBytes)
    {
        var handle = GCHandle.Alloc(rawFieldValue, GCHandleType.Pinned);
        try
        {
            byte* ptr = (byte*)handle.AddrOfPinnedObject();
            int i = initialBytes;
            while (i < numBytes)
            {
                if (instanceBuilder.NextExpectedType is not PaddingType paddingType)
                    throw new NotImplementedException();

                PrimitiveValue paddingValue;
                switch (paddingType.BasicValueType)
                {
                    case BasicValueType.Int8:
                        byte padding8 = ptr[i];
                        paddingValue = CreatePrimitiveValue(location, padding8);
                        break;

                    case BasicValueType.Int16:
                        short padding16 = *(short*)&ptr[i];
                        paddingValue = CreatePrimitiveValue(location, padding16);
                        break;

                    case BasicValueType.Int32:
                        int padding32 = *(int*)&ptr[i];
                        paddingValue = CreatePrimitiveValue(location, padding32);
                        break;

                    case BasicValueType.Int64:
                        long padding64 = *(long*)&ptr[i];
                        paddingValue = CreatePrimitiveValue(location, padding64);
                        break;

                    default:
                        throw new NotImplementedException();
                }
                ;

                instanceBuilder.Add(paddingValue);
                i += paddingValue.PrimitiveType.Size;
            }
            Debug.Assert(i == numBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Creates a new structure instance builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="structureType">The structure type.</param>
    /// <returns>The created structure instance builder.</returns>
    public StructureValue.Builder CreateStructure(
        Location location,
        StructureType structureType) =>
        new(this, location, structureType);

    /// <summary>
    /// Creates a new dynamic structure instance builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>The created structure instance builder.</returns>
    public StructureValue.DynamicBuilder CreateDynamicStructure(
        Location location) =>
        CreateDynamicStructure(location, 2);

    /// <summary>
    /// Creates a new dynamic structure instance builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <returns>The created structure instance builder.</returns>
    public StructureValue.DynamicBuilder CreateDynamicStructure(
        Location location,
        int capacity) =>
        new(this, location, capacity);

    /// <summary>
    /// Creates a new dynamic structure instance.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="values">The initial values.</param>
    /// <returns>The created structure instance.</returns>
    public Value CreateDynamicStructure(
        Location location,
        ref ValueBuilderList values) =>
        new StructureValue.DynamicBuilder(this, location, ref values).Seal();

    /// <summary>
    /// Creates a new dynamic structure instance.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="item1">The first item.</param>
    /// <param name="item2">The second item.</param>
    /// <returns>The created structure instance value.</returns>
    public Value CreateDynamicStructure(Location location, Value? item1, Value? item2)
    {
        var builder = CreateDynamicStructure(location, 2);
        builder.Add(item1);
        builder.Add(item2);
        return builder.Seal();
    }

    /// <summary>
    /// Creates a new dynamic structure instance.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="item1">The first item.</param>
    /// <param name="item2">The second item.</param>
    /// <param name="item3">The third item.</param>
    /// <returns>The created structure instance value.</returns>
    public Value CreateDynamicStructure(
        Location location,
        Value item1,
        Value item2,
        Value item3)
    {
        var builder = CreateDynamicStructure(location, 3);
        builder.Add(item1);
        builder.Add(item2);
        builder.Add(item3);
        return builder.Seal();
    }

    /// <summary>
    /// Creates a new dynamic structure instance.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="values">The list of all values to add.</param>
    /// <returns>The created structure instance value.</returns>
    public Value CreateDynamicStructure<TList>(Location location, TList values)
        where TList : IReadOnlyList<Value>
    {
        var builder = CreateDynamicStructure(location, values.Count);
        for (int i = 0, e = values.Count; i < e; ++i)
            builder.Add(values[i]);
        return builder.Seal();
    }

    /// <summary>
    /// Creates a new structure instance value.
    /// </summary>
    /// <param name="builder">The structure instance builder.</param>
    /// <returns>The created structure instance value.</returns>
    internal Value FinishStructureBuilder<TBuilder>(ref TBuilder builder)
        where TBuilder : struct, StructureValue.IInternalBuilder
    {
        if (builder.Count < 1)
            return CreateNull(builder.Location, ModuleBuilder.CreateEmptyStructureType());
        if (builder.Count < 2)
            return builder[0];

        // Construct structure instance
        var values = ValueBuilderList.Empty(Generation);
        var structureType = builder.Seal(ref values);
        return Append(new StructureValue(
            GetInitializer(builder.Location),
            structureType,
            ref values));
    }

    /// <summary>
    /// Creates a load operation of an object field.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="objectValue">The object value.</param>
    /// <param name="fieldSpan">The field span.</param>
    /// <returns>A reference to the requested value.</returns>
    public Value? CreateGetField(
        Location location,
        Value? objectValue,
        FieldSpan fieldSpan)
    {
        if (objectValue is null) return null;

        StructureType? nullableStructureType = objectValue.Type as StructureType;
        if (nullableStructureType == null && fieldSpan.Span < 2)
            return objectValue;

        // Must be a structure type
        var structureType = nullableStructureType.AsNotNull();
        location.AssertNotNull(structureType);

        // Try to combine different get and set operations operating on similar spans
        switch (objectValue)
        {
            case StructureValue structureValue:
                return structureValue.Get(this, location, fieldSpan);
            case NullValue _:
                return CreateNull(
                    location,
                    structureType.Get(ModuleBuilder, fieldSpan));
            case SetField setField:
                // Optimize for simple cases
                if (setField.FieldSpan == fieldSpan)
                {
                    return setField.Value;
                }
                // Check whether our field span is included in the updated field span
                else if (setField.FieldSpan.Contains(fieldSpan))
                {
                    // Our field span is included in the parent span
                    return CreateGetField(
                        location,
                        setField.Value,
                        new FieldSpan(
                            fieldSpan.Index - setField.FieldSpan.Index,
                            fieldSpan.Span));
                }
                // If our field span overlaps with the found one we have to split
                // the part into an overlapping part and the remaining part(s)
                else if (fieldSpan.Overlaps(setField.FieldSpan))
                {
                    // Ignore this case for now as it adds even more nodes
                    break;
                }
                // These field spans have to be distinct from each other
                else
                {
                    location.Assert(!fieldSpan.Contains(setField.FieldSpan));
                    // We can safely continue with the parent value since this
                    // SetField operation does not influence the new GetField value
                    return CreateGetField(
                        location,
                        setField.Source,
                        fieldSpan);
                }
        }

        // We could not find any matching constant value
        return Append(new GetField(
            GetInitializer(location),
            objectValue,
            fieldSpan));
    }

    /// <summary>
    /// Creates a store operation of an object field using the given field access.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="objectValue">The object value.</param>
    /// <param name="fieldSpan">The field span.</param>
    /// <param name="value">The field value to store.</param>
    /// <returns>A reference to the requested value.</returns>
    public Value? CreateSetField(
        Location location,
        Value? objectValue,
        FieldSpan fieldSpan,
        Value? value)
    {
        if (objectValue is null) return null;
        if (value is null) return objectValue;

        var structureType = objectValue.Type.As<StructureType>();
        location.Assert(structureType.Get(ModuleBuilder, fieldSpan).Equals(value.Type));

        // Fold structure values
        if (objectValue is StructureValue structureValue)
        {
            var instance = CreateStructure(location, structureType);
            foreach (Value fieldValue in structureValue.Values)
                instance.Add(fieldValue);

            for (int i = 0; i < fieldSpan.Span; ++i)
            {
                instance[fieldSpan.Index + i] = CreateGetField(
                    location,
                    value,
                    new FieldSpan(i)).AsNotNull();
            }

            return instance.Seal();
        }

        // Optimize common cases in which this set field operation fills a whole
        // structure instance
        if (objectValue is SetField &&
            !fieldSpan.HasSpan &&
            fieldSpan.Index + 1 == structureType.NumFields &&
            structureType.NumFields < 32)
        {
            // Initialize our internal field-value lookup
            var fieldValues = new Value[structureType.NumFields];
            fieldValues[fieldSpan.Index] = value;

            // Traverse the whole chain of all set-field operations
            var parent = objectValue;
            int traversalLength = structureType.NumFields - 1;
            while (parent is not NullValue && traversalLength > 0)
            {
                // Check whether we are still on the right track while traversing
                // set of (possibly unordered) set-field operations
                if (parent is not SetField otherSetField ||
                    otherSetField.FieldSpan.HasSpan ||
                    fieldValues[otherSetField.FieldSpan.Index] != null)
                {
                    break;
                }

                // Register this set-field operation
                fieldValues[otherSetField.FieldSpan.Index] = otherSetField.Value;
                // Decrease the traversal length
                --traversalLength;
                // Update the parent
                parent = otherSetField.Source;
            }

            // Check whether all indices have been visited found
            if (traversalLength < 1 && parent is NullValue)
            {
                // Build new structure value containing all traversed values
                var instance = CreateStructure(location, structureType);
                foreach (var fieldValue in fieldValues)
                    instance.Add(fieldValue);
                return instance.Seal();
            }
        }

        return objectValue is NullValue && fieldSpan.Span == structureType.NumFields
            ? value
            : Append(new SetField(
                GetInitializer(location),
                objectValue,
                fieldSpan,
                value));
    }

    /// <summary>
    /// Assembles a structure value using the lowering provided.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="structureType">The structure type to use.</param>
    /// <param name="value">The source value.</param>
    /// <param name="lowering">The lowering functionality.</param>
    /// <returns>The assembled structure value.</returns>
    public Value AssembleStructure<TValue>(
        StructureType structureType,
        TValue value,
        Func<TValue, FieldAccess, Value> lowering)
        where TValue : Value
    {
        var instance = CreateStructure(value.Location, structureType);

        // Invoke the lowering implementation for all fields
        for (int i = 0, e = structureType.NumFields; i < e; ++i)
        {
            // Invoke lowering implementation
            instance.Add(lowering(value, new FieldAccess(i)));
        }

        // Create new structure instance
        return instance.Seal();
    }
    /// <summary>
    /// Disassembled a structure value using the lowering provided.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="structureType">The structure type to use.</param>
    /// <param name="value">The source value.</param>
    /// <param name="lowering">The lowering functionality.</param>
    public void DisassembleStructure<TValue>(
        StructureType structureType,
        TValue value,
        Action<TValue, Value, FieldAccess> lowering)
        where TValue : Value
    {
        // Invoke the lowering implementation for all fields
        for (int i = 0, e = structureType.NumFields; i < e; ++i)
        {
            var access = new FieldAccess(i);
            var getField = CreateGetField(
                value.Location,
                value,
                new FieldSpan(access)).AsNotNull();

            // Invoke lowering implementation
            lowering(value, getField, new FieldAccess(i));
        }
    }

    /// <summary>
    /// Lowers the given value by applying the lowering provided to each field value.
    /// Primitive values will be lowered once.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="variable">The variable value.</param>
    /// <param name="lowering">The lowering functionality.</param>
    /// <returns>The assembled structure value holding the result value.</returns>
    public Value LowerValue<TValue>(
        TValue source,
        Value variable,
        Func<TValue, Value, Value> lowering)
        where TValue : Value
    {
        if (source.Type is PrimitiveType)
        {
            return lowering(source, source);
        }
        else
        {
            var structureType = source.Type.As<StructureType>();
            return AssembleStructure(
                structureType,
                source,
                (_, access) =>
                {
                    // Extract field information
                    var getField = CreateGetField(
                        source.Location,
                        variable,
                        new FieldSpan(access)).AsNotNull();
                    var result = lowering(source, getField);
                    return result;
                });
        }
    }

    /// <summary>
    /// Creates a new array value builder.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="arrayType">The parent array type of this array.</param>
    /// <returns>A reference to the requested value.</returns>
    public ArrayValue.Builder CreateNewArray(Location location, ArrayType arrayType) =>
        new(this, location, arrayType);

    /// <summary>
    /// Creates a new empty array value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="arrayType">The parent array type of this array.</param>
    /// <returns>A reference to the requested value.</returns>
    public ArrayValue CreateEmptyArray(Location location, ArrayType arrayType)
    {
        var combined = ValueBuilderList.Create(
            Generation,
            1 + arrayType.NumDimensions);
        combined.Add(ModuleBuilder.UndefinedValue);
        for (int i = 0; i < arrayType.NumDimensions; ++i)
            combined.Add(CreatePrimitiveValue(location, 0));
        return FinishArrayValueCombined(location, arrayType, ref combined);
    }

    /// <summary>
    /// Creates a new array value with the given length in each dimension.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="arrayType">The parent array type of this array.</param>
    /// <param name="dimensions">The list of all array dimension lengths.</param>
    /// <returns>The created array value.</returns>
    internal ArrayValue FinishArrayValue(
        Location location,
        ArrayType arrayType,
        ref ValueBuilderList dimensions)
    {
        // Create new global back buffer while porting constants over
        Value totalLength = CreatePrimitiveValue(location, 1);
        foreach (var length in dimensions)
        {
#if DEBUG
            // Dimension values should be computable expressions.
            // They can be PureValues (constants, arithmetic) or
            // MethodValues (parameters for runtime-sized arrays).
            location.Assert(length is not null,
                "Array dimension value must not be null");
#endif
            totalLength = CreateArithmetic(
                location,
                totalLength,
                length,
                BinaryArithmeticKind.Mul).AsNotNull();
        }

        // Optimize empty allocations
        if (totalLength.IsPrimitiveValue(0))
            return CreateEmptyArray(location, arrayType);

        // Create new global back buffer and return lightweight value reference
        var global = ModuleBuilder.CreateGlobal(
            location,
            arrayType.ElementType,
            MemoryAddressSpace.Local,
            totalLength).ThrowIfNull();

        // Build combined list: [backBuffer, dim0, dim1, ..., dimN-1]
        var combined = ValueBuilderList.Create(
            Generation,
            1 + dimensions.Count);
        combined.Add(global);
        foreach (var dim in dimensions)
            combined.Add(dim);
        return Append(new ArrayValue(
            GetInitializer(location), arrayType, ref combined));
    }

    /// <summary>
    /// Creates a new array value from a pre-built combined value list.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="arrayType">The parent array type of this array.</param>
    /// <param name="combined">
    /// The combined value list: [backBuffer, dim0, dim1, ..., dimN-1].
    /// </param>
    /// <returns>The created array value.</returns>
    internal ArrayValue FinishArrayValueCombined(
        Location location,
        ArrayType arrayType,
        ref ValueBuilderList combined) =>
        Append(new ArrayValue(
            GetInitializer(location), arrayType, ref combined));

    /// <summary>
    /// Creates a value representing the total 32-bit length of the given array.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="array">The array instance.</param>
    /// <returns>A reference representing the total 32-bit array length.</returns>
    public Value? CreateGetArrayLength(Location location, Value? array)
    {
        if (array is null) return null;
        return Append(new GetArrayLength(
            GetInitializer(location),
            array,
            ModuleBuilder.UndefinedValue));
    }

    /// <summary>
    /// Creates a value to determine the length of an array with respect to a
    /// specific dimension.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="array">The array instance.</param>
    /// <param name="dimension">The desired array dimension.</param>
    /// <returns>The target array element address.</returns>
    public Value? CreateGetArrayLength(
        Location location,
        Value? array,
        Value? dimension)
    {
        if (array is null || dimension is null) return null;
        return Append(new GetArrayLength(
            GetInitializer(location),
            array,
            dimension));
    }
}
