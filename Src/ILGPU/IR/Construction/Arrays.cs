// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new array value.
        /// </summary>
        /// <param name="elementType">The array element type.</param>
        /// <param name="length">The array length.</param>
        /// <returns>The created empty array value.</returns>
        public ValueReference CreateArray(TypeNode elementType, int length)
        {
            var arrayType = CreateArrayType(elementType, length);
            return CreateArray(arrayType);
        }

        /// <summary>
        /// Creates a new array value.
        /// </summary>
        /// <param name="type">The array type.</param>
        /// <returns>The created empty array value.</returns>
        public ValueReference CreateArray(ArrayType type) =>
            CreateNull(type);

        /// <summary>
        /// Creates a load operation of an array element.
        /// </summary>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="index">The field index to load.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetElement(
            Value arrayValue,
            Value index)
        {
            Debug.Assert(arrayValue != null, "Invalid array value");
            var arrayType = arrayValue.Type as ArrayType;
            Debug.Assert(arrayType != null, "Invalid array type");
            Debug.Assert(
                index != null || index.BasicValueType == BasicValueType.Int32,
                "Invalid index node");

            // Try to combine different get and set operations on the same value
            var current = arrayValue;
            for (; ;)
            {
                switch (current)
                {
                    case SetElement setElement:
                        if (setElement.Index == index ||
                            setElement.Index.Resolve() is PrimitiveValue targetPrimitive &&
                            index is PrimitiveValue currentPrimitive &&
                            targetPrimitive.Int32Value == currentPrimitive.Int32Value)
                            return setElement.Value;
                        current = setElement.ObjectValue;
                        continue;
                    case GetElement getElement:
                        current = getElement.ObjectValue;
                        continue;
                    case NullValue _:
                        return CreateNull(arrayType.ElementType);
                }

                // Value could not be resolved
                break;
            }

            return Append(new GetElement(
                BasicBlock,
                arrayValue,
                index));
        }

        /// <summary>
        /// Creates a store operation of an array elmeent.
        /// </summary>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="index">The array index to store.</param>
        /// <param name="value">The array value to store.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSetElement(
            Value arrayValue,
            Value index,
            Value value)
        {
            Debug.Assert(
                arrayValue != null || !(arrayValue.Type is ArrayType),
                "Invalid array value");
            Debug.Assert(
                index != null || index.BasicValueType == BasicValueType.Int32,
                "Invalid index node");
            Debug.Assert(value != null, "Invalid value node");

            return Append(new SetElement(
                BasicBlock,
                arrayValue,
                index,
                value));
        }

        /// <summary>
        /// Creates an array accumulation that accumulates all elements
        /// in the array into a single mutiplication value.
        /// </summary>
        /// <param name="arrayValue">The source array value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateArrayAccumulationMultiply(Value arrayValue)
        {
            Debug.Assert(arrayValue != null, "Invalid array value");
            var arrayType = arrayValue.Type as ArrayType;
            Debug.Assert(arrayType != null, "Invalid array type");

            var size = CreateGetElement(
                arrayValue,
                CreatePrimitiveValue(0));
            for (int i = 1, e = arrayType.Length; i < e; ++i)
            {
                var element = CreateGetElement(
                    arrayValue,
                    CreatePrimitiveValue(i));
                size = CreateArithmetic(
                    size,
                    element,
                    BinaryArithmeticKind.Mul);
            }
            return size;
        }

        #region Manged array implementation helpers

        /// <summary>
        /// Creates array implemention extent.
        /// </summary>
        /// <param name="extents">The different extents.</param>
        /// <param name="startIndex">The start index within the extents array.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateArrayImplementationExtent(
            ImmutableArray<ValueReference> extents,
            int startIndex)
        {
            var length = extents.Length - startIndex;
            Debug.Assert(length > 0, "Invalid extent");
            var array = CreateArray(
                GetPrimitiveType(BasicValueType.Int32),
                length);

            for (int i = 0; i < length; ++i)
            {
                array = CreateSetElement(
                    array,
                    CreatePrimitiveValue(i),
                    extents[startIndex + i]);
            }
            return array;
        }

        /// <summary>
        /// Creates an array implementation instance that represents
        /// a managed array.
        /// </summary>
        /// <param name="arrayView">
        /// The raw view to a memory region that realizes the array.
        /// </param>
        /// <param name="extent">The array that holds all dimension indices.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateArrayImplementation(
            Value arrayView,
            Value extent)
        {
            Debug.Assert(arrayView != null, "Invalid array view value");
            var arrayViewType = arrayView.Type as ViewType;
            Debug.Assert(arrayViewType != null, "Invalid array view type");

            Debug.Assert(extent != null, "Invalid extent value");
            var extentType = extent.Type as ArrayType;
            Debug.Assert(extentType != null, "Invalid extent type");

            // Create the actual array instance
            var implementationType = CreateArrayImplementationType(
                arrayViewType.ElementType,
                extentType.Length);

            var value = CreateStructure(implementationType);
            value = CreateSetField(value, 0, arrayView);
            value = CreateSetField(value, 1, extent);
            return value;
        }

        /// <summary>
        /// Resolves the underlying view of an array implementation.
        /// </summary>
        /// <param name="arrayImplementation">An array implementation value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayImplementationView(Value arrayImplementation)
        {
            Debug.Assert(
                arrayImplementation != null &&
                arrayImplementation.Type is StructureType structureType &&
                structureType.Fields[0].IsViewType,
                "Invalid array implementation value");
            return CreateGetField(arrayImplementation, 0);
        }

        /// <summary>
        /// Resolves the linear length of an array implementation.
        /// </summary>
        /// <param name="arrayImplementation">An array implementation value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetLinearArrayImplementationLength(Value arrayImplementation)
        {
            var view = CreateGetArrayImplementationView(arrayImplementation);
            return CreateGetViewLength(view);
        }

        /// <summary>
        /// Resolves the underlying dimension length of an array implementation.
        /// </summary>
        /// <param name="arrayImplementation">An array implementation value.</param>
        /// <param name="dimension">An dimension value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayImplementationLength(
            Value arrayImplementation,
            Value dimension)
        {
            Debug.Assert(
                arrayImplementation != null &&
                arrayImplementation.Type is StructureType structureType &&
                structureType.Fields[1].IsArrayType,
                "Invalid array implementation value");
            Debug.Assert(
                dimension != null &&
                dimension.BasicValueType == BasicValueType.Int32,
                "Invalid dimension value");

            var extent = CreateGetField(arrayImplementation, 1);
            return CreateGetElement(extent, dimension);
        }

        /// <summary>
        /// Resolves the underlying dimension length of an array implementation.
        /// </summary>
        /// <param name="arrayImplementation">An array implementation value.</param>
        /// <param name="indices">An array containing all required index values.</param>
        /// <param name="startIndex">The start index within the indices array.</param>
        /// <param name="numIndices">The number of indices to use from the indices array.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayImplementationElementIndex(
            Value arrayImplementation,
            ImmutableArray<ValueReference> indices,
            int startIndex,
            int numIndices)
        {
            Debug.Assert(
                arrayImplementation != null &&
                arrayImplementation.Type is StructureType structureType &&
                structureType.Fields[1].IsArrayType,
                "Invalid array implementation value");
            Debug.Assert(!indices.IsDefaultOrEmpty, "Invalid element indices");

            var extent = CreateGetField(arrayImplementation, 1);
            var linearIndex = indices[startIndex];
            for (int i = 1; i < numIndices; ++i)
            {
                linearIndex = CreateArithmetic(
                    linearIndex,
                    CreateGetElement(
                        extent,
                        CreatePrimitiveValue(i)),
                    BinaryArithmeticKind.Mul);

                linearIndex = CreateArithmetic(
                    linearIndex,
                    indices[i + startIndex],
                    BinaryArithmeticKind.Add);
            }

            return linearIndex;
        }

        /// <summary>
        /// Resolves the address of an element in the scope of an array implementation.
        /// </summary>
        /// <param name="arrayImplementation">An array implementation value.</param>
        /// <param name="linearIndex">An linear element index.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateLoadArrayImplementationElementAddress(
            Value arrayImplementation,
            Value linearIndex)
        {
            Debug.Assert(
                linearIndex != null &&
                linearIndex.BasicValueType == BasicValueType.Int32,
                "Invalid array implementation value");

            var view = CreateGetArrayImplementationView(arrayImplementation);
            return CreateLoadElementAddress(view, linearIndex);
        }

        /// <summary>
        /// Resolves the address of an element in the scope of an array implementation.
        /// </summary>
        /// <param name="arrayImplementation">An array implementation value.</param>
        /// <param name="indices">An array containing all required index values.</param>
        /// <param name="startIndex">The start index within the indices array.</param>
        /// <param name="numIndices">The number of indices to use from the indices array.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateLoadArrayImplementationElementAddress(
            Value arrayImplementation,
            ImmutableArray<ValueReference> indices,
            int startIndex,
            int numIndices)
        {
            var index = CreateGetArrayImplementationElementIndex(
                arrayImplementation,
                indices,
                startIndex,
                numIndices);
            var view = CreateGetArrayImplementationView(arrayImplementation);
            return CreateLoadElementAddress(view, index);
        }

        #endregion
    }
}
