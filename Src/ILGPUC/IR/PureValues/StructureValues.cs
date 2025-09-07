// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues.Construction;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents an immutable structure value.
/// </summary>
sealed partial class StructureValue : PureValue
{
    #region Nested Types

    /// <summary>
    /// An internal instance builder.
    /// </summary>
    internal interface IInternalBuilder
    {
        /// <summary>
        /// Returns the current location.
        /// </summary>
        Location Location { get; }

        /// <summary>
        /// The number of field values.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns the value that corresponds to the given field access.
        /// </summary>
        /// <param name="access">The field access.</param>
        /// <returns>The resolved field type.</returns>
        Value this[FieldAccess access] { get; }

        /// <summary>
        /// Moves the underlying array builder to a target list and outputs an
        /// assembled structure type.
        /// </summary>
        /// <param name="values">The resulting array of value references.</param>
        /// <returns>The resulting structure type.</returns>
        StructureType Seal(ref ValueBuilderList values);
    }

    /// <summary>
    /// An instance builder for structure instances.
    /// </summary>
    internal struct Builder : IInternalBuilder
    {
        #region Instance

        private ValueBuilderList _builder;

        /// <summary>
        /// Initializes a new instance builder.
        /// </summary>
        /// <param name="pureBuilder">The current pure value builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="parent">The parent type.</param>
        internal Builder(
            PureValueBuilder pureBuilder,
            Location location,
            StructureType parent)
        {
            _builder = ValueBuilderList.Create(pureBuilder.Generation, parent.NumFields);

            PureBuilder = pureBuilder;
            Location = location;
            Parent = parent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent builder.
        /// </summary>
        public PureValueBuilder PureBuilder { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// Returns the corresponding parent type.
        /// </summary>
        public StructureType Parent { get; }

        /// <summary>
        /// The number of field values.
        /// </summary>
        public readonly int Count => _builder.Count;

        /// <summary>
        /// Returns the value that corresponds to the given field access.
        /// </summary>
        /// <param name="access">The field access.</param>
        /// <returns>The resolved field type.</returns>
        public readonly Value this[FieldAccess access]
        {
            get => _builder.AsSpan()[access.Index];
            set
            {
                Location.Assert(value.Type is not ModuleValues.StructureType);
                Location.Assert(
                    value.Type.Equals(Parent[access.Index]) || value is UndefinedValue);

                _builder.AsSpan()[access.Index] = value;
            }
        }

        /// <summary>
        /// Returns the next expected type to be added.
        /// </summary>
        public readonly TypeValue? NextExpectedType =>
            Count < Parent.NumFields ? Parent[Count] : null;

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given value to the instance builder.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(Value? value)
        {
            if (value is null) return;

            Location.Assert(
                value.Type is not ModuleValues.StructureType &&
                Count + 1 <= Parent.NumFields);

            Location.Assert(
                value.Type.Equals(Parent[Count]) ||
                value is UndefinedValue ||
                Parent[Count] is PaddingType);

            _builder.Add(value);
        }

        /// <summary>
        /// Constructs a new value that represents the current value builder.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public Value Seal() => PureBuilder.FinishStructureBuilder(ref this);

        /// <summary>
        /// Moves the underlying array builder to a target list and outputs an
        /// assembled structure type.
        /// </summary>
        StructureType IInternalBuilder.Seal(ref ValueBuilderList values)
        {
            Parent.Assert(Count == Parent.NumFields);
            _builder.MoveTo(ref values);
            return Parent;
        }

        #endregion
    }

    /// <summary>
    /// An instance builder for dynamically typed structure instances.
    /// </summary>
    internal struct DynamicBuilder : IInternalBuilder
    {
        #region Instance

        private ValueBuilderList _builder;

        /// <summary>
        /// Initializes a new instance builder.
        /// </summary>
        /// <param name="pureBuilder">The current IR builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="capacity">The initial capacity.</param>
        public DynamicBuilder(
            PureValueBuilder pureBuilder,
            Location location,
            int capacity)
        {
            _builder = ValueBuilderList.Create(pureBuilder.Generation, capacity);
            PureBuilder = pureBuilder;
            Location = location;
        }

        /// <summary>
        /// Initializes a new instance builder.
        /// </summary>
        /// <param name="pureBuilder">The current pure value builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="values">The initial capacity.</param>
        public DynamicBuilder(
            PureValueBuilder pureBuilder,
            Location location,
            ref ValueBuilderList values)
        {
            _builder = ValueBuilderList.Empty(pureBuilder.Generation);
            values.MoveTo(ref _builder);

            PureBuilder = pureBuilder;
            Location = location;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent builder.
        /// </summary>
        public PureValueBuilder PureBuilder { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// The number of field values.
        /// </summary>
        public readonly int Count => _builder.Count;

        /// <summary>
        /// Returns the value that corresponds to the given field access.
        /// </summary>
        /// <param name="access">The field access.</param>
        /// <returns>The resolved field type.</returns>
        public readonly Value this[FieldAccess access]
        {
            get => _builder.AsSpan()[access.Index];
            set => _builder.AsSpan()[access.Index] = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given value to the instance builder.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(Value? value)
        {
            if (value is null) return;

            Location.Assert(value.Type is not ModuleValues.StructureType);
            _builder.Add(value);
        }

        /// <summary>
        /// Constructs a new value that represents the current value builder.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public Value Seal() => PureBuilder.FinishStructureBuilder(ref this);

        /// <summary>
        /// Moves the underlying array builder to a target list and outputs an
        /// assembled structure type.
        /// </summary>
        StructureType IInternalBuilder.Seal(ref ValueBuilderList values)
        {
            // Create a new structure type that corresponds to all value types
            var typeBuilder = PureBuilder.ModuleBuilder.CreateStructureType(Count);
            foreach (var value in _builder)
                typeBuilder.Add(value.Type);
            _builder.MoveTo(ref values);
            return typeBuilder.Seal().As<StructureType>();
        }

        #endregion
    }

    #endregion

    #region Structure Value

    /// <summary>
    /// Constructs a new structure value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="structureType">The associated structure type.</param>
    /// <param name="fieldValues">The field values.</param>
    public StructureValue(
        in PureValueInitializer initializer,
        StructureType structureType,
        ref ValueBuilderList fieldValues)
        : base(initializer, structureType)
    {
        Seal(ref fieldValues);
    }

    /// <summary>
    /// Returns the structure type.
    /// </summary>
    public StructureType StructureType => Type.AsNotNullCast<StructureType>();

    /// <summary>
    /// Gets a new nested structure value.
    /// </summary>
    /// <param name="builder">The parent builder.</param>
    /// <param name="location">The current location.</param>
    /// <param name="fieldSpan">The field span.</param>
    /// <returns>The resolved structure value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Value Get(
        PureValueBuilder builder,
        Location location,
        FieldSpan fieldSpan)
    {
        if (!fieldSpan.HasSpan)
            return GetValue<Value>(fieldSpan.Index);
        else if (fieldSpan.Index == 0 && fieldSpan.Span == Count)
            return this;

        var resultType = StructureType
            .Get(builder.ModuleBuilder, fieldSpan)
            .As<StructureType>();
        var instance = builder.CreateStructure(location, resultType);
        for (int i = 0; i < fieldSpan.Span; ++i)
            instance.Add(GetValue<Value>(fieldSpan.Index + i));
        return instance.Seal();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var rewrittenType = rewriter.RewriteAs<StructureType>(StructureType);
        var instance = rewriter.Builder.CreateStructure(
            Location,
            rewrittenType);
        foreach (var operand in Values)
            instance.Add(rewriter.Rewrite(operand));
        return instance.Seal();
    }

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "struct";

    #endregion
}

/// <summary>
/// Represents an operation on structure values.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
/// <param name="fieldSpan">The field span.</param>
abstract class StructureOperationValue(
    in PureValueInitializer initializer,
    TypeValue type,
    FieldSpan fieldSpan) : PureValue(initializer, type)
{
    /// <summary>
    /// Returns the object value to load from.
    /// </summary>
    public Value Source => GetValue<Value>(0);

    /// <summary>
    /// Returns the structure type.
    /// </summary>
    public StructureType StructureType => Source.Type.AsNotNullCast<StructureType>();

    /// <summary>(
    /// Returns the field span.
    /// </summary>
    public FieldSpan FieldSpan { get; } = fieldSpan;

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Source.ToReferenceString()}[{FieldSpan}]";
}

/// <summary>
/// Represents an operation to load a single field from an object.
/// </summary>
sealed partial class GetField : StructureOperationValue
{
    /// <summary>
    /// Determines the type of a new view operation.
    /// </summary>
    private static TypeValue GetType(
        in PureValueInitializer initializer,
        Value structValue,
        FieldSpan fieldSpan)
    {
        var structureType = structValue.Type.AsNotNullCast<StructureType>();
        var type = structureType.Get(initializer.ModuleBuilder, fieldSpan);
        return type;
    }

    /// <summary>
    /// Constructs a new field load.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="structValue">The structure value.</param>
    /// <param name="fieldSpan">The field span.</param>
    public GetField(
        in PureValueInitializer initializer,
        Value structValue,
        FieldSpan fieldSpan)
        : base(initializer, GetType(initializer, structValue, fieldSpan), fieldSpan)
    {
        Seal(structValue);
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var span = rewriter.Rewrite(StructureType, FieldSpan);
        return rewriter.Builder.CreateGetField(
            Location,
            rewriter.Rewrite(Source),
            span);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "gfld";
}

/// <summary>
/// Represents an operation to store a single field of an object.
/// </summary>
sealed partial class SetField : StructureOperationValue
{
    /// <summary>
    /// Constructs a new field store.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="structValue">The structure value.</param>
    /// <param name="fieldSpan">The field access.</param>
    /// <param name="value">The value to store.</param>
    internal SetField(
        in PureValueInitializer initializer,
        Value structValue,
        FieldSpan fieldSpan,
        Value value)
        : base(initializer, structValue.Type, fieldSpan)
    {
        Seal(structValue, value);
    }

    /// <summary>
    /// Returns the value to store.
    /// </summary>
    public Value Value => GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var span = rewriter.Rewrite(StructureType, FieldSpan);
        return rewriter.Builder.CreateSetField(
            Location,
            rewriter.Rewrite(Source),
            span,
            rewriter.Rewrite(Value));
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "sfld";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        base.ToArgString() + " -> " + Value.ToReferenceString();
}
