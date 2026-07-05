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
using System.Collections.Generic;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// Method emitter for CPU backend with Tensor&lt;T&gt;-based vectorization support.
/// </summary>
/// <remarks>
/// Emits vectorized methods using Tensor&lt;T&gt; and masked execution semantics.
/// Every block is emitted once in the order of a <see cref="CPUBlockSchedule"/>
/// with predicated (masked) execution: the lanes taking each branch run the
/// target block under an activeMask Tensor&lt;bool&gt;.
/// </remarks>
sealed partial class CPUMethodEmitter(
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

    /// <summary>
    /// The parameter of a helper method holding the indices of the caller's
    /// lanes within their group.
    /// </summary>
    private const string CallerGroupLanesName = "_callerGroupLanes";

    /// <summary>
    /// The parameter of a helper method holding the indices of the caller's
    /// lanes within their warp.
    /// </summary>
    private const string CallerWarpLanesName = "_callerWarpLanes";

    private readonly Dictionary<Method, VectorizationAnalysis> _analyses = [];
    private readonly HashSet<Method> _pendingAnalyses = [];
    private CPUExpressionEmitter? _expressionEmitter;
    private int _layoutCounter;

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
        bool isEntryPoint = method == context.Module.EntryPoint;
        var analysis = GetAnalysis(method)
            ?? throw new System.NotSupportedException(
                $"Recursive calls of '{method.Name}' are not supported by the " +
                "CPU backend.");

        // Schedule the blocks for masked execution
        var schedule = new CPUBlockSchedule(method);
        _schedule = schedule;

        // For entry point in pooled mode, compute buffer pool metadata
        // before emitting the signature (the signature needs the pool slots).
        if (poolBuffers && isEntryPoint)
        {
            var codePlacement = method.CreateCodePlacement();
            var config = (CPULanguageConfiguration)context.LanguageConfig;
            var allocator = new VectorBufferAllocator(
                method, codePlacement, analysis, config, schedule);
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
            else if (param0.Type is StructureType kernelIndex
                && IsKernelIndexStructType(kernelIndex))
            {
                // Grouped launch: the group index differs per lane
                EmitKernelIndexDecomposition(param0);
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
                    $"{CPUExpressionEmitter.ToLaneElement(expr, returnType)}, " +
                    "SIMDWidth);");
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
        var returnType = !isEntryPoint && method.Type is not VoidType
            ? GetLaneArrayTypeName(method.Type, typeEmitter)
            : typeEmitter.GetTypeName(method.Type);
        var methodName = isEntryPoint
            ? "KernelEntryPoint"
            : context.GetValueName(method);

        // All methods in the static class must be static; helpers may take
        // and return uniform pointers
        if (!isEntryPoint)
            context.Write("static unsafe ");
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
                typeName = GetLaneArrayTypeName(param.Type, typeEmitter);
            }
            var paramName = context.GetValueName(param);
            context.Write($"{typeName} {paramName}");
        }

        // Helpers run for the active lanes of their caller
        if (method != context.Module.EntryPoint)
        {
            if (!first)
                context.Write(", ");
            context.Write(
                $"bool[] {CallerMaskName}, int[] {CallerLanesName}, " +
                $"int[] {CallerGroupLanesName}, int[] {CallerWarpLanesName}");
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
    /// Returns the type of the per-lane array holding values of the given
    /// type. Per-lane pointers are stored as addresses (<c>nint[]</c>).
    /// </summary>
    private static string GetLaneArrayTypeName(
        TypeValue type,
        TypeEmitter typeEmitter) =>
        type is PointerType ? "nint[]" : typeEmitter.GetTypeName(type) + "[]";

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
                $"var {CPUIntrinsicEmitter.GroupLanesName} = pool.groupLanes;");
            context.WriteLine(
                $"var {CPUIntrinsicEmitter.WarpLanesName} = pool.warpLanes;");
            EmitLaneNumbering();
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
                $"var {CPUIntrinsicEmitter.GroupLanesName} = " +
                $"{CallerGroupLanesName};");
            context.WriteLine(
                $"var {CPUIntrinsicEmitter.WarpLanesName} = " +
                $"{CallerWarpLanesName};");
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
            context.WriteLine(
                $"var {CPUIntrinsicEmitter.GroupLanesName} = " +
                "CPUVectorIntrinsics.CreateLaneIndices(SIMDWidth);");
            context.WriteLine(
                $"var {CPUIntrinsicEmitter.WarpLanesName} = " +
                "CPUVectorIntrinsics.CreateLaneIndices(SIMDWidth);");
            EmitLaneNumbering();

            // For non-pooled entry points, initialize laneIdx with the
            // baseIndex offset so per-lane indices are [base, base+1, ...].
            // Grouped kernels take no linear base index.
            if (isEntryPoint && method != null && method.Parameters.Count > 0
                && !context.Launch.IsGrouped)
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
    /// Numbers the lanes of an entry point call within their warp and within
    /// their group, which starts at the group offset set by the launcher.
    /// </summary>
    private void EmitLaneNumbering()
    {
        context.WriteLine(
            $"CPUVectorIntrinsics.InitLaneIndices(" +
            $"{CPUIntrinsicEmitter.GroupLanesName}, " +
            $"{CPUIntrinsicEmitter.GroupOffsetName});");
        context.WriteLine(
            $"CPUVectorIntrinsics.InitLaneIndices(" +
            $"{CPUIntrinsicEmitter.WarpLanesName}, 0);");
    }

    /// <summary>
    /// Returns <see langword="true"/> when the entry point's first parameter of
    /// type <paramref name="st"/> is decomposed into per-lane field arrays: a
    /// multi-dim index or a <c>KernelIndex</c>.
    /// </summary>
    private static bool IsDecomposedIndexType(StructureType st) =>
        IsIndexStructType(st) || IsKernelIndexStructType(st);

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
    /// Decomposes the <c>KernelIndex</c> parameter of a grouped launch into
    /// per-lane field arrays: the grid index of the group, which the launcher
    /// passes, and the index of each lane within the group.
    /// </summary>
    private void EmitKernelIndexDecomposition(Parameter param0)
    {
        var paramName = context.GetValueName(param0);
        var gridIndex = $"_idx_{StructureType.GetFieldName(0)}";
        var groupIndex = $"_idx_{StructureType.GetFieldName(1)}";
        context.WriteLine("// Decompose the kernel index into per-lane components");
        context.WriteLine($"var {gridIndex} = new long[SIMDWidth];");
        context.WriteLine(
            $"CPUVectorIntrinsics.Broadcast<long>({gridIndex}, " +
            $"{paramName}.{StructureType.GetFieldName(0)});");
        context.WriteLine($"var {groupIndex} = new int[SIMDWidth];");
        context.WriteLine(
            $"{CPUIntrinsicEmitter.GroupLanesName}.CopyTo({groupIndex}, 0);");

        // Other uses of the whole index, such as helper calls, take the index
        // of every lane
        var lanes = $"_idx_{paramName}";
        var typeName = new TypeEmitter(context).GetTypeName(param0.Type);
        context.WriteLine($"var {lanes} = new {typeName}[SIMDWidth];");
        context.WriteLine("for (int _d = 0; _d < SIMDWidth; _d++)");
        context.OpenScope();
        context.WriteLine($"{lanes}[_d] = {paramName};");
        context.WriteLine(
            $"{lanes}[_d].{StructureType.GetFieldName(1)} = {groupIndex}[_d];");
        context.CloseScope();
        context.WriteLine();
        context.SetValueName(param0, lanes);

        string[] fieldNames = [gridIndex, groupIndex];
        param0.Scope.Blocks.ForEachValue<GetField>(gf =>
        {
            if (gf.Source == param0 && gf.FieldSpan.Index < fieldNames.Length)
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
        context.WriteLine("public int[] groupLanes;");
        context.WriteLine("public int[] warpLanes;");
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
            "groupLanes = CPUVectorIntrinsics.CreateLaneIndices(simdWidth);");
        context.WriteLine(
            "warpLanes = CPUVectorIntrinsics.CreateLaneIndices(simdWidth);");
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

        // Create code placement analysis for value ordering
        var codePlacement = method.CreateCodePlacement();

        // Allocate SSA variables (using vectorized types)
        EmitVariableDeclarations(method, codePlacement, analysis);

        // Emit all blocks in schedule order under their entry masks
        var schedule = _schedule!;
        EmitScheduleDeclarations(schedule, codePlacement, analysis);
        EmitScheduledBody(schedule, codePlacement, analysis, expressionEmitter);
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
                    && IsDecomposedIndexType(gf2St)
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

    #region Value Emission

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

            case Broadcast broadcast:
                expressionEmitter.EmitBroadcast(broadcast);
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

            case LoadElementAddress lea when !analysis.IsScalar(lea):
                // Non-fusable vectorized LEA on a pointer: offset the
                // per-lane addresses
                expressionEmitter.EmitPointerAddresses(
                    lea, context.GetValueName(lea));
                break;

            case BaseAddressSpaceCast cast
                when cast.Type is PointerType && !analysis.IsScalar(cast):
                // Per-lane pointer casts keep the addresses of their source.
                // They are copied, as the buffers of both values may be
                // reused independently.
                expressionEmitter.EmitPointerCopy(
                    context.GetValueName(cast), cast.Source);
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
                    var srcVar = expressionEmitter.ResolvePointers(
                        lfa.Source, lfa);
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
            && IsDecomposedIndexType(gfSt)
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

        // Views that differ per lane are only formed by the operations above
        if (analysis.IsPerLaneView(value))
            throw UnsupportedPerLaneView(value);

        // Field insertion: copy the source struct and overwrite the field
        if (value is SetField setField)
        {
            EmitSetField(setField, varName, analysis);
            return;
        }

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
                    && IsDecomposedIndexType(st)));
        analysis = new VectorizationAnalysis(
            method, vectorizeFirstParam: hasIndexParam, GetAnalysis);
        _pendingAnalyses.Remove(method);
        _analyses[method] = analysis;
        return analysis;
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
    /// Returns true if values of the given C# types are structures or views
    /// that only differ in the address spaces of their views and pointers,
    /// which have the same representation on the CPU.
    /// </summary>
    private static bool IsLayoutConversion(string? from, string? to) =>
        from is not null && to is not null && from != to &&
        (from.StartsWith("struct_", System.StringComparison.Ordinal) &&
            to.StartsWith("struct_", System.StringComparison.Ordinal) ||
        from.StartsWith("CPURuntimeView<", System.StringComparison.Ordinal) &&
            to.StartsWith("CPURuntimeView<", System.StringComparison.Ordinal));

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
        var pointers = expressionEmitter.ResolvePointers(newView.Pointer, newView);
        var length = GetLaneOperand(newView.Length, analysis);
        if (!analysis.IsScalar(newView.Length))
            length += "[_si]";
        var elemType = VectorizationAnalysis.GetScalarTypeName(
            newView.ViewElementType,
            (CPULanguageConfiguration)context.LanguageConfig);
        context.WriteLine("for (int _si = 0; _si < SIMDWidth; _si++)");
        context.WriteLine(
            $"    {result}[_si] = new CPURuntimeView<{elemType}>(" +
            $"({elemType}*){pointers}[_si], {length});");
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
            ?? throw new System.NotSupportedException(
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
                    $"{CPUExpressionEmitter.ToLaneElement(expr, elemType)}, SIMDWidth)";
            }
            arguments.Add(expr);
        }
        arguments.Add(CPUExpressionEmitter.ActiveMaskName);
        arguments.Add("laneIdx");
        arguments.Add(CPUIntrinsicEmitter.GroupLanesName);
        arguments.Add(CPUIntrinsicEmitter.WarpLanesName);

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
