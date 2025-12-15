// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VoidType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a void type.
/// </summary>
sealed partial class VoidType : TypeValue
{
    /// <summary>
    /// Constructs a new void type
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    public VoidType(in ModuleValueInitializer initializer)
        : base(initializer, initializer.Builder.KindType)
    {
        Seal();
    }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.VoidType;

    /// <summary>
    /// Returns the string "void".
    /// </summary>
    protected override string ToPrefixString() => "void";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => nameof(VoidType).GetHashCode(
        System.StringComparison.Ordinal);

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) => obj is VoidType;

    /// <summary>
    /// Returns the string "void".
    /// </summary>
    public override string ToString() => ToPrefixString();
}
