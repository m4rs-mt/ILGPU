// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: KindType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents the class of a single IR value.
/// </summary>
sealed partial class KindType : ObjectType
{
    /// <summary>
    /// Constructs a new kind type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    public KindType(in ModuleValueInitializer initializer)
        : base(initializer, initializer.Builder.KindType)
    {
        OverwriteType(this, BasicValueType.None);

        Seal();
    }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.KindType;

    /// <summary>
    /// Returns the string "kind".
    /// </summary>
    protected override string ToPrefixString() => "kind";

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) => obj is KindType;

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => base.GetHashCode();

    /// <summary>
    /// Returns the string "kind".
    /// </summary>
    public override string ToString() => ToPrefixString();
}
