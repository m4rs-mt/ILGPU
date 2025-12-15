// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues.Construction;
using System;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents an abstract pointer value.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class PointerValue(in PureValueInitializer initializer, TypeValue type) :
    PureValue(initializer, type)
{
    /// <summary>
    /// Returns the source address.
    /// </summary>
    public Value Source => GetValue<Value>(0);

    /// <summary>
    /// Returns the associated element index.
    /// </summary>
    public Value Offset => GetValue<Value>(1);

    /// <summary>
    /// Returns true if this is a 32bit element access.
    /// </summary>
    public bool Is32BitAccess => Offset.BasicValueType <= BasicValueType.Int32;

    /// <summary>
    /// Returns true if this is a 64bit element access.
    /// </summary>
    public bool Is64bitAccess => Offset.BasicValueType == BasicValueType.Int64;

    /// <summary>
    /// Returns the view element type.
    /// </summary>
    public AddressSpaceType AddressSpaceType => Type.As<AddressSpaceType>();

    /// <summary>
    /// Returns the pointer address space.
    /// </summary>
    public MemoryAddressSpace AddressSpace => AddressSpaceType.AddressSpace;

    /// <summary>
    /// Returns the element type.
    /// </summary>
    public TypeValue ElementType => AddressSpaceType.ElementType;
}

/// <summary>
/// Loads an element address of a view or a pointer.
/// </summary>
sealed partial class LoadElementAddress : PointerValue
{
    /// <summary>
    /// Determines the type of a load element address operation.
    /// </summary>
    private static TypeValue GetType(
        in PureValueInitializer initializer,
        Value sourceView)
    {
        var sourceType = sourceView.Type.AsNotNullCast<AddressSpaceType>();
        var type = sourceType is PointerType
            ? sourceView.Type
            : initializer.ModuleBuilder.CreatePointerType(
                sourceType.ElementType,
                sourceType.AddressSpace);
        return type;
    }

    /// <summary>
    /// Constructs a new address value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="sourceView">The source address.</param>
    /// <param name="elementIndex">The address of the referenced element.</param>
    public LoadElementAddress(
        in PureValueInitializer initializer,
        Value sourceView,
        Value elementIndex)
        : base(initializer, GetType(initializer, sourceView))
    {
        Seal(sourceView, elementIndex);
    }

    /// <summary>
    /// Returns true if the current access works on a view.
    /// </summary>
    public bool IsViewAccess => Source.Type is ViewType;

    /// <summary>
    /// Returns true if the current access works on a pointer.
    /// </summary>
    public bool IsPointerAccess => Source.Type is PointerType;

    /// <summary>
    /// Returns true if this access targets the first element.
    /// </summary>
    public bool AccessesFirstElement => Offset.IsPrimitiveValue(0);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateLoadElementAddress(
            Location,
            rewriter.Rewrite(Source),
            rewriter.Rewrite(Offset));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "lea.";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        IsPointerAccess
        ? $"{Source.ToReferenceString()} + {Offset.ToReferenceString()}"
        : $"{Source.ToReferenceString()}[{Offset.ToReferenceString()}]";
}

/// <summary>
/// Loads a field address of an object pointer.
/// </summary>
sealed partial class LoadFieldAddress : PureValue
{
    /// <summary>
    /// Determines the type of a load field address operation.
    /// </summary>
    private static PointerType GetType(
        in PureValueInitializer initializer,
        Value source,
        FieldSpan fieldSpan)
    {
        var pointerType = source.GetTypeAs<PointerType>();
        var structureType = pointerType.ElementType.As<StructureType>();

        var fieldType = structureType.Get(initializer.ModuleBuilder, fieldSpan);
        var type = initializer.ModuleBuilder.CreatePointerType(
            fieldType,
            pointerType.AddressSpace);
        return type;
    }

    /// <summary>
    /// Constructs a new address value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The source address.</param>
    /// <param name="fieldSpan">The structure field span.</param>
    public LoadFieldAddress(
        in PureValueInitializer initializer,
        Value source,
        FieldSpan fieldSpan)
        : base(initializer, GetType(initializer, source, fieldSpan))
    {
        FieldSpan = fieldSpan;

        Seal(source);
    }

    /// <summary>
    /// Returns the source address.
    /// </summary>
    public Value Source => GetValue<Value>(0);

    /// <summary>
    /// Returns the structure type.
    /// </summary>
    public StructureType StructureType =>
        Source.GetTypeAs<PointerType>().ElementType.AsNotNullCast<StructureType>();

    /// <summary>
    /// Returns the managed field information.
    /// </summary>
    public TypeValue FieldType => Type.AsNotNullCast<PointerType>().ElementType;

    /// <summary>
    /// Returns the field span.
    /// </summary>
    public FieldSpan FieldSpan { get; }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var span = rewriter.Rewrite(StructureType, FieldSpan);
        return rewriter.Builder.CreateLoadFieldAddress(
            Location,
            rewriter.Rewrite(Source),
            span);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "lfa.";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Source.ToReferenceString()} -> {FieldSpan}";
}

/// <summary>
/// Loads the address of a single (possibly multi-dimensional) array element.
/// </summary>
sealed partial class LoadArrayElementAddress :
    PointerValue, IArrayValueOperation
{
    #region Nested Types

    /// <summary>
    /// An instance builder for laea values.
    /// </summary>
    internal struct Builder
    {
        private ValueBuilderList _builder;

        /// <summary>
        /// Initializes a new laea builder.
        /// </summary>
        /// <param name="pureBuilder">The current IR builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="arrayValue">The parent array value.</param>
        public Builder(
            PureValueBuilder pureBuilder,
            Location location,
            Value arrayValue)
        {
            // Allocate number of dimensions + 1, to store the array value
            var arrayType = arrayValue.GetTypeAs<ArrayType>();
            _builder = ValueBuilderList.Create(
                pureBuilder.Generation,
                arrayType.NumDimensions + 1);
            _builder.Add(arrayValue);

            PureBuilder = pureBuilder;
            Location = location;
        }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public PureValueBuilder PureBuilder { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// Returns the source array value to load the element address from.
        /// </summary>
        public readonly Value ArrayValue => _builder.Get<Value>(0);

        /// <summary>
        /// Returns the array type.
        /// </summary>
        public readonly ArrayType ArrayType => ArrayValue.GetTypeAs<ArrayType>();

        /// <summary>
        /// The number of dimensions.
        /// </summary>
        public readonly int Count => _builder.Count;

        /// <summary>
        /// Adds the given dimension length to the array value builder.
        /// </summary>
        /// <param name="dimension">The value to add.</param>
        public void Add(Value? dimension)
        {
            if (dimension is null) return;

            Location.Assert(Count < ArrayType.NumDimensions + 1);
            _builder.Add(dimension);
        }

        /// <summary>
        /// Constructs a new value that represents the current array value.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public Value Seal() =>
            PureBuilder.FinishLoadArrayElementAddress(Location, ref _builder);
    }

    #endregion

    #region LoadArrayElementAddress Instance

    /// <summary>
    /// Determines the type of a load array element address operation.
    /// </summary>
    private static PointerType GetType(
        in PureValueInitializer initializer,
        ref ValueBuilderList values)
    {
        var arrayValue = values.Get<Value>(0);
        var arrayType = arrayValue.GetTypeAs<ArrayType>();
        arrayValue.Assert(arrayType.NumDimensions == values.Count - 1);

        var type = initializer.ModuleBuilder.CreatePointerType(
            arrayType.ElementType,
            MemoryAddressSpace.Generic);
        return type;
    }

    /// <summary>
    /// Constructs a new laea value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="values">
    /// The array value and a single value index for each array dimension.
    /// </param>
    public LoadArrayElementAddress(
        in PureValueInitializer initializer,
        ref ValueBuilderList values)
        : base(initializer, GetType(initializer, ref values))
    {
        Seal(ref values);
    }

    /// <summary>
    /// Returns the source array value.
    /// </summary>
    public Value ArrayValue => GetValue<Value>(0);

    /// <summary>
    /// Returns all accessor dimensions.
    /// </summary>
    public ReadOnlySpan<Value> Dimensions => Values[1..];

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var newArray = rewriter.Rewrite(ArrayValue);
        if (newArray is null) return null;

        var addressBuilder = rewriter.Builder.CreateLoadArrayElementAddress(
            Location,
            newArray);
        foreach (var dimension in Dimensions)
            addressBuilder.Add(rewriter.Rewrite(dimension));
        return addressBuilder.Seal();
    }

    /// <inheritdoc/>
    protected override string ToPrefixString() => "laea";

    #endregion
}
