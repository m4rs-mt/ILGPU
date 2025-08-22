// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PaddingType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a padding type.
/// </summary>
sealed partial class PaddingType : TypeValue
{
    /// <summary>
    /// Constructs a new padding type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="primitiveType">The primitive type to use for padding.</param>
    public PaddingType(in ModuleValueInitializer initializer, PrimitiveType primitiveType)
        : base(initializer, initializer.Builder.KindType)
    {
        Size = primitiveType.Size;
        Alignment = primitiveType.Alignment;

        OverwriteType(basicValueType: primitiveType.BasicValueType);
        Seal(primitiveType);
    }

    /// <summary>
    /// Returns the associated basic value type.
    /// </summary>
    public PrimitiveType PrimitiveType => GetValue<PrimitiveType>(0);

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.GetPaddingType(BasicValueType);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => BasicValueType.ToString();

    /// <inheritdoc cref="Value.ToString"/>
    public override string ToString() => $"padding_{BasicValueType}";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => BasicValueType.GetHashCode();

    /// <summary cref="TypeValue.Equals(object)"/>
    public override bool Equals(object? obj) =>
        obj is PaddingType paddingType &&
        paddingType.BasicValueType == BasicValueType;
}
