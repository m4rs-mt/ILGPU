// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a cast operation that casts the element type of a pointer
        /// but does not change its address space.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="targetElementType">The target element type.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreatePointerCast(
            Value node,
            TypeNode targetElementType)
        {
            Debug.Assert(node != null, "Invalid node");
            Debug.Assert(targetElementType != null, "Invalid target element type");

            var type = node.Type as PointerType;
            Debug.Assert(type != null, "Invalid pointer type");

            if (type.ElementType == targetElementType)
                return node;

            var pointerType = CreatePointerType(targetElementType, type.AddressSpace);
            return CreateUnifiedValue(new PointerCast(
                Generation,
                node,
                pointerType));
        }

        /// <summary>
        /// Creates an address-space cast.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="targetAddressSpace">The target address space.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateAddressSpaceCast(
            Value node,
            MemoryAddressSpace targetAddressSpace)
        {
            Debug.Assert(node != null, "Invalid node");

            var type = node.Type as AddressSpaceType;
            Debug.Assert(type != null, "Invalid address space type");

            var sourceAddressSpace = type.AddressSpace;
            if (sourceAddressSpace == targetAddressSpace)
                return node;

            var addressSpaceType = SpecializeAddressSpaceType(type, targetAddressSpace);
            return CreateUnifiedValue(new AddressSpaceCast(
                Generation,
                node,
                addressSpaceType));
        }

        /// <summary>
        /// Creates a view cast.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="targetElementType">The target element type.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateViewCast(
            Value node,
            TypeNode targetElementType)
        {
            Debug.Assert(node != null, "Invalid node");

            var type = node.Type as ViewType;
            Debug.Assert(type != null, "Invalid view type");

            if (type.ElementType == targetElementType)
                return node;

            var viewType = CreateViewType(
                targetElementType,
                type.AddressSpace);
            return CreateUnifiedValue(new ViewCast(
                Generation,
                node,
                viewType));
        }

        /// <summary>
        /// Creates a float as int reinterpret bit cast.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateFloatAsIntCast(Value node)
        {
            Debug.Assert(node != null, "Invalid node");

            var primitiveType = node.Type as PrimitiveType;
            Debug.Assert(primitiveType != null, "Invalid primitive type");

            if (node is PrimitiveValue primitive)
            {
                switch (primitiveType.BasicValueType)
                {
                    case BasicValueType.Float32:
                        return CreatePrimitiveValue(
                            Interop.FloatAsInt(primitive.Float32Value));
                    case BasicValueType.Float64:
                        return CreatePrimitiveValue(
                            Interop.FloatAsInt(primitive.Float64Value));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(node));
                }
            }

            BasicValueType basicValueType;
            switch (primitiveType.BasicValueType)
            {
                case BasicValueType.Float32:
                    basicValueType = BasicValueType.Int32;
                    break;
                case BasicValueType.Float64:
                    basicValueType = BasicValueType.Int64;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }

            var type = CreatePrimitiveType(basicValueType);
            return CreateUnifiedValue(new FloatAsIntCast(
                Generation,
                node,
                type));
        }

        /// <summary>
        /// Creates an int as float reinterpret bit cast.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the cast operation.</returns>
        public ValueReference CreateIntAsFloatCast(Value node)
        {
            Debug.Assert(node != null, "Invalid node");

            var primitiveType = node.Type as PrimitiveType;
            Debug.Assert(primitiveType != null, "Invalid primitive type");

            if (node is PrimitiveValue primitive)
            {
                switch (primitiveType.BasicValueType)
                {
                    case BasicValueType.Int32:
                        return CreatePrimitiveValue(
                            Interop.IntAsFloat(primitive.UInt32Value));
                    case BasicValueType.Int64:
                        return CreatePrimitiveValue(
                            Interop.IntAsFloat(primitive.UInt64Value));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(node));
                }
            }

            BasicValueType basicValueType;
            switch (primitiveType.BasicValueType)
            {
                case BasicValueType.Int32:
                    basicValueType = BasicValueType.Float32;
                    break;
                case BasicValueType.Int64:
                    basicValueType = BasicValueType.Float64;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }

            var type = CreatePrimitiveType(basicValueType);
            return CreateUnifiedValue(new IntAsFloatCast(
                Generation,
                node,
                type));
        }

    }
}
