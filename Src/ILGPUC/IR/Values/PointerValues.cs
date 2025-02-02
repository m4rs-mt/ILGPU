// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Construction;
using ILGPUC.IR.Types;
using System;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Values.ValueReference>;

namespace ILGPUC.IR.Values;

/// <summary>
/// Represents an abstract pointer value.
/// </summary>
abstract class PointerValue : Value
{
    #region Instance

    /// <summary>
    /// Constructs a new pointer value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal PointerValue(in ValueInitializer initializer)
        : base(initializer)
    { }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the source address.
    /// </summary>
    public ValueReference Source => this[0];

    /// <summary>
    /// Returns the associated element index.
    /// </summary>
    public ValueReference Offset => this[1];

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
    public AddressSpaceType AddressSpaceType =>
        Type.AsNotNullCast<AddressSpaceType>();

    /// <summary>
    /// Returns the pointer address space.
    /// </summary>
    public MemoryAddressSpace AddressSpace => AddressSpaceType.AddressSpace;

    /// <summary>
    /// Returns the element type.
    /// </summary>
    public TypeNode ElementType => AddressSpaceType.ElementType;

    #endregion
}

/// <summary>
/// Represents a value to compute a sub-view value.
/// </summary>
sealed partial class SubViewValue : PointerValue
{
    #region Instance

    /// <summary>
    /// Constructs a new sub-view computation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The source view.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    internal SubViewValue(
        in ValueInitializer initializer,
        ValueReference source,
        ValueReference offset,
        ValueReference length)
        : base(initializer)
    {
        Seal(source, offset, length);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the length of the sub view.
    /// </summary>
    public ValueReference Length => this[2];

    #endregion

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer) =>
        Source.Type;

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateSubViewValue(
            Location,
            rebuilder.Rebuild(Source),
            rebuilder.Rebuild(Offset),
            rebuilder.Rebuild(Length));

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "subv";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() => $"{Source}[{Offset} - {Length}]";

    #endregion
}

/// <summary>
/// Loads an element address of a view or a pointer.
/// </summary>
sealed partial class LoadElementAddress : PointerValue
{
    #region Instance

    /// <summary>
    /// Constructs a new address value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="sourceView">The source address.</param>
    /// <param name="elementIndex">The address of the referenced element.</param>
    internal LoadElementAddress(
        in ValueInitializer initializer,
        ValueReference sourceView,
        ValueReference elementIndex)
        : base(initializer)
    {
        Seal(sourceView, elementIndex);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if the current access works on a view.
    /// </summary>
    public bool IsViewAccess => Source.Type.IsViewType;

    /// <summary>
    /// Returns true if the current access works on a pointer.
    /// </summary>
    public bool IsPointerAccess => Source.Type.IsPointerType;

    /// <summary>
    /// Returns true if this access targets the first element.
    /// </summary>
    public bool AccessesFirstElement => Offset.IsPrimitive(0);

    #endregion

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer)
    {
        var sourceType = Source.Type.AsNotNullCast<AddressSpaceType>();
        Location.AssertNotNull(sourceType);

        return sourceType is PointerType
            ? Source.Type
            : initializer.Context.CreatePointerType(
                sourceType.ElementType,
                sourceType.AddressSpace);
    }

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateLoadElementAddress(
            Location,
            rebuilder.Rebuild(Source),
            rebuilder.Rebuild(Offset));

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "lea.";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() =>
        IsPointerAccess
        ? $"{Source} + {Offset}"
        : $"{Source}[{Offset}]";

    #endregion
}

/// <summary>
/// Loads the address of a single (possibly multi-dimensional) array element.
/// </summary>
sealed partial class LoadArrayElementAddress : PointerValue, IArrayValueOperation
{
    #region Nested Types

    /// <summary>
    /// An instance builder for laea values.
    /// </summary>
    internal struct Builder
    {
        #region Instance

        private ValueList builder;

        /// <summary>
        /// Initializes a new laea builder.
        /// </summary>
        /// <param name="irBuilder">The current IR builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="arrayValue">The parent array value.</param>
        internal Builder(IRBuilder irBuilder, Location location, Value arrayValue)
        {
            // Allocate number of dimensions + 1, to store the array value
            var arrayType = arrayValue.Type.As<ArrayType>(location);
            builder = ValueList.Create(arrayType.NumDimensions + 1);
            builder.Add(arrayValue);

            IRBuilder = irBuilder;
            Location = location;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent builder.
        /// </summary>
        public IRBuilder IRBuilder { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// Returns the source array value to load the element address from.
        /// </summary>
        public readonly Value ArrayValue => builder[0];

        /// <summary>
        /// Returns the array type.
        /// </summary>
        public readonly ArrayType ArrayType =>
            ArrayValue.Type.As<ArrayType>(Location);

        /// <summary>
        /// The number of dimensions.
        /// </summary>
        public int Count => builder.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given dimension length to the array value builder.
        /// </summary>
        /// <param name="dimension">The value to add.</param>
        public void Add(Value dimension)
        {
            Location.AssertNotNull(dimension);
            Location.Assert(Count < ArrayType.NumDimensions + 1);
            builder.Add(dimension);
        }

        /// <summary>
        /// Constructs a new value that represents the current array value.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public Value Seal() =>
            IRBuilder.FinishLoadArrayElementAddress(Location, ref builder);

        #endregion
    }

    #endregion

    #region Instance

    /// <summary>
    /// Constructs a new laea value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="values">
    /// The array value and a single value index for each array dimension.
    /// </param>
    internal LoadArrayElementAddress(
        in ValueInitializer initializer,
        ref ValueList values)
        : base(initializer)
    {
        this.Assert(
            values[0].Type.As<ArrayType>(this).NumDimensions == values.Count - 1);

        Seal(ref values);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the source array value.
    /// </summary>
    public ValueReference ArrayValue => this[0];

    /// <summary>
    /// Returns all accessor dimensions.
    /// </summary>
    public ReadOnlySpan<ValueReference> Dimensions => Nodes.Slice(1);

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override TypeNode ComputeType(in ValueInitializer initializer)
    {
        var sourceType = ArrayValue.Type.As<ArrayType>(Location);
        return initializer.Context.CreatePointerType(
            sourceType.ElementType,
            MemoryAddressSpace.Generic);
    }

    /// <inheritdoc/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder)
    {
        var values = rebuilder.Rebuild(Nodes);
        return builder.FinishLoadArrayElementAddress(
            Location,
            ref values);
    }

    #endregion

    #region Object

    /// <inheritdoc/>
    protected override string ToPrefixString() => "laea";

    #endregion
}

/// <summary>
/// Loads a field address of an object pointer.
/// </summary>
sealed partial class LoadFieldAddress : Value
{
    #region Instance

    /// <summary>
    /// Constructs a new address value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The source address.</param>
    /// <param name="fieldSpan">The structure field span.</param>
    internal LoadFieldAddress(
        in ValueInitializer initializer,
        ValueReference source,
        FieldSpan fieldSpan)
        : base(initializer)
    {
        FieldSpan = fieldSpan;
        Seal(source);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the source address.
    /// </summary>
    public ValueReference Source => this[0];

    /// <summary>
    /// Returns the structure type.
    /// </summary>
    public StructureType StructureType =>
        (Source.Type.AsNotNullCast<PointerType>().ElementType as StructureType)
        .AsNotNull();

    /// <summary>
    /// Returns the managed field information.
    /// </summary>
    public TypeNode FieldType => Type.AsNotNullCast<PointerType>().ElementType;

    /// <summary>
    /// Returns the field span.
    /// </summary>
    public FieldSpan FieldSpan { get; }

    #endregion

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer)
    {
        var pointerType = Source.Type.As<PointerType>(Location);
        var structureType = pointerType.ElementType.As<StructureType>(Location);

        var fieldType = structureType.Get(initializer.Context, FieldSpan);
        return initializer.Context.CreatePointerType(
            fieldType,
            pointerType.AddressSpace);
    }

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateLoadFieldAddress(
            Location,
            rebuilder.Rebuild(Source),
            FieldSpan);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "lfa.";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() =>
        $"{Source} -> {FieldSpan}";

    #endregion
}
