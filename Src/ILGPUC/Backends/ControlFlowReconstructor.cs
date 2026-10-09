// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ControlFlowReconstructor.cs
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Backends;

/// <summary>
/// Reconstructs high-level control flow structures (loops, if-statements)
/// from basic blocks in SSA form using dominator analysis.
/// </summary>
/// <remarks>
/// Constructs a new control flow reconstructor.
/// </remarks>
sealed class ControlFlowReconstructor(Method method)
{
    /// <summary>
    /// Reconstructs control flow of the given method.
    /// </summary>
    /// <param name="method">The method to reconstruct control-flow for.</param>
    /// <returns>Reconstructed control-flow structure.</returns>
    public static ControlFlowStructure Reconstruct(Method method) =>
        new ControlFlowReconstructor(method).ReconstructControlFlow();

    private readonly Loops<ReversePostOrder<BasicBlock>, Forwards> _loops =
        method.CreateLoops();
    private readonly Dominators<Backwards> _postDominators =
        method.Blocks.CreatePostDominators();
    private readonly ValueSet<Method, BasicBlock> _processedBlocks =
        method.CreateSet<BasicBlock>();

    /// <summary>
    /// Reconstructs the control flow structure for the method.
    /// </summary>
    public ControlFlowStructure ReconstructControlFlow()
    {
        var structure = new ControlFlowStructure();
        ReconstructBlock(method.EntryBlock, structure);
        CollectOrphanedBlocks(structure);
        return structure;
    }

    /// <summary>
    /// Recursively reconstructs control flow starting from a block.
    /// </summary>
    private void ReconstructBlock(
        BasicBlock block,
        ControlFlowStructure structure,
        BasicBlock? exitBlock = null)
    {
        if (_processedBlocks.Contains(block) || block == exitBlock)
            return;

        _processedBlocks.Add(block);

        // Check if this block is a loop header
        if (_loops.TryGetLoop(block, out var loop) && loop.Headers.Contains(block))
        {
            ReconstructLoop(loop, structure);
            return;
        }

        // Add the block to the structure
        structure.AddBlock(block);

        // Process based on termination kind
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Return:
                // Nothing more to process
                break;
            case BlockTerminationKind.Unconditional:
                ReconstructBlock(block.Successors[0], structure, exitBlock);
                break;
            case BlockTerminationKind.Conditional:
                ReconstructIfStatement(block, structure, exitBlock);
                break;
            case BlockTerminationKind.Switch:
                ReconstructSwitchStatement(block, structure, exitBlock);
                break;
            default:
                throw new NotSupportedException(
                    $"Termination kind {block.TerminationKind} not supported");
        }
    }

    /// <summary>
    /// Reconstructs an if-statement from a conditional branch.
    /// </summary>
    private void ReconstructIfStatement(
        BasicBlock block,
        ControlFlowStructure structure,
        BasicBlock? exitBlock)
    {
        var conditionalView = block.AsConditionalView();
        var trueTarget = conditionalView.TrueTarget;
        var falseTarget = conditionalView.FalseTarget;

        // Try to find the merge point (common successor)
        var mergeBlock = FindMergeBlock(trueTarget, falseTarget);

        // Process true branch
        var trueStructure = new ControlFlowStructure();
        ReconstructBlock(trueTarget, trueStructure, mergeBlock);

        // Process false branch
        var falseStructure = new ControlFlowStructure();
        ReconstructBlock(falseTarget, falseStructure, mergeBlock);

        // Create the if statement with all branches
        var ifStmt = new IfStatement(block, trueStructure, falseStructure, mergeBlock);
        structure.AddStatement(ifStmt);

        // Continue after the merge point
        if (mergeBlock != null && mergeBlock != exitBlock)
            ReconstructBlock(mergeBlock, structure, exitBlock);
    }

    /// <summary>
    /// Reconstructs a switch statement from a switch branch.
    /// </summary>
    private void ReconstructSwitchStatement(
        BasicBlock block,
        ControlFlowStructure structure,
        BasicBlock? exitBlock)
    {
        // Find merge block (common successor of all cases)
        var switchView = block.AsSwitchView();
        var caseTargets = new List<BasicBlock> { switchView.DefaultTarget };
        for (int i = 0; i < switchView.NumCases; i++)
            caseTargets.Add(switchView.GetCaseTarget(i));

        var mergeBlock = FindMergeBlock([.. caseTargets]);

        // Process each case
        var cases = new List<(int CaseValue, ControlFlowStructure Body)>(
            switchView.NumCases);
        for (int i = 0; i < switchView.NumCases; i++)
        {
            var caseStructure = new ControlFlowStructure();
            ReconstructBlock(switchView.GetCaseTarget(i), caseStructure, mergeBlock);
            cases.Add((i, caseStructure));
        }

        // Process default case
        var defaultStructure = new ControlFlowStructure();
        ReconstructBlock(switchView.DefaultTarget, defaultStructure, mergeBlock);

        // Create the switch statement with all cases
        var switchStmt = new SwitchStatement(
            block, cases, defaultStructure, mergeBlock);
        structure.AddStatement(switchStmt);

        // Continue after merge point
        if (mergeBlock != null && mergeBlock != exitBlock)
            ReconstructBlock(mergeBlock, structure, exitBlock);
    }

    /// <summary>
    /// Collects any blocks not reached by structured reconstruction into an
    /// <see cref="UnstructuredRegion"/> fallback.
    /// </summary>
    private void CollectOrphanedBlocks(ControlFlowStructure structure)
    {
        var orphans = new List<BasicBlock>();
        var orphanSet = method.CreateSet<BasicBlock>();
        foreach (var block in method.Blocks)
        {
            if (!_processedBlocks.Contains(block))
            {
                orphans.Add(block);
                orphanSet.Add(block);
                _processedBlocks.Add(block);
            }
        }
        if (orphans.Count == 0) return;

        var region = new UnstructuredRegion(orphans[0], orphans, orphanSet);
        structure.AddStatement(region);
    }

    /// <summary>
    /// Reconstructs an irreducible (multi-header) loop as an
    /// <see cref="UnstructuredRegion"/> state machine.
    /// </summary>
    private void ReconstructIrreducibleRegion(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        ControlFlowStructure structure)
    {
        // Mark all members processed
        var regionSet = method.CreateSet<BasicBlock>();
        foreach (var member in loop.AllMembers)
        {
            _processedBlocks.Add(member);
            regionSet.Add(member);
        }

        // Collect blocks in method RPO order
        var orderedBlocks = new List<BasicBlock>();
        foreach (var block in method.Blocks)
            if (regionSet.Contains(block))
                orderedBlocks.Add(block);

        var region = new UnstructuredRegion(
            loop.Headers[0], orderedBlocks, regionSet);
        structure.AddStatement(region);

        // Continue with exit blocks
        foreach (var exitBlock in loop.Exits)
            ReconstructBlock(exitBlock, structure);
    }

    /// <summary>
    /// Reconstructs a loop structure with proper classification (for, while, do-while).
    /// </summary>
    private void ReconstructLoop(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        ControlFlowStructure structure)
    {
        // Irreducible loops (multi-header) cannot be mapped to structured control flow
        if (loop.Headers.Length > 1)
        {
            ReconstructIrreducibleRegion(loop, structure);
            return;
        }

        var header = loop.Headers[0];
        _processedBlocks.Add(header);

        // Get loop body blocks. Process from the header's non-exit
        // successors recursively — this ensures proper CFG ordering
        // so that if-statement condition blocks are processed before
        // their branch targets (preventing premature processing that
        // would leave branches empty and break phi source tracking).
        var bodyStructure = new ControlFlowStructure();
        bodyStructure.AddBlock(header);

        var exitBlock = loop.Exits.Length > 0 ? loop.Exits[0] : null;
        foreach (var succ in header.Successors)
        {
            if (succ != exitBlock && !_processedBlocks.Contains(succ))
                ReconstructBlock(succ, bodyStructure, exitBlock);
        }

        // Try to classify the loop type and detect induction variables
        var loopStmt = ClassifyLoop(loop, header, bodyStructure);
        structure.AddStatement(loopStmt);

        // Continue after the loop
        if (exitBlock != null)
            ReconstructBlock(exitBlock, structure);
    }

    /// <summary>
    /// Classifies a loop as for, while, or do-while based on its structure.
    /// </summary>
    private LoopStatement ClassifyLoop(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock header,
        ControlFlowStructure body)
    {
        // Extract the loop condition from the header terminator
        Value? condition = null;
        if (header.TerminationKind == BlockTerminationKind.Conditional)
        {
            condition = header.TerminationCondition;
        }

        // Check if this is a do-while loop (back edge directly to header
        // without condition check). Detect BEFORE for-loop, because do-while
        // bodies compute values used after the loop exit, and converting
        // to for-loop would block-scope those values.
        bool isDoWhile = loop.BackEdges.Length > 0 &&
                         loop.BackEdges[0].TerminationKind ==
                         BlockTerminationKind.Unconditional;

        // Self-loop detection: if the header IS a back-edge block, the body
        // and condition are in the same block. This has do-while semantics
        // (body runs at least once). Stored in ForLoop for CPU compatibility,
        // but the GPU emitter uses IsSelfLoop to emit do-while.
        bool isSelfLoop = false;
        foreach (var be in loop.BackEdges)
        {
            if (be == header) { isSelfLoop = true; break; }
        }

        // For-loop detection: try to find an induction variable.
        // Only create a ForLoop if the condition can be evaluated before
        // the loop body — i.e. all its transitive operands are defined
        // outside the body (parameters, header phis, constants).
        if (condition != null)
        {
            var inductionVar = TryDetectInductionVariable(loop, header);
            if (inductionVar.HasValue
                && IsConditionDefinedOutsideBody(condition, loop, header))
                return new ForLoop(loop, body, inductionVar.Value, condition,
                    isSelfLoop);
        }

        if (condition != null)
        {
            // Self-loops have the same do-while semantics as the canonical
            // multi-block do-while (body executes before the condition is
            // checked). Emitting them as `while (cond) { body }` is wrong
            // because `cond` references SSA values defined inside the body.
            if (isDoWhile || isSelfLoop)
                return new DoWhileLoop(loop, body, condition, isSelfLoop);
            else
                return new WhileLoop(loop, body, condition);
        }

        // Default to while loop if no specific pattern is detected
        // Use a constant true condition as a placeholder
        Value? trueCondition = null;
        foreach (var value in method.EntryBlock.Values)
        {
            if (value is PrimitiveValue prim && prim.Int1Value)
            {
                trueCondition = prim;
                break;
            }
        }

        return new WhileLoop(loop, body, trueCondition ?? condition!);
    }

    /// <summary>
    /// Attempts to detect an induction variable in a loop.
    /// </summary>
    private static InductionVariable? TryDetectInductionVariable(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock header)
    {
        // Look for phi values in the header that might be induction variables
        foreach (var value in header.Values)
        {
            if (value is not PhiValue phi)
                continue;

            // A simple induction variable has two sources:
            // 1. Initial value from outside the loop
            // 2. Update value from inside the loop (phi + step or phi - step)
            if (phi.NumArguments != 2)
                continue;

            // Identify which source comes from outside the loop
            Value? initialValue = null;
            Value? updateValue = null;
            BasicBlock? updateBlock = null;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                var sourceBlock = phi.Sources[i];
                var sourceValue = phi.GetValue<Value>(i);

                if (loop.AllMembers.Contains(sourceBlock))
                {
                    updateValue = sourceValue;
                    updateBlock = sourceBlock;
                }
                else
                {
                    initialValue = sourceValue;
                }
            }

            if (initialValue == null || updateValue == null || updateBlock == null)
                continue;

            // Check if the update value is a simple increment or decrement
            if (TryGetStepValue(updateValue, phi, out var stepValue, out var isIncrement))
            {
                return new InductionVariable(phi, initialValue, stepValue, isIncrement);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether any BasicBlockValue defined inside the loop body
    /// (excluding the header) is used outside the loop. If so, a for-loop
    /// would make those values block-scoped and inaccessible after the loop.
    /// </summary>
    private static bool HasBodyValuesUsedOutside(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock header)
    {
        foreach (var block in loop.AllMembers)
        {
            if (block == header)
                continue;
            foreach (BasicBlockValue value in block)
            {
                if (value is PhiValue)
                    continue; // Phis are pre-declared
                if (HasUsesOutsideLoop(value, loop))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether any PureValue whose operands come from body blocks
    /// (excluding header) has uses outside the loop. Such PureValues would
    /// be placed inside the for-loop body by CodePlacement, making them
    /// block-scoped and inaccessible after the loop.
    /// </summary>
    /// <summary>
    /// Checks whether any non-induction PureValue used as a phi update
    /// in the header has uses outside the loop. The induction variable's
    /// phi update is safe because it's the phi itself (pre-declared).
    /// Other PureValues would be placed inside the for-loop body and
    /// become block-scoped.
    /// </summary>
    private static bool HasNonInductionPureValuesUsedOutside(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock header,
        InductionVariable inductionVar)
    {
        foreach (BasicBlockValue value in header)
        {
            if (value is not PhiValue phi || phi == inductionVar.Phi)
                continue;
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.GetValue<Value>(i) is PureValue pv
                    && HasUsesOutsideLoop(pv, loop))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether a value has any transitive use outside the loop.
    /// Follows PureValue chains to find the eventual BBV consumers.
    /// </summary>
    private static bool HasUsesOutsideLoop(
        Value value,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop)
    {
        foreach (var use in value.Uses)
        {
            if (use.Target is BasicBlockValue bbUse)
            {
                if (!loop.AllMembers.Contains(bbUse.BasicBlock))
                    return true;
            }
            else if (use.Target is PureValue pvUse)
            {
                // Follow PureValue chain to its eventual BBV consumers
                if (HasUsesOutsideLoop(pvUse, loop))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether the loop condition (and all its transitive operands)
    /// are defined outside the loop body. A for-loop evaluates its condition
    /// before each iteration, so it must not reference body-scoped values.
    /// Values defined outside the body include: parameters, globals,
    /// constants, header phi values, and pure values composed of the above.
    /// </summary>
    private static bool IsConditionDefinedOutsideBody(
        Value condition,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock header)
    {
        var visited = new HashSet<Value>();
        return CheckDefinedOutside(condition, loop, header, visited);

        static bool CheckDefinedOutside(
            Value value,
            Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
            BasicBlock header,
            HashSet<Value> visited)
        {
            if (!visited.Add(value))
                return true; // Already checked — no cycle

            // Constants, parameters, globals are always safe
            if (value is PrimitiveValue or Parameter or Global or UndefinedValue)
                return true;

            // Phi values in the header are safe (they're loop-carried)
            if (value is PhiValue phi && phi.BasicBlock == header)
                return true;

            // BasicBlockValues defined inside loop body are NOT safe
            if (value is BasicBlockValue bbv)
            {
                if (bbv.BasicBlock != header && loop.AllMembers.Contains(bbv.BasicBlock))
                    return false;
                // Values in the header itself are safe
                return true;
            }

            // PureValues: recursively check all operands
            if (value is PureValue pv)
            {
                foreach (var operand in pv.Values)
                {
                    if (!CheckDefinedOutside(operand, loop, header, visited))
                        return false;
                }
                return true;
            }

            // Unknown value type — be conservative
            return false;
        }
    }

    /// <summary>
    /// Attempts to extract the step value from an update expression.
    /// </summary>
    private static bool TryGetStepValue(
        Value updateValue,
        PhiValue phi,
        out Value stepValue,
        out bool isIncrement)
    {
        stepValue = null!;
        isIncrement = false;

        // Check for arithmetic operations like phi + step or phi - step
        if (updateValue is BinaryArithmeticValue arith)
        {
            // Check if one operand is the phi itself
            var left = arith.GetValue<Value>(0);
            var right = arith.GetValue<Value>(1);

            if (left == phi)
            {
                stepValue = right;
                isIncrement = arith.Kind == BinaryArithmeticKind.Add;
                return true;
            }
            else if (right == phi && arith.Kind == BinaryArithmeticKind.Add)
            {
                stepValue = left;
                isIncrement = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the merge block (immediate common post-dominator) of multiple blocks.
    /// </summary>
    private BasicBlock? FindMergeBlock(params BasicBlock[] blocks)
    {
        if (blocks.Length == 0)
            return null;

        if (blocks.Length == 1)
        {
            // For a single block, return its immediate post-dominator if it exists
            // This is the block that will be executed after this one
            var successors = blocks[0].Successors;
            return successors.Length > 0
                ? _postDominators.GetImmediateDominator(blocks[0])
                : null;
        }

        // Use post-dominator analysis to find the immediate common post-dominator
        // This is the merge point where control flow converges after all branches
        return _postDominators.GetImmediateCommonDominator(blocks);
    }
}

/// <summary>
/// Represents the reconstructed control flow structure.
/// </summary>
sealed class ControlFlowStructure
{
    /// <summary>
    /// Sequential list of blocks and statements.
    /// </summary>
    public List<object> Elements { get; } = new();

    /// <summary>
    /// Adds a basic block to the structure.
    /// </summary>
    public void AddBlock(BasicBlock block) =>
        Elements.Add(block);

    /// <summary>
    /// Adds a control flow statement to the structure.
    /// </summary>
    public void AddStatement(ControlFlowStatement statement) =>
        Elements.Add(statement);
}

/// <summary>
/// Base class for high-level control flow statements.
/// </summary>
abstract class ControlFlowStatement;

/// <summary>
/// Represents a reconstructed if-statement.
/// </summary>
sealed class IfStatement(
    BasicBlock block,
    ControlFlowStructure trueBranch,
    ControlFlowStructure falseBranch,
    BasicBlock? mergeBlock) : ControlFlowStatement
{
    /// <summary>
    /// Returns the source basic block containing the conditional branch.
    /// </summary>
    public BasicBlock Block { get; } = block;

    /// <summary>
    /// Returns the condition value.
    /// </summary>
    public Value Condition => Block.TerminationCondition!;

    /// <summary>
    /// Returns the true branch.
    /// </summary>
    public ControlFlowStructure TrueBranch { get; } = trueBranch;

    /// <summary>
    /// Returns the false branch.
    /// </summary>
    public ControlFlowStructure FalseBranch { get; } = falseBranch;

    /// <summary>
    /// Returns the merge block where control flow converges after both branches,
    /// or null if there is no merge point (e.g., both branches return).
    /// </summary>
    public BasicBlock? MergeBlock { get; } = mergeBlock;
}

/// <summary>
/// Represents a reconstructed loop.
/// </summary>
abstract class LoopStatement(
    Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
    ControlFlowStructure body) : ControlFlowStatement
{
    /// <summary>
    /// Returns the main loop.
    /// </summary>
    public Loops<ReversePostOrder<BasicBlock>, Forwards>.Node Loop { get; } = loop;

    /// <summary>
    /// Returns the body.
    /// </summary>
    public ControlFlowStructure Body { get; } = body;
}

/// <summary>
/// Represents a simple while loop (condition at the beginning).
/// </summary>
sealed class WhileLoop(
    Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
    ControlFlowStructure body,
    Value condition) : LoopStatement(loop, body)
{
    /// <summary>
    /// Returns the loop condition.
    /// </summary>
    public Value Condition { get; } = condition;
}

/// <summary>
/// Represents a do-while loop (condition at the end).
/// </summary>
sealed class DoWhileLoop(
    Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
    ControlFlowStructure body,
    Value condition,
    bool isSelfLoop = false) : LoopStatement(loop, body)
{
    /// <summary>
    /// Returns the loop condition.
    /// </summary>
    public Value Condition { get; } = condition;

    /// <summary>
    /// Returns true if the loop header IS the back-edge block (self-loop).
    /// Self-loop conditions reference SSA values that the back-edge phi
    /// update would overwrite, so the emitter must capture the condition
    /// into a temporary before updating the phi variables.
    /// </summary>
    public bool IsSelfLoop { get; } = isSelfLoop;
}

/// <summary>
/// Represents an induction variable for a for-loop.
/// </summary>
/// <param name="Phi">The induction phi.</param>
/// <param name="InitialValue">The initial for-loop value.</param>
/// <param name="StepValue">The underlying step value.</param>
/// <param name="IsIncrement">True if this is an increment for loop.</param>
readonly record struct InductionVariable(
    PhiValue Phi,
    Value InitialValue,
    Value StepValue,
    bool IsIncrement);

/// <summary>
/// Represents a for loop with an induction variable.
/// </summary>
sealed class ForLoop(
    Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
    ControlFlowStructure body,
    InductionVariable inductionVariable,
    Value condition,
    bool isSelfLoop = false) : LoopStatement(loop, body)
{
    /// <summary>
    /// Returns true if the loop header IS the back-edge block (self-loop).
    /// Self-loops have do-while semantics: the body always executes before
    /// the condition is checked. GPU emitters should emit these as
    /// <c>do { body } while (condition)</c> instead of
    /// <c>for (init; condition; step)</c>.
    /// </summary>
    public bool IsSelfLoop { get; } = isSelfLoop;

    /// <summary>
    /// Returns the underlying induction variable.
    /// </summary>
    public InductionVariable InductionVariable { get; } = inductionVariable;

    /// <summary>
    /// Returns the loop condition.
    /// </summary>
    public Value Condition { get; } = condition;
}

/// <summary>
/// Represents a reconstructed switch statement.
/// </summary>
sealed class SwitchStatement(
    BasicBlock block,
    List<(int CaseValue, ControlFlowStructure Body)> cases,
    ControlFlowStructure defaultCase,
    BasicBlock? mergeBlock = null) : ControlFlowStatement
{
    /// <summary>
    /// Returns the source basic block containing the switch branch.
    /// </summary>
    public BasicBlock Block { get; } = block;

    /// <summary>
    /// Returns the condition value.
    /// </summary>
    public Value Condition => Block.TerminationCondition!;

    /// <summary>
    /// Returns the list of all cases.
    /// </summary>
    public List<(int CaseValue, ControlFlowStructure Body)> Cases { get; } = cases;

    /// <summary>
    /// Returns the default case.
    /// </summary>
    public ControlFlowStructure DefaultCase { get; } = defaultCase;

    /// <summary>
    /// Returns the merge block (common successor of all cases), or null.
    /// </summary>
    public BasicBlock? MergeBlock { get; } = mergeBlock;
}

/// <summary>
/// Represents basic blocks that could not be mapped to structured control flow
/// (e.g. irreducible loops). Emitters convert this to a while+switch state machine.
/// </summary>
sealed class UnstructuredRegion : ControlFlowStatement
{
    /// <summary>Blocks in the region, ordered in method RPO.</summary>
    public List<BasicBlock> Blocks { get; }

    /// <summary>Maps each block to its 0-based integer state ID.</summary>
    public Dictionary<BasicBlock, int> BlockIds { get; }

    /// <summary>The entry block (state machine start).</summary>
    public BasicBlock EntryBlock { get; }

    /// <summary>
    /// Exit blocks (outside region) mapped to sentinel state IDs
    /// (starting at Blocks.Count).
    /// </summary>
    public Dictionary<BasicBlock, int> ExitSentinels { get; }

    /// <summary>
    /// Constructs a new unstructured code block region.
    /// </summary>
    /// <param name="entryBlock">The entry block.</param>
    /// <param name="blocks">The list of blocks in this region.</param>
    /// <param name="regionSet">The set for this region.</param>
    public UnstructuredRegion(
        BasicBlock entryBlock,
        List<BasicBlock> blocks,
        ValueSet<Method, BasicBlock> regionSet)
    {
        EntryBlock = entryBlock;
        Blocks = blocks;
        BlockIds = new(blocks.Count);
        for (int i = 0; i < blocks.Count; i++)
            BlockIds[blocks[i]] = i;

        ExitSentinels = new();
        int sentinel = blocks.Count;
        foreach (var block in blocks)
            foreach (var succ in block.Successors)
                if (!regionSet.Contains(succ) && !ExitSentinels.ContainsKey(succ))
                    ExitSentinels[succ] = sentinel++;
    }

    /// <summary>
    /// True if <paramref name="block"/> is an exit from this region.
    /// </summary>
    public bool IsExit(BasicBlock block) => ExitSentinels.ContainsKey(block);

    /// <summary>
    /// Returns the state ID for a successor (block ID or exit sentinel).
    /// </summary>
    public int GetTargetState(BasicBlock succ) =>
        ExitSentinels.TryGetValue(succ, out var s) ? s : BlockIds[succ];
}
