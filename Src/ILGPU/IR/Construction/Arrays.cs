// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Verifies that array operations work on linear arrays only.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="dimensions">The array dimensions.</param>
        /// <remarks>
        /// TODO: remove this constraint in future releases.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void VerifyLinearArray(Location location, int dimensions)
        {
            if (dimensions < 2)
                return;
            throw location.GetNotSupportedException(
                ErrorMessages.NotSupportedArrayDimension,
                dimensions.ToString());
        }

        /// <summary>
        /// Creates a new array value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="elementType">The array element type.</param>
        /// <param name="dimensions">The array dimensions.</param>
        /// <param name="extent">The array length.</param>
        /// <returns>The created empty array value.</returns>
        public ValueReference CreateArray(
            Location location,
            TypeNode elementType,
            int dimensions,
            Value extent)
        {
            var arrayType = CreateArrayType(elementType, dimensions);
            return CreateArray(location, arrayType, extent);
        }

        /// <summary>
        /// Creates a new array value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The array type.</param>
        /// <param name="extent">The array length.</param>
        /// <returns>The created empty array value.</returns>
        public ValueReference CreateArray(
            Location location,
            ArrayType type,
            Value extent)
        {
            location.AssertNotNull(extent);
            location.Assert(extent.Type == GetIndexType(type.Dimensions));
            VerifyLinearArray(location, type.Dimensions);

            return Append(new ArrayValue(
                GetInitializer(location),
                type,
                extent));
        }

        /// <summary>
        /// Creates an operation to extract the extent from an array value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayExtent(
            Location location,
            Value arrayValue)
        {
            location.AssertNotNull(arrayValue);
            var arrayType = arrayValue.Type.As<ArrayType>(location);
            VerifyLinearArray(location, arrayType.Dimensions);

            if (UseConstantPropagation && arrayValue is ArrayValue constantArray)
                return constantArray.Extent;

            return Append(new GetArrayExtent(
                GetInitializer(location),
                arrayValue));
        }

        /// <summary>
        /// Creates a load operation of an array element.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="index">The field index to load.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGetArrayElement(
            Location location,
            Value arrayValue,
            Value index)
        {
            location.AssertNotNull(arrayValue);
            var arrayType = arrayValue.Type.As<ArrayType>(location);
            location.Assert(index.Type == GetIndexType(arrayType.Dimensions));
            VerifyLinearArray(location, arrayType.Dimensions);

            return Append(new GetArrayElement(
                GetInitializer(location),
                arrayValue,
                index));
        }

        /// <summary>
        /// Creates a store operation of an array element.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="index">The array index to store.</param>
        /// <param name="value">The array value to store.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSetArrayElement(
            Location location,
            Value arrayValue,
            Value index,
            Value value)
        {
            location.AssertNotNull(arrayValue);
            var arrayType = arrayValue.Type.As<ArrayType>(location);
            location.Assert(index.Type == GetIndexType(arrayType.Dimensions));
            VerifyLinearArray(location, arrayType.Dimensions);

            return Append(new SetArrayElement(
                GetInitializer(location),
                arrayValue,
                index,
                value));
        }

        /// <summary>
        /// Creates a value reference that represents an array length.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayValue">The array value to compute the length for.</param>
        /// <returns>The created value reference.</returns>
        public ValueReference CreateGetArrayLength(
            Location location,
            Value arrayValue)
        {
            var extent = CreateGetArrayExtent(location, arrayValue);
            return ComputeArrayLength(location, extent);
        }

        /// <summary>
        /// Computes a linear array length based on an array extent.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayExtent">The array extent.</param>
        /// <returns>The linear array length.</returns>
        internal ValueReference ComputeArrayLength(
            Location location,
            Value arrayExtent)
        {
            // Compute total number of elements
            var size = CreateGetField(
                location,
                arrayExtent,
                new FieldSpan(0));
            int dimensions = StructureType.GetNumFields(arrayExtent.Type);
            for (int i = 1; i < dimensions; ++i)
            {
                var element = CreateGetField(
                    location,
                    arrayExtent,
                    new FieldSpan(i));
                size = CreateArithmetic(
                    location,
                    size,
                    element,
                    BinaryArithmeticKind.Mul);
            }
            return size;
        }

        /// <summary>
        /// Computes a linear array address.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayIndex">The array index.</param>
        /// <param name="arrayExtent">The array extent.</param>
        /// <param name="arrayExtentOffset">The extent offset.</param>
        /// <returns>The linear array address.</returns>
        internal ValueReference ComputeArrayAddress(
            Location location,
            Value arrayIndex,
            Value arrayExtent,
            int arrayExtentOffset)
        {
            int dimensions = StructureType.GetNumFields(arrayExtent.Type);
            Value linearIndex = CreateGetField(
                location,
                arrayIndex,
                new FieldSpan(dimensions - 1));
            for (int i = dimensions - 2; i >= 0; --i)
            {
                var extent = CreateGetField(
                    location,
                    arrayExtent,
                    new FieldSpan(i + arrayExtentOffset));
                var index = CreateGetField(
                    location,
                    arrayIndex,
                    new FieldSpan(i));
                linearIndex = CreateArithmetic(
                    location,
                    linearIndex,
                    extent,
                    index,
                    TernaryArithmeticKind.MultiplyAdd);
            }
            return linearIndex;
        }
    }
}
