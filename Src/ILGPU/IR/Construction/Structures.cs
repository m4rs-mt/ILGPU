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
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new object value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="instance">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateObjectValue(
            Location location,
            object instance)
        {
            if (instance == null)
                throw location.GetArgumentNullException(nameof(instance));

            var managedType = instance.GetType();
            if (managedType.IsILGPUPrimitiveType())
                return CreatePrimitiveValue(location, instance);
            if (managedType.IsEnum)
                return CreateEnumValue(location, instance);
            // Arrays requires a specific treatment
            if (managedType.IsArray)
            {
                return CreateArrayValue(
                    location,
                    instance as Array,
                    managedType.GetElementType(),
                    force: false);
            }
            // Check whether this type is an immutable array which might require special
            if (managedType.IsImmutableArray(out var arrayElementType))
            {
                return CreateImmutableArrayValue(
                    location,
                    instance,
                    managedType,
                    arrayElementType);
            }

            // Reject class types for now
            if (managedType.IsClass)
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedClassType,
                    managedType);
            }

            // Get type information from the parent type context
            var typeInfo = TypeContext.GetTypeInfo(managedType);
            var type = CreateType(managedType);
            if (!(type is StructureType structureType))
            {
                // This type has zero or one element and can be used without an
                // enclosing structure value
                return typeInfo.NumFields > 0
                    ? CreateObjectValue(
                        location,
                        typeInfo.Fields[0].GetValue(instance))
                    : CreateNull(location, type);
            }
            var instanceBuilder = CreateStructure(location, structureType);
            for (int i = 0, e = typeInfo.NumFields; i < e; ++i)
            {
                var field = typeInfo.Fields[i];
                var rawFieldValue = field.GetValue(instance);
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
        /// Create an intrinsic .Net array from a managed array instance.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="array">The managed array instance.</param>
        /// <param name="managedElementType">
        /// The managed element type of the array.
        /// </param>
        /// <param name="force">
        /// True, if you want to ignore safety checks for (potentially) modifiable array
        /// values.
        /// </param>
        /// <returns>The created array value.</returns>
        private unsafe ValueReference CreateArrayValue(
            Location location,
            Array array,
            Type managedElementType,
            bool force)
        {
            // Validate support for arrays
            location.AssertNotNull(array);
            if (!force &&
                BaseContext.Properties.ArrayMode != ArrayMode.InlineMutableStaticArrays)
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedLoadFromStaticArray,
                    array.ToString());
            }

            // Prepare element type and check of empty arrays
            var elementType = CreateType(managedElementType);
            var arrayType = CreateArrayType(elementType, array.Rank);
            if (array.Length < 1)
                return CreateEmptyArray(location, arrayType);

            // Create new array builder
            var arrayBuilder = CreateNewArray(location, arrayType);

            // Convert array dimensions
            for (int i = 0, e = array.Rank; i < e; ++i)
                arrayBuilder.Add(CreatePrimitiveValue(location, array.GetLength(i)));

            // Create a new array value that resides in local memory
            var arrayInstance = arrayBuilder.Seal();
            var arrayView = CreateArrayToViewCast(location, arrayInstance);

            // Convert and store each array element
            var gcHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                int elementSize = Interop.SizeOf(managedElementType);
                byte* baseAddr = (byte*)gcHandle.AddrOfPinnedObject().ToPointer();
                for (int i = 0; i < array.Length; ++i)
                {
                    // Get element from managed array
                    var source = baseAddr + elementSize * i;
                    var instance = Marshal.PtrToStructure(
                        new IntPtr(source),
                        managedElementType);
                    var irValue = CreateValue(location, instance, managedElementType);

                    // Store element
                    CreateStore(
                        location,
                        CreateLoadElementAddress(
                            location,
                            arrayView,
                            CreatePrimitiveValue(location, i)),
                        irValue);
                }
            }
            finally
            {
                gcHandle.Free();
            }

            return arrayInstance;
        }

        /// <summary>
        /// Creates an instance of a managed <see cref="ImmutableArray{T}"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="instance">The instance value.</param>
        /// <param name="managedType">The managed instance type.</param>
        /// <param name="managedElementType">
        /// The managed element type of the array.
        /// </param>
        /// <returns>The created value array.</returns>
        private ValueReference CreateImmutableArrayValue(
            Location location,
            object instance,
            Type managedType,
            Type managedElementType)
        {
            // Get the managed type information and extract the array instance
            var typeInfo = TypeContext.GetTypeInfo(managedType);
            var arrayInstance = typeInfo.Fields[0].GetValue(instance);

            return CreateArrayValue(
                location,
                arrayInstance as Array,
                managedElementType,
                force: true);
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
                    if (!(instanceBuilder.NextExpectedType is PaddingType paddingType))
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
                    };

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
            new StructureValue.Builder(this, location, structureType);

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
            new StructureValue.DynamicBuilder(this, location, capacity);

        /// <summary>
        /// Creates a new dynamic structure instance.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="values">The initial values.</param>
        /// <returns>The created structure instance.</returns>
        public ValueReference CreateDynamicStructure(
            Location location,
            ref ValueList values) =>
            new StructureValue.DynamicBuilder(this, location, ref values).Seal();

        /// <summary>
        /// Creates a new dynamic structure instance.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="item1">The first item.</param>
        /// <param name="item2">The second item.</param>
        /// <returns>The created structure instance value.</returns>
        public ValueReference CreateDynamicStructure(
            Location location,
            ValueReference item1,
            ValueReference item2)
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
        public ValueReference CreateDynamicStructure(
            Location location,
            ValueReference item1,
            ValueReference item2,
            ValueReference item3)
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
        public ValueReference CreateDynamicStructure<TList>(
            Location location,
            TList values)
            where TList : IReadOnlyList<ValueReference>
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
        internal ValueReference FinishStructureBuilder<TBuilder>(ref TBuilder builder)
            where TBuilder : struct, StructureValue.IInternalBuilder
        {
            if (builder.Count < 1)
                return CreateNull(builder.Location, this.CreateEmptyStructureType());
            if (builder.Count < 2)
                return builder[0];

            // Construct structure instance
            var values = ValueList.Empty;
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
        public ValueReference CreateGetField(
            Location location,
            Value objectValue,
            FieldSpan fieldSpan)
        {
            var structureType = objectValue.Type as StructureType;
            if (structureType == null && fieldSpan.Span < 2)
                return objectValue;

            // Must be a structure type
            location.AssertNotNull(structureType);

            // Try to combine different get and set operations operating on similar spans
            switch (objectValue)
            {
                case StructureValue structureValue:
                    return structureValue.Get(this, location, fieldSpan);
                case NullValue _:
                    return CreateNull(
                        location,
                        structureType.Get(BaseContext, fieldSpan));
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
                            setField.ObjectValue,
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
        public ValueReference CreateSetField(
            Location location,
            Value objectValue,
            FieldSpan fieldSpan,
            Value value)
        {
            var structureType = objectValue.Type.As<StructureType>(location);
            location.Assert(structureType.Get(BaseContext, fieldSpan) == value.Type);

            // Fold structure values
            if (objectValue is StructureValue structureValue)
            {
                var instance = CreateStructure(location, structureType);
                foreach (Value fieldValue in structureValue.Nodes)
                    instance.Add(fieldValue);

                for (int i = 0; i < fieldSpan.Span; ++i)
                {
                    instance[fieldSpan.Index + i] = CreateGetField(
                        location,
                        value,
                        new FieldSpan(i));
                }

                return instance.Seal();
            }

            return objectValue is NullValue && fieldSpan.Span == structureType.NumFields
                ? (ValueReference)value
                : Append(new SetField(
                    GetInitializer(location),
                    objectValue,
                    fieldSpan,
                    value));
        }

        /// <summary>
        /// Creates a new array value builder.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayType">The parent array type of this array.</param>
        /// <returns>A reference to the requested value.</returns>
        public NewArray.Builder CreateNewArray(Location location, ArrayType arrayType)
        {
            location.AssertNotNull(arrayType);
            return new NewArray.Builder(this, location, arrayType);
        }

        /// <summary>
        /// Creates a new empty array value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayType">The parent array type of this array.</param>
        /// <returns>A reference to the requested value.</returns>
        public NewArray CreateEmptyArray(Location location, ArrayType arrayType)
        {
            var builder = CreateNewArray(location, arrayType);
            var length = CreatePrimitiveValue(location, 0);
            for (int i = 0, e = arrayType.NumDimensions; i < e; ++i)
                builder.Add(length);
            return builder.Seal();
        }

        /// <summary>
        /// Creates a new array value with the given length in each dimension.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayType">The parent array type of this array.</param>
        /// <param name="dimensions">The list of all array dimension lengths.</param>
        /// <returns>The created array value.</returns>
        internal NewArray FinishNewArray(
            Location location,
            ArrayType arrayType,
            ref ValueList dimensions) =>
            Append(new NewArray(
                GetInitializer(location),
                arrayType,
                ref dimensions));

        /// <summary>
        /// Creates a value representing the total 32-bit length of the given array.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="array">The array instance.</param>
        /// <returns>A reference representing the total 32-bit array length.</returns>
        public ValueReference CreateGetArrayLength(Location location, Value array) =>
            Append(new GetArrayLength(
                GetInitializer(location),
                array,
                CreateUndefined()));

        /// <summary>
        /// Creates a value to determine the length of an array with respect to a
        /// specific dimension.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="array">The array instance.</param>
        /// <param name="dimension">The desired array dimension.</param>
        /// <returns>The target array element address.</returns>
        public ValueReference CreateGetArrayLength(
            Location location,
            Value array,
            Value dimension) =>
            Append(new GetArrayLength(
                GetInitializer(location),
                array,
                dimension));

        /// <summary>
        /// Creates a builder to compute an array element address.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="array">The array instance.</param>
        /// <returns>The target array element address.</returns>
        public LoadArrayElementAddress.Builder CreateLoadArrayElementAddress(
            Location location,
            Value array)
        {
            location.AssertNotNull(array);
            return new LoadArrayElementAddress.Builder(this, location, array);
        }

        /// <summary>
        /// Creates a laea value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="values">
        /// The array value and a single value index for each array dimension.
        /// </param>
        /// <returns>The array element address.</returns>
        internal Value FinishLoadArrayElementAddress(
            Location location,
            ref ValueList values) =>
            Append(new LoadArrayElementAddress(
                GetInitializer(location),
                ref values));

    }
}
