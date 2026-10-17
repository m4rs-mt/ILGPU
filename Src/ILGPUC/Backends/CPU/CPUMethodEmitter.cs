// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMethodEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// Method emitter for CPU backend with Tensor&lt;T&gt;-based vectorization support.
/// </summary>
/// <remarks>
/// Emits vectorized methods using Tensor&lt;T&gt; and masked execution semantics.
/// Control flow is reconstructed from basic blocks and emitted with predicated
/// (masked) execution: both branches of conditionals execute, guarded by
/// an activeMask Tensor&lt;bool&gt;.
/// </remarks>
sealed class CPUMethodEmitter(
    GenerationContext context,
    bool emitDebugSymbols,
    bool poolBuffers = false)
{
    /// <summary>
    /// The parameter of a helper method holding the caller's active mask.
    /// </summary>
    private const string CallerMaskName = "_callerMask";

    /// <summary>
    /// The parameter of a helper method holding the caller's lane indices.
    /// </summary>
    private const string CallerLanesName = "_callerLanes";

    private readonly Dictionary<Method, VectorizationAnalysis> _analyses = [];
    private readonly HashSet<Method> _pendingAnalyses = [];
    private CPUExpressionEmitter? _expressionEmitter;
    private int _layoutCounter;
    private int _maskCounter;
    private int _phiBroadcastId;
    private HashSet<PhiValue> _handledMergePhis = [];

    /// <summary>
    /// Gets the buffer pool metadata computed during code generation.
    /// Only available after <see cref="EmitMethodImplementations"/> has run.
    /// </summary>
    public BufferPoolMetadata? BufferPool { get; private set; }

    /// <summary>
    /// Emits method implementations with vectorization support.
    /// </summary>
    public void EmitMethodImplementations()
    {
        foreach (var method in context.Module.MethodsInReversePostOrder)
        {
            if (!method.HasImplementation)
                continue;

            EmitVectorizedMethod(method);
        }
    }

    /// <summary>
    /// Emits a single vectorized method implementation.
    /// </summary>
    private void EmitVectorizedMethod(Method method)
    {
        _maskCounter = 0;
        _phiBroadcastId = 0;
        _handledMergePhis = [];

        bool isEntryPoint = method == context.Module.EntryPoint;
        var analysis = GetAnalysis(method)
            ?? throw new NotSupportedException(
                $"Recursive calls of '{method.Name}' are not supported by the " +
                "CPU backend.");

        // For entry point in pooled mode, compute buffer pool metadata
        // before emitting the signature (the signature needs the pool slots).
        if (poolBuffers && isEntryPoint)
        {
            var codePlacement = method.CreateCodePlacement();
            var config = (CPULanguageConfiguration)context.LanguageConfig;
            var allocator = new VectorBufferAllocator(
                method, codePlacement, analysis, config);
            var meta = allocator.Allocate();
            // Only use the pool when there are actual buffer slots;
            // otherwise fall through to non-pooled behavior.
            if (meta.Slots.Count > 0)
                BufferPool = meta;
        }

        // Emit BufferPool class before the entry point method
        if (poolBuffers && BufferPool != null && isEntryPoint)
        {
            EmitBufferPoolClass();
            context.WriteLine();
        }

        // Add kernel attribute for entry point
        if (isEntryPoint)
            context.Write(context.LanguageConfig.KernelAttribute + " ");

        // Emit method signature (uses original parameter names)
        EmitMethodSignature(method, analysis);
        context.WriteLine();
        context.OpenScope();

        // Emit vectorization setup (lane indices, active mask)
        EmitVectorizationSetup(isEntryPoint, method);

        // Emit global backing storage (local arrays, shared memory)
        if (isEntryPoint)
            EmitGlobalDeclarations();

        // Redirect the entry point's first parameter to laneIdx — but ONLY
        // when it actually IS the thread index. For auto-sized launches
        // (Index1D, Index2D, Index3D), the first parameter is the index.
        // For grouped launches (KernelConfig), there is no index parameter
        // — the kernel reads Grid.GlobalThreadIndex internally — so the
        // first parameter is a real kernel parameter and must NOT be aliased.
        if (isEntryPoint && method.Parameters.Count > 0)
        {
            var param0 = method.Parameters[0];
            if (param0.Type is StructureType indexStruct
                && IsIndexStructType(indexStruct))
            {
                // Multi-dim index: decompose laneIdx into X, Y (, Z) arrays
                EmitIndexDecomposition(indexStruct, param0, analysis);
            }
            else if (param0.Type is PrimitiveType { BasicValueType: BasicValueType.Int32 })
            {
                // 1D index (Index1D → int): alias to laneIdx
                context.SetValueName(param0, "laneIdx");
            }
            // else: grouped kernel — param0 is a real parameter, don't alias
        }

        // Emit method body with vectorized control flow
        EmitVectorizedMethodBody(method, analysis);

        // Non-entry methods with return values: emit the return statement.
        // The exit block's TerminationValue holds the merged return value
        // (a phi/select after EnsureUniqueExitBlock).
        if (!isEntryPoint && method.Type is not VoidType)
        {
            var retVal = method.ExitBlock.TerminationValue;
            var returnType = GetLaneElementType(method.Type) ?? "int";
            if (retVal is not null && analysis.IsScalar(retVal))
            {
                // Uniform return values are broadcast to all lanes
                var elemType = GetVectorElementType(retVal) ?? "int";
                var expr = new ExpressionEmitter(context, new TypeEmitter(context))
                    .EmitExpression(retVal);
                expr = ConvertLayout(expr, elemType, returnType);
                context.WriteLine(
                    $"return CPUVectorIntrinsics.Broadcast<{returnType}>(" +
                    $"{expr}, SIMDWidth);");
            }
            else if (retVal is not null)
            {
                var retName = GetLaneOperand(retVal, analysis);
                retName = ConvertLaneLayout(
                    retName, GetVectorElementType(retVal), returnType);
                context.WriteLine($"return {retName};");
            }
        }

        context.CloseScope();
        context.WriteLine();
    }

    /// <summary>
    /// Emits a method signature. When pooling is enabled and this is the entry
    /// point, appends pool buffer parameters and a laneIdx parameter.
    /// </summary>
    private void EmitMethodSignature(
        Method method,
        VectorizationAnalysis analysis)
    {
        bool isEntryPoint = method == context.Module.EntryPoint;
        var typeEmitter = new TypeEmitter(context);

        // Non-entry methods are vectorized helpers emitted in the
        // same static class — return type must be T[] for non-void.
        var returnType = typeEmitter.GetTypeName(method.Type);
        if (!isEntryPoint && method.Type is not VoidType)
            returnType += "[]";
        var methodName = isEntryPoint
            ? "KernelEntryPoint"
            : context.GetValueName(method);

        // All methods in the static class must be static
        if (!isEntryPoint)
            context.Write("static ");
        context.Write($"{returnType} {methodName}(");

        // Emit parameters — vectorized params become T[] arrays
        bool first = true;
        foreach (var param in method.Parameters)
        {
            if (!first)
                context.Write(", ");
            first = false;

            var typeName = typeEmitter.GetTypeName(param.Type);
            // Entry point's first param: for multi-dim kernels (Index2D/3D),
            // emit as int (the linear base index) instead of the struct type.
            // The decomposition into X/Y/Z happens in EmitIndexDecomposition.
            if (isEntryPoint && param.Index == 0
                && param.Type is StructureType pSt
                && IsIndexStructType(pSt))
            {
                typeName = "int";
            }
            // Non-entry methods: vectorized params become T[] arrays.
            // Entry point params keep their IR types — the first param
            // is aliased to laneIdx in EmitVectorizationSetup.
            else if (!isEntryPoint && !analysis.IsScalar(param))
            {
                typeName += "[]";
            }
            var paramName = context.GetValueName(param);
            context.Write($"{typeName} {paramName}");
        }

        // Helpers run for the active lanes of their caller
        if (method != context.Module.EntryPoint)
        {
            if (!first)
                context.Write(", ");
            context.Write($"bool[] {CallerMaskName}, int[] {CallerLanesName}");
        }

        // Append pool or activeLanes parameter for entry point
        if (method == context.Module.EntryPoint)
        {
            if (!first)
                context.Write(", ");
            if (poolBuffers && BufferPool != null)
                context.Write("BufferPool pool");
            else
                context.Write("int _activeLanes");

            // Dimension parameters for multi-dim index reconstruction.
            // Only add dim params when the first parameter is a pure
            // numeric struct (Index2D/Index3D). Structs with pointer/view
            // dependencies (e.g. ArrayView) are user parameters from
            // grouped kernels and must not be treated as multi-dim indices.
            if (method.Parameters.Count > 0
                && method.Parameters[0].Type is StructureType idxSt
                && IsIndexStructType(idxSt))
            {
                if (idxSt.NumFields >= 2)
                    context.Write(", int _dimX, int _dimY");
                if (idxSt.NumFields >= 3)
                    context.Write(", int _dimZ");
            }
        }

        context.Write(")");
    }

    /// <summary>
    /// Emits the vectorization setup code (lane indices tensor, active mask).
    /// In pooled mode, laneIdx comes as a parameter so only activeMask is created.
    /// For non-pooled entry points, laneIdx is initialized from the scalar
    /// baseIndex parameter so that per-lane indices are correct.
    /// </summary>
    private void EmitVectorizationSetup(bool isEntryPoint, Method? method = null)
    {
        if (poolBuffers && BufferPool != null && isEntryPoint)
        {
            // Pooled mode: alias pool arrays as locals.
            // pool.Clear(baseIndex, activeLanes) initializes laneIdx and
            // activeMask with per-lane offsets and bounds masking.
            context.WriteLine("var laneIdx = pool.laneIdx;");
            context.WriteLine(
                $"var {CPUExpressionEmitter.ActiveMaskName} = pool.activeMask;");
            context.WriteLine();
        }
        else if (!isEntryPoint)
        {
            // Helpers use the lanes of their caller and copy its active mask,
            // which their own control flow modifies.
            context.WriteLine($"var laneIdx = {CallerLanesName};");
            context.WriteLine(
                $"var {CPUExpressionEmitter.ActiveMaskName} = " +
                $"CPUVectorIntrinsics.CopyMask({CallerMaskName});");
            context.WriteLine();
        }
        else
        {
            // Non-pooled entry point: create laneIdx locally
            context.WriteLine("// Lane indices for vectorized execution");
            context.WriteLine(
                "var laneIdx = CPUVectorIntrinsics.CreateLaneIndices(SIMDWidth);");

            // For non-pooled entry points, initialize laneIdx with the
            // baseIndex offset so per-lane indices are [base, base+1, ...].
            if (isEntryPoint && method != null && method.Parameters.Count > 0)
            {
                var paramName = context.GetValueName(method.Parameters[0]);
                context.WriteLine(
                    $"CPUVectorIntrinsics.InitLaneIndices(laneIdx, {paramName});");
            }

            context.WriteLine();

            // Initialize active mask with proper bounds masking.
            context.WriteLine("// Active mask for predicated execution");
            context.WriteLine(
                $"var {CPUExpressionEmitter.ActiveMaskName} = " +
                $"CPUVectorIntrinsics.CreateAllTrueMask(SIMDWidth);");
            if (isEntryPoint)
            {
                context.WriteLine(
                    $"CPUVectorIntrinsics.InitActiveMask(" +
                    $"{CPUExpressionEmitter.ActiveMaskName}, _activeLanes);");
            }
            context.WriteLine();
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="st"/> is a pure
    /// numeric struct suitable for multi-dim index decomposition (e.g.
    /// Index2D, Index3D). Structs that carry pointer or view dependencies
    /// (e.g. ArrayView, user structs with pointers) are user parameters
    /// from grouped kernels and must not be treated as index types.
    /// </summary>
    internal static bool IsIndexStructType(StructureType st)
    {
        if (st.HasFlags(TypeFlags.PointerDependent | TypeFlags.ViewDependent))
            return false;
        // Index structs (Index2D, Index3D) have homogeneous primitive fields
        // (all i32 or all i64). This distinguishes them from KernelIndex
        // {i64 GridIndex, i32 GroupIndex} which has mixed field types and
        // should NOT be treated as a multi-dimensional index.
        if (st.NumFields < 2)
            return false;
        var first = st.Fields[0];
        if (first is not PrimitiveType)
            return false;
        for (int i = 1; i < st.NumFields; i++)
        {
            if (st.Fields[i] != first)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="st"/> has the
    /// exact shape of <c>ILGPU.KernelIndex</c> — two primitive fields
    /// <c>{i64 GridIndex, i32 GroupIndex}</c>. Unlike
    /// <see cref="IsIndexStructType"/>, which only recognises homogeneous
    /// multi-dim index structs, this check is used to decide whether the
    /// grouped-kernel first parameter should be skipped when marshaling
    /// user-visible arguments.
    /// </summary>
    internal static bool IsKernelIndexStructType(StructureType st)
    {
        if (st.NumFields != 2)
            return false;
        if (st.HasFlags(TypeFlags.PointerDependent | TypeFlags.ViewDependent))
            return false;
        return st.Fields[0] is PrimitiveType
                { BasicValueType: BasicValueType.Int64 }
            && st.Fields[1] is PrimitiveType
                { BasicValueType: BasicValueType.Int32 };
    }

    /// <summary>
    /// Returns true if any field of the StructureValue is vectorized.
    /// </summary>
    private static bool HasVectorizedField(
        StructureValue sv,
        VectorizationAnalysis analysis)
    {
        for (int i = 0; i < sv.Count; i++)
        {
            if (!analysis.IsScalar(sv.Values[i]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Decomposes linear laneIdx into per-field arrays for multi-dim index
    /// parameters (Index2D/3D). Registers GetField results so that
    /// <c>extractvalue %struct param_0, 0</c> resolves to the X array, etc.
    /// </summary>
    private void EmitIndexDecomposition(
        StructureType indexStruct,
        Parameter param0,
        VectorizationAnalysis analysis)
    {
        int numFields = indexStruct.NumFields;
        var config = (CPULanguageConfiguration)context.LanguageConfig;

        context.WriteLine("// Decompose linear index into multi-dim components");
        // Declare per-field arrays
        var fieldNames = new string[numFields];
        for (int f = 0; f < numFields; f++)
        {
            var fieldName = $"_idx_{StructureType.GetFieldName(f)}";
            fieldNames[f] = fieldName;
            context.WriteLine($"var {fieldName} = new int[SIMDWidth];");
        }

        // Decompose linear index into multi-dim components.
        // Row-major (X-fastest): X = linear % dimX, Y = linear / dimX
        context.WriteLine("for (int _d = 0; _d < SIMDWidth; _d++)");
        context.OpenScope();
        context.WriteLine("int _li = laneIdx[_d];");
        if (numFields == 2)
        {
            context.WriteLine($"{fieldNames[0]}[_d] = _li % _dimX;");
            context.WriteLine($"{fieldNames[1]}[_d] = _li / _dimX;");
        }
        else if (numFields >= 3)
        {
            context.WriteLine($"{fieldNames[0]}[_d] = _li % _dimX;");
            context.WriteLine($"{fieldNames[1]}[_d] = (_li / _dimX) % _dimY;");
            context.WriteLine($"{fieldNames[2]}[_d] = _li / (_dimX * _dimY);");
        }
        context.CloseScope();
        context.WriteLine();

        // Register the decomposed fields. When the code accesses
        // GetField(param_0, fieldIndex), it should resolve to the
        // per-field array name instead of emitting source.FieldN.
        param0.Scope.Blocks.ForEachValue<GetField>(gf =>
        {
            if (gf.Source == param0 && gf.FieldSpan.Index < numFields)
            {
                context.SetValueName(gf, fieldNames[gf.FieldSpan.Index]);
                context.MarkNeedsVariable(gf);
            }
        });
    }

    /// <summary>
    /// Emits local storage for Global values in Local or Shared address
    /// spaces. These are backing stores for local arrays and shared memory.
    /// Each global is allocated as stackalloc and wrapped in a
    /// <c>CPURuntimeView&lt;T&gt;</c> so that view operations (GatherLoad,
    /// ScatterStore, indexer) work correctly.
    /// </summary>
    private void EmitGlobalDeclarations()
    {
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        bool hasGlobals = false;

        foreach (var global in context.Module.Globals)
        {
            var elemSize = global.AllocType.Size;
            var arrayLen = global.ArrayLength?.Int32Value ?? 1;
            var totalSize = arrayLen * elemSize;
            // Local arrays are private to each lane: allocate one copy per
            // lane (shared memory stays a single copy for all lanes).
            var storageSize = global.AddressSpace == MemoryAddressSpace.Local
                ? $"SIMDWidth * {totalSize}"
                : $"{totalSize}";
            var name = context.GetValueName(global);
            var storageName = $"_gstorage_{global.Id}";
            string elemType;
            if (global.AllocType is PrimitiveType pt)
                elemType = config.GetPrimitiveTypeName(pt.BasicValueType);
            else if (global.AllocType is StructureType st)
                elemType = $"struct_{st.Id}";
            else
                elemType = "byte";

            if (!hasGlobals)
            {
                context.WriteLine("// Local/shared memory backing storage");
                hasGlobals = true;
            }

            // Allocate raw storage on the stack
            context.WriteLine(
                $"Span<byte> {storageName} = stackalloc byte[{storageSize}];");
            // Wrap in CPURuntimeView<T> via fixed pointer
            context.WriteLine(
                $"CPURuntimeView<{elemType}> {name};");
            context.WriteLine(
                $"fixed ({elemType}* _gptr_{global.Id} = " +
                $"MemoryMarshal.Cast<byte, {elemType}>({storageName}))");
            context.WriteLine(
                $"    {name} = new CPURuntimeView<{elemType}>(" +
                $"_gptr_{global.Id}, {arrayLen});");
        }
        if (hasGlobals)
            context.WriteLine();
    }

    /// <summary>
    /// Emits the <c>BufferPool</c> sealed class inside the kernel static class.
    /// Contains readonly array fields for each pool slot plus laneIdx,
    /// a constructor that allocates all arrays, and a <c>Clear(int baseIndex)</c>
    /// method that resets them each iteration.
    /// </summary>
    private void EmitBufferPoolClass()
    {
        var pool = BufferPool!;

        context.WriteLine("public sealed class BufferPool");
        context.OpenScope();

        // Fields
        foreach (var (elemType, slotIndex) in pool.Slots)
        {
            var poolName = BufferPoolMetadata.GetSlotName(elemType, slotIndex);
            context.WriteLine($"public {elemType}[] {poolName};");
        }
        context.WriteLine("public int[] laneIdx;");
        context.WriteLine("public bool[] activeMask;");
        context.WriteLine();

        // Constructor
        context.WriteLine("public BufferPool(int simdWidth)");
        context.OpenScope();
        foreach (var (elemType, slotIndex) in pool.Slots)
        {
            var poolName = BufferPoolMetadata.GetSlotName(elemType, slotIndex);
            context.WriteLine($"{poolName} = new {elemType}[simdWidth];");
        }
        context.WriteLine(
            "laneIdx = CPUVectorIntrinsics.CreateLaneIndices(simdWidth);");
        context.WriteLine(
            "activeMask = CPUVectorIntrinsics.CreateAllTrueMask(simdWidth);");
        context.CloseScope();
        context.WriteLine();

        // Clear method — resets buffers, lane indices, and active mask
        context.WriteLine("public void Clear(int baseIndex, int activeLanes)");
        context.OpenScope();
        foreach (var (elemType, slotIndex) in pool.Slots)
        {
            var poolName = BufferPoolMetadata.GetSlotName(elemType, slotIndex);
            context.WriteLine($"Array.Clear({poolName});");
        }
        context.WriteLine(
            "CPUVectorIntrinsics.InitLaneIndices(laneIdx, baseIndex);");
        context.WriteLine(
            "CPUVectorIntrinsics.InitActiveMask(activeMask, activeLanes);");
        context.CloseScope();

        context.CloseScope(); // class BufferPool
    }

    /// <summary>
    /// Emits the vectorized method body with proper control flow.
    /// </summary>
    /// <param name="method">The method to emit.</param>
    /// <param name="analysis">The vectorization analysis.</param>
    private void EmitVectorizedMethodBody(
        Method method,
        VectorizationAnalysis analysis)
    {
        // Emit #line directive for method start if available
        EmitLineDirective(method.EntryBlock.Location);

        // Create expression emitter
        var expressionEmitter = new CPUExpressionEmitter(context, analysis);
        _expressionEmitter = expressionEmitter;

        // Set up broadcast name overrides for pooled mode. The emitter
        // constructs broadcast names as "_bcast_tmp_{value.Id}"; the
        // allocator stores assignments under the same key, so we pass
        // the map through directly. (Previous revisions keyed overrides
        // by the result's pool name, which collided when many compares
        // shared a single pool_bool_* slot — only the last assignment
        // won, leaving later broadcasts pointing at a stale pool slot
        // that overlapped their own operands.)
        if (poolBuffers && BufferPool != null
            && method == context.Module.EntryPoint
            && BufferPool.BroadcastAssignments.Count > 0)
        {
            var overrides = new Dictionary<string, string>(
                BufferPool.BroadcastAssignments.Count);
            foreach (var (origBcastName, bcastPoolName) in
                BufferPool.BroadcastAssignments)
            {
                overrides[origBcastName] = $"pool.{bcastPoolName}";
            }
            expressionEmitter.BroadcastNameOverrides = overrides;
        }

        // Reconstruct control flow from basic blocks
        var controlFlow = ControlFlowReconstructor.Reconstruct(method);

        // Create code placement analysis for value ordering
        var codePlacement = method.CreateCodePlacement();

        // Allocate SSA variables (using vectorized types)
        EmitVariableDeclarations(method, codePlacement, analysis);

        // Emit the reconstructed control flow structure with masking
        EmitControlFlow(controlFlow, codePlacement, analysis, expressionEmitter);
    }

    /// <summary>
    /// Emits variable declarations for vectorized values.
    /// Pre-allocates vector buffers as <c>new T[SIMDWidth]</c> and tracks
    /// broadcast temporaries needed for mixed scalar/vector operations.
    /// In pooled mode for the entry point, vector buffers are passed as
    /// parameters and values are redirected via
    /// <see cref="GenerationContext.SetValueName"/>.
    /// </summary>
    private void EmitVariableDeclarations(
        Method method,
        CodePlacement codePlacement,
        VectorizationAnalysis analysis)
    {
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        bool hasDeclarations = false;
        bool isPooledEntryPoint = poolBuffers && BufferPool != null
            && method == context.Module.EntryPoint;

        foreach (var block in method.Blocks)
        {
            if (!codePlacement.TryGetPlacedBlock(block, out var placedBlock))
                continue;

            foreach (var value in placedBlock)
            {
                // Skip values that don't need variable declarations
                if (value.Type is VoidType or KindType)
                    continue;
                if (value is Parameter)
                    continue;

                // PrimitiveValue constants are inlined by the expression emitter
                if (value is PrimitiveValue)
                    continue;

                // LoadElementAddress values consumed only by Load/Store
                // are fused into those operations — no variable needed
                if (value is LoadElementAddress lea && IsLeaFusable(lea))
                    continue;

                // Per-lane index values map directly to laneIdx — no variable
                if (value is GroupIndexValue or SubGroupIndexValue
                    or SubGroupLaneIndexValue)
                    continue;

                // NewView from a Global resolves to the global's nint[] —
                // no separate variable needed
                if (value is NewView nv && nv.Pointer is Global)
                    continue;

                // Store is void — never needs a variable
                if (value is Store)
                    continue;

                // Single-use scalar PureValues are inlined by their consumers
                if (IsInlinedScalar(value, analysis))
                    continue;

                // GetField from decomposed multi-dim index: already declared
                // as _idx_FieldN in EmitIndexDecomposition — skip pool redirect
                // and variable declaration entirely.
                if (value is GetField gf2 && gf2.Source is Parameter p2
                    && p2.Index == 0 && p2.Type is StructureType gf2St
                    && IsIndexStructType(gf2St)
                    && p2.Scope == context.Module.EntryPoint)
                {
                    // Name was already set by EmitIndexDecomposition
                    continue;
                }

                var varName = context.GetValueName(value);

                // In pooled mode, redirect pooled values to their pool names
                if (isPooledEntryPoint
                    && BufferPool!.ValueAssignments.TryGetValue(
                        value, out var poolName))
                {
                    // Redirect this value to use the pool field
                    context.SetValueName(value, "pool." + poolName);
                    context.MarkNeedsVariable(value);

                    // Handle broadcast temporary redirection. The allocator
                    // keys broadcasts by _bcast_tmp_{value.Id}; the emitter
                    // will route through the override map to the pool slot
                    // so no local declaration is needed here.

                    hasDeclarations = true;
                    continue;
                }

                if (!analysis.IsScalar(value) && value.Type is PrimitiveType pt)
                {
                    // Pre-allocate vector buffer
                    var elemType = config.GetPrimitiveTypeName(pt.BasicValueType);
                    context.WriteLine(
                        $"var {varName} = new {elemType}[SIMDWidth];");
                }
                else if (!analysis.IsScalar(value) && value is CompareValue)
                {
                    // CompareValue produces bool[]
                    context.WriteLine(
                        $"var {varName} = new bool[SIMDWidth];");
                }
                else if (!analysis.IsScalar(value) && value.Type is PointerType)
                {
                    // Pointer vectors are nint[]
                    context.WriteLine(
                        $"var {varName} = new nint[SIMDWidth];");
                }
                else if (analysis.IsPerLaneView(value))
                {
                    // Views that differ per lane hold one view per lane
                    context.WriteLine(
                        $"var {varName} = new {GetVectorElementType(value)}" +
                        "[SIMDWidth];");
                }
                else if (!analysis.IsScalar(value) && value.Type is StructureType st2)
                {
                    // Vectorized struct values
                    context.WriteLine(
                        $"var {varName} = new struct_{st2.Id}[SIMDWidth];");
                }
                else
                {
                    // Fallback: typed declaration with default initializer
                    // (required for C# definite assignment when assigned
                    // inside control flow blocks)
                    var typeName = analysis.GetTypeName(value, config);
                    context.WriteLine($"{typeName} {varName} = default;");
                    // Statement emitters must assign, not redeclare
                    context.DeclarePrefix(varName);
                }

                context.MarkNeedsVariable(value);
                hasDeclarations = true;

                // Emit broadcast temporaries for mixed scalar/vector operands.
                // Name by value.Id — that's the key the VectorBufferAllocator
                // uses for BroadcastAssignments, and the key the expression
                // emitter's GetBroadcastName produces (so in pooled mode the
                // override substitutes and in non-pooled mode the declared
                // variable is referenced). context.GetValueName generates
                // a method-local _nextTempId counter that doesn't match
                // value.Id for helper methods.
                if (value is BinaryArithmeticValue bin && !analysis.IsScalar(bin))
                {
                    bool lScalar = analysis.IsScalar(bin.Left);
                    bool rScalar = analysis.IsScalar(bin.Right);
                    if (lScalar != rScalar)
                    {
                        var bcastType = config.GetPrimitiveTypeName(
                            ((PrimitiveType)bin.Type).BasicValueType);
                        context.WriteLine(
                            $"var _bcast_tmp_{value.Id} = " +
                            $"new {bcastType}[SIMDWidth];");
                    }
                }
                else if (value is CompareValue cmp && !analysis.IsScalar(cmp))
                {
                    bool lScalar = analysis.IsScalar(cmp.Left);
                    bool rScalar = analysis.IsScalar(cmp.Right);
                    if (lScalar != rScalar)
                    {
                        // Broadcast temp uses the operand type, not bool
                        var operandType = cmp.Left.Type is PrimitiveType cpt
                            ? config.GetPrimitiveTypeName(cpt.BasicValueType)
                            : "int";
                        context.WriteLine(
                            $"var _bcast_tmp_{value.Id} = " +
                            $"new {operandType}[SIMDWidth];");
                    }
                }
            }
        }

        if (hasDeclarations)
            context.WriteLine();
    }

    /// <summary>
    /// Returns the element type of the per-lane buffer of a vectorized value,
    /// or null if the value is not stored in a per-lane array.
    /// </summary>
    private string? GetVectorElementType(Value value) =>
        value is CompareValue ? "bool" : GetLaneElementType(value.Type);

    /// <summary>
    /// Returns the element type of the per-lane buffer of values of the given
    /// type, or null if such values are not stored in per-lane arrays.
    /// </summary>
    private string? GetLaneElementType(TypeValue type) =>
        type switch
        {
            PrimitiveType pt => ((CPULanguageConfiguration)context.LanguageConfig)
                .GetPrimitiveTypeName(pt.BasicValueType),
            PointerType => "nint",
            StructureType st => $"struct_{st.Id}",
            ViewType view => "CPURuntimeView<" +
                VectorizationAnalysis.GetScalarTypeName(
                    view.ElementType,
                    (CPULanguageConfiguration)context.LanguageConfig) + ">",
            _ => null
        };

    /// <summary>
    /// Returns true if a single-use scalar pure value is inlined into the
    /// expression of its consumer. Scalar arithmetic, comparisons and
    /// conversions are always assigned to variables: their C# expressions
    /// need casts for small and unsigned integer types that the shared
    /// expression emitter does not produce. Field insertions need a
    /// variable to assign the field to.
    /// </summary>
    private static bool IsInlinedScalar(
        Value value,
        VectorizationAnalysis analysis) =>
        value is PureValue
        && value is not (BinaryArithmeticValue or UnaryArithmeticValue
            or CompareValue or ConvertValue or SetField)
        && value.Uses.HasExactlyOne
        && analysis.IsScalar(value);

    /// <summary>
    /// Returns true if a LoadElementAddress is only consumed by Load/Store
    /// and can be fused into those operations.
    /// </summary>
    private static bool IsLeaFusable(LoadElementAddress lea)
    {
        foreach (var use in lea.Uses)
        {
            if (use.Target is not (Load or Store))
                return false;
        }
        return true;
    }

    #region Control Flow Emission

    /// <summary>
    /// Emits a control flow structure, dispatching on element type.
    /// </summary>
    private void EmitControlFlow(
        ControlFlowStructure structure,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        foreach (var element in structure.Elements)
        {
            switch (element)
            {
                case BasicBlock block:
                    EmitBasicBlock(
                        block, placement, analysis, expressionEmitter);
                    break;

                case IfStatement ifStmt:
                    EmitMaskedIfStatement(
                        ifStmt, placement, analysis, expressionEmitter);
                    break;

                case ForLoop forLoop:
                    EmitMaskedForLoop(
                        forLoop, placement, analysis, expressionEmitter);
                    break;

                case WhileLoop whileLoop:
                    EmitMaskedWhileLoop(
                        whileLoop, placement, analysis, expressionEmitter);
                    break;

                case DoWhileLoop doWhileLoop:
                    EmitMaskedDoWhileLoop(
                        doWhileLoop, placement, analysis, expressionEmitter);
                    break;

                case SwitchStatement switchStmt:
                    EmitMaskedSwitchStatement(
                        switchStmt, placement, analysis, expressionEmitter);
                    break;

                case UnstructuredRegion region:
                    EmitMaskedUnstructuredRegion(
                        region, placement, analysis, expressionEmitter);
                    break;
            }
        }
    }

    /// <summary>
    /// Like <see cref="EmitControlFlow"/> but skips a specific block that was
    /// already emitted (e.g. the loop header emitted before the condition check).
    /// </summary>
    private void EmitControlFlowSkipping(
        ControlFlowStructure structure,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter,
        BasicBlock skipBlock)
    {
        foreach (var element in structure.Elements)
        {
            if (element is BasicBlock bb && bb == skipBlock)
                continue;

            switch (element)
            {
                case BasicBlock block:
                    EmitBasicBlock(
                        block, placement, analysis, expressionEmitter);
                    break;

                case IfStatement ifStmt:
                    EmitMaskedIfStatement(
                        ifStmt, placement, analysis, expressionEmitter);
                    break;

                case ForLoop forLoop:
                    EmitMaskedForLoop(
                        forLoop, placement, analysis, expressionEmitter);
                    break;

                case WhileLoop whileLoop:
                    EmitMaskedWhileLoop(
                        whileLoop, placement, analysis, expressionEmitter);
                    break;

                case DoWhileLoop doWhileLoop:
                    EmitMaskedDoWhileLoop(
                        doWhileLoop, placement, analysis, expressionEmitter);
                    break;

                case SwitchStatement switchStmt:
                    EmitMaskedSwitchStatement(
                        switchStmt, placement, analysis, expressionEmitter);
                    break;

                case UnstructuredRegion region:
                    EmitMaskedUnstructuredRegion(
                        region, placement, analysis, expressionEmitter);
                    break;
            }
        }
    }

    /// <summary>
    /// Emits a basic block's contents with vectorized value emission.
    /// </summary>
    private void EmitBasicBlock(
        BasicBlock block,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        // Emit label (with empty statement to make it valid before '}')
        var blockName = context.GetValueName(block);
        context.WriteLine($"{blockName}: ;");

        // Get the placed block with all values in correct order
        if (!placement.TryGetPlacedBlock(block, out var placedBlock))
            return;

        // Emit phi values as masked selects
        foreach (var value in placedBlock)
        {
            if (value is PhiValue phi)
                EmitPhiValue(phi, analysis);
        }

        // Emit all non-phi values
        foreach (var value in placedBlock)
        {
            if (value is PhiValue)
                continue;

            EmitValue(value, analysis, expressionEmitter);
        }
    }

    /// <summary>
    /// Emits only the non-phi values of a basic block (no label, no phis).
    /// Used to emit the loop header's condition-computing instructions
    /// inside the loop body without re-emitting phi initializations.
    /// </summary>
    private void EmitBasicBlockValuesOnly(
        BasicBlock block,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter,
        bool forceNeedsVariable = false)
    {
        if (!placement.TryGetPlacedBlock(block, out var placedBlock))
            return;

        foreach (var value in placedBlock)
        {
            if (value is PhiValue)
                continue;
            // When re-emitting header values inside a loop body,
            // mark values as needing a variable so they emit as
            // assignments (x = ...) instead of declarations (var x = ...).
            // Skip scalar PureValues — they should be inlined, not
            // assigned to variables (marking them breaks exit-block
            // uses that expect inline resolution).
            if (forceNeedsVariable && value.Type is not VoidType
                && !(value is PureValue && analysis.IsScalar(value)))
                context.MarkNeedsVariable(value);
            EmitValue(value, analysis, expressionEmitter);
        }
    }

    /// <summary>
    /// Returns the text to reference a branch/loop condition. Scalar pure
    /// conditions with exactly one use have no variable allocated (the
    /// SSAVariableAllocator inlines them), so asking the context for a name
    /// would mint an undeclared temporary; inline the expression instead.
    /// </summary>
    private string EmitConditionExpression(
        Value condition,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter) =>
        analysis.IsScalar(condition) && !context.NeedsVariable(condition)
            ? expressionEmitter.ResolveOperand(condition)
            : context.GetValueName(condition);

    /// <summary>
    /// Emits non-phi values of a basic block split into two parts:
    /// values that the <paramref name="condition"/> transitively depends on
    /// (emitted first), and all remaining values (emitted second).
    /// Returns the list of remaining values for separate emission.
    /// </summary>
    private List<Value<Method>> EmitHeaderConditionDeps(
        BasicBlock block,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter,
        Value condition)
    {
        if (!placement.TryGetPlacedBlock(block, out var placedBlock))
            return [];

        // Compute the transitive dependency set of the condition value
        var condDeps = new HashSet<Value>();
        ComputeTransitiveDeps(condition, condDeps);

        var remaining = new List<Value<Method>>();
        foreach (var value in placedBlock)
        {
            if (value is PhiValue)
                continue;
            if (condDeps.Contains(value))
                EmitValue(value, analysis, expressionEmitter);
            else
                remaining.Add(value);
        }

        return remaining;
    }

    /// <summary>
    /// Emits a list of pre-collected values.
    /// </summary>
    private void EmitValues(
        List<Value<Method>> values,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        foreach (var value in values)
            EmitValue(value, analysis, expressionEmitter);
    }

    /// <summary>
    /// Computes the transitive set of IR values that a given value depends on,
    /// including the value itself. Follows operands that are placed block
    /// values (PureValue, BasicBlockValue) but not globals, parameters, or
    /// type values.
    /// </summary>
    private static void ComputeTransitiveDeps(Value value, HashSet<Value> deps)
    {
        if (!deps.Add(value))
            return;

        foreach (var operand in value.Values)
        {
            // Follow any operand that would be placed in a block
            if (operand is PureValue or BasicBlockValue)
                ComputeTransitiveDeps(operand, deps);
        }
    }

    /// <summary>
    /// Emits a phi value. Phis handled via Select in branch emitters are skipped;
    /// others fall back to a simple first-source assignment.
    /// </summary>
    private void EmitPhiValue(PhiValue phi, VectorizationAnalysis analysis)
    {
        // Phis in if-statement merge blocks are already handled by
        // EmitPhiSelectsForBranch; no assignment needed here.
        if (_handledMergePhis.Contains(phi))
            return;

        if (phi.NumArguments == 0)
            return;

        var varName = context.GetValueName(phi);
        var arg = phi.Arguments[0];

        // Resolve the incoming value — PrimitiveValues and other
        // inlined values need the expression emitter, not GetValueName
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);
        string source;
        if (arg is PrimitiveValue || !context.NeedsVariable(arg))
            source = exprEmitter.EmitExpression(arg);
        else
            source = context.GetValueName(arg);

        EmitPhiAssignment(phi, arg, varName, source, analysis);
    }

    /// <summary>
    /// Assigns an incoming value to a phi. Uniform values are broadcast to a
    /// phi that differs per lane, and the per-lane structures and views of
    /// other values are copied, as the loop and branch emitters update the
    /// buffers of phis in place.
    /// </summary>
    private void EmitPhiAssignment(
        PhiValue phi,
        Value arg,
        string phiVar,
        string source,
        VectorizationAnalysis analysis)
    {
        var elemType = GetVectorElementType(phi) ?? "int";
        if (analysis.IsScalar(phi))
        {
            context.WriteLine($"{phiVar} = {source};");
        }
        else if (analysis.IsScalar(arg))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{phiVar}, {source});");
        }
        else if (phi.Type is ViewType or StructureType)
        {
            if (phi.Type is ViewType)
                source = _expressionEmitter!.ResolveViews(arg);
            context.WriteLine($"{source}.AsSpan().CopyTo({phiVar});");
        }
        else
        {
            context.WriteLine($"{phiVar} = {source};");
        }
    }

    /// <summary>
    /// Emits a single value (BasicBlockValue or PureValue).
    /// </summary>
    private void EmitValue(
        Value<Method> value,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        // Single-use scalar PureValues are inlined into their consumer's expression
        if (IsInlinedScalar(value, analysis))
            return;

        // Per-lane index values map to laneIdx — no emission needed
        if (value is GroupIndexValue or SubGroupIndexValue
            or SubGroupLaneIndexValue)
            return;

        switch (value)
        {
            case Load load:
                expressionEmitter.EmitLoad(load);
                break;

            case MethodCall call when call.Target.HasImplementation:
                EmitHelperCall(call, analysis, expressionEmitter);
                break;

            case Store store:
                expressionEmitter.EmitStore(store);
                break;

            case BinaryArithmeticValue binary:
                expressionEmitter.EmitBinaryOp(binary);
                break;

            case UnaryArithmeticValue unary:
                expressionEmitter.EmitUnaryOp(unary);
                break;

            case CompareValue compare:
                expressionEmitter.EmitCompare(compare);
                break;

            case GenericAtomic atomic:
                expressionEmitter.EmitAtomic(atomic);
                break;

            case AtomicCAS cas:
                expressionEmitter.EmitAtomicCAS(cas);
                break;

            case ConvertValue convert:
                expressionEmitter.EmitConvert(convert);
                break;

            case FloatAsIntCast fai:
                expressionEmitter.EmitFloatAsIntCast(fai);
                break;

            case IntAsFloatCast iaf:
                expressionEmitter.EmitIntAsFloatCast(iaf);
                break;

            case WarpReduce reduce:
                expressionEmitter.EmitWarpReduce(reduce);
                break;

            case WarpScan scan:
                expressionEmitter.EmitWarpScan(scan);
                break;

            case Shuffle shuffle:
                expressionEmitter.EmitShuffle(shuffle);
                break;

            case PredicateBarrier barrier:
                expressionEmitter.EmitPredicateBarrier(barrier);
                break;

            case Predicate predicate
                when !analysis.IsScalar(predicate.Condition) ||
                    !analysis.IsScalar(predicate):
                expressionEmitter.EmitPredicate(predicate);
                break;

            case LoadElementAddress lea when IsLeaFusable(lea):
                // Fused into Load/Store — skip emission
                break;

            case LoadElementAddress lea
                when lea.IsViewAccess && !analysis.IsScalar(lea):
                // Non-fusable vectorized LEA on a view: compute per-lane
                // addresses into the nint[] buffer for use by atomics.
                expressionEmitter.EmitViewLEA(lea);
                break;

            case Alloca alloca when !analysis.IsScalar(alloca):
                // Vectorized alloca: use stackalloc for per-lane local
                // memory and initialize the nint[] pointer array.
                {
                    var allocVar = context.GetValueName(alloca);
                    var elemSize = alloca.AllocType.Size;
                    var storageVar = $"_alloca_{alloca.Id}";
                    context.WriteLine(
                        $"Span<byte> {storageVar} = stackalloc byte" +
                        $"[SIMDWidth * {elemSize}];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.InitAllocaPointers(" +
                        $"{allocVar}, {storageVar}, {elemSize});");
                }
                break;

            case LoadFieldAddress lfa when !analysis.IsScalar(lfa):
                // Vectorized LFA: offset each lane's pointer in the
                // nint[] by the field's byte offset within the struct.
                {
                    var lfaVar = context.GetValueName(lfa);
                    var srcVar = context.GetValueName(lfa.Source);
                    var fieldOffset = lfa.FieldSpan.Index;
                    if (fieldOffset == 0)
                    {
                        // Field 0: just copy the pointer array
                        context.WriteLine(
                            $"CPUVectorIntrinsics.CopyPointers(" +
                            $"{lfaVar}, {srcVar});");
                    }
                    else
                    {
                        // Compute byte offset from field index and element sizes
                        var structType = lfa.StructureType;
                        int byteOffset = 0;
                        for (int f = 0; f < fieldOffset; f++)
                            byteOffset += structType[f].Size;
                        context.WriteLine(
                            $"CPUVectorIntrinsics.OffsetPointers(" +
                            $"{lfaVar}, {srcVar}, {byteOffset});");
                    }
                }
                break;

            case GetViewLength viewLen when !analysis.IsScalar(viewLen):
                // The length of a view that differs per lane: read the length
                // of every lane's view
                {
                    var vlResult = context.GetValueName(viewLen);
                    var views = expressionEmitter.ResolveViews(viewLen.Source);
                    var lenField = context.LanguageConfig.ViewLengthFieldName;
                    var cfg = (CPULanguageConfiguration)
                        context.LanguageConfig;
                    var lenType = cfg.GetPrimitiveTypeName(
                        viewLen.BasicValueType);
                    context.WriteLine(
                        "for (int _si = 0; _si < SIMDWidth; _si++)");
                    context.WriteLine(
                        $"    {vlResult}[_si] = ({lenType})" +
                        $"{views}[_si].{lenField};");
                }
                break;

            case SubView subView when analysis.IsPerLaneView(subView):
                EmitPerLaneSubView(subView, analysis, expressionEmitter);
                break;

            case NewView newView when analysis.IsPerLaneView(newView):
                EmitPerLaneNewView(newView, analysis, expressionEmitter);
                break;

            case BaseAddressSpaceCast cast when analysis.IsPerLaneView(cast):
                EmitPerLaneViewCast(cast, expressionEmitter);
                break;

            case PointerAsIntCast ptrCast:
                // Pointer-to-int: resolve the source pointer and cast.
                {
                    var castResult = context.GetValueName(ptrCast);
                    var typeEmitter2 = new TypeEmitter(context);
                    var exprEmitter2 = new ExpressionEmitter(
                        context, typeEmitter2);
                    var sourceExpr = exprEmitter2.EmitExpression(
                        ptrCast.Source);
                    var config2 = (CPULanguageConfiguration)
                        context.LanguageConfig;
                    var castType = config2.GetPrimitiveTypeName(
                        ptrCast.BasicValueType);
                    if (analysis.IsScalar(ptrCast))
                    {
                        context.WriteLine(
                            $"{castResult} = ({castType})(nint)" +
                            $"{sourceExpr};");
                    }
                    else
                    {
                        // Vectorized: broadcast the scalar pointer to
                        // all lanes (the view pointer is shared).
                        context.WriteLine(
                            $"CPUVectorIntrinsics.Broadcast<{castType}>(" +
                            $"{castResult}, ({castType})(nint)" +
                            $"{sourceExpr});");
                    }
                }
                break;

            case PureValue pureValue:
                EmitPureValue(pureValue, analysis);
                break;

            case BasicBlockValue bbValue:
                EmitBasicBlockValue(bbValue, analysis);
                break;
        }
    }

    /// <summary>
    /// Emits a pure value as a variable assignment if needed.
    /// </summary>
    private void EmitPureValue(PureValue value, VectorizationAnalysis analysis)
    {
        if (!context.NeedsVariable(value))
            return;

        var varName = context.GetValueName(value);

        // GetField values from decomposed multi-dim index parameters:
        // the decomposed array is already computed, just assign it to
        // the pool variable (or whatever varName was set to).
        if (value is GetField gf && gf.Source is Parameter p
            && p.Index == 0 && p.Type is StructureType gfSt
            && IsIndexStructType(gfSt)
            && p.Scope == context.Module.EntryPoint)
        {
            // The field's decomposed name was registered via SetValueName
            // in EmitIndexDecomposition. Read it back from the original
            // field name pattern.
            var fieldName = $"_idx_{StructureType.GetFieldName(gf.FieldSpan.Index)}";
            context.WriteLine($"{varName} = {fieldName};");
            return;
        }

        // Vectorized struct construction: when a StructureValue has any
        // vectorized (non-scalar) field, emit per-lane construction.
        if (value is StructureValue sv
            && HasVectorizedField(sv, analysis))
        {
            var structType = (StructureType)sv.Type;
            var typeName = new TypeEmitter(context).GetTypeName(structType);
            var fieldValues = new string[sv.Count];
            for (int i = 0; i < sv.Count; i++)
                fieldValues[i] = GetLaneOperand(sv.Values[i], analysis);

            context.WriteLine(
                "for (int _si = 0; _si < SIMDWidth; _si++)");
            context.OpenScope();
            var sb = new System.Text.StringBuilder();
            sb.Append($"{varName}[_si] = new {typeName} {{ ");
            for (int i = 0; i < sv.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var fieldName = StructureType.GetFieldName(i);
                var fieldVal = fieldValues[i];
                if (analysis.IsScalar(sv.Values[i]))
                    sb.Append($"{fieldName} = {fieldVal}");
                else
                    sb.Append($"{fieldName} = {fieldVal}[_si]");
            }
            sb.Append(" };");
            context.WriteLine(sb.ToString());
            context.CloseScope();
            return;
        }

        // Field extraction from a vectorized struct: read the field of every
        // lane's struct (a multi-field span forms a sub-struct)
        if (value is GetField getField && !analysis.IsScalar(getField.Source))
        {
            var source = context.GetValueName(getField.Source);
            var span = getField.FieldSpan;
            context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
            context.OpenScope();
            if (span.Span == 1)
            {
                var fieldName = StructureType.GetFieldName(span.Index);
                context.WriteLine($"{varName}[_si] = {source}[_si].{fieldName};");
            }
            else
            {
                var typeName = new TypeEmitter(context).GetTypeName(getField.Type);
                var fields = new System.Text.StringBuilder();
                for (int i = 0; i < span.Span; i++)
                {
                    if (i > 0)
                        fields.Append(", ");
                    fields.Append(
                        $"{StructureType.GetFieldName(i)} = {source}[_si]." +
                        StructureType.GetFieldName(span.Index + i));
                }
                context.WriteLine(
                    $"{varName}[_si] = new {typeName} {{ {fields} }};");
            }
            context.CloseScope();
            return;
        }

        // Field insertion: copy the source struct and overwrite the field
        if (value is SetField setField)
        {
            EmitSetField(setField, varName, analysis);
            return;
        }

        // Views that differ per lane are only formed by the operations above
        if (analysis.IsPerLaneView(value))
            throw UnsupportedPerLaneView(value);

        // Use the shared expression infrastructure for scalar pure values
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);
        var expr = exprEmitter.EmitDefinition(value);
        context.WriteLine($"{varName} = {expr};");
    }

    /// <summary>
    /// Returns the vectorization analysis of a method, or null if the
    /// analysis of the method is being computed (a recursive call).
    /// </summary>
    private VectorizationAnalysis? GetAnalysis(Method method)
    {
        if (_analyses.TryGetValue(method, out var analysis))
            return analysis;
        if (!_pendingAnalyses.Add(method))
            return null;

        // The entry point's first parameter (the thread index) is vectorized,
        // mapping to per-lane laneIdx. For grouped kernels, there is no index
        // parameter, so param0 is a real parameter and must NOT be vectorized.
        bool hasIndexParam = method == context.Module.EntryPoint
            && method.Parameters.Count > 0
            && (method.Parameters[0].Type is PrimitiveType
                    { BasicValueType: BasicValueType.Int32 }
                || (method.Parameters[0].Type is StructureType st
                    && IsIndexStructType(st)));
        analysis = new VectorizationAnalysis(
            method, vectorizeFirstParam: hasIndexParam, GetAnalysis);
        _pendingAnalyses.Remove(method);
        _analyses[method] = analysis;
        return analysis;
    }

    /// <summary>
    /// Returns true if values of the given C# types are structures or views
    /// that only differ in the address spaces of their views and pointers,
    /// which have the same representation on the CPU.
    /// </summary>
    private static bool IsLayoutConversion(string? from, string? to) =>
        from is not null && to is not null && from != to &&
        (from.StartsWith("struct_", StringComparison.Ordinal) &&
            to.StartsWith("struct_", StringComparison.Ordinal) ||
        from.StartsWith("CPURuntimeView<", StringComparison.Ordinal) &&
            to.StartsWith("CPURuntimeView<", StringComparison.Ordinal));

    /// <summary>
    /// Converts a uniform structure or view to another type of the same
    /// layout. Helper methods may take or return values whose types only
    /// differ in address spaces from the values at their call sites.
    /// </summary>
    private static string ConvertLayout(string expr, string? from, string? to) =>
        IsLayoutConversion(from, to)
            ? $"Unsafe.BitCast<{from}, {to}>({expr})"
            : expr;

    /// <summary>
    /// Converts a per-lane buffer of structures or views to a buffer of
    /// another type of the same layout (see <see cref="ConvertLayout"/>).
    /// </summary>
    private string ConvertLaneLayout(string buffer, string? from, string? to)
    {
        if (!IsLayoutConversion(from, to))
            return buffer;
        var converted = $"_layout_{_layoutCounter++}";
        context.WriteLine($"var {converted} = new {to}[SIMDWidth];");
        context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
        context.WriteLine(
            $"    {converted}[_si] = Unsafe.BitCast<{from}, {to}>({buffer}[_si]);");
        return converted;
    }

    /// <summary>
    /// Returns the text of an operand of a per-lane operation: the expression
    /// of a uniform operand, or the per-lane buffer of an operand that differs
    /// per lane. Views of local arrays become the views of the private arrays
    /// of all lanes.
    /// </summary>
    private string GetLaneOperand(Value operand, VectorizationAnalysis analysis)
    {
        if (analysis.IsScalar(operand))
        {
            return new ExpressionEmitter(context, new TypeEmitter(context))
                .EmitExpression(operand);
        }
        return operand.Type is ViewType
            ? _expressionEmitter!.ResolveViews(operand)
            : context.GetValueName(operand);
    }

    /// <summary>
    /// Returns the exception reporting a view that differs per lane, which
    /// the CPU backend cannot represent for the given value.
    /// </summary>
    private System.NotSupportedException UnsupportedPerLaneView(Value value) =>
        new($"The kernel '{context.Module.EntryPoint.Name}' uses a view that " +
            $"differs per lane in a '{value.GetType().Name}' operation, " +
            "which the CPU backend does not support.");

    /// <summary>
    /// Emits a sub-view of a view that differs per lane, or with an offset or
    /// length that differs per lane.
    /// </summary>
    private void EmitPerLaneSubView(
        SubView subView,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var result = context.GetValueName(subView);
        var source = expressionEmitter.ResolveViews(subView.Source);
        var offset = GetLaneOperand(subView.Offset, analysis);
        var length = GetLaneOperand(subView.Length, analysis);
        string Lane(Value operand, string text) =>
            analysis.IsScalar(operand) ? $"({text})" : $"{text}[_si]";
        context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
        context.WriteLine(
            $"    {result}[_si] = {source}[_si].SubView(" +
            $"{Lane(subView.Offset, offset)}, {Lane(subView.Length, length)});");
    }

    /// <summary>
    /// Emits a view of per-lane pointers or lengths.
    /// </summary>
    private void EmitPerLaneNewView(
        NewView newView,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        if (newView.Pointer.Type is not PointerType)
            throw UnsupportedPerLaneView(newView);
        var result = context.GetValueName(newView);
        var pointer = analysis.IsScalar(newView.Pointer)
            ? $"({GetLaneOperand(newView.Pointer, analysis)})"
            : $"{expressionEmitter.ResolveOperand(newView.Pointer)}[_si]";
        var length = GetLaneOperand(newView.Length, analysis);
        if (!analysis.IsScalar(newView.Length))
            length += "[_si]";
        var elemType = VectorizationAnalysis.GetScalarTypeName(
            newView.ViewElementType,
            (CPULanguageConfiguration)context.LanguageConfig);
        context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
        context.WriteLine(
            $"    {result}[_si] = new CPURuntimeView<{elemType}>(" +
            $"({elemType}*){pointer}, {length});");
    }

    /// <summary>
    /// Emits an address space or element type cast of a view that differs per
    /// lane. The views are copied, as the buffers of both values may be
    /// reused independently.
    /// </summary>
    private void EmitPerLaneViewCast(
        BaseAddressSpaceCast cast,
        CPUExpressionEmitter expressionEmitter)
    {
        var result = context.GetValueName(cast);
        var source = expressionEmitter.ResolveViews(cast.Source);
        var sourceType = GetVectorElementType(cast.Source);
        var targetType = GetVectorElementType(cast);
        context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
        context.WriteLine(sourceType == targetType
            ? $"    {result}[_si] = {source}[_si];"
            : $"    {result}[_si] = Unsafe.BitCast<{sourceType}, {targetType}>(" +
                $"{source}[_si]);");
    }

    /// <summary>
    /// Emits a call of a vectorized helper method. Uniform arguments of
    /// per-lane parameters are broadcast, and the helper runs for the active
    /// lanes of the caller.
    /// </summary>
    private void EmitHelperCall(
        MethodCall call,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var target = call.Target;
        var targetAnalysis = GetAnalysis(target)
            ?? throw new NotSupportedException(
                $"Recursive calls of '{target.Name}' are not supported by the " +
                "CPU backend.");
        var exprEmitter = new ExpressionEmitter(context, new TypeEmitter(context));
        var arguments = new List<string>();
        for (int i = 0; i < call.Arguments.Length; i++)
        {
            var arg = call.Arguments[i];
            var param = target.Parameters[i];
            bool perLaneParam = !targetAnalysis.IsScalar(param);
            var paramType = GetVectorElementType(param);
            if (!analysis.IsScalar(arg))
            {
                var buffer = arg.Type is ViewType
                    ? expressionEmitter.ResolveViews(arg)
                    : expressionEmitter.ResolveOperand(arg);
                arguments.Add(ConvertLaneLayout(
                    buffer, GetVectorElementType(arg), paramType));
                continue;
            }
            var expr = ConvertLayout(
                exprEmitter.EmitExpression(arg),
                GetVectorElementType(arg),
                paramType);
            if (perLaneParam)
            {
                var elemType = paramType ?? "int";
                expr = $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                    $"{expr}, SIMDWidth)";
            }
            arguments.Add(expr);
        }
        arguments.Add(CPUExpressionEmitter.ActiveMaskName);
        arguments.Add("laneIdx");

        var callText =
            $"{context.GetValueName(target)}({string.Join(", ", arguments)})";
        if (call.Type is VoidType || !context.NeedsVariable(call))
        {
            context.WriteLine($"{callText};");
            return;
        }
        // Copy the per-lane results instead of rebinding the buffer
        var result = context.GetValueName(call);
        context.WriteLine($"{callText}.CopyTo({result}, 0);");
    }

    /// <summary>
    /// Emits a field insertion into a (possibly vectorized) struct value.
    /// </summary>
    private void EmitSetField(
        SetField setField,
        string varName,
        VectorizationAnalysis analysis)
    {
        bool vectorized = !analysis.IsScalar(setField);
        var lane = vectorized ? "[_si]" : "";
        string Operand(Value operand)
        {
            var text = GetLaneOperand(operand, analysis);
            return analysis.IsScalar(operand) ? text : text + lane;
        }
        var source = Operand(setField.Source);
        var fieldValue = Operand(setField.Value);

        if (vectorized)
        {
            context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
            context.OpenScope();
        }
        var target = $"{varName}{lane}";
        context.WriteLine($"{target} = {source};");
        var span = setField.FieldSpan;
        if (span.Span == 1)
        {
            context.WriteLine(
                $"{target}.{StructureType.GetFieldName(span.Index)} = " +
                $"{fieldValue};");
        }
        else
        {
            for (int i = 0; i < span.Span; i++)
            {
                context.WriteLine(
                    $"{target}.{StructureType.GetFieldName(span.Index + i)} = " +
                    $"({fieldValue}).{StructureType.GetFieldName(i)};");
            }
        }
        if (vectorized)
            context.CloseScope();
    }

    /// <summary>
    /// Emits a basic block value (instruction) as a statement.
    /// </summary>
    private void EmitBasicBlockValue(
        BasicBlockValue value,
        VectorizationAnalysis analysis)
    {
        if (value.Type is not VoidType && context.NeedsVariable(value))
        {
            var varName = context.GetValueName(value);
            var typeEmitter = new TypeEmitter(context);
            var exprEmitter = new ExpressionEmitter(context, typeEmitter);
            var expr = exprEmitter.EmitDefinition(value);
            context.WriteLine($"{varName} = {expr};");
        }
        else
        {
            var typeEmitter = new TypeEmitter(context);
            var exprEmitter = new ExpressionEmitter(context, typeEmitter);
            var stmt = exprEmitter.EmitStatement(value);
            if (!string.IsNullOrEmpty(stmt))
                context.WriteLine(stmt);
        }
    }

    #endregion

    #region Masked Control Flow

    /// <summary>
    /// Emits a masked if-statement where both branches execute under masks.
    /// </summary>
    private void EmitMaskedIfStatement(
        IfStatement ifStmt,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var am = CPUExpressionEmitter.ActiveMaskName;

        // NOTE: The condition block has already been emitted by
        // EmitControlFlow as a standalone BasicBlock element.
        // ReconstructBlock adds it both as an element AND via the
        // IfStatement — we only emit it once (the standalone one).

        var condition = EmitConditionExpression(
            ifStmt.Condition, analysis, expressionEmitter);

        // Scalar-condition fast path: emit a native C# if/else, no masking needed
        if (analysis.IsScalar(ifStmt.Condition))
        {
            {
                // Collect only DIRECT blocks in each branch (not blocks
                // from nested sub-structures) for phi assignment. Nested
                // if/else chains handle their own phi assignments.
                var sTrueBlocks = GetIfBranchPhiSources(
                    ifStmt, isTrueBranch: true, direct: true);
                var sFalseBlocks = GetIfBranchPhiSources(
                    ifStmt, isTrueBranch: false, direct: true);

                context.WriteLine($"if ({condition})");
                context.OpenScope();
                EmitControlFlow(
                    ifStmt.TrueBranch, placement, analysis, expressionEmitter);
                if (ifStmt.MergeBlock != null)
                    EmitScalarPhiAssignments(
                        ifStmt.MergeBlock, sTrueBlocks, analysis);
                context.CloseScope();

                if (ifStmt.FalseBranch.Elements.Count > 0 ||
                    FeedsMergePhis(ifStmt.MergeBlock, sFalseBlocks))
                {
                    context.WriteLine("else");
                    context.OpenScope();
                    EmitControlFlow(
                        ifStmt.FalseBranch, placement, analysis, expressionEmitter);
                    if (ifStmt.MergeBlock != null)
                        EmitScalarPhiAssignments(
                            ifStmt.MergeBlock, sFalseBlocks, analysis);
                    context.CloseScope();
                }
            }
            return;
        }

        // Vectorized condition: emit masked execution for divergent branches
        var maskId = _maskCounter++;
        var mask = $"mask_{maskId}";
        var savedMask = $"savedMask_{maskId}";

        // Compute mask from condition (bool[] from EmitCompare)
        context.WriteLine($"var {mask} = {condition};");

        // Save current mask
        context.WriteLine(
            $"var {savedMask} = CPUVectorIntrinsics.CopyMask({am});");
        context.WriteLine();

        // Pre-compute which blocks belong to each branch for phi Select emission
        var trueBlocks = GetIfBranchPhiSources(
            ifStmt, isTrueBranch: true, direct: false);
        var falseBlocks = GetIfBranchPhiSources(
            ifStmt, isTrueBranch: false, direct: false);

        // True branch: activeMask = savedMask & mask
        context.WriteLine("// True branch");
        context.WriteLine(
            $"CPUVectorIntrinsics.AndMask({am}, {savedMask}, {mask});");
        context.OpenScope();
        EmitControlFlow(
            ifStmt.TrueBranch, placement, analysis, expressionEmitter);
        if (ifStmt.MergeBlock != null)
            EmitPhiSelectsForBranch(ifStmt.MergeBlock, trueBlocks, am, analysis);
        context.CloseScope();

        // False branch: activeMask = savedMask & !mask
        if (ifStmt.FalseBranch.Elements.Count > 0 ||
            FeedsMergePhis(ifStmt.MergeBlock, falseBlocks))
        {
            context.WriteLine("// False branch");
            context.WriteLine(
                $"CPUVectorIntrinsics.AndNotMask(" +
                $"{am}, {savedMask}, {mask});");
            context.OpenScope();
            EmitControlFlow(
                ifStmt.FalseBranch, placement, analysis, expressionEmitter);
            if (ifStmt.MergeBlock != null)
                EmitPhiSelectsForBranch(ifStmt.MergeBlock, falseBlocks, am, analysis);
            context.CloseScope();
        }

        // Restore mask
        context.WriteLine("// Restore mask");
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Returns the scalar continue condition of the given loop, negating the
    /// header condition if the header exits the loop on true.
    /// </summary>
    private static string GetScalarContinueCondition(
        LoopStatement loop,
        string condition) =>
        loop.IsConditionInverted ? $"!({condition})" : condition;

    /// <summary>
    /// Returns the mask intrinsic that keeps the lanes continuing the given
    /// loop: <c>AndMask</c> or, if the header exits on true, <c>AndNotMask</c>.
    /// </summary>
    private static string GetContinueMaskIntrinsic(LoopStatement loop) =>
        loop.IsConditionInverted ? "AndNotMask" : "AndMask";

    /// <summary>
    /// Emits a masked while loop that continues while any lane is active.
    /// </summary>
    private void EmitMaskedWhileLoop(
        WhileLoop whileLoop,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var maskId = _maskCounter++;
        var savedMask = $"savedMask_{maskId}";
        var am = CPUExpressionEmitter.ActiveMaskName;

        // Pre-loop phi initialization and back-edge registration
        var wConfig = (CPULanguageConfiguration)context.LanguageConfig;
        var wTypeEmitter = new TypeEmitter(context);
        var wExprEmitter = new ExpressionEmitter(context, wTypeEmitter);
        var loopPhis = GetLoopNonInductionPhis(whileLoop);
        foreach (var (phi, preHeader, _) in loopPhis)
        {
            var phiVar = context.GetValueName(phi);
            string initVal;
            if (preHeader is PrimitiveValue || !context.NeedsVariable(preHeader))
                initVal = wExprEmitter.EmitExpression(preHeader);
            else
                initVal = context.GetValueName(preHeader);

            EmitPhiAssignment(phi, preHeader, phiVar, initVal, analysis);
            _handledMergePhis.Add(phi);
        }

        // Identify the header block
        var headerBlock = whileLoop.Loop.Headers[0];

        // Save mask before loop
        context.WriteLine(
            $"var {savedMask} = CPUVectorIntrinsics.CopyMask({am});");

        // Pre-loop: emit header values for initial condition
        EmitBasicBlockValuesOnly(
            headerBlock, placement, analysis, expressionEmitter);

        // Pre-loop condition check
        var condition = EmitConditionExpression(
            whileLoop.Condition, analysis, expressionEmitter);
        if (analysis.IsScalar(whileLoop.Condition))
        {
            context.WriteLine(
                $"while ({GetScalarContinueCondition(whileLoop, condition)})");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.{GetContinueMaskIntrinsic(whileLoop)}(" +
                $"{am}, {am}, {condition});");
            context.WriteLine(
                $"while (CPUVectorIntrinsics.Any({am}))");
        }
        context.OpenScope();

        // Emit loop body, skipping the header block
        EmitControlFlowSkipping(
            whileLoop.Body, placement, analysis, expressionEmitter,
            headerBlock);

        // Emit back-edge Selects for loop-carried phis (am = continuing lanes)
        EmitLoopPhiBackEdges(
            loopPhis, analysis, wConfig, wExprEmitter, am);

        // Re-compute header values for next iteration's condition.
        // forceNeedsVariable avoids re-declaring with 'var'.
        EmitBasicBlockValuesOnly(
            headerBlock, placement, analysis, expressionEmitter,
            forceNeedsVariable: true);

        // Bottom condition check: lanes that continue are the currently-
        // active lanes that still satisfy the condition. Using the current
        // activeMask (am) — not savedMask — is essential: lanes that have
        // already exited the loop must stay exited, even if they would
        // spuriously re-satisfy the condition (e.g., a CAS-retry loop
        // where inactive lanes' CAS returns default(T) which !=
        // previous-current).
        if (!analysis.IsScalar(whileLoop.Condition))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.{GetContinueMaskIntrinsic(whileLoop)}(" +
                $"{am}, {am}, {condition});");

            // Break handling: scan the header's PureValues for
            // CompareValues besides the loop condition. These represent
            // break/continue conditions from inner branches. Exclude
            // breaking lanes from the continuation mask.
            EmitBreakExclusion(whileLoop.Loop, headerBlock, analysis, am);

            context.WriteLine(
                $"if (!CPUVectorIntrinsics.Any({am})) break;");
        }
        else
        {
            context.WriteLine(
                $"if (!({GetScalarContinueCondition(whileLoop, condition)})) break;");
        }

        context.CloseScope();

        // Restore mask after loop
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Emits a masked for loop with induction variable setup.
    /// </summary>
    private void EmitMaskedForLoop(
        ForLoop forLoop,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var maskId = _maskCounter++;
        var savedMask = $"savedMask_{maskId}";
        var am = CPUExpressionEmitter.ActiveMaskName;

        var inductionVar = forLoop.InductionVariable;
        var varName = context.GetValueName(inductionVar.Phi);

        // Suppress EmitPhiValue for the induction variable (already handled below)
        _handledMergePhis.Add(inductionVar.Phi);

        // Pre-loop init for other loop-carried phis
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);
        var loopPhis = GetLoopNonInductionPhis(forLoop, skipPhi: inductionVar.Phi);
        foreach (var (phi, preHeader, _) in loopPhis)
        {
            var phiVar = context.GetValueName(phi);
            string initVal;
            if (preHeader is PrimitiveValue || !context.NeedsVariable(preHeader))
                initVal = exprEmitter.EmitExpression(preHeader);
            else
                initVal = context.GetValueName(preHeader);

            EmitPhiAssignment(phi, preHeader, phiVar, initVal, analysis);
            _handledMergePhis.Add(phi);
        }

        // Emit induction variable initialization
        var initial = exprEmitter.EmitExpression(inductionVar.InitialValue);
        if (!analysis.IsScalar(inductionVar.Phi)
            && analysis.IsScalar(inductionVar.InitialValue))
        {
            var et = GetVectorElementType(inductionVar.Phi) ?? "int";
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{et}>(" +
                $"{varName}, {initial});");
        }
        else
        {
            context.WriteLine($"{varName} = {initial};");
        }

        // Identify the header block — its values compute the loop condition.
        var headerBlock = forLoop.Loop.Headers[0];

        // Save mask before loop
        context.WriteLine(
            $"var {savedMask} = CPUVectorIntrinsics.CopyMask({am});");

        // Pre-loop: emit header values once to compute the initial condition.
        // This is needed because pool.Clear zeros all buffers — the condition
        // buffer is all-false until the header runs.
        EmitBasicBlockValuesOnly(
            headerBlock, placement, analysis, expressionEmitter);

        // Pre-loop condition check — skip the loop entirely if false
        var condition = EmitConditionExpression(
            forLoop.Condition, analysis, expressionEmitter);
        if (analysis.IsScalar(forLoop.Condition))
        {
            context.WriteLine(
                $"while ({GetScalarContinueCondition(forLoop, condition)})");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.{GetContinueMaskIntrinsic(forLoop)}(" +
                $"{am}, {savedMask}, {condition});");
            context.WriteLine(
                $"while (CPUVectorIntrinsics.Any({am}))");
        }
        context.OpenScope();

        // Emit loop body, skipping the header block
        EmitControlFlowSkipping(
            forLoop.Body, placement, analysis, expressionEmitter,
            headerBlock);

        // Emit back-edge Selects for non-induction loop-carried phis
        EmitLoopPhiBackEdges(
            loopPhis, analysis, config, exprEmitter, am);

        // Emit induction variable increment
        var step = exprEmitter.EmitExpression(inductionVar.StepValue);
        if (!analysis.IsScalar(inductionVar.Phi))
        {
            // Vectorized induction variable: use CPUMathIntrinsics
            var indType = GetVectorElementType(inductionVar.Phi) ?? "int";
            var bcastStep = $"_step_{maskId}";
            context.WriteLine(
                $"var {bcastStep} = new {indType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{indType}>(" +
                $"{bcastStep}, {step});");
            var addMethod = inductionVar.IsIncrement ? "Add" : "Subtract";
            context.WriteLine(
                $"CPUMathIntrinsics.{addMethod}<{indType}>(" +
                $"{varName}, {varName}, {bcastStep});");
        }
        else
        {
            var incrementOp = inductionVar.IsIncrement ? "+=" : "-=";
            context.WriteLine($"{varName} {incrementOp} {step};");
        }

        // Re-compute header values for next iteration's condition.
        // forceNeedsVariable avoids re-declaring with 'var'.
        EmitBasicBlockValuesOnly(
            headerBlock, placement, analysis, expressionEmitter,
            forceNeedsVariable: true);

        // Bottom condition check: AND with the current activeMask (as the
        // while-loop emitter does) so that lanes that already left the loop
        // through a break stay retired instead of being revived from the
        // pre-loop mask.
        if (!analysis.IsScalar(forLoop.Condition))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.{GetContinueMaskIntrinsic(forLoop)}(" +
                $"{am}, {am}, {condition});");

            // Break handling: before excluding break lanes, capture
            // the header's computed values for lanes that are about to
            // break. These lanes need their final accumulated values.
            bool hasBreakers = false;
            foreach (var b in forLoop.Loop.Breakers)
                if (b != headerBlock) { hasBreakers = true; break; }

            if (hasBreakers)
            {
                foreach (var (phi, _, backEdge) in loopPhis)
                {
                    if (analysis.IsScalar(phi))
                        continue; // scalar phis already captured
                    var phiVar = context.GetValueName(phi);
                    var newVal = context.GetValueName(backEdge);
                    var selType = GetVectorElementType(phi) ?? "int";
                    // Capture final values for ALL active lanes (including
                    // those about to break). Break lanes keep this value;
                    // continuing lanes will overwrite at the next Select.
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Select<{selType}>(" +
                        $"{phiVar}, {am}, {newVal}, {phiVar});");
                }
            }

            EmitBreakExclusion(forLoop.Loop, headerBlock, analysis, am);

            context.WriteLine(
                $"if (!CPUVectorIntrinsics.Any({am})) break;");
        }
        else
        {
            context.WriteLine(
                $"if (!({GetScalarContinueCondition(forLoop, condition)})) break;");
        }

        context.CloseScope();

        // Restore mask after loop
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Emits a masked do-while loop.
    /// </summary>
    private void EmitMaskedDoWhileLoop(
        DoWhileLoop doWhileLoop,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var maskId = _maskCounter++;
        var savedMask = $"savedMask_{maskId}";
        var am = CPUExpressionEmitter.ActiveMaskName;

        // Pre-loop phi initialization
        var loopPhis = GetLoopNonInductionPhis(doWhileLoop);
        foreach (var (phi, preHeader, _) in loopPhis)
        {
            var phiVar = context.GetValueName(phi);
            var initVal = context.GetValueName(preHeader);
            EmitPhiAssignment(phi, preHeader, phiVar, initVal, analysis);
            _handledMergePhis.Add(phi);
        }

        // Save mask before loop
        context.WriteLine(
            $"var {savedMask} = CPUVectorIntrinsics.CopyMask({am});");

        // Do-while: execute body first, then check condition
        context.WriteLine("do");
        context.OpenScope();

        // Emit loop body
        EmitControlFlow(
            doWhileLoop.Body, placement, analysis, expressionEmitter);

        // Emit back-edge Selects before condition filters the mask
        // (all lanes that ran the body in this iteration must update their phi)
        var dwConfig = (CPULanguageConfiguration)context.LanguageConfig;
        var dwPhiNames = SnapshotLoopPhis(loopPhis, analysis);
        foreach (var (phi, _, backEdge) in loopPhis)
        {
            var phiVar = dwPhiNames[phi];
            var newVal = context.GetValueName(backEdge);
            var selT = GetVectorElementType(phi) ?? "int";
            context.WriteLine(
                $"CPUVectorIntrinsics.Select<{selT}>(" +
                $"{phiVar}, {am}, {newVal}, {phiVar});");
        }
        RestoreLoopPhiNames(dwPhiNames);

        // Evaluate condition and combine with mask
        var condition = EmitConditionExpression(
            doWhileLoop.Condition, analysis, expressionEmitter);
        if (analysis.IsScalar(doWhileLoop.Condition))
        {
            // Scalar condition: all lanes agree — use the condition directly
            context.CloseScope();
            context.WriteLine(
                $"while ({GetScalarContinueCondition(doWhileLoop, condition)});");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.{GetContinueMaskIntrinsic(doWhileLoop)}(" +
                $"{am}, {am}, {condition});");
            context.CloseScope();
            context.WriteLine(
                $"while (CPUVectorIntrinsics.Any({am}));");
        }

        // Restore mask after loop
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Emits a masked switch statement where each case executes under its mask.
    /// </summary>
    private void EmitMaskedSwitchStatement(
        SwitchStatement switchStmt,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var maskId = _maskCounter++;
        var savedMask = $"savedMask_{maskId}";
        var am = CPUExpressionEmitter.ActiveMaskName;

        // NOTE: The switch condition block was already emitted by
        // EmitControlFlow as a standalone BasicBlock element.

        var condition = EmitConditionExpression(
            switchStmt.Condition, analysis, expressionEmitter);

        // Scalar switch: emit plain C# switch with phi assignments
        if (analysis.IsScalar(switchStmt.Condition))
        {
            context.WriteLine($"switch ({condition})");
            context.OpenScope();
            foreach (var (caseValue, body) in switchStmt.Cases)
            {
                context.WriteLine($"case {caseValue}:");
                context.OpenScope();
                EmitControlFlow(body, placement, analysis, expressionEmitter);
                if (switchStmt.MergeBlock != null)
                {
                    EmitScalarPhiAssignments(
                        switchStmt.MergeBlock,
                        CollectCaseBlocks(switchStmt, body, direct: true),
                        analysis);
                }
                context.WriteLine("break;");
                context.CloseScope();
            }
            if (switchStmt.DefaultCase.Elements.Count > 0 ||
                HasDirectMergeEdge(switchStmt))
            {
                context.WriteLine("default:");
                context.OpenScope();
                EmitControlFlow(
                    switchStmt.DefaultCase, placement, analysis,
                    expressionEmitter);
                if (switchStmt.MergeBlock != null)
                {
                    EmitScalarPhiAssignments(
                        switchStmt.MergeBlock,
                        CollectCaseBlocks(
                            switchStmt, switchStmt.DefaultCase, direct: true),
                        analysis);
                }
                context.WriteLine("break;");
                context.CloseScope();
            }
            context.CloseScope();
            return;
        }
        var swConfig = (CPULanguageConfiguration)context.LanguageConfig;
        var condType = switchStmt.Condition.Type is PrimitiveType cpt
            ? swConfig.GetPrimitiveTypeName(cpt.BasicValueType) : "int";

        // Save current mask
        context.WriteLine(
            $"var {savedMask} = CPUVectorIntrinsics.CopyMask({am});");
        context.WriteLine();

        // Emit each case with its mask (unique caseMask per case)
        int caseIdx = 0;
        foreach (var (caseValue, body) in switchStmt.Cases)
        {
            var caseMask = $"caseMask_{maskId}_{caseIdx}";
            context.WriteLine($"// Case {caseValue}");
            context.WriteLine(
                $"var {caseMask} = CPUMathIntrinsics.Equal<{condType}>(" +
                $"{condition}, CPUVectorIntrinsics.Broadcast<{condType}>(" +
                $"{caseValue}, SIMDWidth));");
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask(" +
                $"{am}, {savedMask}, {caseMask});");
            context.OpenScope();
            EmitControlFlow(
                body, placement, analysis, expressionEmitter);
            if (switchStmt.MergeBlock != null)
            {
                EmitPhiSelectsForBranch(
                    switchStmt.MergeBlock,
                    CollectCaseBlocks(switchStmt, body),
                    am,
                    analysis);
            }
            context.CloseScope();
            caseIdx++;
        }

        // Emit default case (also when it is empty but its lanes still
        // select their incoming phi values on the direct edge to the merge)
        if (switchStmt.DefaultCase.Elements.Count > 0 ||
            HasDirectMergeEdge(switchStmt))
        {
            context.WriteLine("// Default case");
            context.WriteLine(
                $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
            // Mask out all matched cases
            foreach (var (caseValue, _) in switchStmt.Cases)
            {
                context.WriteLine(
                    $"CPUVectorIntrinsics.AndNotMask({am}, {am}, " +
                    $"CPUMathIntrinsics.Equal<{condType}>({condition}, " +
                    $"CPUVectorIntrinsics.Broadcast<{condType}>(" +
                    $"{caseValue}, SIMDWidth)));");
            }
            context.OpenScope();
            EmitControlFlow(
                switchStmt.DefaultCase, placement, analysis,
                expressionEmitter);
            if (switchStmt.MergeBlock != null)
            {
                EmitPhiSelectsForBranch(
                    switchStmt.MergeBlock,
                    CollectCaseBlocks(switchStmt, switchStmt.DefaultCase),
                    am,
                    analysis);
            }
            context.CloseScope();
        }

        // Restore mask
        context.WriteLine("// Restore mask");
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Returns the blocks of an if branch whose edges into the merge block
    /// feed its phis. An empty branch reaches the merge block either directly
    /// (the if block is the phi source) or through an empty forwarding block
    /// that is shared with another path and emitted elsewhere.
    /// </summary>
    private static HashSet<BasicBlock> GetIfBranchPhiSources(
        IfStatement ifStmt,
        bool isTrueBranch,
        bool direct)
    {
        var branch = isTrueBranch ? ifStmt.TrueBranch : ifStmt.FalseBranch;
        var blocks = new HashSet<BasicBlock>();
        if (direct)
            CollectDirectBlocks(branch, blocks);
        else
            CollectBlocks(branch, blocks, ifStmt.MergeBlock);

        var merge = ifStmt.MergeBlock;
        if (merge is null || branch.Elements.Count > 0 ||
            ifStmt.Block.TerminationKind != BlockTerminationKind.Conditional ||
            IsLoopExit(ifStmt.Block, merge))
        {
            // Loop exits (breaks) are handled by the masked loop emitters,
            // which retire breaking lanes before the branch body runs.
            return blocks;
        }

        var view = ifStmt.Block.AsConditionalView();
        var target = isTrueBranch ? view.TrueTarget : view.FalseTarget;
        if (target == merge)
            blocks.Add(ifStmt.Block);
        else if (IsForwardingBlock(target, merge))
            blocks.Add(target);
        return blocks;
    }

    /// <summary>
    /// Returns true if <paramref name="block"/> lies on a cycle that does not
    /// pass through <paramref name="merge"/>, i.e. the edge into the merge
    /// block leaves a loop.
    /// </summary>
    private static bool IsLoopExit(BasicBlock block, BasicBlock merge)
    {
        var visited = new HashSet<BasicBlock>();
        var stack = new Stack<BasicBlock>();
        foreach (var successor in block.Successors)
            stack.Push(successor);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == block)
                return true;
            if (current == merge || !visited.Add(current))
                continue;
            foreach (var successor in current.Successors)
                stack.Push(successor);
        }
        return false;
    }

    /// <summary>
    /// Returns true if the block holds no values and only branches to
    /// <paramref name="merge"/>.
    /// </summary>
    private static bool IsForwardingBlock(BasicBlock block, BasicBlock merge) =>
        block.FirstPhiValue is null &&
        block.FirstValue is null &&
        block.TerminationKind == BlockTerminationKind.Unconditional &&
        block.Successors.Length == 1 &&
        block.Successors[0] == merge;

    /// <summary>
    /// Returns true if any phi of the merge block has an incoming value from
    /// one of the given blocks.
    /// </summary>
    private static bool FeedsMergePhis(
        BasicBlock? merge,
        HashSet<BasicBlock> sources)
    {
        if (merge is null || sources.Count == 0)
            return false;
        foreach (var phi in merge.PhiValues)
        {
            for (int i = 0; i < phi.NumArguments; ++i)
            {
                if (sources.Contains(phi.Sources[i]))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the blocks of a switch case whose edges into the merge block
    /// feed its phis. An empty case branches directly from the switch block
    /// to the merge block, so the switch block itself is the phi source.
    /// </summary>
    private static HashSet<BasicBlock> CollectCaseBlocks(
        SwitchStatement switchStmt,
        ControlFlowStructure body,
        bool direct = false)
    {
        var blocks = new HashSet<BasicBlock>();
        if (direct)
            CollectDirectBlocks(body, blocks);
        else
            CollectBlocks(body, blocks, switchStmt.MergeBlock);
        if (body.Elements.Count == 0)
            blocks.Add(switchStmt.Block);
        return blocks;
    }

    /// <summary>
    /// Returns true if the switch block branches directly to the merge block
    /// and the merge block has phis fed over that edge.
    /// </summary>
    private static bool HasDirectMergeEdge(SwitchStatement switchStmt)
    {
        if (switchStmt.MergeBlock is null)
            return false;
        foreach (var phi in switchStmt.MergeBlock.PhiValues)
        {
            for (int i = 0; i < phi.NumArguments; ++i)
            {
                if (phi.Sources[i] == switchStmt.Block)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Collects all <see cref="BasicBlock"/> instances reachable within a
    /// control flow structure into <paramref name="result"/>.
    /// </summary>
    private static void CollectBlocks(
        ControlFlowStructure structure,
        HashSet<BasicBlock> result,
        BasicBlock? ownMerge = null)
    {
        foreach (var element in structure.Elements)
        {
            switch (element)
            {
                case BasicBlock block:
                    result.Add(block);
                    break;
                // Nested statements merging into the same block select their
                // own incoming phi values under their narrower masks.
                case IfStatement nested when
                    ownMerge is null || nested.MergeBlock != ownMerge:
                    CollectBlocks(nested.TrueBranch, result, ownMerge);
                    CollectBlocks(nested.FalseBranch, result, ownMerge);
                    break;
                case LoopStatement loop:
                    CollectBlocks(loop.Body, result, ownMerge);
                    break;
                case SwitchStatement sw when
                    ownMerge is null || sw.MergeBlock != ownMerge:
                    foreach (var (_, body) in sw.Cases)
                        CollectBlocks(body, result, ownMerge);
                    CollectBlocks(sw.DefaultCase, result, ownMerge);
                    break;
                case UnstructuredRegion ur:
                    foreach (var b in ur.Blocks)
                        result.Add(b);
                    break;
            }
        }
    }

    /// <summary>
    /// Collects only the direct BasicBlocks at the top level of a structure,
    /// without recursing into sub-structures (IfStatement, loops, etc.).
    /// This is used for scalar phi assignments so that nested if/else chains
    /// don't cause duplicate assignments at outer nesting levels.
    /// </summary>
    private static void CollectDirectBlocks(
        ControlFlowStructure structure,
        HashSet<BasicBlock> result)
    {
        foreach (var element in structure.Elements)
        {
            if (element is BasicBlock block)
                result.Add(block);
        }
    }

    /// <summary>
    /// At the end of a scalar if/else branch, emits phi assignments for the
    /// merge block. For scalar incoming values assigned to vectorized phi vars,
    /// broadcasts the scalar to the vectorized target.
    /// </summary>
    /// <summary>
    /// Emits back-edge updates for loop-carried phi values. When the
    /// back-edge is a Predicate (select), decomposes it into a vectorized
    /// Select operation instead of relying on scalar ternary inlining.
    /// </summary>
    private void EmitLoopPhiBackEdges(
        List<(PhiValue Phi, Value PreHeader, Value BackEdge)> loopPhis,
        VectorizationAnalysis analysis,
        CPULanguageConfiguration config,
        ExpressionEmitter exprEmitter,
        string activeMask)
    {
        var phiNames = SnapshotLoopPhis(loopPhis, analysis);
        foreach (var (phi, _, backEdge) in loopPhis)
        {
            var phiVar = phiNames[phi];

            // PhiValue back-edge with self-reference: the inner phi merges
            // a "continue" path (self = old value) and an "accumulate" path
            // (new value like sum+i). Emit a vectorized Select using the
            // inner phi's source block condition.
            if (backEdge is PhiValue innerPhi
                && innerPhi.NumArguments == 2
                && !analysis.IsScalar(phi))
            {
                // Find which argument is the self-reference (continue path)
                int selfIdx = -1;
                for (int a = 0; a < 2; a++)
                    if (innerPhi.Arguments[a] == phi) selfIdx = a;

                if (selfIdx >= 0)
                {
                    int otherIdx = 1 - selfIdx;
                    var otherArg = innerPhi.Arguments[otherIdx];
                    var otherVal = context.NeedsVariable(otherArg)
                        ? context.GetValueName(otherArg)
                        : exprEmitter.EmitExpression(otherArg);

                    // Find the condition: the inner phi's source blocks
                    // correspond to the if/else branches. The source block
                    // for the NON-self argument tells us which branch
                    // computed the new value. The if/else condition for
                    // that branch is in the block's termination.
                    // For simplicity, find the CompareValue that controls
                    // the if/else by checking which bool[] was used as
                    // the condition.
                    var otherBlock = innerPhi.Sources[otherIdx];
                    string? condName = null;
                    bool isLoopExit = false;
                    // Walk predecessors to find the conditional branch
                    foreach (var predBlock in otherBlock.Predecessors)
                    {
                        if (predBlock.TerminationKind == BlockTerminationKind.Conditional
                            && predBlock.TerminationValue is not null)
                        {
                            // Check if this predecessor is a loop header
                            // (has a back-edge from one of its successors'
                            // successors). If so, the condition is stale
                            // after loop exit — use activeMask instead.
                            foreach (var pSucc in predBlock.Successors)
                            {
                                foreach (var ppSucc in pSucc.Successors)
                                {
                                    if (ppSucc == predBlock)
                                    {
                                        isLoopExit = true;
                                        break;
                                    }
                                }
                                if (isLoopExit) break;
                            }
                            if (isLoopExit) break;

                            var tv = predBlock.TerminationValue;
                            condName = context.NeedsVariable(tv)
                                ? context.GetValueName(tv)
                                : exprEmitter.EmitExpression(tv);
                            // Check branch direction: does True→otherBlock?
                            if (predBlock.Successors.Length >= 2
                                && predBlock.Successors[0] != otherBlock)
                            {
                                // True goes elsewhere → otherBlock is False
                                // Negate: use NOT condition
                                var negCond = $"_negcond_{phi.Id}";
                                context.WriteLine(
                                    $"var {negCond} = " +
                                    $"CPUVectorIntrinsics.NotMask({condName});");
                                condName = negCond;
                            }
                            break;
                        }
                    }

                    // For loop exit conditions (inner loop completed),
                    // the inner phi's variable already holds the final
                    // accumulated value. Use a simple activeMask Select
                    // with the phi's own variable, not its arguments.
                    if (isLoopExit)
                    {
                        var innerPhiVar = context.NeedsVariable(backEdge)
                            ? context.GetValueName(backEdge)
                            : exprEmitter.EmitExpression(backEdge);
                        var elemType2 = GetVectorElementType(phi) ?? "int";
                        context.WriteLine(
                            $"CPUVectorIntrinsics.Select<{elemType2}>(" +
                            $"{phiVar}, {activeMask}, " +
                            $"{innerPhiVar}, {phiVar});");
                        continue;
                    }

                    if (condName != null)
                    {
                        var elemType = GetVectorElementType(phi) ?? "int";

                        // Broadcast scalar values if needed
                        if (analysis.IsScalar(otherArg))
                        {
                            var bcast = $"_selbcast_{phi.Id}";
                            context.WriteLine(
                                $"var {bcast} = new {elemType}[SIMDWidth];");
                            context.WriteLine(
                                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                                $"{bcast}, {otherVal});");
                            otherVal = bcast;
                        }

                        // Select: condition ? otherVal : phiVar
                        context.WriteLine(
                            $"CPUVectorIntrinsics.Select<{elemType}>(" +
                            $"{phiVar}, {condName}, " +
                            $"{otherVal}, {phiVar});");
                        continue;
                    }
                }
            }

            // Predicate (select) back-edge on a vectorized phi: decompose
            // into a vectorized Select using the Predicate's operands.
            if (backEdge is Predicate pred && !analysis.IsScalar(phi))
            {
                var elemType = GetVectorElementType(phi) ?? "int";

                // Resolve each operand: use pool name if vectorized,
                // broadcast if scalar, or inline expression.
                string ResolveForSelect(Value val)
                {
                    if (context.NeedsVariable(val))
                        return context.GetValueName(val);
                    return exprEmitter.EmitExpression(val);
                }

                var cond = ResolveForSelect(pred.Condition);
                var trueVal = ResolveForSelect(pred.TrueValue);
                var falseVal = ResolveForSelect(pred.FalseValue);
                bool isView = phi.Type is ViewType;
                if (isView)
                {
                    trueVal = _expressionEmitter!.ResolveViews(pred.TrueValue);
                    falseVal = _expressionEmitter!.ResolveViews(pred.FalseValue);
                }

                // Broadcast scalar condition to bool[] if needed
                if (analysis.IsScalar(pred.Condition))
                {
                    var bcast = $"_selcond_{phi.Id}";
                    context.WriteLine(
                        $"var {bcast} = new bool[SIMDWidth];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<bool>(" +
                        $"{bcast}, {cond});");
                    cond = bcast;
                }

                // Broadcast scalar true/false values
                if (!isView && analysis.IsScalar(pred.TrueValue))
                {
                    var bcast = $"_seltrue_{phi.Id}";
                    context.WriteLine(
                        $"var {bcast} = new {elemType}[SIMDWidth];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                        $"{bcast}, {trueVal});");
                    trueVal = bcast;
                }
                if (!isView && analysis.IsScalar(pred.FalseValue))
                {
                    var bcast = $"_selfalse_{phi.Id}";
                    context.WriteLine(
                        $"var {bcast} = new {elemType}[SIMDWidth];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                        $"{bcast}, {falseVal});");
                    falseVal = bcast;
                }

                context.WriteLine(
                    $"CPUVectorIntrinsics.Select<{elemType}>(" +
                    $"{phiVar}, {cond}, {trueVal}, {falseVal});");
            }
            else if (analysis.IsScalar(phi))
            {
                // Scalar phi: simple assignment
                var newVal = (analysis.IsScalar(backEdge)
                    || !context.NeedsVariable(backEdge))
                    ? exprEmitter.EmitExpression(backEdge)
                    : context.GetValueName(backEdge);
                context.WriteLine($"{phiVar} = {newVal};");
            }
            else
            {
                // Vectorized phi with non-Predicate back-edge
                var newVal = context.NeedsVariable(backEdge)
                    ? context.GetValueName(backEdge)
                    : exprEmitter.EmitExpression(backEdge);
                var selType = GetVectorElementType(phi) ?? "int";
                if (phi.Type is ViewType)
                {
                    // Uniform views and views of local arrays become the
                    // views of all lanes
                    newVal = _expressionEmitter!.ResolveViews(backEdge);
                }
                else if (analysis.IsScalar(backEdge))
                {
                    // Uniform back-edge values of phis that differ per lane
                    // (the lanes leave the loop in different iterations)
                    var bcast = $"_backedge_{phi.Id}";
                    context.WriteLine($"var {bcast} = new {selType}[SIMDWidth];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{selType}>(" +
                        $"{bcast}, {newVal});");
                    newVal = bcast;
                }
                context.WriteLine(
                    $"CPUVectorIntrinsics.Select<{selType}>(" +
                    $"{phiVar}, {activeMask}, {newVal}, {phiVar});");
            }
        }
        RestoreLoopPhiNames(phiNames);
    }

    /// <summary>
    /// Prepares the back-edge updates of the given loop phis, which must act
    /// as a parallel copy: copies every phi that the update of a later phi may
    /// read (e.g. when two phis swap their values) into a snapshot, and lets
    /// all expressions refer to the snapshot until the updates are done.
    /// </summary>
    /// <returns>The variable names of all phis to assign.</returns>
    private Dictionary<PhiValue, string> SnapshotLoopPhis(
        List<(PhiValue Phi, Value PreHeader, Value BackEdge)> loopPhis,
        VectorizationAnalysis analysis)
    {
        var phiNames = new Dictionary<PhiValue, string>();
        foreach (var (phi, _, _) in loopPhis)
            phiNames[phi] = context.GetValueName(phi);

        var assigned = new HashSet<Value>();
        var snapshots = new HashSet<PhiValue>();
        foreach (var (phi, _, backEdge) in loopPhis)
        {
            CollectReadPhis(backEdge, assigned, snapshots);
            if (backEdge != phi)
                assigned.Add(phi);
        }

        foreach (var phi in snapshots)
        {
            var snapshot = $"_phisnap_{phi.Id}_{_phiBroadcastId++}";
            var name = phiNames[phi];
            context.WriteLine(analysis.IsScalar(phi)
                ? $"var {snapshot} = {name};"
                : $"var {snapshot} = {name}.Clone() as " +
                    $"{GetVectorTypeName(phi)};");
            context.SetValueName(phi, snapshot);
        }
        return phiNames;
    }

    /// <summary>
    /// Returns the C# type of the vectorized buffer of the given phi.
    /// </summary>
    private string GetVectorTypeName(PhiValue phi)
    {
        var elementType = GetVectorElementType(phi) ?? "int";
        return $"{elementType}[]";
    }

    /// <summary>
    /// Adds all phis of <paramref name="assigned"/> that the given value may
    /// read to <paramref name="result"/>. Phis are stored in variables; all
    /// other operands may be inlined and are followed.
    /// </summary>
    private static void CollectReadPhis(
        Value value,
        HashSet<Value> assigned,
        HashSet<PhiValue> result)
    {
        if (assigned.Count == 0)
            return;
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        worklist.Push(value);
        while (worklist.TryPop(out var current))
        {
            if (!visited.Add(current))
                continue;
            if (current is PhiValue phi)
            {
                if (assigned.Contains(phi))
                    result.Add(phi);
                continue;
            }
            if (current is BasicBlock or Method or TypeValue)
                continue;
            foreach (var operand in current.Values)
                worklist.Push(operand);
        }
    }

    /// <summary>
    /// Restores the variable names of all phis after their back-edge updates.
    /// </summary>
    private void RestoreLoopPhiNames(Dictionary<PhiValue, string> phiNames)
    {
        foreach (var (phi, name) in phiNames)
            context.SetValueName(phi, name);
    }

    /// <summary>
    /// Emits AndNotMask/AndMask for inner break conditions in a loop.
    /// Breaker blocks (non-header blocks with exits outside the loop)
    /// have conditional branches where one successor exits the loop.
    /// The break condition is used to exclude those lanes from the
    /// loop continuation mask.
    /// </summary>
    private void EmitBreakExclusion(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock headerBlock,
        VectorizationAnalysis analysis,
        string activeMask)
    {
        foreach (var breaker in loop.Breakers)
        {
            if (breaker == headerBlock)
                continue;
            if (breaker.TerminationKind != BlockTerminationKind.Conditional)
                continue;
            var termCond = breaker.TerminationValue;
            if (termCond is null || analysis.IsScalar(termCond))
                continue;

            // The breaker has one successor inside the loop (continue)
            // and one outside (break). The termination value is the
            // branch condition: True→Successors[0], False→Successors[1].
            var succs = breaker.Successors;
            bool trueIsBreak = succs.Length >= 2
                && !loop.AllMembers.Contains(succs[0]);
            var breakCond = context.GetValueName(termCond);
            if (trueIsBreak)
                context.WriteLine(
                    $"CPUVectorIntrinsics.AndNotMask(" +
                    $"{activeMask}, {activeMask}, {breakCond});");
            else
                context.WriteLine(
                    $"CPUVectorIntrinsics.AndMask(" +
                    $"{activeMask}, {activeMask}, {breakCond});");
        }
    }

    private void EmitScalarPhiAssignments(
        BasicBlock mergeBlock,
        HashSet<BasicBlock> branchBlocks,
        VectorizationAnalysis analysis)
    {
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);

        foreach (var value in mergeBlock.Values)
        {
            if (value is not PhiValue phi)
                continue;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (!branchBlocks.Contains(phi.Sources[i]))
                    continue;

                var phiVar = context.GetValueName(phi);
                var arg = phi.Arguments[i];

                string source;
                if (arg is PrimitiveValue || !context.NeedsVariable(arg))
                    source = exprEmitter.EmitExpression(arg);
                else
                    source = context.GetValueName(arg);

                EmitPhiAssignment(phi, arg, phiVar, source, analysis);

                _handledMergePhis.Add(phi);
            }
        }
    }

    /// <summary>
    /// At the end of a branch, emits masked Select assignments for every phi in
    /// <paramref name="mergeBlock"/> whose corresponding predecessor block is
    /// contained in <paramref name="branchBlocks"/>. This ensures each lane
    /// receives the value from whichever branch it actually took.
    /// </summary>
    private void EmitPhiSelectsForBranch(
        BasicBlock mergeBlock,
        HashSet<BasicBlock> branchBlocks,
        string activeMask,
        VectorizationAnalysis analysis)
    {
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);

        foreach (var value in mergeBlock.Values)
        {
            if (value is not PhiValue phi)
                continue;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (!branchBlocks.Contains(phi.Sources[i]))
                    continue;

                var phiVar = context.GetValueName(phi);
                var arg = phi.Arguments[i];

                // Scalar incoming values (constants, parameters) need to
                // be broadcast before Select can use them.
                // Use VectorizationAnalysis to determine if the argument
                // is truly scalar — NeedsVariable is unreliable for
                // values like laneIdx that are already vectorized.
                string incoming;
                if (phi.Type is ViewType)
                {
                    // Views of local arrays become the views of the private
                    // arrays of all lanes
                    incoming = _expressionEmitter!.ResolveViews(arg);
                }
                else if (analysis.IsScalar(arg))
                {
                    var scalarExpr = exprEmitter.EmitExpression(arg);
                    var elemType = GetVectorElementType(phi) ?? "int";
                    var bcastName =
                        $"_phi_bcast_{phi.Id}_{i}_{_phiBroadcastId++}";
                    context.WriteLine(
                        $"var {bcastName} = new {elemType}[SIMDWidth];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                        $"{bcastName}, {scalarExpr});");
                    incoming = bcastName;
                }
                else
                {
                    incoming = context.GetValueName(arg);
                }

                var elemT = GetVectorElementType(phi) ?? "int";
                context.WriteLine(
                    $"CPUVectorIntrinsics.Select<{elemT}>(" +
                    $"{phiVar}, {activeMask}, {incoming}, {phiVar});");
                _handledMergePhis.Add(phi);
            }
        }
    }

    /// <summary>
    /// Returns all phi values in the loop header that are not <paramref name="skipPhi"/>,
    /// along with their pre-header and back-edge incoming values.
    /// Only simple 2-source phis are included (more exotic shapes are left to fallback).
    /// </summary>
    private static List<(PhiValue Phi, Value PreHeader, Value BackEdge)>
        GetLoopNonInductionPhis(LoopStatement loop, PhiValue? skipPhi = null)
    {
        var header = loop.Loop.Headers[0];
        var result = new List<(PhiValue, Value, Value)>();

        foreach (var value in header.Values)
        {
            if (value is not PhiValue phi) continue;
            if (phi == skipPhi) continue;
            if (phi.NumArguments != 2) continue;

            Value? preHeader = null, backEdge = null;
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (loop.Loop.AllMembers.Contains(phi.Sources[i]))
                    backEdge = phi.Arguments[i];
                else
                    preHeader = phi.Arguments[i];
            }

            if (preHeader != null && backEdge != null)
                result.Add((phi, preHeader, backEdge));
        }

        return result;
    }

    #region Unstructured Region (Masked State Machine)

    /// <summary>
    /// Emits an unstructured region as a masked per-lane state machine for CPU
    /// vectorized execution. Each lane has an independent state that progresses
    /// through the region's blocks.
    /// </summary>
    private void EmitMaskedUnstructuredRegion(
        UnstructuredRegion region,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var maskId = _maskCounter++;
        var savedMask = $"savedMask_{maskId}";
        var stateName = $"_regionState_{maskId}";
        var blockMask = $"_blockMask_{maskId}";
        var am = CPUExpressionEmitter.ActiveMaskName;
        int entryId = region.BlockIds[region.EntryBlock];
        int numBlocks = region.Blocks.Count;

        // Suppress EmitPhiValue for all phis in the region
        foreach (var block in region.Blocks)
            foreach (var value in block.Values)
                if (value is PhiValue phi)
                    _handledMergePhis.Add(phi);

        // Initialize phis with external sources
        foreach (var block in region.Blocks)
            EmitRegionExternalPhiInitsCPU(block, region);

        // Per-lane state tensor
        context.WriteLine(
            $"var {stateName} = CPUVectorIntrinsics.Broadcast<int>(" +
            $"{entryId}, SIMDWidth);");
        context.WriteLine(
            $"var {savedMask} = CPUVectorIntrinsics.CopyMask({am});");

        context.WriteLine("while (true)");
        context.OpenScope();

        // Active = savedMask & (state < numBlocks)
        // Exited lanes have state >= numBlocks (exit sentinels)
        var running = $"_running_{maskId}";
        context.WriteLine(
            $"var {running} = CPUMathIntrinsics.LessThan<int>(" +
            $"{stateName}, CPUVectorIntrinsics.Broadcast<int>(" +
            $"{numBlocks}, SIMDWidth));");
        context.WriteLine(
            $"CPUVectorIntrinsics.AndMask({am}, {savedMask}, {running});");
        context.WriteLine($"if (!CPUVectorIntrinsics.Any({am})) break;");
        context.WriteLine();

        // Emit each block as a masked section
        foreach (var block in region.Blocks)
        {
            int stateId = region.BlockIds[block];
            var blockName = context.GetValueName(block);
            context.WriteLine($"// State {stateId}: {blockName}");
            context.WriteLine(
                $"var {blockMask} = CPUMathIntrinsics.Equal<int>(" +
                $"{stateName}, CPUVectorIntrinsics.Broadcast<int>(" +
                $"{stateId}, SIMDWidth));");
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask({am}, {am}, {blockMask});");
            context.WriteLine($"if (CPUVectorIntrinsics.Any({am}))");
            context.OpenScope();

            // Emit block values under activeMask
            EmitBasicBlock(block, placement, analysis, expressionEmitter);

            // Emit state transition (masked per-lane update)
            EmitRegionTransitionCPU(
                block, region, stateName, am, analysis);
            context.CloseScope();

            // Restore activeMask for next block
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask({am}, {savedMask}, {running});");
            context.WriteLine();
        }

        context.CloseScope(); // while
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Emits masked per-lane state transitions for a block within an
    /// unstructured region on the CPU backend.
    /// </summary>
    private void EmitRegionTransitionCPU(
        BasicBlock block,
        UnstructuredRegion region,
        string stateName,
        string activeMask,
        VectorizationAnalysis analysis)
    {
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Unconditional:
                {
                    var succ = block.Successors[0];
                    EmitPhiTransitionsCPU(block, succ, region, activeMask);
                    int targetState = region.GetTargetState(succ);
                    context.WriteLine(
                        $"{stateName} = CPUVectorIntrinsics.Select<int>(" +
                        $"{activeMask}, CPUVectorIntrinsics.Broadcast<int>(" +
                        $"{targetState}, SIMDWidth), {stateName});");
                    break;
                }

            case BlockTerminationKind.Conditional:
                {
                    var view = block.AsConditionalView();
                    var condition = context.GetValueName(view.Condition);
                    int trueState = region.GetTargetState(view.TrueTarget);
                    int falseState = region.GetTargetState(view.FalseTarget);

                    // Compute per-lane next state
                    var nextState = $"_nextState_{_maskCounter}";
                    context.WriteLine(
                        $"var {nextState} = CPUVectorIntrinsics.Select<int>(" +
                        $"{condition}, CPUVectorIntrinsics.Broadcast<int>(" +
                        $"{trueState}, SIMDWidth), CPUVectorIntrinsics.Broadcast<int>(" +
                        $"{falseState}, SIMDWidth));");

                    // Phi transitions for true branch
                    var trueMask = $"_trueMask_{_maskCounter}";
                    context.WriteLine(
                        $"var {trueMask} = CPUVectorIntrinsics.AndMask(" +
                        $"{activeMask}, {condition});");
                    EmitPhiTransitionsCPU(
                        block, view.TrueTarget, region, trueMask);

                    // Phi transitions for false branch
                    var falseMask = $"_falseMask_{_maskCounter}";
                    context.WriteLine(
                        $"var {falseMask} = CPUVectorIntrinsics.AndNotMask(" +
                        $"{activeMask}, {condition});");
                    EmitPhiTransitionsCPU(
                        block, view.FalseTarget, region, falseMask);

                    // Update state
                    context.WriteLine(
                        $"{stateName} = CPUVectorIntrinsics.Select<int>(" +
                        $"{activeMask}, {nextState}, {stateName});");
                    _maskCounter++;
                    break;
                }

            case BlockTerminationKind.Switch:
                {
                    var view = block.AsSwitchView();
                    var condition = context.GetValueName(view.Condition);

                    // Start with default state
                    int defaultState = region.GetTargetState(view.DefaultTarget);
                    var nextState = $"_nextState_{_maskCounter}";
                    context.WriteLine(
                        $"var {nextState} = CPUVectorIntrinsics.Broadcast<int>(" +
                        $"{defaultState}, SIMDWidth);");

                    // Default mask starts as activeMask
                    var defaultMask = $"_defMask_{_maskCounter}";
                    context.WriteLine(
                        $"var {defaultMask} = CPUVectorIntrinsics.CopyMask(" +
                        $"{activeMask});");

                    // Per-case overrides
                    for (int i = 0; i < view.NumCases; i++)
                    {
                        var target = view.GetCaseTarget(i);
                        int caseState = region.GetTargetState(target);
                        var caseMask = $"_caseMask_{_maskCounter}_{i}";
                        context.WriteLine(
                            $"var {caseMask} = CPUVectorIntrinsics.AndMask(" +
                            $"{activeMask}, CPUMathIntrinsics.Equal<int>(" +
                            $"{condition}, CPUVectorIntrinsics.Broadcast<int>(" +
                            $"{i}, SIMDWidth)));");
                        context.WriteLine(
                            $"{nextState} = CPUVectorIntrinsics.Select<int>(" +
                            $"{caseMask}, CPUVectorIntrinsics.Broadcast<int>(" +
                            $"{caseState}, SIMDWidth), {nextState});");
                        EmitPhiTransitionsCPU(block, target, region, caseMask);
                        // Remove matched lanes from default mask
                        context.WriteLine(
                            $"CPUVectorIntrinsics.AndNotMask(" +
                            $"{defaultMask}, {defaultMask}, {caseMask});");
                    }

                    // Default case phi transitions
                    EmitPhiTransitionsCPU(
                        block, view.DefaultTarget, region, defaultMask);

                    // Update state
                    context.WriteLine(
                        $"{stateName} = CPUVectorIntrinsics.Select<int>(" +
                        $"{activeMask}, {nextState}, {stateName});");
                    _maskCounter++;
                    break;
                }

            case BlockTerminationKind.Return:
                {
                    // Set state to a sentinel >= numBlocks to mark lane as exited
                    int exitSentinel = region.Blocks.Count;
                    context.WriteLine(
                        $"{stateName} = CPUVectorIntrinsics.Select<int>(" +
                        $"{activeMask}, CPUVectorIntrinsics.Broadcast<int>(" +
                        $"{exitSentinel}, SIMDWidth), {stateName});");
                    break;
                }
        }
    }

    /// <summary>
    /// Emits masked phi assignments for the transition edge source → target
    /// within an unstructured region on the CPU backend.
    /// </summary>
    private void EmitPhiTransitionsCPU(
        BasicBlock source,
        BasicBlock target,
        UnstructuredRegion region,
        string mask)
    {
        if (region.IsExit(target))
            return;

        foreach (var value in target.Values)
        {
            if (value is not PhiValue phi) continue;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.Sources[i] != source) continue;

                var phiVar = context.GetValueName(phi);
                var incoming = context.GetValueName(phi.Arguments[i]);
                context.WriteLine(
                    $"CPUVectorIntrinsics.Select<int>(" +
                    $"{phiVar}, {mask}, {incoming}, {phiVar});");
            }
        }
    }

    /// <summary>
    /// Emits initial assignments for phis in a block whose sources are outside
    /// the unstructured region. These run before the state machine loop.
    /// </summary>
    private void EmitRegionExternalPhiInitsCPU(
        BasicBlock block,
        UnstructuredRegion region)
    {
        foreach (var value in block.Values)
        {
            if (value is not PhiValue phi) continue;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (region.BlockIds.ContainsKey(phi.Sources[i]))
                    continue;

                var phiVar = context.GetValueName(phi);
                var incoming = context.GetValueName(phi.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }
    }

    #endregion

    #endregion

    /// <summary>
    /// Emits a #line directive for debugging support.
    /// </summary>
    private void EmitLineDirective(Location location)
    {
        if (!emitDebugSymbols)
            return;

        if (location is FileLocation fileLocation)
        {
            context.WriteLine(
                $"#line {fileLocation.StartLine} \"{fileLocation.FileName}\"");
        }
    }
}
