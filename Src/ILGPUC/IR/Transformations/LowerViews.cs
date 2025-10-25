// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers views (values and types) into platform specific instances.
/// </summary>
sealed class LowerViews(TransformationArgs args) : LowerTypes<ViewType>(args)
{
    /// <summary>
    /// Maps all view operations to be transformed.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapPureValue<NewView>(Lower);
        MapPureValue<GetViewLength>(Lower);
        MapPureValue<SubView>(Lower);
        MapPureValue<AddressSpaceCast>(Lower);
        MapPureValue<ViewCast>(Lower);
        MapPureValue<LoadElementAddress>(Lower);
        MapPureValue<AlignTo>(Lower);
        MapPureValue<AsAligned>(Lower);
    }

    /// <summary>
    /// Returns two fields required to implement a view.
    /// </summary>
    protected override int GetNumFields(ViewType type) => 2;

    /// <summary>
    /// Returns true if the given type is depending on a view type.
    /// </summary>
    protected override bool IsTypeDependent(TypeValue type) =>
        type.HasFlags(TypeFlags.ViewDependent);

    /// <summary>
    /// Transforms the given view type into a structure of two values.
    /// </summary>
    protected override TypeValue TransformType(ModuleTransform transform, ViewType type)
    {
        var elementType = transform.Rewrite(type.ElementType);

        var builder = transform.CreateStructureType(2);
        builder.Add(transform.CreatePointerType(elementType, type.AddressSpace));
        builder.Add(transform.GetPrimitiveType(BasicValueType.Int64));
        return builder.Seal();
    }

    /// <summary>
    /// Lowers a new view.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, NewView value)
    {
        var longLength = transform.CreateConvertToInt64(
            value.Location,
            value.Length);
        var viewInstance = transform.CreateDynamicStructure(
            value.Location,
            value.Pointer,
            longLength);
        return viewInstance;
    }

    /// <summary>
    /// Lowers get-view-length property.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, GetViewLength value)
    {
        var length = transform.CreateGetField(
            value.Location,
            transform.Rewrite(value.Source),
            new FieldSpan(1));

        // Convert to a 32bit length value
        if (value.Is32BitProperty)
        {
            length = transform.CreateConvertToInt32(
                value.Location,
                length);
        }
        return length;
    }

    /// <summary>
    /// Lowers a sub-view value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, SubView value)
    {
        var location = value.Location;
        var pointer = transform.CreateGetField(
            location,
            transform.Rewrite(value.Source),
            new FieldSpan(0));
        var newPointer = transform.CreateLoadElementAddress(
            location,
            pointer,
            value.Offset);

        var length = value.Length;
        if (length.BasicValueType != BasicValueType.Int64)
        {
            length = transform.CreateConvertToInt64(
                value.Location,
                length);
        }
        var subView = transform.CreateDynamicStructure(
            location,
            newPointer,
            length);
        return subView;
    }

    /// <summary>
    /// Lowers an address-space cast.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, AddressSpaceCast value)
    {
        // Only lower view address-space casts; pointer casts are handled
        // by the standard Rewrite path which reconstructs with lowered types.
        if (value.Type is not ViewType)
            return value;

        var location = value.Location;
        var source = transform.Rewrite(value.Source);
        var pointer = transform.CreateGetField(location, source, new FieldSpan(0));
        var length = transform.CreateGetField(location, source, new FieldSpan(1));

        var newPointer = transform.CreateAddressSpaceCast(
            location,
            pointer,
            value.TargetAddressSpace);
        var newInstance = transform.CreateDynamicStructure(
            location,
            newPointer,
            length);
        return newInstance;
    }

    /// <summary>
    /// Lowers a view cast.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, ViewCast value)
    {
        var location = value.Location;
        var source = transform.Rewrite(value.Source);
        var pointer = transform.CreateGetField(location, source, new FieldSpan(0));
        var length = transform.CreateGetField(location, source, new FieldSpan(1));

        // New pointer
        var newPointer = transform.CreatePointerCast(
            location,
            pointer,
            transform.Rewrite(value.TargetElementType));

        // Compute new length:
        // newLength = length * sourceElementSize / targetElementSize;
        var sourceElementType = value.Type.ElementType;
        var sourceElementSize = transform.CreateLongSizeOf(
            location,
            sourceElementType);
        var targetElementSize = transform.CreateLongSizeOf(
            location,
            value.TargetElementType);
        var newLength = transform.CreateArithmetic(
            location,
            transform.CreateArithmetic(
                location,
                length,
                sourceElementSize,
                BinaryArithmeticKind.Mul),
            targetElementSize, BinaryArithmeticKind.Div);

        var newInstance = transform.CreateDynamicStructure(
            location,
            newPointer,
            newLength);
        return newInstance;
    }

    /// <summary>
    /// Lowers a lea operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, LoadElementAddress value)
    {
        var location = value.Location;
        var source = transform.Rewrite(value.Source);

        // If the source is already a pointer (not a lowered view struct),
        // just create a new LEA with the rewritten operands directly.
        Value? pointer = source is not null && source.Type is PointerType
            ? source
            : transform.CreateGetField(location, source, new FieldSpan(0));

        var newLea = transform.CreateLoadElementAddress(
            location,
            pointer,
            transform.Rewrite(value.Offset));
        return newLea;
    }

    /// <summary>
    /// Lowers an align-view-to operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, AlignTo value)
    {
        // Only lower view align operations
        if (!value.IsViewOperation)
            return value;

        var location = value.Location;

        // Extract basic view information from the converted structure
        var source = transform.Rewrite(value.Source);
        var pointer = transform.CreateGetField(location, source, new FieldSpan(0));
        var length = transform.CreateGetField(location, source, new FieldSpan(1));

        // Build the final result structure instance
        var resultBuilder = transform.CreateDynamicStructure(location);

        // Convert the current input pointer to a 64-bit integer value
        var pointerAsInt = transform.CreatePointerAsIntCast(
            location,
            pointer,
            BasicValueType.Int64);

        // Compute the aligned pointer and convert it to an integer value
        var aligned = transform.CreateAlignTo(
            location,
            pointer,
            value.AlignmentInBytes);
        var alignedAsInt = transform.CreatePointerAsIntCast(
            location,
            aligned,
            BasicValueType.Int64);

        // Compute the number of elements to skip:
        // Min((aligned - ptr) / SizeOf(ElementType), length)
        var viewType = value.GetTypeAs<AddressSpaceType>();
        var elementsToSkip = transform.CreateArithmetic(
            location,
            transform.CreateArithmetic(
                location,
                transform.CreateArithmetic(
                    location,
                    alignedAsInt,
                    pointerAsInt,
                    BinaryArithmeticKind.Sub),
                transform.CreateSizeOf(location, viewType.ElementType),
                BinaryArithmeticKind.Div),
            length,
            BinaryArithmeticKind.Min);

        // Create the prefix view that starts at the original pointer offset and
        // includes elementsToSkip many elements.
        {
            resultBuilder.Add(pointer);
            resultBuilder.Add(elementsToSkip);
        }

        // Create the main view that starts at the aligned pointer offset and has a
        // length of remainingLength
        {
            resultBuilder.Add(aligned);
            var remainingLength = transform.CreateArithmetic(
                location,
                length,
                elementsToSkip,
                BinaryArithmeticKind.Sub);
            resultBuilder.Add(remainingLength);
        }

        return resultBuilder.Seal();
    }

    /// <summary>
    /// Lowers an as-aligned-view operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, AsAligned value)
    {
        // Only lower view align operations
        if (!value.IsViewOperation)
            return value;

        var location = value.Location;
        var source = transform.Rewrite(value.Source);

        // Extract basic view information from the converted structure
        var pointer = transform.CreateGetField(location, source, new FieldSpan(0));
        var length = transform.CreateGetField(location, source, new FieldSpan(1));

        // Ensure that the underlying pointer is aligned
        var aligned = transform.CreateAsAligned(
            location,
            pointer,
            value.AlignmentInBytes);

        // Create a new wrapped instance
        var newInstance = transform.CreateDynamicStructure(
            location,
            aligned,
            length);
        return newInstance;
    }
}
