// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ILGPUC.IR.PureValues.Construction;

partial class PureValueBuilder
{
    /// <summary>
    /// Cache for address-space cast deduplication. Maps (sourceId, targetSpace)
    /// to the first created cast, so subsequent identical casts are reused.
    /// </summary>
    private readonly Dictionary<(ValueId, MemoryAddressSpace), AddressSpaceCast>
        _addrSpaceCastCache = new();
}

partial class PureValueBuilder
{
    /// <summary>
    /// Creates a cast operation that casts an integer value to a raw pointer.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="node">The operand.</param>
    /// <returns>A node that represents the cast operation.</returns>
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreateIntAsPointerCast(Location location, Value? node)
    {
        if (node is null) return null;

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
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreatePointerAsIntCast(
        Location location,
        Value? node,
        BasicValueType targetType)
    {
        if (node is null) return null;

        location.Assert(node.Type is PointerType && targetType.IsInt());
        return Append(new PointerAsIntCast(
            GetInitializer(location),
            node,
            ModuleBuilder.GetPrimitiveType(targetType)));
    }

    /// <summary>
    /// Creates a cast operation that casts the element type of a pointer
    /// but does not change its address space.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="node">The operand.</param>
    /// <param name="targetElementType">The target element type.</param>
    /// <returns>A node that represents the cast operation.</returns>
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreatePointerCast(
        Location location,
        Value? node,
        TypeValue targetElementType)
    {
        if (node is null) return null;

        var type = node.GetTypeAs<PointerType>();

        // Check whether the element types are the same
        if (type.ElementType.Equals(targetElementType))
            return node;

        // Check whether we are casting a nested pointer cast
        if (node is PointerCast pointerCast)
            node = pointerCast.Source;

        // Try to match casts of the initial base field to the its parent type
        if (
            node is LoadFieldAddress address &&
            address.StructureType.Equals(targetElementType) &&
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
    /// Creates an array to a view cast.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="value">The value to cast into a view.</param>
    /// <returns>A node that represents the cast operation.</returns>
    public Value? CreateArrayToViewCast(Location location, Value? value)
    {
        if (value is null) return null;

        return value.Type is ViewType
            ? value
            : Append(new ArrayToViewCast(GetInitializer(location), value));
    }

    /// <summary>
    /// Creates an address-space cast.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="node">The operand.</param>
    /// <param name="targetAddressSpace">The target address space.</param>
    /// <returns>A node that represents the cast operation.</returns>
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreateAddressSpaceCast(
        Location location,
        Value? node,
        MemoryAddressSpace targetAddressSpace)
    {
        if (node is null) return null;

        var type = node.GetTypeAs<AddressSpaceType>();
        // Simplify chained casts
        if (node is AddressSpaceCast cast)
        {
            node = cast.Source;
            type = cast.SourceType;
        }

        var sourceAddressSpace = type.AddressSpace;
        if (sourceAddressSpace == targetAddressSpace)
            return node;

        // Deduplicate: check if an identical cast was already created
        // for this source and target within the current builder scope.
        var key = (node.Id, targetAddressSpace);
        if (_addrSpaceCastCache.TryGetValue(key, out var cached))
            return cached;

        var result = Append(new AddressSpaceCast(
            GetInitializer(location),
            node,
            targetAddressSpace));
        _addrSpaceCastCache[key] = result;
        return result;
    }

    /// <summary>
    /// Creates a view cast.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="node">The operand.</param>
    /// <param name="targetElementType">The target element type.</param>
    /// <returns>A node that represents the cast operation.</returns>
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreateViewCast(
        Location location,
        Value? node,
        TypeValue targetElementType)
    {
        if (node is null) return null;
        if (node is ViewCast cast && cast.TargetElementType.Equals(targetElementType))
            return node;

        var type = node.GetTypeAs<ViewType>();
        return type.ElementType.Equals(targetElementType)
            ? node
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
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreateFloatAsIntCast(Location location, Value? node)
    {
        if (node is null or FloatAsIntCast) return node;

        var primitiveType = node.GetTypeAs<PrimitiveType>();
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
        var type = ModuleBuilder.GetPrimitiveType(basicValueType);
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
    [return: NotNullIfNotNull(nameof(node))]
    public Value? CreateIntAsFloatCast(Location location, Value? node)
    {
        if (node is null or IntAsFloatCast) return node;

        var primitiveType = node.GetTypeAs<PrimitiveType>();
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
        var type = ModuleBuilder.GetPrimitiveType(basicValueType);
        return Append(new IntAsFloatCast(
            GetInitializer(location),
            node,
            type));
    }
}
