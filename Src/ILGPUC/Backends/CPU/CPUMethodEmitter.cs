// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMethodEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
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
    private int _maskCounter;
    private HashSet<PhiValue> _handledMergePhis = [];

    /// <summary>
    /// Gets the generation context.
    /// </summary>
    public GenerationContext Context => context;

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
        _handledMergePhis = [];

        bool isEntryPoint = method == context.Module.EntryPoint;

        // Perform vectorization analysis — the entry point's first parameter
        // (the thread index) is vectorized, mapping to per-lane laneIdx.
        // For grouped kernels, there is no index parameter, so param0 is
        // a real parameter and must NOT be vectorized.
        bool hasIndexParam = isEntryPoint
            && method.Parameters.Count > 0
            && (method.Parameters[0].Type is PrimitiveType
                    { BasicValueType: BasicValueType.Int32 }
                || (method.Parameters[0].Type is StructureType st
                    && IsIndexStructType(st)));
        var analysis = new VectorizationAnalysis(
            method, vectorizeFirstParam: hasIndexParam);

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
            if (retVal is not null)
            {
                var retName = context.GetValueName(retVal);
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
        else
        {
            // Non-pooled or non-entry-point: create laneIdx locally
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
                $"Span<byte> {storageName} = stackalloc byte[{totalSize}];");
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
            var poolName = $"pool_{elemType}_{slotIndex}";
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
            var poolName = $"pool_{elemType}_{slotIndex}";
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
            var poolName = $"pool_{elemType}_{slotIndex}";
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
                if (value is PureValue && value.Uses.HasExactlyOne
                    && analysis.IsScalar(value))
                    continue;

                // Scalar values handled by inline 'var' in expression emitters
                if (analysis.IsScalar(value) && value is
                    (Load or BinaryArithmeticValue or CompareValue
                     or GenericAtomic or ConvertValue))
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

        // If the phi is vectorized but the source is scalar, broadcast
        if (!analysis.IsScalar(phi) && analysis.IsScalar(arg))
        {
            var config = (CPULanguageConfiguration)context.LanguageConfig;
            var elemType = phi.Type is PrimitiveType pt
                ? config.GetPrimitiveTypeName(pt.BasicValueType)
                : "int";
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{varName}, {source});");
        }
        else
        {
            context.WriteLine($"{varName} = {source};");
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
        if (value is PureValue && value.Uses.HasExactlyOne && analysis.IsScalar(value))
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

            case Predicate predicate
                when !analysis.IsScalar(predicate.Condition):
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
                // Vectorized GetViewLength: the length is a scalar
                // property but the pool buffer is an array — broadcast.
                // Resolve the source view expression directly to avoid
                // pool redirection of the GetViewLength value itself.
                {
                    var vlResult = context.GetValueName(viewLen);
                    var te = new TypeEmitter(context);
                    var ee = new ExpressionEmitter(context, te);
                    // Get the source view expression (not the viewLen's
                    // pool name) and append .Length
                    var srcExpr = ee.EmitExpression(viewLen.Source);
                    var lenField = context.LanguageConfig.ViewLengthFieldName;
                    var cfg = (CPULanguageConfiguration)
                        context.LanguageConfig;
                    var lenType = cfg.GetPrimitiveTypeName(
                        viewLen.BasicValueType);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{lenType}>(" +
                        $"{vlResult}, {srcExpr}.{lenField});");
                }
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

            context.WriteLine(
                "for (int _si = 0; _si < SIMDWidth; _si++)");
            context.OpenScope();
            var sb = new System.Text.StringBuilder();
            sb.Append($"{varName}[_si] = new {typeName} {{ ");
            for (int i = 0; i < sv.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var fieldName = StructureType.GetFieldName(i);
                var fieldVal = context.GetValueName(sv.Values[i]);
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

        // Use the shared expression infrastructure for scalar pure values
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);
        var expr = exprEmitter.EmitDefinition(value);
        context.WriteLine($"{varName} = {expr};");
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

        var condition = context.GetValueName(ifStmt.Condition);

        // Scalar-condition fast path: emit a native C# if/else, no masking needed
        if (analysis.IsScalar(ifStmt.Condition))
        {
            {
                // Collect only DIRECT blocks in each branch (not blocks
                // from nested sub-structures) for phi assignment. Nested
                // if/else chains handle their own phi assignments.
                var sTrueBlocks = new HashSet<BasicBlock>();
                CollectDirectBlocks(ifStmt.TrueBranch, sTrueBlocks);
                var sFalseBlocks = new HashSet<BasicBlock>();
                CollectDirectBlocks(ifStmt.FalseBranch, sFalseBlocks);

                context.WriteLine($"if ({condition})");
                context.OpenScope();
                EmitControlFlow(
                    ifStmt.TrueBranch, placement, analysis, expressionEmitter);
                if (ifStmt.MergeBlock != null)
                    EmitScalarPhiAssignments(
                        ifStmt.MergeBlock, sTrueBlocks, analysis);
                context.CloseScope();

                if (ifStmt.FalseBranch.Elements.Count > 0)
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
        var trueBlocks = new HashSet<BasicBlock>();
        CollectBlocks(ifStmt.TrueBranch, trueBlocks);
        var falseBlocks = new HashSet<BasicBlock>();
        CollectBlocks(ifStmt.FalseBranch, falseBlocks);

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
        if (ifStmt.FalseBranch.Elements.Count > 0)
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

            if (!analysis.IsScalar(phi) && analysis.IsScalar(preHeader))
            {
                var et = phi.Type is PrimitiveType pt
                    ? wConfig.GetPrimitiveTypeName(pt.BasicValueType) : "int";
                context.WriteLine(
                    $"CPUVectorIntrinsics.Broadcast<{et}>(" +
                    $"{phiVar}, {initVal});");
            }
            else
            {
                context.WriteLine($"{phiVar} = {initVal};");
            }
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
        var condition = context.GetValueName(whileLoop.Condition);
        if (analysis.IsScalar(whileLoop.Condition))
        {
            context.WriteLine($"while ({condition})");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask(" +
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
                $"CPUVectorIntrinsics.AndMask(" +
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
            context.WriteLine($"if (!{condition}) break;");
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

            // Broadcast scalar init to vectorized phi target
            if (!analysis.IsScalar(phi) && analysis.IsScalar(preHeader))
            {
                var et = phi.Type is PrimitiveType pt
                    ? config.GetPrimitiveTypeName(pt.BasicValueType) : "int";
                context.WriteLine(
                    $"CPUVectorIntrinsics.Broadcast<{et}>(" +
                    $"{phiVar}, {initVal});");
            }
            else
            {
                context.WriteLine($"{phiVar} = {initVal};");
            }
            _handledMergePhis.Add(phi);
        }

        // Emit induction variable initialization
        var initial = exprEmitter.EmitExpression(inductionVar.InitialValue);
        if (!analysis.IsScalar(inductionVar.Phi)
            && analysis.IsScalar(inductionVar.InitialValue))
        {
            var et = inductionVar.Phi.Type is PrimitiveType ipt
                ? config.GetPrimitiveTypeName(ipt.BasicValueType) : "int";
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
        var condition = context.GetValueName(forLoop.Condition);
        if (analysis.IsScalar(forLoop.Condition))
        {
            context.WriteLine($"while ({condition})");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask(" +
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
            var indType = inductionVar.Phi.Type is PrimitiveType ipt2
                ? config.GetPrimitiveTypeName(ipt2.BasicValueType) : "int";
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

        // Bottom condition check
        if (!analysis.IsScalar(forLoop.Condition))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask(" +
                $"{am}, {savedMask}, {condition});");

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
                    var selType = phi.Type is PrimitiveType spt2
                        ? config.GetPrimitiveTypeName(spt2.BasicValueType)
                        : "int";
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
            context.WriteLine($"if (!{condition}) break;");
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
            context.WriteLine($"{phiVar} = {initVal};");
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
        foreach (var (phi, _, backEdge) in loopPhis)
        {
            var phiVar = context.GetValueName(phi);
            var newVal = context.GetValueName(backEdge);
            var selT = phi.Type is PrimitiveType dwpt
                ? dwConfig.GetPrimitiveTypeName(dwpt.BasicValueType) : "int";
            context.WriteLine(
                $"CPUVectorIntrinsics.Select<{selT}>(" +
                $"{phiVar}, {am}, {newVal}, {phiVar});");
        }

        // Evaluate condition and combine with mask
        var condition = context.GetValueName(doWhileLoop.Condition);
        if (analysis.IsScalar(doWhileLoop.Condition))
        {
            // Scalar condition: all lanes agree — use the condition directly
            context.CloseScope();
            context.WriteLine($"while ({condition});");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.AndMask(" +
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

        var condition = context.GetValueName(switchStmt.Condition);

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
                    var caseBlocks = new HashSet<BasicBlock>();
                    CollectDirectBlocks(body, caseBlocks);
                    EmitScalarPhiAssignments(
                        switchStmt.MergeBlock, caseBlocks, analysis);
                }
                context.WriteLine("break;");
                context.CloseScope();
            }
            if (switchStmt.DefaultCase.Elements.Count > 0)
            {
                context.WriteLine("default:");
                context.OpenScope();
                EmitControlFlow(
                    switchStmt.DefaultCase, placement, analysis,
                    expressionEmitter);
                if (switchStmt.MergeBlock != null)
                {
                    var defaultBlocks = new HashSet<BasicBlock>();
                    CollectDirectBlocks(switchStmt.DefaultCase, defaultBlocks);
                    EmitScalarPhiAssignments(
                        switchStmt.MergeBlock, defaultBlocks, analysis);
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
            context.CloseScope();
            caseIdx++;
        }

        // Emit default case
        if (switchStmt.DefaultCase.Elements.Count > 0)
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
            context.CloseScope();
        }

        // Restore mask
        context.WriteLine("// Restore mask");
        context.WriteLine(
            $"CPUVectorIntrinsics.CopyMask({am}, {savedMask});");
        context.WriteLine();
    }

    /// <summary>
    /// Collects all <see cref="BasicBlock"/> instances reachable within a
    /// control flow structure into <paramref name="result"/>.
    /// </summary>
    private static void CollectBlocks(
        ControlFlowStructure structure,
        HashSet<BasicBlock> result)
    {
        foreach (var element in structure.Elements)
        {
            switch (element)
            {
                case BasicBlock block:
                    result.Add(block);
                    break;
                case IfStatement nested:
                    CollectBlocks(nested.TrueBranch, result);
                    CollectBlocks(nested.FalseBranch, result);
                    break;
                case LoopStatement loop:
                    CollectBlocks(loop.Body, result);
                    break;
                case SwitchStatement sw:
                    foreach (var (_, body) in sw.Cases)
                        CollectBlocks(body, result);
                    CollectBlocks(sw.DefaultCase, result);
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
        foreach (var (phi, _, backEdge) in loopPhis)
        {
            var phiVar = context.GetValueName(phi);

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
                        var elemType2 = phi.Type is PrimitiveType pt2
                            ? config.GetPrimitiveTypeName(pt2.BasicValueType)
                            : "int";
                        context.WriteLine(
                            $"CPUVectorIntrinsics.Select<{elemType2}>(" +
                            $"{phiVar}, {activeMask}, " +
                            $"{innerPhiVar}, {phiVar});");
                        continue;
                    }

                    if (condName != null)
                    {
                        var elemType = phi.Type is PrimitiveType pt
                            ? config.GetPrimitiveTypeName(pt.BasicValueType)
                            : "int";

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
                var elemType = phi.Type is PrimitiveType pt
                    ? config.GetPrimitiveTypeName(pt.BasicValueType) : "int";

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
                if (analysis.IsScalar(pred.TrueValue))
                {
                    var bcast = $"_seltrue_{phi.Id}";
                    context.WriteLine(
                        $"var {bcast} = new {elemType}[SIMDWidth];");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                        $"{bcast}, {trueVal});");
                    trueVal = bcast;
                }
                if (analysis.IsScalar(pred.FalseValue))
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
                var selType = phi.Type is PrimitiveType spt
                    ? config.GetPrimitiveTypeName(spt.BasicValueType) : "int";
                context.WriteLine(
                    $"CPUVectorIntrinsics.Select<{selType}>(" +
                    $"{phiVar}, {activeMask}, {newVal}, {phiVar});");
            }
        }
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

                if (!analysis.IsScalar(phi) && analysis.IsScalar(arg))
                {
                    var elemType = phi.Type is PrimitiveType pt
                        ? config.GetPrimitiveTypeName(pt.BasicValueType)
                        : "int";
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                        $"{phiVar}, {source});");
                }
                else
                {
                    context.WriteLine($"{phiVar} = {source};");
                }

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
                if (analysis.IsScalar(arg))
                {
                    var scalarExpr = exprEmitter.EmitExpression(arg);
                    var elemType = phi.Type is PrimitiveType pt
                        ? config.GetPrimitiveTypeName(pt.BasicValueType)
                        : "int";
                    var bcastName = $"_phi_bcast_{phi.Id}_{i}";
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

                var elemT = phi.Type is PrimitiveType pt2
                    ? config.GetPrimitiveTypeName(pt2.BasicValueType)
                    : "int";
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
