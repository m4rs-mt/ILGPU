// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents an abstract type that relies on addresses.
/// </summary>
abstract class AddressSpaceType : TypeValue
{
    /// <summary>
    /// Constructs a new address type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="addressSpace">The associated address space.</param>
    protected AddressSpaceType(
        in ModuleValueInitializer initializer,
        TypeValue elementType,
        MemoryAddressSpace addressSpace)
        : base(initializer, initializer.Builder.KindType)
    {
        AddressSpace = addressSpace;
        AddFlags(elementType.Flags);

        Seal(elementType);
    }

    /// <summary>
    /// Returns the underlying element type.
    /// </summary>
    public TypeValue ElementType => GetValue<TypeValue>(0);

    /// <summary>
    /// Returns the associated address space.
    /// </summary>
    public MemoryAddressSpace AddressSpace { get; }

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() =>
        HashCode.Combine(base.GetHashCode(), AddressSpace, ElementType);

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) =>
        obj is AddressSpaceType type &&
        type.AddressSpace == AddressSpace &&
        type.ElementType.Equals(ElementType) &&
        base.Equals(obj);

    /// <inheritdoc/>
    public override string ToString() =>
        $"{ToPrefixString()}<{ElementType}, {AddressSpace}>";
}

/// <summary>
/// Represents the type of a generic pointer.
/// </summary>
sealed partial class PointerType : AddressSpaceType
{
    /// <summary>
    /// Constructs a new pointer type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="addressSpace">The associated address space.</param>
    public PointerType(
        in ModuleValueInitializer initializer,
        TypeValue elementType,
        MemoryAddressSpace addressSpace)
        : base(initializer, elementType, addressSpace)
    {
        var pointerType = initializer.Module.IntPointerType;
        Size = pointerType.Size;
        Alignment = pointerType.Alignment;

        OverwriteType(basicValueType: pointerType.BasicValueType);
        AddFlags(TypeFlags.PointerDependent);
    }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.CreatePointerType(
            rewriter.Rewrite(ElementType), AddressSpace);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "ptr";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() =>
        HashCode.Combine(nameof(PointerType), ElementType);

    /// <summary cref="AddressSpaceType.Equals(object?)"/>
    public override bool Equals(object? obj) =>
        obj is PointerType && base.Equals(obj);
}

/// <summary>
/// Represents the type of a generic view.
/// </summary>
sealed partial class ViewType : AddressSpaceType
{
    /// <summary>
    /// Constructs a new view type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="addressSpace">The associated address space.</param>
    public ViewType(
        in ModuleValueInitializer initializer,
        TypeValue elementType,
        MemoryAddressSpace addressSpace)
        : base(initializer, elementType, addressSpace)
    {
        Size = Alignment = 4;
        AddFlags(TypeFlags.ViewDependent);
    }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.CreateViewType(
            rewriter.Rewrite(ElementType),
            AddressSpace);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "view";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() =>
        HashCode.Combine(nameof(ViewType), ElementType);

    /// <summary cref="AddressSpaceType.Equals(object?)"/>
    public override bool Equals(object? obj) =>
        obj is ViewType && base.Equals(obj);
}
