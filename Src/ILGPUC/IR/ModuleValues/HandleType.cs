// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HandleType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a .Net runtime-specific handle type.
/// </summary>
sealed partial class HandleType : ObjectType
{
    /// <summary>
    /// Constructs a new .Net runtime-specific handle type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    public HandleType(in ModuleValueInitializer initializer)
        : base(initializer, initializer.Builder.KindType)
    {
        Seal();
    }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.HandleType;

    /// <summary>
    /// Returns the string "handle".
    /// </summary>
    protected override string ToPrefixString() => "handle";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => nameof(HandleType).GetHashCode(
        System.StringComparison.Ordinal);

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) => obj is HandleType;
}
