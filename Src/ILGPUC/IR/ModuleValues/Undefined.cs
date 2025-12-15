// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Undefined.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents an undefined value.
/// </summary>
sealed partial class UndefinedValue : ModuleValue
{
    /// <summary>
    /// Constructs a undefined value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    public UndefinedValue(in ModuleValueInitializer initializer)
        : base(initializer, initializer.Builder.KindType)
    {
        Seal(initializer.Builder.VoidType);
    }

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "undef";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => string.Empty;
}
