// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a cast operation that casts the element type of a pointer
        /// but does not change its address space.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="targetElementType">The target element type.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreatePointerCast(
            Location location,
            Value node,
            TypeNode targetElementType)
        {
            var type = node.Type.As<PointerType>(location);

            if (type.ElementType == targetElementType)
                return node;
            if (node is PointerCast pointerCast)
                node = pointerCast.Value;

            return Append(new PointerCast(
                GetInitializer(location),
                node,
                targetElementType));
        }

        /// <summary>
        /// Creates an address-space cast.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="targetAddressSpace">The target address space.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateAddressSpaceCast(
            Location location,
            Value node,
            MemoryAddressSpace targetAddressSpace)
        {
            var type = node.Type.As<AddressSpaceType>(location);

            var sourceAddressSpace = type.AddressSpace;
            return sourceAddressSpace == targetAddressSpace
                ? (ValueReference)node
                : Append(new AddressSpaceCast(
                    GetInitializer(location),
                    node,
                    targetAddressSpace));
        }

        /// <summary>
        /// Creates a view cast.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="targetElementType">The target element type.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateViewCast(
            Location location,
            Value node,
            TypeNode targetElementType)
        {
            var type = node.Type.As<ViewType>(location);

            return type.ElementType == targetElementType
                ? (ValueReference)node
                : Append(new ViewCast(
                    GetInitializer(location),
                    node,
                    targetElementType));
        }

        /// <summary>
        /// Creates a float as int reinterpret bit cast.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateFloatAsIntCast(
            Location location,
            Value node)
        {
            var primitiveType = node.Type.As<PrimitiveType>(location);
            if (UseConstantPropagation && node is PrimitiveValue primitive)
            {
                return primitiveType.BasicValueType switch
                {
                    BasicValueType.Float16 => CreatePrimitiveValue(
                        location,
                        Interop.FloatAsInt(primitive.Float16Value)),
                    BasicValueType.Float32 => CreatePrimitiveValue(
                        location,
                        Interop.FloatAsInt(primitive.Float32Value)),
                    BasicValueType.Float64 => CreatePrimitiveValue(
                        location,
                        Interop.FloatAsInt(primitive.Float64Value)),
                    _ => throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportedFloatIntCast,
                        primitiveType),
                };
            }

            var basicValueType = primitiveType.BasicValueType switch
            {
                BasicValueType.Float16 => BasicValueType.Int16,
                BasicValueType.Float32 => BasicValueType.Int32,
                BasicValueType.Float64 => BasicValueType.Int64,
                _ => throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedFloatIntCast,
                    primitiveType),
            };
            var type = GetPrimitiveType(basicValueType);
            return Append(new FloatAsIntCast(
                GetInitializer(location),
                node,
                type));
        }

        /// <summary>
        /// Creates an int as float reinterpret bit cast.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateIntAsFloatCast(
            Location location,
            Value node)
        {
            var primitiveType = node.Type.As<PrimitiveType>(location);
            if (UseConstantPropagation && node is PrimitiveValue primitive)
            {
                return primitiveType.BasicValueType switch
                {
                    BasicValueType.Int16 => CreatePrimitiveValue(
                        location,
                        Interop.IntAsFloat(primitive.UInt16Value)),
                    BasicValueType.Int32 => CreatePrimitiveValue(
                        location,
                        Interop.IntAsFloat(primitive.UInt32Value)),
                    BasicValueType.Int64 => CreatePrimitiveValue(
                        location,
                        Interop.IntAsFloat(primitive.UInt64Value)),
                    _ => throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportedFloatIntCast,
                        primitiveType),
                };
            }

            var basicValueType = primitiveType.BasicValueType switch
            {
                BasicValueType.Int16 => BasicValueType.Float16,
                BasicValueType.Int32 => BasicValueType.Float32,
                BasicValueType.Int64 => BasicValueType.Float64,
                _ => throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedFloatIntCast,
                    primitiveType),
            };
            var type = GetPrimitiveType(basicValueType);
            return Append(new IntAsFloatCast(
                GetInitializer(location),
                node,
                type));
        }
    }
}
