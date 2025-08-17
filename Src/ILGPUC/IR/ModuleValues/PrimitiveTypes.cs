// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PrimitiveTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Util;
using System.Collections.Generic;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a primitive type.
/// </summary>
sealed partial class PrimitiveType : TypeValue
{
    #region Static

    /// <summary>
    /// Contains default size information about built-in types.
    /// </summary>
    private static readonly BasicValueTypeMap<int> _basicTypeInformation = new()
    {
        { BasicValueType.None, 0 },
        { BasicValueType.Int1, 1 },
        { BasicValueType.Int8, 1 },
        { BasicValueType.Int16, 2 },
        { BasicValueType.Int32, 4 },
        { BasicValueType.Int64, 8 },
        { BasicValueType.Float16, 2 },
        { BasicValueType.Float32, 4 },
        { BasicValueType.Float64, 8 },
    };

    /// <summary>
    /// Maps integer-based type size values to <see cref="BasicValueType"/> entries.
    /// </summary>
    private static readonly Dictionary<int, BasicValueType> _basicSizeInformation = new()
    {
        { 0, BasicValueType.None },
        { 1, BasicValueType.Int8 },
        { 2, BasicValueType.Int16 },
        { 4, BasicValueType.Int32 },
        { 8, BasicValueType.Int64 },
    };

    /// <summary>
    /// Determines the <see cref="BasicValueType"/> that corresponds to the given
    /// type size in bytes (if any).
    /// </summary>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The basic value type (if any).</returns>
    public static BasicValueType GetBasicValueTypeBySize(int size) =>
        _basicSizeInformation[size];

    #endregion

    #region Primitive Type

    /// <summary>
    /// Constructs a new primitive type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="basicValueType">The basic value type.</param>
    public PrimitiveType(
        in ModuleValueInitializer initializer,
        BasicValueType basicValueType)
        : base(initializer, initializer.Builder.KindType)
    {
        Size = Alignment = _basicTypeInformation[basicValueType];

        OverwriteType(basicValueType: basicValueType);
        Seal();
    }

    /// <summary>
    /// Returns true if this type represents a bool type.
    /// </summary>
    public bool IsBool => BasicValueType == BasicValueType.Int1;

    /// <summary>
    /// Returns true if this type represents a 32 bit type.
    /// </summary>
    public bool Is32Bit =>
        BasicValueType == BasicValueType.Int32 ||
        BasicValueType == BasicValueType.Float32;

    /// <summary>
    /// Returns true if this type represents a 64 bit type.
    /// </summary>
    public bool Is64Bit =>
        BasicValueType == BasicValueType.Int64 ||
        BasicValueType == BasicValueType.Float64;

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.GetPrimitiveType(BasicValueType);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => BasicValueType.ToString();

    /// <summary cref="Value.ToString"/>
    public override string ToString() => BasicValueType.ToString();

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => BasicValueType.GetHashCode();

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) =>
        obj is PrimitiveType primitiveType &&
        primitiveType.BasicValueType == BasicValueType;

    #endregion
}

/// <summary>
/// Represents a string type.
/// </summary>
sealed partial class StringType : TypeValue
{
    /// <summary>
    /// Constructs a new string type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    public StringType(in ModuleValueInitializer initializer)
        : base(initializer, initializer.Builder.KindType)
    {
        Seal();
    }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.StringType;

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "string";

    /// <summary cref="Value.ToString"/>
    public override string ToString() => "string";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => nameof(StringType).GetHashCode(
        System.StringComparison.Ordinal);

    /// <summary cref="TypeValue.Equals(object)"/>
    public override bool Equals(object? obj) => obj is StringType;
}
