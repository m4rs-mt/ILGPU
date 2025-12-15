// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents an abstract cast operation.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class CastValue(in PureValueInitializer initializer, TypeValue type) :
    PureValue(initializer, type)
{
    /// <summary>
    /// Returns the operand.
    /// </summary>
    public Value Source => GetValue<Value>(0);

    /// <summary>
    /// Returns the source type to convert the value from.
    /// </summary>
    public TypeValue SourceType => Source.Type;

    /// <summary>
    /// Returns the target type to convert the value to.
    /// </summary>
    /// <remarks>This is equivalent to asking for the type.</remarks>
    public TypeValue TargetType => Type;

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Source.ToString();
}

/// <summary>
/// An abstract base class for converting pointers to integers and vice versa.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class PointerIntCast(in PureValueInitializer initializer, TypeValue type) :
    CastValue(initializer, type);

/// <summary>
/// Casts from an integer to a raw pointer value.
/// </summary>
sealed partial class IntAsPointerCast : PointerIntCast
{
    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The view to cast.</param>
    public IntAsPointerCast(
        in PureValueInitializer initializer,
        Value source)
        : base(
            initializer,
            initializer.ModuleBuilder.CreatePointerType(
                initializer.ModuleBuilder.VoidType,
                MemoryAddressSpace.Generic))
    {
        initializer.Assert(
            source.Type.BasicValueType.IsInt());
        Seal(source);
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateIntAsPointerCast(Location, rewriter.Rewrite(Source));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "intasptr";
}

/// <summary>
/// Casts from a pointer value to an integer.
/// </summary>
sealed partial class PointerAsIntCast : PointerIntCast
{
    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The pointer to cast.</param>
    /// <param name="targetType">The target int type to cast to.</param>
    public PointerAsIntCast(
        in PureValueInitializer initializer,
        Value source,
        PrimitiveType targetType)
        : base(initializer, targetType)
    {
        initializer.Assert(
            source.Type is PointerType &&
            targetType.BasicValueType.IsInt());

        Seal(source);
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreatePointerAsIntCast(
            Location,
            rewriter.Rewrite(Source),
            BasicValueType);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => $"ptras<{TargetType}>";
}

/// <summary>
/// Represents an abstract cast operation that works on address spaces.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class BaseAddressSpaceCast(in PureValueInitializer initializer, TypeValue type) :
    CastValue(initializer, type)
{
    /// <summary>
    /// Returns the associated type.
    /// </summary>
    public new AddressSpaceType Type => base.Type.As<AddressSpaceType>();

    /// <summary>
    /// Returns the source type.
    /// </summary>
    public new AddressSpaceType SourceType => base.SourceType.As<AddressSpaceType>();
}

/// <summary>
/// Casts the type of a pointer to a different type.
/// </summary>
sealed partial class PointerCast : BaseAddressSpaceCast
{
    /// <summary>
    /// Determines the type of a new cast operation.
    /// </summary>
    private static PointerType GetType(
        in PureValueInitializer initializer,
        Value value,
        TypeValue targetElementType)
    {
        var pointerType = value.Type.As<PointerType>();
        var type = initializer.ModuleBuilder.CreatePointerType(
            targetElementType,
            pointerType.AddressSpace);
        return type;
    }

    /// <summary>
    /// Constructs a new convert value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="targetElementType">The target element type.</param>
    public PointerCast(
        in PureValueInitializer initializer,
        Value value,
        TypeValue targetElementType)
        : base(initializer, GetType(initializer, value, targetElementType))
    {
        Seal(value);
    }

    /// <summary>
    /// Returns the source element type.
    /// </summary>
    public TypeValue SourceElementType => Source.Type.As<PointerType>().ElementType;

    /// <summary>
    /// Returns the target element type.
    /// </summary>
    public TypeValue TargetElementType => Type.ElementType;

    /// <inheritdoc cref="PureValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreatePointerCast(
            Location,
            rewriter.Rewrite(Source),
            rewriter.RewriteAs<TypeValue>(TargetElementType));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "ptrcast";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Source} -> {TargetType}";
}

/// <summary>
/// Cast a pointer from one address space to another.
/// </summary>
sealed partial class AddressSpaceCast : BaseAddressSpaceCast
{
    /// <summary>
    /// Determines the type of a new cast operation.
    /// </summary>
    private static TypeValue GetType(
        in PureValueInitializer initializer,
        Value value,
        MemoryAddressSpace targetAddressSpace)
    {
        initializer.Assert(
            (value.Type is ViewType || value.Type is PointerType) &&
            value.Type.As<AddressSpaceType>().AddressSpace !=
            targetAddressSpace);

        TypeValue type;
        if (value.Type is ViewType viewType)
        {
            type = initializer.ModuleBuilder.CreateViewType(
                viewType.ElementType,
                targetAddressSpace);
        }
        else
        {
            var pointerType = value.Type.As<PointerType>();
            type = initializer.ModuleBuilder.CreatePointerType(
                pointerType.ElementType,
                targetAddressSpace);
        }
        return type;
    }

    /// <summary>
    /// Constructs a new convert value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="targetAddressSpace">The target address space.</param>
    public AddressSpaceCast(
        in PureValueInitializer initializer,
        Value value,
        MemoryAddressSpace targetAddressSpace)
        : base(initializer, GetType(initializer, value, targetAddressSpace))
    {
        Seal(value);
    }

    /// <summary>
    /// Returns the target address space.
    /// </summary>
    public MemoryAddressSpace TargetAddressSpace => Type.AddressSpace;

    /// <summary>
    /// Returns true if the current access works on a view.
    /// </summary>
    public bool IsViewCast => !IsPointerCast;

    /// <summary>
    /// Returns true if the current access works on a pointer.
    /// </summary>
    public bool IsPointerCast => Type is PointerType;

    /// <inheritdoc cref="PureValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAddressSpaceCast(
            Location,
            rewriter.Rewrite(Source),
            TargetAddressSpace);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "addrcast";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Source.ToReferenceString()} -> {TargetAddressSpace}";
}

/// <summary>
/// Casts a view from one element type to another.
/// </summary>
sealed partial class ViewCast : BaseAddressSpaceCast
{
    /// <summary>
    /// Determines the type of a new cast operation.
    /// </summary>
    private static ViewType GetType(
        in PureValueInitializer initializer,
        Value value,
        TypeValue targetElementType)
    {
        var viewType = value.Type.AsNotNullCast<ViewType>();
        var type = initializer.ModuleBuilder.CreateViewType(
            targetElementType,
            viewType.AddressSpace);
        return type;
    }

    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="sourceView">The view to cast.</param>
    /// <param name="targetElementType">The target element type.</param>
    public ViewCast(
        in PureValueInitializer initializer,
        Value sourceView,
        TypeValue targetElementType)
        : base(initializer, GetType(initializer, sourceView, targetElementType))
    {
        Seal(sourceView);
    }

    /// <summary>
    /// Returns the source element type.
    /// </summary>
    public TypeValue SourceElementType => SourceType.As<ViewType>().ElementType;

    /// <summary>
    /// Returns the target element type.
    /// </summary>
    public TypeValue TargetElementType => Type.ElementType;

    /// <inheritdoc cref="PureValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateViewCast(
            Location,
            rewriter.Rewrite(Source),
            rewriter.RewriteAs<TypeValue>(TargetElementType));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "vcast";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Source} -> {TargetElementType}";
}

/// <summary>
/// Casts an array to a linear array view.
/// </summary>
sealed partial class ArrayToViewCast : CastValue, IArrayValueOperation
{
    /// <summary>
    /// Determines the type of a new cast operation.
    /// </summary>
    private static ViewType GetType(
        in PureValueInitializer initializer,
        Value sourceArray)
    {
        var arrayType = sourceArray.GetTypeAs<ArrayType>();
        var type = initializer.ModuleBuilder.CreateViewType(
            arrayType.ElementType,
            MemoryAddressSpace.Local);
        return type;
    }

    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="sourceArray">The source array to cast to a view.</param>
    internal ArrayToViewCast(
        in PureValueInitializer initializer,
        Value sourceArray)
        : base(initializer, GetType(initializer, sourceArray))
    {
        Seal(sourceArray);
    }

    /// <summary>
    /// Returns the source array value.
    /// </summary>
    public Value ArrayValue => GetValue<Value>(0);

    /// <summary>
    /// Returns the array type of the source value.
    /// </summary>
    public new ArrayType SourceType => ArrayValue.GetTypeAs<ArrayType>();

    /// <summary>
    /// Returns the array element type.
    /// </summary>
    public TypeValue ElementType => SourceType.ElementType;

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateArrayToViewCast(
            Location,
            rewriter.Rewrite(Source));

    /// <inheritdoc/>
    protected override string ToPrefixString() => "arrayToView";

    /// <inheritdoc/>
    protected override string ToArgString() => $"{Source} -> View<{ElementType}>";
}

/// <summary>
/// Casts from one value type to another while reinterpreting
/// the value as another type.
/// </summary>
abstract class BitCast : CastValue
{
    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The view to cast.</param>
    /// <param name="targetType">The primitive target type.</param>
    public BitCast(
        in PureValueInitializer initializer,
        Value source,
        PrimitiveType targetType)
        : base(initializer, targetType)
    {
        initializer.Assert(source.Type is PrimitiveType);

        Seal(source);
    }

    /// <summary>
    /// Returns the target type to convert the value to.
    /// </summary>
    public PrimitiveType TargetPrimitiveType => Type.As<PrimitiveType>();

    /// <summary>
    /// Returns true if this type represents a 32 bit type.
    /// </summary>
    public bool Is32Bit => TargetPrimitiveType.Is32Bit;

    /// <summary>
    /// Returns true if this type represents a 64 bit type.
    /// </summary>
    public bool Is64Bit => TargetPrimitiveType.Is64Bit;

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Source} as {TargetType}";
}

/// <summary>
/// Casts from a float to an int while preserving bits.
/// </summary>
sealed partial class FloatAsIntCast : BitCast
{
    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The view to cast.</param>
    /// <param name="targetType">The primitive target type.</param>
    public FloatAsIntCast(
        in PureValueInitializer initializer,
        Value source,
        PrimitiveType targetType)
        : base(
              initializer,
              source,
              targetType)
    {
        var basicValueType = source.Type.BasicValueType;
        initializer.Assert(
            basicValueType == BasicValueType.Float16 ||
            basicValueType == BasicValueType.Float32 ||
            basicValueType == BasicValueType.Float64);
        initializer.Assert(
            targetType.BasicValueType == BasicValueType.Int16 ||
            targetType.BasicValueType == BasicValueType.Int32 ||
            targetType.BasicValueType == BasicValueType.Int64);
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateFloatAsIntCast(
            Location,
            rewriter.Rewrite(Source));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "flt_as_int";
}

/// <summary>
/// Casts from an int to a float while preserving bits.
/// </summary>
sealed partial class IntAsFloatCast : BitCast
{
    /// <summary>
    /// Constructs a new cast value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The view to cast.</param>
    /// <param name="targetType">The primitive target type.</param>
    public IntAsFloatCast(
        in PureValueInitializer initializer,
        Value source,
        PrimitiveType targetType)
        : base(
              initializer,
              source,
              targetType)
    {
        var basicValueType = source.Type.BasicValueType;
        initializer.Assert(
            basicValueType == BasicValueType.Int16 ||
            basicValueType == BasicValueType.Int32 ||
            basicValueType == BasicValueType.Int64);
        initializer.Assert(
            targetType.BasicValueType == BasicValueType.Float16 ||
            targetType.BasicValueType == BasicValueType.Float32 ||
            targetType.BasicValueType == BasicValueType.Float64);
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateIntAsFloatCast(
            Location,
            rewriter.Rewrite(Source));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "int_as_flt";
}
