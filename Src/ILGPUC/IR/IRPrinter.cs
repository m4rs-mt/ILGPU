// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRPrinter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ILGPUC.IR;

/// <summary>
/// Emits ILGPU-native IR text for a module or method by delegating directly to
/// each value's own <see cref="Value.ToString"/> and
/// <c>ToReferenceString()</c> representations.
/// </summary>
/// <remarks>
/// <para>
/// Each value definition line is the raw output of <see cref="Value.ToString"/>,
/// which has the form <c>{prefix}_{id}: {argString}</c>. Block labels use
/// <c>ToReferenceString()</c>; parameter declarations in method
/// headers also use <c>ToReferenceString()</c>.
/// </para>
/// <para>
/// This printer intentionally avoids any renaming or normalization: the output
/// faithfully reflects internal value identifiers, making it suitable for
/// low-level debugging. For stable, normalized output (sequential <c>%0</c>,
/// <c>%1</c> names), use <see cref="LLVMIRPrinter"/> instead.
/// </para>
/// <para>
/// Complex aggregate types (<see cref="StructureType"/>,
/// <see cref="ViewType"/>) are given short alias names in the module preamble
/// and those aliases are used in method signatures.
/// </para>
/// </remarks>
internal sealed class IRPrinter
{
    #region Instance

    /// <summary>
    /// The target text writer that receives all emitted IR text.
    /// </summary>
    private readonly TextWriter _writer;

    /// <summary>
    /// Maps type-value IDs to their declared alias names
    /// (e.g., <c>%view.f32</c>, <c>%struct.0</c>).
    /// Used only for method-signature type formatting.
    /// </summary>
    private readonly Dictionary<long, string> _typeNames = new();

    /// <summary>
    /// Ordered list of type alias declarations emitted before method bodies.
    /// Each entry is <c>(alias, body)</c>.
    /// </summary>
    private readonly List<(string Alias, string Body)> _typeAliasLines = new();

    /// <summary>
    /// Initializes a new <see cref="IRPrinter"/> that writes to
    /// <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="point">
    /// The pipeline stage at which the dump is taken (informational only;
    /// not used by this printer).
    /// </param>
    public IRPrinter(TextWriter writer, IRDumpPoint point = default)
    {
        _writer = writer;
        _ = point;
    }

    #endregion

    #region Public Entry Points

    /// <summary>
    /// Emits complete ILGPU-native IR text for the entire
    /// <paramref name="module"/>, including the module header, type alias
    /// declarations, globals, and all methods in post order.
    /// </summary>
    /// <param name="module">The module to print.</param>
    public void Print(Module module)
    {
        _typeNames.Clear();
        _typeAliasLines.Clear();
        BuildTypeAliases(module.Types);

        EmitModuleHeader(module);
        EmitTypeAliases();
        EmitGlobals(module);
        foreach (var method in module.MethodsInPostOrder)
            EmitMethod(method);
    }

    /// <summary>
    /// Emits ILGPU-native IR text for a single <paramref name="method"/> in
    /// isolation. Type aliases are derived from the method's enclosing module.
    /// </summary>
    /// <param name="method">The method to print.</param>
    public void Print(Method method)
    {
        _typeNames.Clear();
        _typeAliasLines.Clear();
        BuildTypeAliases(method.Scope.Types);
        EmitMethod(method);
    }

    #endregion

    #region Module-Level Emission

    /// <summary>
    /// Emits the module header comment, including the entry-point name, IR
    /// generation index, and native pointer type.
    /// Example: <c>; module: VectorKernel  [gen: 1]  [intptr: i64]</c>.
    /// </summary>
    /// <param name="module">The module being printed.</param>
    private void EmitModuleHeader(Module module)
    {
        var entryName = module.EntryPoint.Name;
        var gen = module.Generation.Index;
        var intptrStr = Prim(module.IntPointerType.BasicValueType);
        L($"; module: {entryName}  [gen: {gen}]  [intptr: {intptrStr}]");
        L("");
    }

    /// <summary>
    /// Emits all accumulated type alias declarations followed by a blank line.
    /// View types use ILGPU syntax (<c>%view.f32 = view float</c>); struct
    /// types use braced syntax (<c>%struct.0 = type { … }</c>).
    /// </summary>
    private void EmitTypeAliases()
    {
        if (_typeAliasLines.Count == 0) return;
        foreach (var (alias, body) in _typeAliasLines)
        {
            if (body.StartsWith("view ", System.StringComparison.Ordinal))
                L($"{alias} = {body}");
            else
                L($"{alias} = type {body}");
        }
        L("");
    }

    /// <summary>
    /// Emits each global using its own <see cref="Value.ToString"/>
    /// representation, followed by a blank line.
    /// </summary>
    /// <param name="module">The module whose globals to emit.</param>
    private void EmitGlobals(Module module)
    {
        if (module.NumGlobals == 0) return;
        foreach (var global in module.Globals)
            L(global.ToString());
        L("");
    }

    /// <summary>
    /// Emits a single method as either an intrinsic comment, a
    /// <c>declare</c> prototype, or a full <c>define … { … }</c> body.
    /// Parameter declarations use <c>ToReferenceString()</c>;
    /// types are formatted via <see cref="T"/>.
    /// </summary>
    /// <param name="method">The method to emit.</param>
    private void EmitMethod(Method method)
    {
        if (method.IsIntrinsic)
        {
            L($"; intrinsic @{method.Name}");
            L("");
            return;
        }

        if (!method.HasImplementation)
        {
            var declParts = new string[method.NumParameters];
            for (int i = 0; i < method.NumParameters; i++)
                declParts[i] = T(method.Parameters[i].Type);
            L($"declare @{method.Name}("
                + $"{string.Join(", ", declParts)}): {T(method.Type)}");
            L("");
            return;
        }

        var paramParts = new string[method.NumParameters];
        for (int i = 0; i < method.NumParameters; i++)
        {
            var p = method.Parameters[i];
            paramParts[i] = $"{p.ToReferenceString()}: {T(p.Type)}";
        }
        var paramList = string.Join(", ", paramParts);
        L($"define @{method.Name}({paramList}): {T(method.Type)} {{");

        var placement = CodePlacement.Create(method);
        foreach (var block in method.Blocks)
        {
            L($"  {block.ToReferenceString()}:");
            EmitBlock(block, placement.GetPlacedBlock(block));
        }

        L("}");
        L("");
    }

    #endregion

    #region Block Emission

    /// <summary>
    /// Emits all phi nodes, placed values, and the terminator for a basic
    /// block. Each value line is the raw <see cref="Value.ToString"/> output.
    /// Parameters are skipped because they are declared in the method header.
    /// </summary>
    /// <param name="block">The basic block to emit.</param>
    /// <param name="placed">The code-placement result for this block.</param>
    private void EmitBlock(BasicBlock block, PlacedBlock placed)
    {
        foreach (var phi in block.PhiValues)
            L($"    {phi.ToString()}");

        foreach (var value in placed)
        {
            if (value is Parameter) continue;
            L($"    {value.ToString()}");
        }

        EmitTermination(block);
    }

    /// <summary>
    /// Emits the terminator instruction for a basic block using ILGPU-style
    /// branch mnemonics (<c>br.uncond</c>, <c>br.cond</c>, <c>br.switch</c>,
    /// <c>ret</c>). Block references use
    /// <c>ToReferenceString()</c>; condition values use
    /// <c>ToReferenceString()</c>. A blank line follows each
    /// terminator.
    /// </summary>
    /// <param name="block">The basic block whose terminator to emit.</param>
    private void EmitTermination(BasicBlock block)
    {
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Pending:
                _writer.WriteLine("    ; pending");
                break;

            case BlockTerminationKind.Unconditional:
                {
                    var uv = block.AsUnconditionalView();
                    _writer.WriteLine(
                        $"    br.uncond {uv.Target.ToReferenceString()}");
                    break;
                }

            case BlockTerminationKind.Conditional:
                {
                    var cv = block.AsConditionalView();
                    _writer.WriteLine(
                        $"    br.cond {cv.Condition.ToReferenceString()}, " +
                        $"{cv.TrueTarget.ToReferenceString()}, " +
                        $"{cv.FalseTarget.ToReferenceString()}");
                    break;
                }

            case BlockTerminationKind.Switch:
                {
                    var sv = block.AsSwitchView();
                    var cond = sv.Condition.ToReferenceString();
                    var sb = new StringBuilder(
                        $"    br.switch {cond}, " +
                        $"{sv.DefaultTarget.ToReferenceString()} [");
                    for (int i = 0; i < sv.NumCases; i++)
                        sb.Append(
                            $"\n      {i}: " +
                            $"{sv.GetCaseTarget(i).ToReferenceString()}");
                    sb.Append("\n    ]");
                    _writer.WriteLine(sb);
                    break;
                }

            case BlockTerminationKind.Return:
                {
                    var rv = block.AsReturnView();
                    if (rv.IsVoidReturn)
                        _writer.WriteLine("    ret void");
                    else
                        _writer.WriteLine(
                            $"    ret {rv.ReturnValue.ToReferenceString()}");
                    break;
                }
        }

        _writer.WriteLine();
    }

    #endregion

    #region Type Building

    /// <summary>
    /// Scans <paramref name="types"/> to build alias declarations for all
    /// <see cref="StructureType"/> and <see cref="ViewType"/> values,
    /// populating <see cref="_typeNames"/> and <see cref="_typeAliasLines"/>.
    /// </summary>
    /// <param name="types">The module-level type values to scan.</param>
    private void BuildTypeAliases(System.ReadOnlySpan<TypeValue> types)
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
    /// Returns a compact, identifier-safe name for <paramref name="t"/>, used
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

    #endregion

    #region Type Formatting

    /// <summary>
    /// Formats a type value as an ILGPU IR type string for use in method
    /// signatures and type alias declarations. Complex types use their
    /// declared alias when available; otherwise a canonical inline form.
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
        StructureType st => _typeNames.TryGetValue(st.Id.Value, out var sn)
            ? sn
            : "%struct",
        ArrayType at => $"[{at.NumDimensions} x {T(at.ElementType)}]",
        _ => t.ToString() ?? "?"
    };

    /// <summary>
    /// Maps a <see cref="BasicValueType"/> to its ILGPU IR type keyword.
    /// </summary>
    /// <param name="bvt">The basic value type to map.</param>
    /// <returns>
    /// A type keyword string such as <c>i32</c>, <c>float</c>, or
    /// <c>i1</c>.
    /// </returns>
    private static string Prim(BasicValueType bvt) => bvt switch
    {
        BasicValueType.Int1 => "i1",
        BasicValueType.Int8 => "i8",
        BasicValueType.Int16 => "i16",
        BasicValueType.Int32 => "i32",
        BasicValueType.Int64 => "i64",
        BasicValueType.Float16 => "f16",
        BasicValueType.Float32 => "float",
        BasicValueType.Float64 => "double",
        _ => bvt.ToString().ToLowerInvariant()
    };

    #endregion

    #region Writer Helper

    /// <summary>
    /// Writes a single line to the output writer.
    /// </summary>
    /// <param name="line">The line to write.</param>
    private void L(string line) => _writer.WriteLine(line);

    #endregion
}
