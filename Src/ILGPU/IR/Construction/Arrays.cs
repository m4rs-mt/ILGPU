// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new array value.
        /// </summary>
        /// <param name="elementType">The array element type.</param>
        /// <param name="dimensions">The array dimensions.</param>
        /// <param name="extent">The array length.</param>
        /// <returns>The created empty array value.</returns>
        public ValueReference CreateArray(
            TypeNode elementType,
            int dimensions,
            Value extent)
        {
            var arrayType = CreateArrayType(elementType, dimensions);
            return CreateArray(arrayType, extent);
        }

        /// <summary>
        /// Creates a new array value.
        /// </summary>
        /// <param name="type">The array type.</param>
        /// <param name="extent">The array length.</param>
        /// <returns>The created empty array value.</returns>
        public ValueReference CreateArray(ArrayType type, Value extent)
        {
            Debug.Assert(extent != null, "Invalid extent");
            Debug.Assert(extent.Type == GetIndexType(type.Dimensions), "Invalid extent type");
            return Append(new ArrayValue(
                BasicBlock,
                type,
                extent));
        }

        /// <summary>
        /// Creates an operation to extract the extent from an array value.
        /// </summary>
        /// <param name="arrayValue">The array value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayExtent(Value arrayValue)
        {
            Debug.Assert(arrayValue != null, "Invalid array value");
            var arrayType = arrayValue.Type as ArrayType;
            Debug.Assert(arrayType != null, "Invalid array type");

            return Append(new GetArrayExtent(
                Context,
                BasicBlock,
                arrayValue));
        }

        /// <summary>
        /// Creates a load operation of an array element.
        /// </summary>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="index">The field index to load.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayElement(
            Value arrayValue,
            Value index)
        {
            Debug.Assert(arrayValue != null, "Invalid array value");
            var arrayType = arrayValue.Type as ArrayType;
            Debug.Assert(arrayType != null, "Invalid array type");
            Debug.Assert(index.Type == GetIndexType(arrayType.Dimensions), "Invalid index type");

            return Append(new GetArrayElement(
                BasicBlock,
                arrayValue,
                index));
        }

        /// <summary>
        /// Creates a store operation of an array element.
        /// </summary>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="index">The array index to store.</param>
        /// <param name="value">The array value to store.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSetArrayElement(
            Value arrayValue,
            Value index,
            Value value)
        {
            Debug.Assert(arrayValue != null, "Invalid array value");
            var arrayType = arrayValue.Type as ArrayType;
            Debug.Assert(arrayType != null, "Invalid array type");
            Debug.Assert(index.Type == GetIndexType(arrayType.Dimensions), "Invalid index type");

            return Append(new SetArrayElement(
                Context,
                BasicBlock,
                arrayValue,
                index,
                value));
        }

        /// <summary>
        /// Creates a value reference that represents an array length.
        /// </summary>
        /// <param name="arrayValue">The array value to compute the length for.</param>
        /// <returns>The created value reference.</returns>
        public ValueReference CreateGetArrayLength(Value arrayValue)
        {
            var extent = CreateGetArrayExtent(arrayValue);
            return ComputeArrayLength(extent);
        }

        /// <summary>
        /// Computes a linear array length based on an array extent.
        /// </summary>
        /// <param name="arrayExtent">The array extent.</param>
        /// <returns>The linear array length.</returns>
        internal ValueReference ComputeArrayLength(Value arrayExtent)
        {
            // Compute total number of elements
            var size = CreateGetField(arrayExtent, new FieldSpan(0));
            int dimensions = StructureType.GetNumFields(arrayExtent.Type);
            for (int i = 1; i < dimensions; ++i)
            {
                var element = CreateGetField(arrayExtent, new FieldSpan(i));
                size = CreateArithmetic(
                    size,
                    element,
                    BinaryArithmeticKind.Mul);
            }
            return size;
        }

        /// <summary>
        /// Computes a linear array address.
        /// </summary>
        /// <param name="arrayIndex">The array index.</param>
        /// <param name="arrayExtent">The array extent.</param>
        /// <param name="arrayExtentOffset">The extent offset.</param>
        /// <returns>The linear array address.</returns>
        internal ValueReference ComputeArrayAddress(
            Value arrayIndex,
            Value arrayExtent,
            int arrayExtentOffset)
        {
            int dimensions = StructureType.GetNumFields(arrayExtent.Type);
            Value linearIndex = CreateGetField(arrayIndex, new FieldSpan(dimensions - 1));
            for (int i = dimensions - 2; i >= 0; --i)
            {
                var extent = CreateGetField(
                    arrayExtent,
                    new FieldSpan(i + arrayExtentOffset));
                var index = CreateGetField(arrayIndex, new FieldSpan(i));
                linearIndex = CreateArithmetic(
                    linearIndex,
                    extent,
                    index,
                    TernaryArithmeticKind.MultiplyAdd);
            }
            return linearIndex;
        }
    }
}
