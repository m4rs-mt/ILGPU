// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerAlignments.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Stores alignment information of an alignment analysis run.
/// </summary>
/// <remarks>
/// <param name="result">The result mapping.</param>
/// Constructs a new alignment analysis.
/// </remarks>
readonly struct AlignmentInfo(FixPointAnalysisResult<int>? result)
{
    /// <summary>
    /// Empty allocation information.
    /// </summary>
    public static readonly AlignmentInfo Empty = new(null);

    /// <summary>
    /// Returns pointer alignment information for the given value.
    /// </summary>
    /// <param name="value">The value to get alignment information for.</param>
    /// <returns>Pointer alignment in bytes (can be 1 byte).</returns>
    public int this[Value value] => result?.GetResult(value).Data ?? 1;

    /// <summary>
    /// Returns true if this alignment information object is empty.
    /// </summary>
    public bool IsEmpty => !result.HasValue;

    /// <summary>
    /// Returns safe alignment information.
    /// </summary>
    /// <param name="value">
    /// The value for which to compute the alignment for.
    /// </param>
    /// <param name="safeMinAlignment">
    /// The safe minimum alignment in bytes.
    /// </param>
    /// <returns>The computed alignment.</returns>
    public int GetAlignment(Value value, int safeMinAlignment) =>
        Math.Max(this[value], safeMinAlignment);

    /// <summary>
    /// Returns safe alignment information.
    /// </summary>
    /// <param name="value">
    /// The value for which to compute the alignment for.
    /// </param>
    /// <param name="safeMinTypeAlignment">
    /// The safe minimum type alignment.
    /// </param>
    /// <returns>The computed alignment.</returns>
    public int GetAlignment(
        Value value,
        TypeValue safeMinTypeAlignment) =>
        GetAlignment(value, safeMinTypeAlignment.Alignment);
}

/// <summary>
/// An analysis to determine safe alignment information for all pointer values.
/// </summary>
/// <remarks>
/// Constructs a new analysis implementation.
/// </remarks>
/// <param name="globalAlignment">The global alignment information.</param>
sealed class PointerAlignments(int globalAlignment) :
    FixPointAnalysis<int, Forwards>(defaultValue: 1)
{
    #region Main Analysis

    /// <summary>
    /// Creates a new alignment analysis.
    /// </summary>
    /// <param name="globalAlignment">
    /// The initial alignment information of all pointers and views of the root
    /// method.
    /// </param>
    public static PointerAlignments Create(int globalAlignment) =>
        new(globalAlignment);

    /// <summary>
    /// Applies a new alignment analysis to the given root method.
    /// </summary>
    /// <param name="rootMethod">The root (entry) method.</param>
    /// <param name="globalAlignment">
    /// The initial alignment information of all pointers and views of the root
    /// method.
    /// </param>
    public static AlignmentInfo Apply(Method rootMethod, int globalAlignment)
    {
        var analysis = Create(globalAlignment);

        // Mark all global values as global
        var globalMap = rootMethod.Module.CreateGlobalMap<
            AnalysisValue<int>>();
        foreach (var global in rootMethod.Module.Globals)
        {
            globalMap.Add(global, AnalysisValue.Create(
                globalAlignment, global.Type));
        }

        var result = analysis.AnalyzeModule(rootMethod);
        return new AlignmentInfo(result);
    }

    /// <summary>
    /// Determines the allocation alignment information based on the given type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The compatible allocation alignment in bytes.</returns>
    public static int GetAllocaTypeAlignment(TypeValue type) =>
        // Assume that we can align the type to an appropriate power of
        // 2 if the type size is compatible
        XMath.IsPowerOf2(type.Size)
        ? Math.Max(type.Alignment, type.Size)
        : type.Alignment;

    /// <summary>
    /// Determines the initial alloca alignment based on the type of the allocation.
    /// </summary>
    /// <param name="alloca">
    /// The alloca to determine to alignment information for.
    /// </param>
    /// <returns>The initial alignment in bytes.</returns>
    public static int GetInitialAllocaAlignment(Alloca alloca) =>
        GetAllocaTypeAlignment(alloca.AllocType);

    /// <summary>
    /// Tries to determine power of 2 information for the given unary operation.
    /// </summary>
    /// <param name="unary">The unary operation to analyze.</param>
    /// <returns>The power of 2 value (if any).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int? TryGetPowerOf2(UnaryArithmeticValue unary) =>
        unary.Kind switch
        {
            UnaryArithmeticKind.Abs => TryGetPowerOf2(unary.Value),
            _ => null
        };

    /// <summary>
    /// Tries to determine power of 2 information for the given binary operation.
    /// </summary>
    /// <param name="binary">The binary operation to analyze.</param>
    /// <returns>The power of 2 value (if any).</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int? TryGetPowerOf2(BinaryArithmeticValue binary) =>
        binary.Kind switch
        {
            // Check whether either the left or the right operand are a power of 2
            BinaryArithmeticKind.Mul =>
                TryGetPowerOf2(binary.Left) ??
                TryGetPowerOf2(binary.Right),
            // Check whether we can determine a power of 2 of the left operand or
            // whether the RHS of the SHL operation is a primitive value
            BinaryArithmeticKind.Shl =>
                TryGetPowerOf2(binary.Left) ??
                (binary.Right is PrimitiveValue shlPrimitive &&
                shlPrimitive.Int32Value > 0
                ? (int?)(shlPrimitive.Int32Value * 2)
                : null),
            _ => null,
        };

    /// <summary>
    /// Tries to determine a power of 2 for the given value (if any could be
    /// determined).
    /// </summary>
    /// <param name="value">The value to analyze.</param>
    /// <returns>The power of 2 value (if any).</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int? TryGetPowerOf2(Value value)
    {
        // Ensure that the value is operating on an integer type
        value.Assert(value.BasicValueType.IsInt());
        return value switch
        {
            // Check whether the value is a power of two in the case of a raw value
            PrimitiveValue primitive =>
                primitive.Int32Value > 1 &&
                XMath.IsPowerOf2(primitive.Int32Value)
                ? primitive.Int32Value
                : null,
            // Propagate information in the presence of arithmetic operations
            UnaryArithmeticValue unary => TryGetPowerOf2(unary),
            BinaryArithmeticValue binary => TryGetPowerOf2(binary),
            _ => null
        };
    }

    /// <summary>
    /// Returns initial and unconstrained alignment information.
    /// </summary>
    /// <param name="node">The IR node.</param>
    /// <returns>The initial alignment information.</returns>
    private static int GetInitialAlignment(Value node) =>
        node switch
        {
            Alloca alloca => GetInitialAllocaAlignment(alloca),
            // Use a compile-time known alignment constant for the alignment
            // information instead of type-based alignment reasoning
            BaseAlignOperationValue alignment => alignment.GetAlignmentConstant(),
            NewView _ or SubView _ or BaseAddressSpaceCast _ or
            LoadElementAddress _ or LoadFieldAddress _ or GetField _ or SetField _ or
            StructureValue _ or Load _ or Store _ or PhiValue _ or PrimitiveValue _ or
            NullValue _ or UndefinedValue _ => int.MaxValue,
            _ => 1,
        };

    /// <summary>
    /// Provides specialized alignment values for pointer and view types.
    /// </summary>
    protected override AnalysisValue<int>? TryProvideForType(TypeValue type) =>
        type is AddressSpaceType
        ? AnalysisValue.Create(globalAlignment, type)
        : null;

    /// <summary>
    /// Returns the minimum of the first and the second value.
    /// </summary>
    protected override int Merge(int first, int second) =>
        Math.Min(first, second);

    /// <summary>
    /// Analyzes a value to compute its alignment information.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override AnalysisValue<int> Analyze(
        Value value,
        GlobalValueMap<AnalysisValue<int>> data)
    {
        // Get current value or create initial one
        var current = data.TryGetValue(value, out var existing)
            ? existing
            : CreateInitialValue(value.Type);

        // Handle special cases that need custom analysis
        var specialized = value switch
        {
            Alloca alloca => AnalysisValue.Create(
                GetInitialAllocaAlignment(alloca),
                alloca.Type),

            BaseAlignOperationValue align => MergeAlignmentValue(align, data),
            LoadFieldAddress lfa => MergeLoadFieldAddress(lfa, data),
            LoadElementAddress lea => MergeLoadElementAddress(lea, data),

            // For method calls, use the method return value
            MethodCall call when !call.Target.IsVoid =>
                data.TryGetValue(call.Target, out var retValue)
                    ? retValue
                    : current,
            PhiValue phiValue => MergePhiValue(phiValue, data),

            // Default: use initial alignment
            _ => AnalysisValue.Create(GetInitialAlignment(value), value.Type)
        };

        return specialized;
    }

    #endregion

    #region Merging

    /// <summary>
    /// Computes merged alignment information of the given
    /// <see cref="BaseAlignOperationValue"/> node.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static AnalysisValue<int> MergeAlignmentValue(
        BaseAlignOperationValue align,
        GlobalValueMap<AnalysisValue<int>> valueData)
    {
        // Determine the base alignment of the input address
        int baseAlignment = valueData.TryGetValue(align.Source, out var sourceValue)
            ? sourceValue.Data
            : 1;

        // Simply assume the specified alignment information
        int newAlignment = align.GetAlignmentConstant();

        // Take the maximum of both values to compensate cases in which the alignment
        // constant could not be properly resolved at compile time
        return AnalysisValue.Create(
            Math.Max(baseAlignment, newAlignment),
            align.Type);
    }

    /// <summary>
    /// Computes merged alignment information of the given
    /// <see cref="LoadFieldAddress"/> node.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static AnalysisValue<int> MergeLoadFieldAddress(
        LoadFieldAddress lfa,
        GlobalValueMap<AnalysisValue<int>> valueData)
    {
        // Determine the base alignment of the input address
        int baseAlignment = valueData.TryGetValue(lfa.Source, out var sourceValue)
            ? sourceValue.Data
            : 1;

        // Determine the alignment of the referenced field
        int fieldAlignment = lfa.StructureType[lfa.FieldSpan.Access].Alignment;

        // Use the minimum alignment information of both addresses. Note that this
        // is required to check for non-properly aligned fields.
        return AnalysisValue.Create(
            Math.Min(baseAlignment, fieldAlignment),
            lfa.Type);
    }

    /// <summary>
    /// Computes merged alignment information of the given
    /// <see cref="LoadElementAddress"/> node.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static AnalysisValue<int> MergeLoadElementAddress(
        LoadElementAddress lea,
        GlobalValueMap<AnalysisValue<int>> valueData)
    {
        // Determine the base alignment of the input address
        int baseAlignment = valueData.TryGetValue(lea.Source, out var sourceValue)
            ? sourceValue.Data
            : 1;

        // Determine the alignment of the referenced element type (used for indexing)
        var elementType = lea.Type.AsNotNullCast<AddressSpaceType>().ElementType;
        int typeAlignment = GetAllocaTypeAlignment(elementType);

        // Check whether we have found a power of 2 != 0
        int? powerOf2 = TryGetPowerOf2(lea.Offset);
        if (powerOf2.HasValue && powerOf2.Value > 0)
        {
            // We can use a multiple of the type alignment
            typeAlignment *= powerOf2.Value;
        }

        // Use the minimum alignment information of both addresses. Note that this
        // is required to check for non-properly aligned accesses.
        return AnalysisValue.Create(
            Math.Min(baseAlignment, typeAlignment),
            lea.Type);
    }

    #endregion
}
