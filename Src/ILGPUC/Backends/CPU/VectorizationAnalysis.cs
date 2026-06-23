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
using ILGPUC.IR.Analyses;
using ILGPUC.IR.MethodValues;
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
    private Dominators<Forwards>? _dominators;
    private Dominators<Backwards>? _postDominators;
    private Loops<ReversePostOrder<BasicBlock>, Forwards>? _loops;

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
            // A view load is scalar when its LEA offset is scalar, unless
            // it reads a local array that every lane owns privately
            Load load when load.Source is LoadElementAddress lea
                && lea.IsViewAccess => IsScalar(lea.Offset)
                && CPUExpressionEmitter.GetLaneArrayLength(lea.Source) is null,
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

            // Uniform incoming values still differ per lane when a divergent
            // branch decides which of them each lane receives.
            return !IsControlDivergent(phi);
        }
        finally
        {
            _phiVisiting.Remove(phi);
        }
    }

    /// <summary>
    /// Returns true if lanes can reach the phi's block over different
    /// incoming edges with different values, because a branch on a per-lane
    /// condition sits between their divergence point and the phi. Loop-header
    /// phis diverge when lanes can leave the loop in different iterations.
    /// </summary>
    private bool IsControlDivergent(PhiValue phi)
    {
        if (!HasDistinctArguments(phi))
            return false;

        var merge = phi.BasicBlock;
        _dominators ??= _method.Blocks.CreateDominators();
        foreach (var pred in merge.Predecessors)
        {
            if (_dominators.Dominates(merge, pred))
                return !IsInvariantStructPhi(phi) && HasDivergentExit(merge);
        }

        _postDominators ??= _method.Blocks.CreatePostDominators();
        foreach (var block in _method.Blocks)
        {
            if (block.TerminationKind is not (
                BlockTerminationKind.Conditional or BlockTerminationKind.Switch))
            {
                continue;
            }
            var condition = block.TerminationCondition;
            if (condition is null || IsScalar(condition))
                continue;

            var join = _postDominators.GetImmediateDominator(block);
            if (join == merge || ReachesBefore(block, merge, join))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if lanes can leave the loop with the given header in
    /// different iterations, because a branch on a per-lane condition decides
    /// whether a lane exits. Loop-carried values then differ per lane after
    /// the loop even when every iteration computes uniform values.
    /// </summary>
    private bool HasDivergentExit(BasicBlock header)
    {
        _loops ??= _method.CreateLoops();
        if (!_loops.TryGetLoop(header, out var loop))
            return false;
        while (loop.Parent is not null && !CPUBlockSchedule.IsHeaderOf(loop, header))
            loop = loop.Parent;
        foreach (var breaker in loop.Breakers)
        {
            var condition = breaker.TerminationCondition;
            if (condition is not null && !IsScalar(condition))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if every back-edge value of a structure-typed loop phi is
    /// the phi itself, possibly rebuilt from its own fields. Such phis (e.g.
    /// views passed through a loop) keep their initial value.
    /// </summary>
    private bool IsInvariantStructPhi(PhiValue phi)
    {
        if (phi.Type is not StructureType)
            return false;
        for (int i = 0; i < phi.NumArguments; i++)
        {
            if (!_dominators!.Dominates(phi.BasicBlock, phi.Sources[i]))
                continue;
            var arg = phi.Arguments[i];
            if (arg == phi)
                continue;
            if (arg is not StructureValue sv)
                return false;
            for (int j = 0; j < sv.Count; j++)
            {
                if (sv.Values[j] is not GetField gf || gf.Source != phi ||
                    gf.FieldSpan.Index != j || gf.FieldSpan.Span != 1)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Returns true if the phi has at least two different incoming values.
    /// </summary>
    private static bool HasDistinctArguments(PhiValue phi)
    {
        for (int i = 1; i < phi.NumArguments; i++)
        {
            if (phi.Arguments[i] != phi.Arguments[0])
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="target"/> is reachable from the
    /// successors of <paramref name="source"/> without passing through
    /// <paramref name="stop"/>.
    /// </summary>
    private static bool ReachesBefore(
        BasicBlock source,
        BasicBlock target,
        BasicBlock stop)
    {
        var visited = new HashSet<BasicBlock>();
        var stack = new Stack<BasicBlock>();
        foreach (var successor in source.Successors)
            stack.Push(successor);
        while (stack.Count > 0)
        {
            var block = stack.Pop();
            if (block == stop || !visited.Add(block))
                continue;
            if (block == target)
                return true;
            foreach (var successor in block.Successors)
                stack.Push(successor);
        }
        return false;
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
            // Views and structures holding views or pointers are passed
            // as they are; they cannot be split into per-lane arrays.
            bool perLaneType = param.Type is not ViewType
                && !param.Type.HasFlags(
                    TypeFlags.ViewDependent | TypeFlags.PointerDependent);
            if (perLaneType && vectorizedParams is not null
                && vectorizedParams.Contains(param.Index))
            {
                continue;
            }
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
