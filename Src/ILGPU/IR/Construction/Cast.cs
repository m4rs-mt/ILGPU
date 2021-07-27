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
using ILGPU.Util;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a cast operation that casts an integer value to a raw pointer.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateIntAsPointerCast(
            Location location,
            Value node)
        {
            location.Assert(node.BasicValueType.IsInt());
            return Append(new IntAsPointerCast(
                GetInitializer(location),
                node));
        }

        /// <summary>
        /// Creates a cast operation that casts a pointer into an integer.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="targetType">The target integer type.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreatePointerAsIntCast(
            Location location,
            Value node,
            BasicValueType targetType)
        {
            location.Assert(node.Type.IsPointerType && targetType.IsInt());
            return Append(new PointerAsIntCast(
                GetInitializer(location),
                node,
                GetPrimitiveType(targetType)));
        }

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

            // Check whether the element types are the same
            if (type.ElementType == targetElementType)
                return node;

            // Check whether we are casting a nested pointer cast
            if (node is PointerCast pointerCast)
                node = pointerCast.Value;

            // Try to match casts of the initial base field to the its parent type
            if (
                node is LoadFieldAddress address &&
                address.StructureType == targetElementType &&
                address.FieldSpan == new FieldSpan(0))
            {
                // Convert to the appropriate address space
                return CreateAddressSpaceCast(
                    location,
                    address.Source,
                    type.AddressSpace);
            }

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

            // Simplify chained casts
            if (node is AddressSpaceCast cast)
            {
                node = cast.Value;
                type = cast.SourceType;
            }

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
        /// Creates an array to a view cast.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value to cast into a view.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateArrayToViewCast(Location location, Value value) =>
            value.Type is ViewType
                ? value
                : Append(new ArrayToViewCast(GetInitializer(location), value));

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
            if (node is PrimitiveValue primitive)
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
            if (node is PrimitiveValue primitive)
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
