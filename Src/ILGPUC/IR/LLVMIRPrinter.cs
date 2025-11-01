// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LLVMIRPrinter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ILGPUC.IR;

/// <summary>
/// Emits LLVM-style IR text for a module or method. Sequential value numbering
/// (<c>%0</c>, <c>%1</c>, …) and block labels (<c>entry</c>, <c>bb1</c>, …)
/// make the output deterministic and independent of internal value IDs.
/// </summary>
/// <remarks>
/// Standard LLVM math and bit-manipulation intrinsics (<c>sqrt</c>, <c>sin</c>,
/// <c>cos</c>, <c>fabs</c>, <c>floor</c>, <c>ceil</c>, <c>exp</c>, <c>log</c>,
/// <c>log2</c>, <c>log10</c>, <c>pow</c>, <c>copysign</c>, <c>smax</c>/
/// <c>smin</c>/etc., <c>ctpop</c>, <c>ctlz</c>, <c>cttz</c>) use the
/// <c>@llvm.*</c> prefix. GPU built-ins and math functions that have no
/// standard LLVM equivalent (<c>rsqrt</c>, <c>tan</c>, <c>atan2</c>,
/// <c>isinf</c>, barriers, shuffles, …) use <c>@ilgpu.*</c>.
/// ILGPU type extensions preserve type semantics.
/// In debug mode (<see cref="IRDumpMode.Raw"/>), real internal IDs are used
/// instead of sequential names.
/// </remarks>
internal sealed class LLVMIRPrinter
{
    #region Instance

    /// <summary>
    /// The target text writer that receives all emitted IR text.
    /// </summary>
    private readonly TextWriter _writer;

    /// <summary>
    /// When <see langword="true"/>, value names include real internal IDs
    /// (<c>%_42</c>, <c>b_17</c>); when <see langword="false"/>, names are
    /// sequential (<c>%0</c>, <c>entry</c>).
    /// </summary>
    private readonly bool _debugIds;

    /// <summary>
    /// Maps value IDs to their printed names (e.g., <c>%0</c>, <c>@kernel</c>).
    /// </summary>
    private readonly Dictionary<long, string> _nameTable = new();

    /// <summary>
    /// Tracks value IDs that were discovered as operands but are not placed
    /// (defined) in any block. These orphaned values are emitted as <c>undef</c>.
    /// </summary>
    private readonly HashSet<long> _orphanedValueIds = new();

    /// <summary>
    /// Maps type value IDs to their declared alias names
    /// (e.g., <c>%view.f32</c>, <c>%struct.0</c>).
    /// </summary>
    private readonly Dictionary<long, string> _typeNames = new();

    /// <summary>
    /// Ordered list of type alias declarations to emit before method bodies.
    /// Each entry is <c>(alias, body)</c>.
    /// </summary>
    private readonly List<(string Alias, string Body)> _typeAliasLines = new();

    /// <summary>
    /// Counter for assigning sequential value names within the current method.
    /// Reset to zero at the start of each method.
    /// </summary>
    private int _nextValueId;

    /// <summary>
    /// Initializes a new <see cref="IRPrinter"/> that writes to
    /// <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="point">
    /// The pipeline stage at which the dump is taken (informational only).
    /// </param>
    /// <param name="debugIds">
    /// When <see langword="true"/>, emit real internal IDs instead of sequential
    /// names.
    /// </param>
    public LLVMIRPrinter(
        TextWriter writer,
        IRDumpPoint point = default,
        bool debugIds = false)
    {
        _writer = writer;
        _debugIds = debugIds;
        _ = point;
    }

    #endregion

    #region Public Entry Points

    /// <summary>
    /// Emits complete IR text for the entire <paramref name="module"/>, including
    /// the module header comment, type alias declarations, globals, and all methods
    /// in post order.
    /// </summary>
    /// <param name="module">The module to print.</param>
    public void Print(Module module)
    {
        Reset();
        BuildTypeAliases(module.Types);
        BuildModuleNames(module);
        foreach (var method in module.Methods)
        {
            if (method.HasImplementation)
                BuildMethodNames(method);
        }

        EmitModuleHeader(module);
        EmitTypeAliases();
        EmitGlobals(module);
        foreach (var method in module.MethodsInPostOrder)
            EmitMethod(method);
    }

    /// <summary>
    /// Emits IR text for a single <paramref name="method"/> in isolation.
    /// Type aliases and method names are derived from the method's enclosing
    /// module scope.
    /// </summary>
    /// <param name="method">The method to print.</param>
    public void Print(Method method)
    {
        Reset();
        BuildTypeAliases(method.Scope.Types);
        BuildModuleNames(method.Scope);
        if (method.HasImplementation)
            BuildMethodNames(method);
        EmitMethod(method);
    }

    #endregion

    #region Reset

    /// <summary>
    /// Clears all accumulated name-table and type-alias state so the printer
    /// can be reused for another module or method.
    /// </summary>
    private void Reset()
    {
        _nameTable.Clear();
        _orphanedValueIds.Clear();
        _typeNames.Clear();
        _typeAliasLines.Clear();
        _nextValueId = 0;
    }

    #endregion

    #region Name Building

    /// <summary>
    /// Scans <paramref name="types"/> to build alias declarations for all
    /// <see cref="StructureType"/> and <see cref="ViewType"/> values, populating
    /// <see cref="_typeNames"/> and <see cref="_typeAliasLines"/>.
    /// </summary>
    /// <param name="types">The module-level type values to scan.</param>
    private void BuildTypeAliases(ReadOnlySpan<TypeValue> types)
    {
        int structIndex = 0;
        var viewAliasByElem = new Dictionary<long, string>();

        foreach (var type in types)
        {
            if (type is StructureType st)
            {
                var alias = $"%struct.{structIndex++}";
                _typeNames[type.Id.Value] = alias;

                var sb = new StringBuilder("{ ");
                bool first = true;
                foreach (var field in st.Fields)
                {
                    if (field is PaddingType) continue;
                    if (!first) sb.Append(", ");
                    sb.Append(T(field));
                    first = false;
                }
                sb.Append(first ? "}" : " }");
                _typeAliasLines.Add((alias, sb.ToString()));
            }
            else if (type is ViewType vt)
            {
                var elemId = vt.ElementType.Id.Value;
                if (!viewAliasByElem.TryGetValue(elemId, out var viewAlias))
                {
                    var elemName = ElemTypeName(vt.ElementType);
                    viewAlias = $"%view.{elemName}";
                    viewAliasByElem[elemId] = viewAlias;
                    _typeAliasLines.Add((viewAlias, $"view {T(vt.ElementType)}"));
                }
                _typeNames[type.Id.Value] = viewAlias;
            }
        }
    }

    /// <summary>
    /// Returns a compact, identifier-safe name for <paramref name="t"/> used
    /// when constructing view alias names such as <c>%view.f32</c>.
    /// </summary>
    /// <param name="t">The element type to name.</param>
    /// <returns>A short identifier string for the type.</returns>
    private static string ElemTypeName(TypeValue t) => t switch
    {
        PrimitiveType p => Prim(p.BasicValueType),
        StructureType _ => "struct",
        PointerType _ => "ptr",
        ArrayType at =>
            $"array.{ElemTypeName(at.ElementType)}.{at.NumDimensions}",
        _ => "t"
    };

    /// <summary>
    /// Registers module-level names for all methods (<c>@name</c>) and globals
    /// (<c>@g0</c>, <c>@g1</c>, …) in <see cref="_nameTable"/>.
    /// </summary>
    /// <param name="module">The module whose symbols are to be named.</param>
    private void BuildModuleNames(Module module)
    {
        foreach (var method in module.Methods)
            _nameTable[method.Id.Value] = "@" + Sanitize(method.Name);

        int gi = 0;
        foreach (var global in module.Globals)
            _nameTable[global.Id.Value] = $"@g{gi++}";
    }

    /// <summary>
    /// Assigns value names for all parameters, block labels, phi nodes, and
    /// placed values within <paramref name="method"/>. Resets the sequential
    /// counter before scanning.
    /// </summary>
    /// <param name="method">The method whose values are to be named.</param>
    private void BuildMethodNames(Method method)
    {
        _nextValueId = 0;

        for (int i = 0; i < method.NumParameters; i++)
        {
            var p = method.Parameters[i];
            _nameTable[p.Id.Value] = _debugIds
                ? $"%_{p.Id.Value}"
                : $"%p{i}";
        }

        var placement = CodePlacement.Create(method);
        int blockIndex = 0;
        foreach (var block in method.Blocks)
        {
            _nameTable[block.Id.Value] = _debugIds
                ? $"b_{block.Id.Value}"
                : blockIndex == 0 ? "entry" : $"bb{blockIndex}";

            foreach (var phi in block.PhiValues)
                _nameTable[phi.Id.Value] = _debugIds
                    ? $"%_{phi.Id.Value}"
                    : $"%{_nextValueId++}";

            var placed = placement.GetPlacedBlock(block);
            foreach (var value in placed)
            {
                if (value is Parameter or PhiValue) continue;
                if (!HasResult(value)) continue;
                _nameTable[value.Id.Value] = _debugIds
                    ? $"%_{value.Id.Value}"
                    : $"%{_nextValueId++}";
            }
            blockIndex++;
        }

        // Second pass: name any operands that weren't named in the first pass.
        // This handles values that were created during optimization (e.g.,
        // inlining) but aren't in any reachable block of the method.
        NameUnnamedOperands(placement);
    }

    /// <summary>
    /// Scans all named values' operands and assigns names to any unnamed values
    /// that are referenced but not in any placed block (e.g., intermediate values
    /// from inlining or optimization).
    /// </summary>
    private void NameUnnamedOperands(CodePlacement placement)
    {
        var worklist = new Queue<Value>();

        // Seed with all placed values, phi values, and terminators
        foreach (var placedBlock in placement)
        {
            foreach (var phi in placedBlock.BasicBlock.PhiValues)
                worklist.Enqueue(phi);
            foreach (var value in placedBlock)
                worklist.Enqueue(value);
            // Also check the terminator — name the termination value itself
            // if it wasn't placed (e.g., a BasicBlockValue condition that
            // ended up outside the block's value chain after transformation)
            if (placedBlock.BasicBlock.TerminationValue is { } tv)
            {
                if (!_nameTable.ContainsKey(tv.Id.Value)
                    && HasResult(tv)
                    && tv is not PrimitiveValue and not NullValue
                        and not UndefinedValue)
                {
                    _nameTable[tv.Id.Value] = _debugIds
                        ? $"%_{tv.Id.Value}"
                        : $"%{_nextValueId++}";
                }
                worklist.Enqueue(tv);
            }
        }

        while (worklist.Count > 0)
        {
            var value = worklist.Dequeue();
            foreach (var operand in value.Values)
            {
                if (operand is PrimitiveValue or NullValue or UndefinedValue)
                    continue;
                if (_nameTable.ContainsKey(operand.Id.Value))
                    continue;

                // Methods get @name format
                if (operand is Method m)
                {
                    _nameTable[m.Id.Value] = "@" + Sanitize(m.Name);
                    continue;
                }

                if (!HasResult(operand))
                    continue;

                // This operand exists in the IR graph but isn't placed in any
                // block — mark as orphaned so it prints as undef.
                _orphanedValueIds.Add(operand.Id.Value);

                // Recursively check its operands too
                worklist.Enqueue(operand);
            }
        }
    }

    #endregion

    #region Module-Level Emission

    /// <summary>
    /// Emits the module header comment line, e.g.
    /// <c>; module: VectorKernel  [intptr: i64]</c>.
    /// </summary>
    /// <param name="module">The module being printed.</param>
    private void EmitModuleHeader(Module module)
    {
        var entryName = Sanitize(module.EntryPoint.Name);
        var intptrStr = Prim(module.IntPointerType.BasicValueType);
        L($"; module: {entryName}  [intptr: {intptrStr}]");
        L("");
    }

    /// <summary>
    /// Emits all accumulated type alias declarations followed by a blank line.
    /// View types use ILGPU syntax (<c>%view.f32 = view float</c>); struct types
    /// use LLVM syntax (<c>%struct.0 = type { … }</c>).
    /// </summary>
    private void EmitTypeAliases()
    {
        if (_typeAliasLines.Count == 0) return;
        foreach (var (alias, body) in _typeAliasLines)
        {
            // View types use ILGPU-specific syntax; struct types use LLVM
            // "type { ... }"
            if (body.StartsWith("view ", StringComparison.Ordinal))
                L($"{alias} = {body}");
            else
                L($"{alias} = type {body}");
        }
        L("");
    }

    /// <summary>
    /// Emits a global variable declaration for each global in the module,
    /// e.g. <c>@g0 = internal global i32 zeroinitializer ; generic</c>.
    /// </summary>
    /// <param name="module">The module whose globals are to be emitted.</param>
    private void EmitGlobals(Module module)
    {
        if (module.NumGlobals == 0) return;
        foreach (var global in module.Globals)
        {
            var name = N(global);
            var addrStr = global.AddressSpace.ToString().ToLowerInvariant();
            L($"{name} = internal global {T(global.AllocType)} " +
                $"zeroinitializer ; {addrStr}");
        }
        L("");
    }

    /// <summary>
    /// Emits a single method as either an intrinsic comment, a
    /// <c>declare</c> prototype, or a full <c>define … { … }</c> body.
    /// </summary>
    /// <param name="method">The method to emit.</param>
    private void EmitMethod(Method method)
    {
        if (method.IsIntrinsic)
        {
            L($"; intrinsic {GetMethodName(method)}");
            L("");
            return;
        }

        if (!method.HasImplementation)
        {
            var declRet = T(method.Type);
            var declName = GetMethodName(method);
            var declParams = BuildParamTypeList(method);
            L($"declare {declRet} {declName}({declParams})");
            L("");
            return;
        }

        var retType = T(method.Type);
        var methodName = GetMethodName(method);
        var paramList = BuildParamList(method);
        L($"define {retType} {methodName}({paramList}) {{");

        var placement = CodePlacement.Create(method);
        foreach (var block in method.Blocks)
        {
            L($"{B(block)}:");
            EmitBlock(block, placement.GetPlacedBlock(block));
        }

        L("}");
        L("");
    }

    /// <summary>
    /// Returns the printed name of <paramref name="method"/>, falling back to
    /// <c>@sanitized_name</c> if the method was not registered in the name table.
    /// </summary>
    /// <param name="method">The method whose name to look up.</param>
    /// <returns>The printed method name, e.g. <c>@VectorKernel</c>.</returns>
    private string GetMethodName(Method method) =>
        _nameTable.TryGetValue(method.Id.Value, out var n)
            ? n
            : "@" + Sanitize(method.Name);

    /// <summary>
    /// Builds a comma-separated list of parameter types for a
    /// <c>declare</c> prototype, e.g. <c>ptr&lt;float&gt;, i32</c>.
    /// </summary>
    /// <param name="method">The method whose parameter types to format.</param>
    /// <returns>A comma-separated type list string.</returns>
    private string BuildParamTypeList(Method method)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < method.NumParameters; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(T(method.Parameters[i].Type));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds a comma-separated list of typed parameter names for a
    /// <c>define</c> header, e.g. <c>ptr&lt;float&gt; %p0, i32 %p1</c>.
    /// </summary>
    /// <param name="method">The method whose parameters to format.</param>
    /// <returns>A comma-separated typed parameter list string.</returns>
    private string BuildParamList(Method method)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < method.NumParameters; i++)
        {
            if (i > 0) sb.Append(", ");
            var p = method.Parameters[i];
            sb.Append($"{T(p.Type)} {N(p)}");
        }
        return sb.ToString();
    }

    #endregion

    #region Block Emission

    /// <summary>
    /// Emits all phi nodes, placed values, and the terminator instruction for a
    /// single basic block.
    /// </summary>
    /// <param name="block">The basic block being emitted.</param>
    /// <param name="placed">The code-placement result for this block.</param>
    private void EmitBlock(BasicBlock block, PlacedBlock placed)
    {
        foreach (var phi in block.PhiValues)
            EmitPhi(phi);

        foreach (var value in placed)
        {
            if (value is Parameter) continue;
            EmitValue(value);
        }

        EmitTermination(block);
    }

    /// <summary>
    /// Emits a phi instruction for <paramref name="phi"/>, e.g.
    /// <c>%0 = phi i32 [ %p0, %entry ], [ %1, %bb1 ]</c>.
    /// </summary>
    /// <param name="phi">The phi value to emit.</param>
    private void EmitPhi(PhiValue phi)
    {
        if (!_nameTable.TryGetValue(phi.Id.Value, out var name)) return;
        var ty = T(phi.Type);
        var sb = new StringBuilder($"{name} = phi {ty}");
        for (int i = 0; i < phi.NumArguments; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($" [ {N(phi.Arguments[i])}, %{B(phi.Sources[i])} ]");
        }
        _writer.WriteLine($"  {sb}");
    }

    /// <summary>
    /// Dispatches a single IR value to the appropriate typed emission helper.
    /// Inline constants (<see cref="PrimitiveValue"/>, <see cref="NullValue"/>,
    /// <see cref="UndefinedValue"/>) are skipped because they are formatted
    /// inline by <see cref="N"/>.
    /// </summary>
    /// <param name="value">The value to emit.</param>
    private void EmitValue(Value value)
    {
        switch (value)
        {
            case PrimitiveValue _:
            case NullValue _:
            case UndefinedValue _:
            case PhiValue _:
                return;

            case UnaryArithmeticValue u:
                EmitUnary(u);
                return;

            case BinaryArithmeticValue b:
                EmitBinary(b);
                return;

            case TernaryArithmeticValue t:
                EmitTernary(t);
                return;

            case ConvertValue cv:
                EmitConvert(cv);
                return;

            case CompareValue cmp:
                EmitCompare(cmp);
                return;

            case Predicate sel:
                {
                    var selTy = T(sel.Type);
                    Def(sel,
                        $"select i1 {N(sel.Condition)}, " +
                        $"{selTy} {N(sel.TrueValue)}, " +
                        $"{selTy} {N(sel.FalseValue)}");
                    return;
                }

            case Load ld:
                Def(ld, $"load {T(ld.Type)}, ptr {N(ld.Source)}");
                return;

            case Store st:
                Op($"store {T(st.Value.Type)} {N(st.Value)}, ptr {N(st.Target)}");
                return;

            case Alloca alloca:
                EmitAlloca(alloca);
                return;

            case MemoryBarrier mb:
                var mbStr = mb.Kind switch
                {
                    MemoryBarrierKind.GroupLevel => "group",
                    MemoryBarrierKind.DeviceLevel => "device",
                    _ => "system"
                };
                Op($"call void @ilgpu.membar.{mbStr}()");
                return;

            case StructureValue sv:
                EmitStructureValue(sv);
                return;

            case GetField gf:
                Def(gf,
                    $"extractvalue {T(gf.StructureType)} " +
                    $"{N(gf.Source)}, {gf.FieldSpan}");
                return;

            case SetField sf:
                Def(sf,
                    $"insertvalue {T(sf.StructureType)} " +
                    $"{N(sf.Source)}, " +
                    $"{T(sf.Value.Type)} {N(sf.Value)}, " +
                    $"{sf.FieldSpan}");
                return;

            case NewView nv:
                Def(nv,
                    $"call {T(nv.Type)} @ilgpu.view.new(" +
                    $"ptr {N(nv.Pointer)}, i64 {N(nv.Length)})");
                return;

            case GetViewLength gvl:
                Def(gvl,
                    $"call i64 @ilgpu.view.length(" +
                    $"{T(gvl.Source.Type)} {N(gvl.Source)})");
                return;

            case SubView subv:
                Def(subv,
                    $"call {T(subv.Type)} @ilgpu.view.sub(" +
                    $"{T(subv.Source.Type)} {N(subv.Source)}, " +
                    $"i64 {N(subv.Offset)}, i64 {N(subv.Length)})");
                return;

            case ViewCast vc:
                Def(vc,
                    $"call {T(vc.TargetType)} @ilgpu.view.cast(" +
                    $"{T(vc.SourceType)} {N(vc.Source)})");
                return;

            case ArrayToViewCast avc:
                Def(avc,
                    $"call {T(avc.Type)} @ilgpu.view.from_array(" +
                    $"{T(avc.SourceType)} {N(avc.Source)})");
                return;

            case GCInit gci:
                Def(gci,
                    $"call {T(gci.Type)} @ilgpu.gc_init({N(gci.Global)})");
                return;

            case LoadElementAddress lea:
                Def(lea,
                    $"getelementptr {T(lea.ElementType)}, " +
                    $"ptr {N(lea.Source)}, " +
                    $"{T(lea.Offset.Type)} {N(lea.Offset)}");
                return;

            case LoadFieldAddress lfa:
                Def(lfa,
                    $"getelementptr inbounds {T(lfa.StructureType)}, " +
                    $"ptr {N(lfa.Source)}, " +
                    $"i32 0, i32 {lfa.FieldSpan.Index}");
                return;

            case LoadArrayElementAddress laea:
                {
                    var sb2 = new StringBuilder(
                        $"getelementptr {T(laea.ElementType)}, " +
                        $"ptr {N(laea.ArrayValue)}");
                    foreach (var dim in laea.Dimensions)
                        sb2.Append($", i32 {N(dim)}");
                    Def(laea, sb2.ToString());
                    return;
                }

            case IntAsPointerCast iapc:
                Def(iapc,
                    $"inttoptr {T(iapc.SourceType)} {N(iapc.Source)} to ptr");
                return;

            case PointerAsIntCast paic:
                Def(paic,
                    $"ptrtoint ptr {N(paic.Source)} to {T(paic.TargetType)}");
                return;

            case FloatAsIntCast faic:
                Def(faic,
                    $"bitcast {T(faic.SourceType)} " +
                    $"{N(faic.Source)} to {T(faic.TargetType)}");
                return;

            case IntAsFloatCast iafc:
                Def(iafc,
                    $"bitcast {T(iafc.SourceType)} " +
                    $"{N(iafc.Source)} to {T(iafc.TargetType)}");
                return;

            case PointerCast pc:
                Def(pc,
                    $"bitcast ptr {N(pc.Source)} to ptr " +
                    $"; {T(pc.SourceElementType)} -> " +
                    $"{T(pc.TargetElementType)}");
                return;

            case AddressSpaceCast asc:
                if (asc.IsPointerCast)
                    Def(asc,
                        $"addrspacecast ptr {N(asc.Source)} " +
                        $"to {T(asc.TargetType)}");
                else
                    Def(asc,
                        $"bitcast {T(asc.SourceType)} " +
                        $"{N(asc.Source)} to {T(asc.TargetType)}");
                return;

            case GenericAtomic ga:
                EmitGenericAtomic(ga);
                return;

            case AtomicCAS cas:
                Def(cas,
                    $"cmpxchg ptr {N(cas.Target)}, " +
                    $"{T(cas.Type)} {N(cas.Value)}, " +
                    $"{T(cas.Type)} seq_cst seq_cst");
                return;

            case Barrier bar:
                if (bar.Kind == BarrierKind.WarpLevel)
                    Op("call void @ilgpu.barrier.warp()");
                else
                    Op("call void @ilgpu.barrier.group()");
                return;

            case PredicateBarrier pb:
                Def(pb,
                    $"call {T(pb.Type)} " +
                    $"@ilgpu.predicate_barrier(i1 {N(pb.Predicate)})");
                return;

            case Broadcast bc:
                Def(bc,
                    $"call {T(bc.Type)} @ilgpu.broadcast(" +
                    $"{T(bc.Type)} {N(bc.Variable)}, i32 {N(bc.Origin)})");
                return;

            case Shuffle sh:
                Def(sh,
                    $"call {T(sh.Type)} " +
                    $"@ilgpu.shuffle.{ShuffleSuffix(sh.Kind)}(" +
                    $"{T(sh.Type)} {N(sh.Variable)}, i32 {N(sh.Origin)})");
                return;

            case WarpReduce wr:
                Def(wr,
                    wr.HasIntrinsicOperation
                    ? $"call {T(wr.Type)} " +
                      $"@ilgpu.warp.reduce.{wr.Kind}.{wr.IntrinsicOp}(" +
                      $"{T(wr.Type)} {N(wr.Variable)})"
                    : $"call {T(wr.Type)} " +
                      $"@ilgpu.warp.reduce.{wr.Kind}.custom(" +
                      $"{T(wr.Type)} {N(wr.Variable)}, " +
                      $"ptr {N(wr.Operation)})");
                return;

            case WarpScan ws:
                Def(ws,
                    ws.HasIntrinsicOperation
                    ? $"call {T(ws.Type)} " +
                      $"@ilgpu.warp.scan.{ws.Kind}.{ws.IntrinsicOp}(" +
                      $"{T(ws.Type)} {N(ws.Variable)})"
                    : $"call {T(ws.Type)} " +
                      $"@ilgpu.warp.scan.{ws.Kind}.custom(" +
                      $"{T(ws.Type)} {N(ws.Variable)}, " +
                      $"ptr {N(ws.Operation)})");
                return;

            case GroupReduce gr:
                Def(gr,
                    gr.HasIntrinsicOperation
                    ? $"call {T(gr.Type)} " +
                      $"@ilgpu.group.reduce.{gr.Kind}.{gr.IntrinsicOp}(" +
                      $"{T(gr.Type)} {N(gr.Variable)}, " +
                      $"{T(gr.Identity.Type)} {N(gr.Identity)})"
                    : $"call {T(gr.Type)} " +
                      $"@ilgpu.group.reduce.{gr.Kind}.custom(" +
                      $"{T(gr.Type)} {N(gr.Variable)}, " +
                      $"ptr {N(gr.Operation)}, " +
                      $"{T(gr.Identity.Type)} {N(gr.Identity)})");
                return;

            case GroupScan gs:
                Def(gs,
                    gs.HasIntrinsicOperation
                    ? $"call {T(gs.Type)} " +
                      $"@ilgpu.group.scan.{gs.Kind}.{gs.IntrinsicOp}(" +
                      $"{T(gs.Type)} {N(gs.Variable)}, " +
                      $"{T(gs.Identity.Type)} {N(gs.Identity)})"
                    : $"call {T(gs.Type)} " +
                      $"@ilgpu.group.scan.{gs.Kind}.custom(" +
                      $"{T(gs.Type)} {N(gs.Variable)}, " +
                      $"ptr {N(gs.Operation)}, " +
                      $"{T(gs.Identity.Type)} {N(gs.Identity)})");
                return;

            case MethodCall call:
                EmitMethodCall(call);
                return;

            case GroupIndexValue _:
                Def(value, "call i32 @ilgpu.group.index()");
                return;
            case GridIndexValue _:
                Def(value, "call i32 @ilgpu.grid.index()");
                return;
            case GroupDimensionValue _:
                Def(value, "call i32 @ilgpu.group.dim()");
                return;
            case GridDimensionValue _:
                Def(value, "call i32 @ilgpu.grid.dim()");
                return;
            case SubGroupIndexValue _:
                Def(value, "call i32 @ilgpu.subgroup.index()");
                return;
            case SubGroupLaneIndexValue _:
                Def(value, "call i32 @ilgpu.subgroup.lane()");
                return;
            case SubGroupDimensionValue _:
                Def(value, "call i32 @ilgpu.subgroup.size()");
                return;
            case AcceleratorTypeValue _:
                Def(value, "call i32 @ilgpu.accelerator.type()");
                return;
            case AcceleratorArchitectureValue _:
                Def(value, $"call {T(value.Type)} @ilgpu.accelerator.arch()");
                return;
            case FastMathValue _:
                Def(value, "call i1 @ilgpu.fast_math()");
                return;
            case FlushToZeroValue _:
                Def(value, "call i1 @ilgpu.flush_to_zero()");
                return;

            case ArrayValue arr:
                {
                    var sb3 = new StringBuilder(
                        $"call {T(arr.Type)} @ilgpu.array.new(" +
                        $"ptr {N(arr.BackBuffer)}");
                    foreach (var dim in arr.Dimensions)
                        sb3.Append($", i32 {N(dim)}");
                    sb3.Append(')');
                    Def(arr, sb3.ToString());
                    return;
                }

            case GetArrayLength gal:
                if (gal.IsFullLength)
                    Def(gal,
                        $"call i32 @ilgpu.array.length(" +
                        $"{T(gal.ArrayValue.Type)} {N(gal.ArrayValue)})");
                else
                    Def(gal,
                        $"call i32 @ilgpu.array.length(" +
                        $"{T(gal.ArrayValue.Type)} {N(gal.ArrayValue)}, " +
                        $"i32 {N(gal.Dimension)})");
                return;

            case StringValue sv2:
                Def(sv2,
                    $"call ptr @ilgpu.string(\"{Sanitize(sv2.String)}\")");
                return;

            case DebugAssertOperation dbg:
                Op($"call void @ilgpu.debug.assert(" +
                    $"i1 {N(dbg.Condition)}, ptr {N(dbg.Message)})");
                return;

            case WriteToOutput wto:
                {
                    var sb4 = new StringBuilder(
                        "call void @ilgpu.write_output(");
                    bool first2 = true;
                    foreach (Value arg in wto.Values)
                    {
                        if (!first2) sb4.Append(", ");
                        sb4.Append($"{T(arg.Type)} {N(arg)}");
                        first2 = false;
                    }
                    sb4.Append(')');
                    Op(sb4.ToString());
                    return;
                }

            default:
                if (HasResult(value))
                    _writer.WriteLine(
                        $"  {N(value)} = ; unhandled: {value.GetType().Name}");
                else
                    _writer.WriteLine(
                        $"  ; unhandled: {value.GetType().Name}");
                return;
        }
    }

    /// <summary>
    /// Emits an <c>alloca</c> instruction, optionally with a static or dynamic
    /// array length operand.
    /// </summary>
    /// <param name="alloca">The alloca value to emit.</param>
    private void EmitAlloca(Alloca alloca)
    {
        if (alloca.ArrayLength.HasValue)
            Def(alloca,
                $"alloca {T(alloca.AllocType)}, " +
                $"i32 {alloca.ArrayLength.Value.Int32Value}");
        else if (alloca.ArrayLengthValue is not null)
            Def(alloca,
                $"alloca {T(alloca.AllocType)}, " +
                $"i32 {N(alloca.ArrayLengthValue)}");
        else
            Def(alloca, $"alloca {T(alloca.AllocType)}");
    }

    /// <summary>
    /// Emits a simplified aggregate literal for a structure construction value,
    /// e.g. <c>%0 = { float %p0, i32 %p1 }  ; %struct.0</c>.
    /// </summary>
    /// <param name="sv">The structure value to emit.</param>
    private void EmitStructureValue(StructureValue sv)
    {
        if (!_nameTable.TryGetValue(sv.Id.Value, out var name)) return;
        var ty = T(sv.StructureType);
        var sb = new StringBuilder("{ ");
        bool first = true;
        foreach (var field in sv.Values)
        {
            if (!first) sb.Append(", ");
            sb.Append($"{T(field.Type)} {N(field)}");
            first = false;
        }
        sb.Append(first ? "}" : " }");
        _writer.WriteLine($"  {name} = {sb}  ; {ty}");
    }

    /// <summary>
    /// Emits a unary arithmetic or math intrinsic instruction.
    /// Integer negation maps to <c>sub T 0, v</c>; boolean complement maps to
    /// <c>xor T v, -1</c>. Standard LLVM math operations use <c>@llvm.*</c>
    /// intrinsics (e.g. <c>@llvm.sqrt.f32</c>, <c>@llvm.fabs.f32</c>). Functions
    /// without a standard LLVM equivalent use <c>@ilgpu.*</c> extensions (e.g.
    /// <c>@ilgpu.rsqrt</c>, <c>@ilgpu.tan</c>, <c>@ilgpu.isinf</c>).
    /// </summary>
    /// <param name="u">The unary arithmetic value to emit.</param>
    private void EmitUnary(UnaryArithmeticValue u)
    {
        var ty = T(u.Type);
        var srcTy = T(u.Value.Type);
        var v = N(u.Value);
        bool isFloat = u.IsFloatOperation;

        var rhs = u.Kind switch
        {
            UnaryArithmeticKind.Neg => isFloat
                ? $"fneg {srcTy} {v}"
                : $"sub {srcTy} 0, {v}",
            UnaryArithmeticKind.Not =>
                $"xor {srcTy} {v}, -1",
            // Standard LLVM intrinsics
            UnaryArithmeticKind.Abs => isFloat
                ? $"call {ty} @llvm.fabs.{ty}({srcTy} {v})"
                : $"call {ty} @llvm.abs.{ty}({srcTy} {v}, i1 false)",
            UnaryArithmeticKind.Sqrt =>
                $"call {ty} @llvm.sqrt.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Sin =>
                $"call {ty} @llvm.sin.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Cos =>
                $"call {ty} @llvm.cos.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Exp =>
                $"call {ty} @llvm.exp.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Exp2 =>
                $"call {ty} @llvm.exp2.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Log =>
                $"call {ty} @llvm.log.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Log2 =>
                $"call {ty} @llvm.log2.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Log10 =>
                $"call {ty} @llvm.log10.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Floor =>
                $"call {ty} @llvm.floor.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Ceiling =>
                $"call {ty} @llvm.ceil.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Rcp => isFloat
                ? $"fdiv {ty} 1.0, {v}"
                : $"sdiv {ty} 1, {v}",
            UnaryArithmeticKind.PopC =>
                $"call {ty} @llvm.ctpop.{srcTy}({srcTy} {v})",
            UnaryArithmeticKind.CLZ =>
                $"call {ty} @llvm.ctlz.{srcTy}({srcTy} {v}, i1 false)",
            UnaryArithmeticKind.CTZ =>
                $"call {ty} @llvm.cttz.{srcTy}({srcTy} {v}, i1 false)",
            UnaryArithmeticKind.IsNaN =>
                $"fcmp uno {srcTy} {v}, {v}",
            // ILGPU extensions — no standard LLVM equivalent
            UnaryArithmeticKind.Rsqrt =>
                $"call {ty} @ilgpu.rsqrt.{ty}({srcTy} {v})",
            UnaryArithmeticKind.IsInf =>
                $"call i1 @ilgpu.isinf.{srcTy}({srcTy} {v})",
            UnaryArithmeticKind.IsFin =>
                $"call i1 @ilgpu.isfinite.{srcTy}({srcTy} {v})",
            UnaryArithmeticKind.Tan =>
                $"call {ty} @ilgpu.tan.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Tanh =>
                $"call {ty} @ilgpu.tanh.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Sinh =>
                $"call {ty} @ilgpu.sinh.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Cosh =>
                $"call {ty} @ilgpu.cosh.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Asin =>
                $"call {ty} @ilgpu.asin.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Acos =>
                $"call {ty} @ilgpu.acos.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Atan =>
                $"call {ty} @ilgpu.atan.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Asinh =>
                $"call {ty} @ilgpu.asinh.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Acosh =>
                $"call {ty} @ilgpu.acosh.{ty}({srcTy} {v})",
            UnaryArithmeticKind.Atanh =>
                $"call {ty} @ilgpu.atanh.{ty}({srcTy} {v})",
            _ => $"; unhandled unary {u.Kind} {srcTy} {v}"
        };
        Def(u, rhs);
    }

    /// <summary>
    /// Emits a binary arithmetic instruction. Unsigned vs signed variants are
    /// selected via <see cref="ArithmeticValue.IsUnsigned"/>.
    /// Standard LLVM binary intrinsics (max, min, pow, copysign) use the
    /// <c>@llvm.*</c> prefix; ILGPU extensions without a standard LLVM
    /// equivalent (atan2, BinaryLog, IEEERemainder) use <c>@ilgpu.*</c>.
    /// </summary>
    /// <param name="b">The binary arithmetic value to emit.</param>
    private void EmitBinary(BinaryArithmeticValue b)
    {
        var ty = T(b.Type);
        var l = N(b.Left);
        var r = N(b.Right);
        bool isFloat = b.IsFloatOperation;
        bool isUnsigned = b.IsUnsigned;

        var rhs = b.Kind switch
        {
            BinaryArithmeticKind.Add => isFloat
                ? $"fadd {ty} {l}, {r}"
                : $"add {ty} {l}, {r}",
            BinaryArithmeticKind.Sub => isFloat
                ? $"fsub {ty} {l}, {r}"
                : $"sub {ty} {l}, {r}",
            BinaryArithmeticKind.Mul => isFloat
                ? $"fmul {ty} {l}, {r}"
                : $"mul {ty} {l}, {r}",
            BinaryArithmeticKind.Div => isFloat
                ? $"fdiv {ty} {l}, {r}"
                : isUnsigned
                    ? $"udiv {ty} {l}, {r}"
                    : $"sdiv {ty} {l}, {r}",
            BinaryArithmeticKind.Rem => isFloat
                ? $"frem {ty} {l}, {r}"
                : isUnsigned
                    ? $"urem {ty} {l}, {r}"
                    : $"srem {ty} {l}, {r}",
            BinaryArithmeticKind.And => $"and {ty} {l}, {r}",
            BinaryArithmeticKind.Or => $"or {ty} {l}, {r}",
            BinaryArithmeticKind.Xor => $"xor {ty} {l}, {r}",
            BinaryArithmeticKind.Shl => $"shl {ty} {l}, {r}",
            BinaryArithmeticKind.Shr => isUnsigned
                ? $"lshr {ty} {l}, {r}"
                : $"ashr {ty} {l}, {r}",
            // Standard LLVM intrinsics
            BinaryArithmeticKind.Max => isFloat
                ? $"call {ty} @llvm.maxnum.{ty}({ty} {l}, {ty} {r})"
                : isUnsigned
                    ? $"call {ty} @llvm.umax.{ty}({ty} {l}, {ty} {r})"
                    : $"call {ty} @llvm.smax.{ty}({ty} {l}, {ty} {r})",
            BinaryArithmeticKind.Min => isFloat
                ? $"call {ty} @llvm.minnum.{ty}({ty} {l}, {ty} {r})"
                : isUnsigned
                    ? $"call {ty} @llvm.umin.{ty}({ty} {l}, {ty} {r})"
                    : $"call {ty} @llvm.smin.{ty}({ty} {l}, {ty} {r})",
            BinaryArithmeticKind.Pow =>
                $"call {ty} @llvm.pow.{ty}({ty} {l}, {ty} {r})",
            BinaryArithmeticKind.CopySign =>
                $"call {ty} @llvm.copysign.{ty}({ty} {l}, {ty} {r})",
            // ILGPU extensions — no standard LLVM equivalent
            BinaryArithmeticKind.Atan2 =>
                $"call {ty} @ilgpu.atan2.{ty}({ty} {l}, {ty} {r})",
            BinaryArithmeticKind.BinaryLog =>
                $"call {ty} @ilgpu.log_base.{ty}({ty} {l}, {ty} {r})",
            BinaryArithmeticKind.IEEERemainder =>
                $"call {ty} @ilgpu.ieee_rem.{ty}({ty} {l}, {ty} {r})",
            _ => $"; unhandled binary {b.Kind} {ty} {l}, {r}"
        };
        Def(b, rhs);
    }

    /// <summary>
    /// Emits a ternary arithmetic instruction. Currently only
    /// <see cref="TernaryArithmeticKind.MultiplyAdd"/> is supported, mapping to
    /// the <c>@ilgpu.fma.*</c> intrinsic.
    /// </summary>
    /// <param name="t">The ternary arithmetic value to emit.</param>
    private void EmitTernary(TernaryArithmeticValue t)
    {
        var ty = T(t.Type);
        var a = N(t.First);
        var b = N(t.Second);
        var c = N(t.Third);
        var rhs = t.Kind switch
        {
            TernaryArithmeticKind.MultiplyAdd =>
                $"call {ty} @ilgpu.fma.{ty}({ty} {a}, {ty} {b}, {ty} {c})",
            _ => $"; unhandled ternary {t.Kind}"
        };
        Def(t, rhs);
    }

    /// <summary>
    /// Emits a type conversion instruction (<c>trunc</c>, <c>zext</c>,
    /// <c>sext</c>, <c>fptrunc</c>, <c>fpext</c>, <c>sitofp</c>, <c>uitofp</c>,
    /// <c>fptosi</c>, <c>fptoui</c>).
    /// </summary>
    /// <param name="cv">The conversion value to emit.</param>
    private void EmitConvert(ConvertValue cv)
    {
        var srcBvt = cv.Value.Type.BasicValueType;
        var tgtBvt = cv.Type.BasicValueType;
        var srcTy = T(cv.Value.Type);
        var tgtTy = T(cv.Type);
        var v = N(cv.Value);
        bool srcFloat = srcBvt.IsFloat();
        bool tgtFloat = tgtBvt.IsFloat();
        bool srcUnsigned = cv.IsSourceUnsigned;
        bool tgtUnsigned = cv.IsResultUnsigned;

        string opcode;
        if (srcFloat && tgtFloat)
            opcode = FloatBits(tgtBvt) < FloatBits(srcBvt) ? "fptrunc" : "fpext";
        else if (srcFloat)
            opcode = tgtUnsigned ? "fptoui" : "fptosi";
        else if (tgtFloat)
            opcode = srcUnsigned ? "uitofp" : "sitofp";
        else
        {
            int sb = IntBits(srcBvt), tb = IntBits(tgtBvt);
            opcode = tb < sb ? "trunc" : srcUnsigned ? "zext" : "sext";
        }

        Def(cv, $"{opcode} {srcTy} {v} to {tgtTy}");
    }

    /// <summary>
    /// Emits an integer (<c>icmp</c>) or floating-point (<c>fcmp</c>) comparison
    /// instruction with the appropriate LLVM predicate string.
    /// </summary>
    /// <param name="cmp">The comparison value to emit.</param>
    private void EmitCompare(CompareValue cmp)
    {
        bool isFloat = cmp.Left.BasicValueType.IsFloat();
        var ty = T(cmp.Left.Type);
        var l = N(cmp.Left);
        var r = N(cmp.Right);
        bool uu = cmp.IsUnsignedOrUnordered;

        string pred = (cmp.Kind, isFloat, uu) switch
        {
            (CompareKind.Equal, false, _) => "eq",
            (CompareKind.NotEqual, false, _) => "ne",
            (CompareKind.LessThan, false, false) => "slt",
            (CompareKind.LessThan, false, true) => "ult",
            (CompareKind.LessEqual, false, false) => "sle",
            (CompareKind.LessEqual, false, true) => "ule",
            (CompareKind.GreaterThan, false, false) => "sgt",
            (CompareKind.GreaterThan, false, true) => "ugt",
            (CompareKind.GreaterEqual, false, false) => "sge",
            (CompareKind.GreaterEqual, false, true) => "uge",
            (CompareKind.Equal, true, false) => "oeq",
            (CompareKind.NotEqual, true, false) => "one",
            (CompareKind.LessThan, true, false) => "olt",
            (CompareKind.LessEqual, true, false) => "ole",
            (CompareKind.GreaterThan, true, false) => "ogt",
            (CompareKind.GreaterEqual, true, false) => "oge",
            (CompareKind.Equal, true, true) => "ueq",
            (CompareKind.NotEqual, true, true) => "une",
            (CompareKind.LessThan, true, true) => "ult",
            (CompareKind.LessEqual, true, true) => "ule",
            (CompareKind.GreaterThan, true, true) => "ugt",
            (CompareKind.GreaterEqual, true, true) => "uge",
            _ => "eq"
        };
        var op = isFloat ? "fcmp" : "icmp";
        Def(cmp, $"{op} {pred} {ty} {l}, {r}");
    }

    /// <summary>
    /// Emits an <c>atomicrmw</c> instruction for a generic atomic
    /// read-modify-write operation on a pointer target.
    /// </summary>
    /// <param name="ga">The generic atomic value to emit.</param>
    private void EmitGenericAtomic(GenericAtomic ga)
    {
        var ty = T(ga.Type);
        var kindStr = ga.Kind switch
        {
            GenericAtomicKind.Exchange => "xchg",
            GenericAtomicKind.Add => "add",
            GenericAtomicKind.And => "and",
            GenericAtomicKind.Or => "or",
            GenericAtomicKind.Xor => "xor",
            GenericAtomicKind.Max => ga.IsUnsigned ? "umax" : "max",
            GenericAtomicKind.Min => ga.IsUnsigned ? "umin" : "min",
            _ => "add"
        };
        Def(ga,
            $"atomicrmw {kindStr} ptr {N(ga.Target)}, " +
            $"{ty} {N(ga.Value)} seq_cst");
    }

    /// <summary>
    /// Emits a method call instruction, using <c>call void</c> for
    /// void-returning targets or <c>%N = call T</c> for value-returning targets.
    /// </summary>
    /// <param name="call">The method call value to emit.</param>
    private void EmitMethodCall(MethodCall call)
    {
        var target = call.Target;
        var targetName = GetMethodName(target);
        var retTy = T(target.Type);

        var sb = new StringBuilder();
        bool first = true;
        foreach (var arg in call.Arguments)
        {
            if (!first) sb.Append(", ");
            sb.Append($"{T(arg.Type)} {N(arg)}");
            first = false;
        }
        var argsStr = sb.ToString();

        if (target.Type is VoidType)
            Op($"call void {targetName}({argsStr})");
        else
            Def(call, $"call {retTy} {targetName}({argsStr})");
    }

    /// <summary>
    /// Emits the terminator instruction for a basic block:
    /// unconditional branch, conditional branch, switch, or return.
    /// A blank line is written after each terminator for readability.
    /// </summary>
    /// <param name="block">The basic block whose terminator to emit.</param>
    private void EmitTermination(BasicBlock block)
    {
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Pending:
                _writer.WriteLine("  ; pending");
                break;

            case BlockTerminationKind.Unconditional:
                {
                    var uv = block.AsUnconditionalView();
                    _writer.WriteLine($"  br label %{B(uv.Target)}");
                    break;
                }

            case BlockTerminationKind.Conditional:
                {
                    var cv = block.AsConditionalView();
                    _writer.WriteLine(
                        $"  br i1 {N(cv.Condition)}, " +
                        $"label %{B(cv.TrueTarget)}, " +
                        $"label %{B(cv.FalseTarget)}");
                    break;
                }

            case BlockTerminationKind.Switch:
                {
                    var sv = block.AsSwitchView();
                    var switchTy = T(sv.Condition.Type);
                    var cond = N(sv.Condition);
                    var sw = new StringBuilder(
                        $"  switch {switchTy} {cond}, " +
                        $"label %{B(sv.DefaultTarget)} [");
                    for (int i = 0; i < sv.NumCases; i++)
                        sw.Append(
                            $"\n    {switchTy} {i}, " +
                            $"label %{B(sv.GetCaseTarget(i))}");
                    sw.Append("\n  ]");
                    _writer.WriteLine(sw);
                    break;
                }

            case BlockTerminationKind.Return:
                {
                    var rv = block.AsReturnView();
                    if (rv.IsVoidReturn)
                        _writer.WriteLine("  ret void");
                    else
                        _writer.WriteLine(
                            $"  ret {T(rv.ReturnValue.Type)} " +
                            $"{N(rv.ReturnValue)}");
                    break;
                }
        }

        _writer.WriteLine();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> produces a
    /// result that should be assigned to a named slot. Inline constants, stores,
    /// barriers, and void-typed values return <see langword="false"/>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>
    /// <see langword="true"/> if the value needs a <c>%N = …</c> assignment.
    /// </returns>
    private static bool HasResult(Value value) => value switch
    {
        PrimitiveValue _ => false,
        NullValue _ => false,
        UndefinedValue _ => false,
        Store _ => false,
        Barrier _ => false,
        MemoryBarrier _ => false,
        _ when value.Type is VoidType => false,
        _ => true
    };

    /// <summary>
    /// Returns the printed operand representation of <paramref name="value"/>.
    /// Inline constants are formatted directly; named values are looked up in
    /// <see cref="_nameTable"/>; unknown values fall back to <c>???ID</c>.
    /// </summary>
    /// <param name="value">
    /// The value to format as an operand, or <see langword="null"/>.
    /// </param>
    /// <returns>The operand string.</returns>
    private string N(Value? value)
    {
        if (value is null) return "undef";
        if (value is PrimitiveValue pv) return FormatPrimitive(pv);
        if (value is NullValue) return "null";
        if (value is UndefinedValue) return "undef";
        if (value is StringValue sv)
            return _nameTable.TryGetValue(sv.Id.Value, out var sn)
                ? sn
                : $"@\"{Sanitize(sv.String)}\"";
        // Orphaned values (exist in IR graph but not placed in any block)
        // are emitted as undef — they represent promoted allocas or other
        // values that no longer have a valid definition.
        if (_orphanedValueIds.Contains(value.Id.Value))
            return "undef";
        if (_nameTable.TryGetValue(value.Id.Value, out var name))
            return name;
        return $"???{value.Id.Value}";
    }

    /// <summary>
    /// Returns the block label for <paramref name="block"/> as a plain
    /// identifier (e.g., <c>entry</c>, <c>bb1</c>). Callers prepend <c>%</c>
    /// when used as a branch or phi operand.
    /// </summary>
    /// <param name="block">The basic block whose label to look up.</param>
    /// <returns>The block label string.</returns>
    private string B(BasicBlock block) =>
        _nameTable.TryGetValue(block.Id.Value, out var label)
            ? label
            : $"bb.{block.Id.Value}";

    /// <summary>
    /// Formats a type value as an ILGPU IR type string. Complex types use their
    /// declared alias when available; otherwise a canonical inline form is used.
    /// </summary>
    /// <param name="t">The type value to format.</param>
    /// <returns>
    /// A type string such as <c>i32</c>, <c>ptr&lt;float&gt;</c>,
    /// <c>%view.f32</c>, or <c>%struct.0</c>.
    /// </returns>
    private string T(TypeValue t) => t switch
    {
        VoidType _ => "void",
        PrimitiveType p => Prim(p.BasicValueType),
        PointerType pt => pt.AddressSpace == MemoryAddressSpace.Generic
            ? $"ptr<{T(pt.ElementType)}>"
            : $"ptr<{T(pt.ElementType)}>" +
                $" addrspace({pt.AddressSpace.ToString().ToLowerInvariant()})",
        ViewType vt => _typeNames.TryGetValue(vt.Id.Value, out var vn)
            ? vn
            : $"view<{T(vt.ElementType)}>",
        StringType _ => "ptr",
        StructureType st => _typeNames.TryGetValue(st.Id.Value, out var sn)
            ? sn
            : BuildInlineStructType(st),
        ArrayType at => $"array<{T(at.ElementType)}, {at.NumDimensions}>",
        PaddingType _ => "i8",
        _ => "ptr"
    };

    /// <summary>
    /// Builds an inline LLVM struct type literal for a <see cref="StructureType"/>
    /// that was not registered in <see cref="_typeNames"/> (e.g. types introduced
    /// during optimization).
    /// </summary>
    /// <param name="st">The structure type.</param>
    /// <returns>An inline type string such as <c>{ i32, float }</c>.</returns>
    private string BuildInlineStructType(StructureType st)
    {
        var sb = new StringBuilder("{ ");
        bool first = true;
        foreach (var field in st.Fields)
        {
            if (field is PaddingType) continue;
            if (!first) sb.Append(", ");
            sb.Append(T(field));
            first = false;
        }
        sb.Append(first ? "}" : " }");
        return sb.ToString();
    }

    /// <summary>
    /// Maps a <see cref="BasicValueType"/> to its LLVM type keyword
    /// (<c>i1</c>, <c>i32</c>, <c>float</c>, <c>double</c>, etc.).
    /// </summary>
    /// <param name="bvt">The basic value type to map.</param>
    /// <returns>The LLVM type keyword string.</returns>
    private static string Prim(BasicValueType bvt) => bvt switch
    {
        BasicValueType.Int1 => "i1",
        BasicValueType.Int8 => "i8",
        BasicValueType.Int16 => "i16",
        BasicValueType.Int32 => "i32",
        BasicValueType.Int64 => "i64",
        BasicValueType.Float16 => "half",
        BasicValueType.Float32 => "float",
        BasicValueType.Float64 => "double",
        _ => bvt.ToString().ToLowerInvariant()
    };

    /// <summary>
    /// Formats a <see cref="PrimitiveValue"/> as an inline literal operand
    /// string, e.g. <c>42</c>, <c>3.14</c>, <c>true</c>.
    /// </summary>
    /// <param name="pv">The primitive value to format.</param>
    /// <returns>The literal string representation.</returns>
    private static string FormatPrimitive(PrimitiveValue pv)
    {
        var box = pv.Box;
        return box.BasicValueType switch
        {
            BasicValueType.Int1 => box.Int1Value ? "true" : "false",
            BasicValueType.Int8 =>
                box.Int8Value.ToString(CultureInfo.InvariantCulture),
            BasicValueType.Int16 =>
                box.Int16Value.ToString(CultureInfo.InvariantCulture),
            BasicValueType.Int32 =>
                box.Int32Value.ToString(CultureInfo.InvariantCulture),
            BasicValueType.Int64 =>
                box.Int64Value.ToString(CultureInfo.InvariantCulture),
            BasicValueType.Float16 => FormatFloat((float)box.Float16Value),
            BasicValueType.Float32 => FormatFloat(box.Float32Value),
            BasicValueType.Float64 => FormatFloat(box.Float64Value),
            _ => box.RawValue.ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Formats a single-precision float literal, appending <c>.0</c> when
    /// needed to ensure the string is recognizable as floating-point.
    /// Special values (<c>NaN</c>, <c>±infinity</c>) use named forms.
    /// </summary>
    /// <param name="f">The float value to format.</param>
    /// <returns>The formatted float string.</returns>
    private static string FormatFloat(float f)
    {
        if (float.IsNaN(f)) return "nan";
        if (float.IsPositiveInfinity(f)) return "+infinity";
        if (float.IsNegativeInfinity(f)) return "-infinity";
        var s = f.ToString("G9", CultureInfo.InvariantCulture);
        return s.Contains('.', StringComparison.Ordinal) ||
            s.Contains('E', StringComparison.Ordinal) ? s : s + ".0";
    }

    /// <summary>
    /// Formats a double-precision float literal, appending <c>.0</c> when
    /// needed to ensure the string is recognizable as floating-point.
    /// Special values (<c>NaN</c>, <c>±infinity</c>) use named forms.
    /// </summary>
    /// <param name="d">The double value to format.</param>
    /// <returns>The formatted double string.</returns>
    private static string FormatFloat(double d)
    {
        if (double.IsNaN(d)) return "nan";
        if (double.IsPositiveInfinity(d)) return "+infinity";
        if (double.IsNegativeInfinity(d)) return "-infinity";
        var s = d.ToString("G17", CultureInfo.InvariantCulture);
        return s.Contains('.', StringComparison.Ordinal) ||
            s.Contains('E', StringComparison.Ordinal) ? s : s + ".0";
    }

    /// <summary>
    /// Returns the bit-width of a floating-point <see cref="BasicValueType"/>
    /// (16, 32, or 64).
    /// </summary>
    /// <param name="bvt">The floating-point basic value type.</param>
    /// <returns>The bit-width of the type.</returns>
    private static int FloatBits(BasicValueType bvt) => bvt switch
    {
        BasicValueType.Float16 => 16,
        BasicValueType.Float64 => 64,
        _ => 32
    };

    /// <summary>
    /// Returns the bit-width of an integer <see cref="BasicValueType"/>
    /// (1, 8, 16, 32, or 64).
    /// </summary>
    /// <param name="bvt">The integer basic value type.</param>
    /// <returns>The bit-width of the type.</returns>
    private static int IntBits(BasicValueType bvt) => bvt switch
    {
        BasicValueType.Int1 => 1,
        BasicValueType.Int8 => 8,
        BasicValueType.Int16 => 16,
        BasicValueType.Int64 => 64,
        _ => 32
    };

    /// <summary>
    /// Returns the suffix used in <c>@ilgpu.shuffle.*</c> intrinsic names for
    /// a given <see cref="ShuffleKind"/>.
    /// </summary>
    /// <param name="kind">The shuffle kind.</param>
    /// <returns>The intrinsic suffix string.</returns>
    private static string ShuffleSuffix(ShuffleKind kind) => kind switch
    {
        ShuffleKind.Up => "up",
        ShuffleKind.Down => "down",
        ShuffleKind.Xor => "bfly",
        _ => "idx"
    };

    /// <summary>
    /// Replaces characters that are not valid in LLVM identifiers with
    /// underscores. Returns <c>anon</c> for null or empty input.
    /// </summary>
    /// <param name="name">The raw name to sanitize.</param>
    /// <returns>A sanitized identifier string.</returns>
    private static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return "anon";
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.Length == 0 ? "anon" : sb.ToString();
    }

    /// <summary>
    /// Writes a complete line to the output writer.
    /// </summary>
    /// <param name="line">The line to write.</param>
    private void L(string line) => _writer.WriteLine(line);

    /// <summary>
    /// Writes an instruction line of the form <c>  %N = {rhs}</c>, using the
    /// registered name for <paramref name="result"/>. If the value has no
    /// registered name, emits a comment instead.
    /// </summary>
    /// <param name="result">
    /// The value whose name to use on the left-hand side.
    /// </param>
    /// <param name="rhs">The right-hand side of the assignment.</param>
    private void Def(Value result, string rhs)
    {
        if (_nameTable.TryGetValue(result.Id.Value, out var name))
            _writer.WriteLine($"  {name} = {rhs}");
        else
            _writer.WriteLine($"  ; unbound: {rhs}");
    }

    /// <summary>
    /// Writes a void instruction line of the form <c>  {text}</c>, i.e.,
    /// without a left-hand side assignment.
    /// </summary>
    /// <param name="text">The instruction text to write.</param>
    private void Op(string text) => _writer.WriteLine($"  {text}");

    #endregion
}
