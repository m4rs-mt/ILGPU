// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: View.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents a new view.
/// </summary>
sealed partial class NewView : PureValue
{
    /// <summary>
    /// Determines the type of a new view operation.
    /// </summary>
    private static ViewType GetType(in PureValueInitializer initializer, Value pointer)
    {
        var pointerType = pointer.GetTypeAs<PointerType>();
        var type = initializer.ModuleBuilder.CreateViewType(
            pointerType.ElementType,
            pointerType.AddressSpace);
        return type;
    }

    /// <summary>
    /// Constructs a view.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="pointer">The underlying pointer.</param>
    /// <param name="length">The number of elements.</param>
    public NewView(
        in PureValueInitializer initializer,
        Value pointer,
        Value length)
        : base(initializer, GetType(initializer, pointer))
    {
        Seal(pointer, length);
    }

    /// <summary>
    /// Returns the underlying pointer.
    /// </summary>
    public Value Pointer => GetValue<Value>(0);

    /// <summary>
    /// Returns the view's element type.
    /// </summary>
    public TypeValue ViewElementType => GetTypeAs<ViewType>().ElementType;

    /// <summary>
    /// Returns the view's address space.
    /// </summary>
    public MemoryAddressSpace ViewAddressSpace => GetTypeAs<ViewType>().AddressSpace;

    /// <summary>
    /// Returns the length of the view.
    /// </summary>
    public Value Length => GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateNewView(
            Location,
            rewriter.Rewrite(Pointer),
            rewriter.Rewrite(Length));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "newview";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"({Pointer}, {Length})";
}

/// <summary>
/// Represents a generic operation of an <see cref="ILGPU.ArrayView{T}"/>.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class ViewOperationValue(in PureValueInitializer initializer, TypeValue type) :
    PureValue(initializer, type)
{
    /// <summary>
    /// Returns the underlying view.
    /// </summary>
    public Value Source => GetValue<Value>(0);

    /// <summary>
    /// Returns the associated element index.
    /// </summary>
    public Value Offset => GetValue<Value>(1);

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Source.ToString();
}

/// <summary>
/// Represents a value to compute a sub-view value.
/// </summary>
sealed partial class SubView : ViewOperationValue
{
    /// <summary>
    /// Constructs a new sub-view computation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The source view.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    public SubView(
        in PureValueInitializer initializer,
        Value source,
        Value offset,
        Value length)
        : base(initializer, source.Type)
    {
        Seal(source, offset, length);
    }

    /// <summary>
    /// Returns the length of the sub view.
    /// </summary>
    public Value Length => GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateSubView(
            Location,
            rewriter.Rewrite(Source),
            rewriter.Rewrite(Offset),
            rewriter.Rewrite(Length));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "subv";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Source.ToReferenceString()}[{Offset.ToReferenceString()} - " +
        $"{Length.ToReferenceString()}";
}

/// <summary>
/// Represents a generic property of an <see cref="ILGPU.ArrayView{T}"/>.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class ViewPropertyValue(in PureValueInitializer initializer, TypeValue type) :
    ViewOperationValue(initializer, type)
{
    /// <summary>
    /// Returns true if this is a 32bit element access.
    /// </summary>
    public bool Is32BitProperty => BasicValueType <= BasicValueType.Int32;

    /// <summary>
    /// Returns true if this is a 64bit element access.
    /// </summary>
    public bool Is64BitProperty => BasicValueType == BasicValueType.Int64;
}

/// <summary>
/// Represents the <see cref="ILGPU.ArrayView{T}.Length"/> property inside the IR.
/// </summary>
sealed partial class GetViewLength : ViewPropertyValue
{
    /// <summary>
    /// Constructs a new view length property.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="view">The underlying view.</param>
    /// <param name="lengthType">The underlying length type.</param>
    public GetViewLength(
        in PureValueInitializer initializer,
        Value view,
        BasicValueType lengthType)
        : base(initializer, initializer.ModuleBuilder.GetPrimitiveType(lengthType))
    {
        Seal(view);
    }

    /// <summary>
    /// Returns the associated length type to return.
    /// </summary>
    public BasicValueType LengthType => GetTypeAs<PrimitiveType>().BasicValueType;

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGetViewLength(
            Location,
            rewriter.Rewrite(Source),
            LengthType);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "len";
}
