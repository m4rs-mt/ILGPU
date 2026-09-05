// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateHandleType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents the opaque element type of delegate handles: values of delegate
/// types are pointers to this type. The frontend binds delegates to their
/// targets at compile time, so handles are never dereferenced; their own type
/// lets the frontend recognize them independently of how they were created.
/// Backends and IR dumps treat the type like a pointer-sized integer, which
/// keeps delegate handles opaque pointers in generated code.
/// </summary>
sealed partial class DelegateHandleType : TypeValue
{
    /// <summary>
    /// Constructs a new delegate handle type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="intPointerType">The pointer-sized integer type.</param>
    public DelegateHandleType(
        in ModuleValueInitializer initializer,
        PrimitiveType intPointerType)
        : base(initializer, initializer.Builder.KindType)
    {
        RepresentationType = intPointerType.BasicValueType;
        Size = intPointerType.Size;
        Alignment = intPointerType.Alignment;
        Seal();
    }

    /// <summary>
    /// Returns the pointer-sized integer type backends emit for this type.
    /// </summary>
    public BasicValueType RepresentationType { get; }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.DelegateHandleType;

    /// <summary>
    /// Returns the string "delegate".
    /// </summary>
    protected override string ToPrefixString() => "delegate";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => nameof(DelegateHandleType).GetHashCode(
        System.StringComparison.Ordinal);

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) => obj is DelegateHandleType;
}
