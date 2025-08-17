// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents the type of a generic array that lives in the local address space.
/// </summary>
sealed partial class ArrayType : ObjectType
{
    /// <summary>
    /// Constructs a new array type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="numDimensions">The number of array dimensions.</param>
    public ArrayType(
        in ModuleValueInitializer initializer,
        TypeValue elementType,
        int numDimensions)
        : base(initializer, initializer.Builder.KindType)
    {
        this.Assert(
            numDimensions > 0,
            $"ArrayType requires numDimensions > 0 (got {numDimensions})");

        NumDimensions = numDimensions;

        Size = Alignment = 4;
        AddFlags(TypeFlags.ArrayDependent);

        Seal(elementType);
    }

    /// <summary>
    /// Returns the underlying element type.
    /// </summary>
    public TypeValue ElementType => GetValue<TypeValue>(0);

    /// <summary>
    /// Returns the number of array dimensions.
    /// </summary>
    public int NumDimensions { get; }

    /// <inheritdoc/>
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.ModuleBuilder.CreateArrayType(
            rewriter.Rewrite(ElementType),
            NumDimensions);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => $"Array{NumDimensions}D";

    /// <inheritdoc/>
    public override string ToString() => $"{ToPrefixString()}<{ElementType}>";

    /// <summary cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() =>
        HashCode.Combine(base.GetHashCode(), NumDimensions);

    /// <summary cref="TypeValue.Equals(object?)"/>
    public override bool Equals(object? obj) =>
        obj is ArrayType arrayType &&
        arrayType.ElementType.Equals(ElementType) &&
        arrayType.NumDimensions == NumDimensions &&
        base.Equals(obj);
}
