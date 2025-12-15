// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorizationAnalysis.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// Analyzes IR to determine vectorization requirements.
/// </summary>
/// <remarks>
/// Performs analysis to identify:
/// - Which values need vectorization (most values)
/// - Control flow divergence points
/// - Values that remain scalar (constants, thread indices)
/// </remarks>
sealed class VectorizationAnalysis
{
    private readonly Method _method;
    private readonly bool _vectorizeFirstParam;
    private readonly HashSet<Value> _scalarValues = [];
    private readonly HashSet<Value> _phiVisiting = [];

    /// <summary>
    /// Creates a new vectorization analysis for a method.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <param name="vectorizeFirstParam">
    /// When true, the first parameter (thread index) is treated as
    /// per-lane vectorized rather than scalar. Set this for entry-point
    /// methods where the first parameter maps to <c>laneIdx</c>.
    /// </param>
    public VectorizationAnalysis(
        Method method,
        bool vectorizeFirstParam = false)
    {
        _method = method;
        _vectorizeFirstParam = vectorizeFirstParam;
        PerformAnalysis();
    }

    /// <summary>
    /// Returns true if the value should remain scalar (not vectorized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool IsScalar(Value value)
    {
        if (_scalarValues.Contains(value) || IsIntrinsicallyScalar(value))
            return true;

        // Propagate scalarity through operations whose outputs are scalar
        // when all their inputs are scalar
        return value switch
        {
            ConvertValue conv => IsScalar(conv.Value),
            BinaryArithmeticValue bin => IsScalar(bin.Left) && IsScalar(bin.Right),
            UnaryArithmeticValue un => IsScalar(un.Value),
            CompareValue cmp => IsScalar(cmp.Left) && IsScalar(cmp.Right),
            GetField gf => IsScalar(gf.Source),
            StructureValue sv => AreAllScalar(sv),
            Predicate pred => IsScalar(pred.Condition)
                && IsScalar(pred.TrueValue)
                && IsScalar(pred.FalseValue),
            // A phi is scalar when all its incoming values are scalar
            // (guard against infinite recursion for loop-carried phis)
            PhiValue phi => AreAllPhiArgsScalar(phi),
            // A view load is scalar when its LEA offset is scalar
            Load load when load.Source is LoadElementAddress lea
                && lea.IsViewAccess => IsScalar(lea.Offset),
            // View property reads (Length) are always scalar — view metadata
            // is uniform across all SIMD lanes. This makes SubView length
            // computations (viewLength - offset) propagate as scalar.
            ViewPropertyValue => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if all phi arguments are scalar. Uses a guard set to prevent
    /// infinite recursion for loop-carried phis that reference themselves.
    /// </summary>
    private bool AreAllPhiArgsScalar(PhiValue phi)
    {
        // Guard against cycles: if we're already checking this phi,
        // conservatively say it's not scalar for primitive types.
        // For struct-typed phis (like loop-invariant view parameters),
        // optimistically assume scalar — a struct phi that only
        // self-references through field extract/reconstruct IS scalar
        // if its initial value is scalar.
        if (!_phiVisiting.Add(phi))
            return phi.Type is StructureType;

        try
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (!IsScalar(phi.Arguments[i]))
                    return false;
            }
            return true;
        }
        finally
        {
            _phiVisiting.Remove(phi);
        }
    }

    /// <summary>
    /// Analyzes the given structure value and determined whether all values are scalar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool AreAllScalar(StructureValue sv)
    {
        foreach (var v in sv.Values)
        {
            if (!IsScalar(v))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns true for values that are always scalar regardless of analysis.
    /// This catches PureValues that may not be visited by block-based analysis.
    /// In CPU vectorized mode, per-lane values (GroupIndex,
    /// SubGroupLaneIndex) are NOT scalar — they vary per lane.
    /// SubGroupIndex (warp index) IS scalar — the CPU model uses a single warp.
    /// Parameters are handled in <see cref="PerformAnalysis"/> — the first
    /// entry-point parameter (thread index) may be vectorized.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntrinsicallyScalar(Value value) => value is
        GridIndexValue or
        GridDimensionValue or
        GroupDimensionValue or
        SubGroupDimensionValue or
        SubGroupIndexValue or
        PrimitiveValue or
        NullValue;

    /// <summary>
    /// Performs the vectorization analysis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PerformAnalysis()
    {
        // Mark parameters as scalar. When vectorizeFirstParam is set,
        // the first parameter (the thread index) is vectorized — it maps
        // to the per-lane laneIdx array rather than the scalar baseIndex.
        //
        // For non-entry methods with callers in this module, propagate
        // scalarity from the call site: if the caller passes a per-lane
        // (vectorized) argument, the parameter must also be vectorized.
        // This handles MetalKernelLowering-added parameters like groupIdx
        // (Group.Index → per-lane) vs gridIdx (Grid.Index → scalar).
        var vectorizedParams = _vectorizeFirstParam
            ? null
            : FindVectorizedParamsFromCallSites();

        foreach (var param in _method.Parameters)
        {
            if (_vectorizeFirstParam && param.Index == 0)
                continue;
            if (vectorizedParams is not null && vectorizedParams.Contains(param.Index))
                continue;
            _scalarValues.Add(param);
        }

        foreach (var block in _method)
        {
            foreach (var value in block.Values)
                AnalyzeValue(value);
        }
    }

    /// <summary>
    /// Finds parameter indices that receive per-lane (vectorized) arguments
    /// at any call site within the module. A parameter is vectorized if any
    /// caller passes a non-scalar value (e.g., GroupIndexValue) for it.
    /// </summary>
    private HashSet<int>? FindVectorizedParamsFromCallSites()
    {
        HashSet<int>? result = null;
        var module = _method.Module;
        foreach (var caller in module.Methods)
        {
            caller.ForEachValue<MethodCall>(call =>
            {
                if (call.Target != _method)
                    return;
                for (int i = 0; i < call.Arguments.Length; i++)
                {
                    var arg = call.Arguments[i];
                    // Per-lane device constants are NOT intrinsically scalar
                    if (!IsIntrinsicallyScalar(arg))
                    {
                        result ??= [];
                        result.Add(i);
                    }
                }
            });
        }
        return result;
    }

    /// <summary>
    /// Analyzes a single value to determine its vectorization status.
    /// </summary>
    private void AnalyzeValue(Value value)
    {
        switch (value)
        {
            case GridIndexValue:
            case GridDimensionValue:
            case GroupDimensionValue:
            case SubGroupDimensionValue:
            case SubGroupIndexValue:
                _scalarValues.Add(value);
                break;

            case PrimitiveValue primitive:
                _scalarValues.Add(primitive);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Gets the C# type name for a value (scalar or vectorized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public string GetTypeName(Value value, CPULanguageConfiguration config)
    {
        var baseType = value.Type;

        // Scalar values use their natural type
        if (IsScalar(value))
            return GetScalarTypeName(baseType, config);

        // Vectorized pointer values → nint[] (per-lane addresses)
        if (baseType is PointerType)
            return "nint[]";

        // Vectorized view values → CPURuntimeView<elementType>
        if (baseType is ViewType view)
            return $"CPURuntimeView<{GetScalarTypeName(view.ElementType, config)}>";

        // Vectorized primitive values use Tensor<T>
        if (baseType is PrimitiveType primitive)
            return config.GetVectorTypeName(primitive.BasicValueType);

        // For non-primitive types, use scalar type for now
        return GetScalarTypeName(baseType, config);
    }

    /// <summary>
    /// Gets the scalar type name for a type value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static string GetScalarTypeName(
        TypeValue type,
        CPULanguageConfiguration config)
    {
        if (type is PrimitiveType primitive)
            return config.GetPrimitiveTypeName(primitive.BasicValueType);
        else if (type is PointerType pointer)
            return GetScalarTypeName(pointer.ElementType, config) + "*";
        else if (type is VoidType)
            return "void";
        else if (type is ViewType view)
            return $"CPURuntimeView<{GetScalarTypeName(view.ElementType, config)}>";
        else if (type is StructureType structure)
            return $"struct_{structure.Id}";
        else if (type is StringType)
            return "string";
        else if (type is ArrayType arrayType)
            return GetScalarTypeName(arrayType.ElementType, config) + "[]";
        else if (type is HandleType)
            return "nint";
        else if (type is PaddingType paddingType)
            return config.GetPrimitiveTypeName(paddingType.BasicValueType);
        else
            throw new System.NotSupportedException(
                $"Unsupported type in CPU backend: {type.GetType().Name}");
    }
}
