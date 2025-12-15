// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers array values and types into view/pointer operations.
/// 1D arrays become ViewType values; nD arrays become structs with a view + dim lengths.
/// </summary>
sealed class LowerArrays(TransformationArgs args) : LowerTypes<ArrayType>(args)
{
    /// <summary>
    /// Maps all array operations to be transformed.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapPureValue<ArrayValue>(Lower);
        MapPureValue<GetArrayLength>(Lower);
        MapPureValue<LoadArrayElementAddress>(Lower);
        MapPureValue<ArrayToViewCast>(Lower);
    }

    /// <summary>
    /// Returns the number of fields for the lowered type.
    /// 1D → 1 field (just the view).
    /// nD → N+1 fields (view + N dimension lengths).
    /// </summary>
    protected override int GetNumFields(ArrayType type) =>
        type.NumDimensions == 1 ? 1 : type.NumDimensions + 1;

    /// <summary>
    /// Returns true if the given type depends on an array type.
    /// </summary>
    protected override bool IsTypeDependent(TypeValue type) =>
        type.HasFlags(TypeFlags.ArrayDependent);

    /// <summary>
    /// Transforms the given array type into a view or struct type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override TypeValue TransformType(
        ModuleTransform transform,
        ArrayType type)
    {
        var elementType = transform.Rewrite(type.ElementType);
        var viewType = transform.CreateViewType(
            elementType,
            MemoryAddressSpace.Local);

        if (type.NumDimensions == 1)
            return viewType;

        // nD: struct { ViewType, Int32, Int32, ..., Int32 }
        var builder = transform.CreateStructureType(type.NumDimensions + 1);
        builder.Add(viewType);
        var int32Type = transform.GetPrimitiveType(BasicValueType.Int32);
        for (int i = 0; i < type.NumDimensions; ++i)
            builder.Add(int32Type);
        return builder.Seal();
    }

    /// <summary>
    /// Lowers an array value (allocation) to a view or struct.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(PureValueTransform transform, ArrayValue value)
    {
        var location = value.Location;
        var backBuffer = transform.Rewrite(value.BackBuffer);
        int numDims = value.NumDimensions;

        // Empty array: BackBuffer is UndefinedValue
        if (value.BackBuffer is UndefinedValue)
        {
            return LowerEmptyArray(transform, value);
        }

        // Compute total length for the view
        Value totalLength = transform.CreatePrimitiveValue(location, 1);
        for (int i = 0; i < numDims; ++i)
        {
            totalLength = transform.CreateArithmetic(
                location,
                totalLength,
                transform.Rewrite(value.Dimensions[i])!,
                BinaryArithmeticKind.Mul)!;
        }

        var view = transform.CreateNewView(location, backBuffer, totalLength);

        if (numDims == 1)
            return view;

        // nD: build struct { view, dim0, dim1, ..., dimN-1 }
        var structBuilder = transform.CreateDynamicStructure(
            location,
            numDims + 1);
        structBuilder.Add(view);
        for (int i = 0; i < numDims; ++i)
            structBuilder.Add(transform.Rewrite(value.Dimensions[i]));
        return structBuilder.Seal();
    }

    /// <summary>
    /// Lowers an empty array to a null view or struct.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? LowerEmptyArray(
        PureValueTransform transform,
        ArrayValue value)
    {
        var location = value.Location;
        int numDims = value.NumDimensions;
        var arrayType = value.Type;

        // Create null pointer for empty view
        var elementType = transform.Rewrite(arrayType.ElementType);
        var ptrType = transform.ModuleTransform.CreatePointerType(
            elementType,
            MemoryAddressSpace.Local);
        var nullPtr = transform.CreateNull(location, ptrType);
        var zeroLength = transform.CreatePrimitiveValue(location, 0);
        var view = transform.CreateNewView(location, nullPtr, zeroLength);

        if (numDims == 1)
            return view;

        // nD: struct { null view, 0, 0, ..., 0 }
        var structBuilder = transform.CreateDynamicStructure(
            location,
            numDims + 1);
        structBuilder.Add(view);
        for (int i = 0; i < numDims; ++i)
            structBuilder.Add(transform.CreatePrimitiveValue(location, 0));
        return structBuilder.Seal();
    }

    /// <summary>
    /// Lowers a GetArrayLength operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(
        PureValueTransform transform,
        GetArrayLength value)
    {
        var location = value.Location;
        var loweredArray = transform.Rewrite(value.ArrayValue)!;
        var arrayType = value.ArrayValue.GetTypeAs<ArrayType>();
        int numDims = arrayType.NumDimensions;

        if (value.IsFullLength)
        {
            // Full linear length
            if (numDims == 1)
                return transform.CreateGetViewLength(location, loweredArray);

            // nD: get view length from field 0
            var view = transform.CreateGetField(
                location,
                loweredArray,
                new FieldSpan(0));
            return transform.CreateGetViewLength(location, view);
        }

        // Specific dimension length
        if (numDims == 1)
        {
            // 1D: dimension length is just the view length
            return transform.CreateGetViewLength(location, loweredArray);
        }

        // nD: check if dimension is a constant
        var dimension = value.Dimension;
        if (dimension is PrimitiveValue constDim)
        {
            int dimIndex = constDim.Int32Value;
            // Dimension lengths are stored at fields 1..N
            return transform.CreateGetField(
                location,
                loweredArray,
                new FieldSpan(dimIndex + 1));
        }

        // Dynamic dimension index: build a conditional ladder
        // Start with dim 0 length, then select based on the index
        Value result = transform.CreateGetField(
            location,
            loweredArray,
            new FieldSpan(1))!;
        for (int i = 1; i < numDims; ++i)
        {
            var dimValue = transform.CreateGetField(
                location,
                loweredArray,
                new FieldSpan(i + 1));
            var cmp = transform.CreateCompare(
                location,
                transform.Rewrite(dimension)!,
                transform.CreatePrimitiveValue(location, i),
                CompareKind.Equal,
                CompareFlags.None)!;
            result = transform.CreatePredicate(
                location,
                cmp,
                dimValue,
                result)!;
        }
        return result;
    }

    /// <summary>
    /// Lowers a LoadArrayElementAddress to a pointer via Horner linearization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(
        PureValueTransform transform,
        LoadArrayElementAddress value)
    {
        var location = value.Location;
        var loweredArray = transform.Rewrite(value.ArrayValue)!;
        var arrayType = value.ArrayValue.GetTypeAs<ArrayType>();
        int numDims = arrayType.NumDimensions;

        if (numDims == 1)
        {
            // 1D: direct element address into the view
            return transform.CreateLoadElementAddress(
                location,
                loweredArray,
                transform.Rewrite(value.Dimensions[0]));
        }

        // nD: extract the view from field 0
        var view = transform.CreateGetField(
            location,
            loweredArray,
            new FieldSpan(0));

        // Horner linearization:
        // linearIdx = idx0
        // for i = 1 to N-1:
        //   linearIdx = linearIdx * dimLength[i] + idx[i]
        Value linearIdx = transform.Rewrite(value.Dimensions[0])!;
        for (int i = 1; i < numDims; ++i)
        {
            var dimLength = transform.CreateGetField(
                location,
                loweredArray,
                new FieldSpan(i + 1));
            linearIdx = transform.CreateArithmetic(
                location,
                linearIdx,
                dimLength,
                BinaryArithmeticKind.Mul)!;
            linearIdx = transform.CreateArithmetic(
                location,
                linearIdx,
                transform.Rewrite(value.Dimensions[i])!,
                BinaryArithmeticKind.Add)!;
        }

        return transform.CreateLoadElementAddress(
            location,
            view,
            linearIdx);
    }

    /// <summary>
    /// Lowers an ArrayToViewCast.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? Lower(
        PureValueTransform transform,
        ArrayToViewCast value)
    {
        var location = value.Location;
        var loweredSource = transform.Rewrite(value.Source)!;
        var arrayType = value.SourceType;

        if (arrayType.NumDimensions == 1)
        {
            // 1D: the lowered array IS the view
            return loweredSource;
        }

        // nD: extract the view from field 0
        return transform.CreateGetField(
            location,
            loweredSource,
            new FieldSpan(0));
    }
}
