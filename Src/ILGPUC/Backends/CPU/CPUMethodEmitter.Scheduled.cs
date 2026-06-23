// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMethodEmitter.Scheduled.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// Scheduled masked control flow: every block is emitted once in the order of
/// a <see cref="CPUBlockSchedule"/> and runs for the lanes in its entry mask.
/// </summary>
/// <remarks>
/// A terminator adds the lanes taking each outgoing edge to the entry mask of
/// the edge target and assigns the target's phis for exactly those lanes.
/// Lanes that leave a loop through any edge (a break, a return or a jump out
/// of several loops) wait in the entry mask of the exit target until the loop
/// has finished for all lanes, and lanes that branch back to the loop header
/// wait for the next iteration. Values defined in a loop and used after it
/// are written for the active lanes only, which freezes them per lane.
/// </remarks>
partial class CPUMethodEmitter
{
    /// <summary>
    /// The name of the scratch mask holding the lanes of a conditional edge.
    /// </summary>
    private const string EdgeMaskName = "_edgeMask";

    private CPUBlockSchedule? _schedule;
    private readonly Dictionary<Value, BasicBlock> _valueBlocks = [];
    private readonly HashSet<Value> _maskedWrites = [];
    private int _phiCopyCounter;

    /// <summary>
    /// Returns the name of the entry mask of a block.
    /// </summary>
    private static string GetBlockMaskName(BasicBlock block) => $"_bm_{block.Id}";

    /// <summary>
    /// Returns the name of the scratch buffer of a value written per lane.
    /// </summary>
    private static string GetMaskedWriteName(Value value) => $"_mw_{value.Id}";

    /// <summary>
    /// Returns the element type of the per-lane buffer of a vectorized value,
    /// or null if the value is not stored in a per-lane array.
    /// </summary>
    private string? GetVectorElementType(Value value)
    {
        if (value is CompareValue)
            return "bool";
        return value.Type switch
        {
            PrimitiveType pt => ((CPULanguageConfiguration)context.LanguageConfig)
                .GetPrimitiveTypeName(pt.BasicValueType),
            PointerType => "nint",
            StructureType st => $"struct_{st.Id}",
            _ => null
        };
    }

    /// <summary>
    /// Prepares the scheduled emission of a method: maps values to their
    /// blocks and declares block entry masks and per-lane write buffers.
    /// Must run after <see cref="EmitVariableDeclarations"/>.
    /// </summary>
    private void EmitScheduleDeclarations(
        CPUBlockSchedule schedule,
        CodePlacement placement,
        VectorizationAnalysis analysis)
    {
        _valueBlocks.Clear();
        _maskedWrites.Clear();
        _phiCopyCounter = 0;
        foreach (var block in schedule.LinearOrder)
        {
            if (!placement.TryGetPlacedBlock(block, out var placedBlock))
                continue;
            foreach (var value in placedBlock)
                _valueBlocks[value] = block;
        }

        context.WriteLine("// Block entry masks");
        foreach (var block in schedule.LinearOrder)
        {
            if (schedule.HasElidedMask(block))
                continue;
            context.WriteLine(
                $"var {GetBlockMaskName(block)} = new bool[SIMDWidth];");
        }
        context.WriteLine($"var {EdgeMaskName} = new bool[SIMDWidth];");

        // Values defined in a loop and used outside of it must keep the value
        // of the iteration in which each lane left the loop.
        foreach (var block in schedule.LinearOrder)
        {
            var loop = schedule.GetLoop(block);
            if (loop is null ||
                !placement.TryGetPlacedBlock(block, out var placedBlock))
            {
                continue;
            }
            foreach (var value in placedBlock)
            {
                if (value is PhiValue || analysis.IsScalar(value) ||
                    !context.NeedsVariable(value))
                {
                    continue;
                }
                var elemType = GetVectorElementType(value);
                if (elemType is null || !IsUsedOutside(value, loop, analysis))
                    continue;
                _maskedWrites.Add(value);
                context.WriteLine(
                    $"var {GetMaskedWriteName(value)} = " +
                    $"new {elemType}[SIMDWidth];");
            }
        }
        context.WriteLine();
    }

    /// <summary>
    /// Returns true if the value has a use outside the given loop.
    /// </summary>
    private bool IsUsedOutside(
        Value value,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        VectorizationAnalysis analysis)
    {
        var useBlocks = new HashSet<BasicBlock>();
        CollectUseBlocks(value, useBlocks, analysis, 0);
        foreach (var block in useBlocks)
        {
            if (!loop.Contains(block))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Collects the blocks in which a value is read. A phi argument is read on
    /// the edge from its source block, a termination value in its block, and
    /// an operand of an inlined value wherever the inlined value is read.
    /// </summary>
    private void CollectUseBlocks(
        Value value,
        HashSet<BasicBlock> blocks,
        VectorizationAnalysis analysis,
        int depth)
    {
        foreach (var use in value.Uses)
        {
            var target = use.Target;
            switch (target)
            {
                case PhiValue phi:
                    for (int i = 0; i < phi.NumArguments; i++)
                    {
                        if (phi.Arguments[i] == value)
                            blocks.Add(phi.Sources[i]);
                    }
                    break;
                case BasicBlock block:
                    blocks.Add(block);
                    break;
                default:
                    if (_valueBlocks.TryGetValue(target, out var useBlock))
                        blocks.Add(useBlock);
                    bool inlined = target is LoadElementAddress ||
                        target is PureValue && !context.NeedsVariable(target);
                    if (inlined && depth < 16)
                        CollectUseBlocks(target, blocks, analysis, depth + 1);
                    break;
            }
        }
    }

    /// <summary>
    /// Emits the body of a method in schedule order.
    /// </summary>
    private void EmitScheduledBody(
        CPUBlockSchedule schedule,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var entry = schedule.Method.EntryBlock;
        if (!schedule.HasElidedMask(entry))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.CopyMask({GetBlockMaskName(entry)}, " +
                $"{CPUExpressionEmitter.ActiveMaskName});");
        }
        EmitScheduledItems(
            schedule.Items, 0, placement, analysis, expressionEmitter);
    }

    /// <summary>
    /// Emits a list of schedule items starting at the given index.
    /// </summary>
    private void EmitScheduledItems(
        List<CPUBlockSchedule.Item> items,
        int startIndex,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        for (int i = startIndex; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Loop is null)
            {
                EmitScheduledBlock(
                    item.Block, false, placement, analysis, expressionEmitter);
                continue;
            }

            // A loop runs while lanes are waiting at its header
            context.WriteLine("while (true)");
            context.OpenScope();
            var headers = item.Loop.Headers;
            if (headers.Length == 1)
            {
                EmitScheduledBlock(
                    item.Block, true, placement, analysis, expressionEmitter);
                EmitScheduledItems(
                    item.Items, 1, placement, analysis, expressionEmitter);
            }
            else
            {
                // A loop with several headers runs while lanes are waiting at
                // any of them; each header is entered through its own mask.
                var waiting = new string[headers.Length];
                for (int j = 0; j < headers.Length; j++)
                {
                    waiting[j] = $"CPUVectorIntrinsics.Any(" +
                        $"{GetBlockMaskName(headers[j])})";
                }
                context.WriteLine(
                    $"if (!({string.Join(" || ", waiting)})) break;");
                EmitScheduledItems(
                    item.Items, 0, placement, analysis, expressionEmitter);
            }
            context.CloseScope();
        }
    }

    /// <summary>
    /// Emits a block for the lanes in its entry mask.
    /// </summary>
    private void EmitScheduledBlock(
        BasicBlock block,
        bool isLoopHeader,
        CodePlacement placement,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var am = CPUExpressionEmitter.ActiveMaskName;
        var schedule = _schedule!;
        context.WriteLine($"{context.GetValueName(block)}: ;");

        bool openScope = true;
        if (isLoopHeader)
        {
            context.WriteLine(
                $"if (!CPUVectorIntrinsics.TakeMask({am}, " +
                $"{GetBlockMaskName(block)})) break;");
            openScope = false;
        }
        else if (block == schedule.Method.EntryBlock &&
            schedule.HasElidedMask(block))
        {
            // The entry block always has active lanes
            openScope = false;
        }
        else if (schedule.HasElidedMask(block))
        {
            context.WriteLine($"if (CPUVectorIntrinsics.Any({am}))");
        }
        else
        {
            context.WriteLine(
                $"if (CPUVectorIntrinsics.TakeMask({am}, " +
                $"{GetBlockMaskName(block)}))");
        }
        if (openScope)
            context.OpenScope();

        if (placement.TryGetPlacedBlock(block, out var placedBlock))
        {
            foreach (var value in placedBlock)
            {
                if (value is PhiValue)
                    continue;
                EmitScheduledValue(value, analysis, expressionEmitter);
            }
        }

        EmitScheduledTerminator(block, analysis, expressionEmitter);

        if (openScope)
            context.CloseScope();
    }

    /// <summary>
    /// Emits a value; values that must stay frozen for inactive lanes are
    /// computed into a scratch buffer and merged for the active lanes.
    /// </summary>
    private void EmitScheduledValue(
        Value<Method> value,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        if (!_maskedWrites.Contains(value))
        {
            EmitValue(value, analysis, expressionEmitter);
            return;
        }

        var name = context.GetValueName(value);
        var scratch = GetMaskedWriteName(value);
        context.SetValueName(value, scratch);
        EmitValue(value, analysis, expressionEmitter);
        context.SetValueName(value, name);
        context.WriteLine(
            $"CPUVectorIntrinsics.Select<{GetVectorElementType(value)}>(" +
            $"{name}, {CPUExpressionEmitter.ActiveMaskName}, {scratch}, {name});");
    }

    /// <summary>
    /// Emits the terminator of a block: distributes the active lanes onto the
    /// outgoing edges.
    /// </summary>
    private void EmitScheduledTerminator(
        BasicBlock block,
        VectorizationAnalysis analysis,
        CPUExpressionEmitter expressionEmitter)
    {
        var am = CPUExpressionEmitter.ActiveMaskName;
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Unconditional:
                EmitScheduledEdge(block, block.Successors[0], am, analysis);
                break;

            case BlockTerminationKind.Conditional:
                {
                    var view = block.AsConditionalView();
                    if (view.TrueTarget == view.FalseTarget)
                    {
                        EmitScheduledEdge(block, view.TrueTarget, am, analysis);
                        break;
                    }
                    if (analysis.IsScalar(view.Condition))
                    {
                        var condition = EmitConditionExpression(
                            view.Condition, analysis, expressionEmitter);
                        context.WriteLine($"if ({condition})");
                        context.OpenScope();
                        EmitScheduledEdge(block, view.TrueTarget, am, analysis);
                        context.CloseScope();
                        context.WriteLine("else");
                        context.OpenScope();
                        EmitScheduledEdge(block, view.FalseTarget, am, analysis);
                        context.CloseScope();
                        break;
                    }

                    var mask = expressionEmitter.ResolveOperand(view.Condition);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.AndMask({EdgeMaskName}, {am}, " +
                        $"{mask});");
                    EmitScheduledEdge(
                        block, view.TrueTarget, EdgeMaskName, analysis);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.AndNotMask({EdgeMaskName}, {am}, " +
                        $"{mask});");
                    EmitScheduledEdge(
                        block, view.FalseTarget, EdgeMaskName, analysis);
                    break;
                }

            case BlockTerminationKind.Switch:
                {
                    var view = block.AsSwitchView();
                    if (analysis.IsScalar(view.Condition))
                    {
                        var condition = EmitConditionExpression(
                            view.Condition, analysis, expressionEmitter);
                        context.WriteLine($"switch ({condition})");
                        context.OpenScope();
                        for (int i = 0; i < view.NumCases; i++)
                        {
                            context.WriteLine($"case {i}:");
                            context.OpenScope();
                            EmitScheduledEdge(
                                block, view.GetCaseTarget(i), am, analysis);
                            context.WriteLine("break;");
                            context.CloseScope();
                        }
                        context.WriteLine("default:");
                        context.OpenScope();
                        EmitScheduledEdge(block, view.DefaultTarget, am, analysis);
                        context.WriteLine("break;");
                        context.CloseScope();
                        context.CloseScope();
                        break;
                    }

                    var values = expressionEmitter.ResolveOperand(view.Condition);
                    var condType = GetVectorElementType(view.Condition) ?? "int";
                    for (int i = 0; i < view.NumCases; i++)
                    {
                        context.WriteLine(
                            $"CPUVectorIntrinsics.CaseMask<{condType}>(" +
                            $"{EdgeMaskName}, {am}, {values}, ({condType}){i});");
                        EmitScheduledEdge(
                            block, view.GetCaseTarget(i), EdgeMaskName, analysis);
                    }
                    context.WriteLine(
                        $"CPUVectorIntrinsics.DefaultCaseMask<{condType}>(" +
                        $"{EdgeMaskName}, {am}, {values}, " +
                        $"({condType}){view.NumCases});");
                    EmitScheduledEdge(
                        block, view.DefaultTarget, EdgeMaskName, analysis);
                    break;
                }
        }
    }

    /// <summary>
    /// Emits the transfer of the lanes in <paramref name="mask"/> along the
    /// edge from <paramref name="source"/> to <paramref name="target"/>.
    /// </summary>
    private void EmitScheduledEdge(
        BasicBlock source,
        BasicBlock target,
        string mask,
        VectorizationAnalysis analysis)
    {
        if (!_schedule!.HasElidedMask(target))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.AccumulateMask(" +
                $"{GetBlockMaskName(target)}, {mask});");
        }
        EmitScheduledPhiAssignments(source, target, mask, analysis);
    }

    /// <summary>
    /// Assigns the phis of <paramref name="target"/> for the lanes taking the
    /// edge from <paramref name="source"/>. All incoming values are read
    /// before any phi is written, as the edge assigns all phis in parallel.
    /// </summary>
    private void EmitScheduledPhiAssignments(
        BasicBlock source,
        BasicBlock target,
        string mask,
        VectorizationAnalysis analysis)
    {
        var assignments = new List<(PhiValue Phi, Value Argument)>();
        foreach (var phi in target.PhiValues)
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.Sources[i] == source)
                {
                    assignments.Add((phi, phi.Arguments[i]));
                    break;
                }
            }
        }
        if (assignments.Count < 1)
            return;

        var exprEmitter = new ExpressionEmitter(context, new TypeEmitter(context));
        var targetPhis = new HashSet<Value>();
        foreach (var (phi, _) in assignments)
            targetPhis.Add(phi);
        bool parallel = assignments.Count > 1;

        // Resolve all incoming values first
        var sources = new string[assignments.Count];
        for (int i = 0; i < assignments.Count; i++)
        {
            var (phi, arg) = assignments[i];
            if (analysis.IsScalar(arg))
            {
                var expr = exprEmitter.EmitExpression(arg);
                if (parallel)
                {
                    var tmp = $"_pc_{_phiCopyCounter++}";
                    context.WriteLine($"var {tmp} = {expr};");
                    expr = tmp;
                }
                sources[i] = expr;
            }
            else
            {
                var name = context.GetValueName(arg);
                if (parallel && targetPhis.Contains(arg) && arg != phi)
                {
                    var tmp = $"_pc_{_phiCopyCounter++}";
                    var elemType = GetVectorElementType(arg) ?? "int";
                    context.WriteLine(
                        $"var {tmp} = ({elemType}[]){name}.Clone();");
                    name = tmp;
                }
                sources[i] = name;
            }
        }

        bool fullMask = mask == CPUExpressionEmitter.ActiveMaskName;
        for (int i = 0; i < assignments.Count; i++)
        {
            var (phi, arg) = assignments[i];
            if (arg == phi)
                continue;
            var phiName = context.GetValueName(phi);
            if (analysis.IsScalar(phi))
            {
                // All lanes taking this edge agree on a uniform phi
                context.WriteLine(fullMask
                    ? $"{phiName} = {sources[i]};"
                    : $"if (CPUVectorIntrinsics.Any({mask})) " +
                        $"{phiName} = {sources[i]};");
                continue;
            }

            var elemType = GetVectorElementType(phi) ?? "int";
            if (analysis.IsScalar(arg))
            {
                var value = elemType == "nint"
                    ? $"(nint)({sources[i]})"
                    : sources[i];
                context.WriteLine(
                    $"CPUVectorIntrinsics.SelectFill<{elemType}>(" +
                    $"{phiName}, {mask}, {value});");
            }
            else
            {
                context.WriteLine(
                    $"CPUVectorIntrinsics.Select<{elemType}>(" +
                    $"{phiName}, {mask}, {sources[i]}, {phiName});");
            }
        }
    }
}
