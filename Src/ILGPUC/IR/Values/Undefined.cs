// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Undefined.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Construction;

namespace ILGPUC.IR.Values;

/// <summary>
/// Represents an undefined value.
/// </summary>
sealed partial class UndefinedValue : Value
{
    #region Instance

    /// <summary>
    /// Constructs a undefined value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal UndefinedValue(in ValueInitializer initializer)
        : base(
              initializer,
              ValueFlags.NotReplaceable | ValueFlags.NoUses,
              initializer.Context.VoidType)
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateUndefined();

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "undef";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() => string.Empty;

    #endregion
}
