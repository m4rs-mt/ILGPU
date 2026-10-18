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
    private readonly System.Func<Method, VectorizationAnalysis?>? _callerAnalyses;
    private readonly HashSet<Value> _scalarValues = [];
    private readonly HashSet<Value> _phiVisiting = [];
    private readonly HashSet<(PhiValue, Value)> _equivalentPhis = [];
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
    /// <param name="callerAnalyses">
    /// Returns the analysis of a calling method, or null if it is not
    /// available. View parameters differ per lane if a caller passes a view
    /// that differs per lane.
    /// </param>
    public VectorizationAnalysis(
        Method method,
        bool vectorizeFirstParam = false,
        System.Func<Method, VectorizationAnalysis?>? callerAnalyses = null)
    {
        _method = method;
        _vectorizeFirstParam = vectorizeFirstParam;
        _callerAnalyses = callerAnalyses;
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
            // Address space casts keep the address (or view) of their source
            AddressSpaceCast cast => IsScalar(cast.Source),
            // A uniform integer is a uniform address (e.g. a null pointer)
            IntAsPointerCast cast => IsScalar(cast.Source),
            // An element address with a uniform offset into a uniform pointer
            // or view is uniform, unless it addresses a local array that every
            // lane owns privately
            LoadElementAddress lea => IsScalar(lea.Source)
                && IsScalar(lea.Offset)
                && CPUExpressionEmitter.GetLaneArrayLength(lea.Source) is null,
            StructureValue sv => AreAllScalar(sv),
            Predicate pred => IsScalar(pred.Condition)
                && IsScalar(pred.TrueValue)
                && IsScalar(pred.FalseValue),
            // A phi is scalar when all its incoming values are scalar
            // (guard against infinite recursion for loop-carried phis)
            PhiValue phi => AreAllPhiArgsScalar(phi),
            // A view load is scalar when its LEA offset is scalar, unless
            // it reads a local array that every lane owns privately or
            // reads through a view that differs per lane
            Load load when load.Source is LoadElementAddress lea
                && lea.IsViewAccess => IsScalar(lea.Offset)
                && CPUExpressionEmitter.GetLaneArrayLength(lea.Source) is null
                && !IsPerLaneView(lea.Source),
            // Sub-views and casts of uniform views are uniform
            SubView sub => IsScalar(sub.Source)
                && IsScalar(sub.Offset)
                && IsScalar(sub.Length),
            ViewCast cast => IsScalar(cast.Source),
            // Views of shared memory are the same for all lanes, whereas
            // every lane owns a private copy of a local array
            NewView { Pointer: Global global } =>
                global.AddressSpace != MemoryAddressSpace.Local,
            NewView nv => IsScalar(nv.Pointer) && IsScalar(nv.Length),
            // View properties (Length) are uniform unless the view differs
            // per lane; all private arrays of a local array have the same
            // length. This makes SubView length computations
            // (viewLength - offset) propagate as scalar.
            ViewPropertyValue property => !IsPerLaneView(property.Source),
            _ => false
        };
    }

    /// <summary>
    /// Returns true if the value is a view of a local array, which addresses
    /// the private arrays of all lanes (see
    /// <see cref="CPUExpressionEmitter.GetLaneArrayLength(Value)"/>).
    /// </summary>
    public static bool IsLaneArrayView(Value value) =>
        CPUExpressionEmitter.GetLaneArrayLength(value) is not null;

    /// <summary>
    /// Returns true if the value is a view that differs per lane. Such views
    /// are stored as one view per lane (<c>CPURuntimeView&lt;T&gt;[]</c>).
    /// </summary>
    public bool IsPerLaneView(Value value) =>
        value.Type is ViewType && !IsScalar(value) && !IsLaneArrayView(value);

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
    /// equivalent to the phi itself, e.g. the phi rebuilt from its own fields
    /// or a merge of such copies. Such phis (e.g. views passed through a loop)
    /// keep their initial value.
    /// </summary>
    private bool IsInvariantStructPhi(PhiValue phi)
    {
        if (phi.Type is not StructureType)
            return false;
        for (int i = 0; i < phi.NumArguments; i++)
        {
            if (_dominators!.Dominates(phi.BasicBlock, phi.Sources[i]) &&
                !AreEquivalent(phi.Arguments[i], phi, 0))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns true if the phi has at least two different incoming values.
    /// </summary>
    private bool HasDistinctArguments(PhiValue phi)
    {
        for (int i = 1; i < phi.NumArguments; i++)
        {
            if (!AreEquivalent(phi.Arguments[i], phi.Arguments[0], 0))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if two values are equal in every lane because they are
    /// built by the same structural operations from the same values. Address
    /// space inference and view lowering rebuild views and structures holding
    /// views on several paths; the phis merging these copies are uniform.
    /// </summary>
    private bool AreEquivalent(Value left, Value right, int depth)
    {
        if (left == right)
            return true;
        if (depth > 8 || left.Type != right.Type)
            return false;
        switch (left, right)
        {
            case (AddressSpaceCast l, AddressSpaceCast r):
                return AreEquivalent(l.Source, r.Source, depth + 1);
            case (GetField l, GetField r):
                return l.FieldSpan.Index == r.FieldSpan.Index
                    && l.FieldSpan.Span == r.FieldSpan.Span
                    && AreEquivalent(l.Source, r.Source, depth + 1);
            case (StructureValue l, StructureValue r):
                for (int i = 0; i < l.Count; i++)
                {
                    if (!AreEquivalent(l.Values[i], r.Values[i], depth + 1))
                        return false;
                }
                return true;
            case (StructureValue l, _):
                return IsRebuiltFrom(l, right, depth);
            case (_, StructureValue r):
                return IsRebuiltFrom(r, left, depth);
            case (PhiValue l, _):
                return IsMergeOf(l, right, depth);
            case (_, PhiValue r):
                return IsMergeOf(r, left, depth);
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns true if a structure value consists of all fields of another
    /// value of the same type, in order.
    /// </summary>
    private bool IsRebuiltFrom(StructureValue value, Value source, int depth)
    {
        for (int i = 0; i < value.Count; i++)
        {
            if (value.Values[i] is not GetField gf ||
                gf.FieldSpan.Index != i || gf.FieldSpan.Span != 1 ||
                !AreEquivalent(gf.Source, source, depth + 1))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns true if all incoming values of a phi are equivalent to another
    /// value. A phi that is reached again while it is being compared to the
    /// same value is assumed to be equivalent, as a cycle of phis only merges
    /// the values entering it from outside.
    /// </summary>
    private bool IsMergeOf(PhiValue phi, Value value, int depth)
    {
        if (!_equivalentPhis.Add((phi, value)))
            return true;
        try
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (!AreEquivalent(phi.Arguments[i], value, depth + 1))
                    return false;
            }
            return true;
        }
        finally
        {
            _equivalentPhis.Remove((phi, value));
        }
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
    /// SubGroupIndex (warp index) IS scalar — the lanes of a call form one warp.
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
            // Pointers are passed as per-lane addresses (nint[]), and views
            // and structures, including those holding pointers or views, as
            // one value per lane if a caller passes one that differs per lane.
            if (vectorizedParams is not null
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
                    if (!IsIntrinsicallyScalar(arg) && IsPerLaneArgument(caller, arg))
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
    /// Returns true if a call argument may differ per lane. Views and
    /// structures holding views differ per lane if the caller's analysis
    /// classifies them as per-lane or they address a local array, which every
    /// lane owns privately. Without the caller's analysis, they are assumed
    /// to differ per lane.
    /// </summary>
    private bool IsPerLaneArgument(Method caller, Value arg)
    {
        if (arg.Type is not ViewType &&
            !arg.Type.HasFlags(TypeFlags.ViewDependent))
        {
            return true;
        }
        if (IsLaneArrayView(arg))
            return true;
        var analysis = caller == _method ? this : _callerAnalyses?.Invoke(caller);
        return analysis is null || !analysis.IsScalar(arg);
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

        // Views of local arrays address the private arrays of all lanes;
        // views that differ per lane are stored as one view per lane
        if (baseType is ViewType view)
        {
            var viewType =
                $"CPURuntimeView<{GetElementTypeName(view.ElementType, config)}>";
            return IsLaneArrayView(value) ? viewType : viewType + "[]";
        }

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
            return $"CPURuntimeView<{GetElementTypeName(view.ElementType, config)}>";
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
        else if (type is DelegateHandleType handleType)
            return config.GetPrimitiveTypeName(handleType.RepresentationType);
        else
            throw new System.NotSupportedException(
                $"Unsupported type in CPU backend: {type.GetType().Name}");
    }

    /// <summary>
    /// Gets the type name of the elements of a view: pointers are stored as
    /// addresses, as C# cannot use pointer types as type arguments.
    /// </summary>
    public static string GetElementTypeName(
        TypeValue elementType,
        CPULanguageConfiguration config) =>
        elementType is PointerType
            ? config.PointerStorageTypeName
            : GetScalarTypeName(elementType, config);
}
