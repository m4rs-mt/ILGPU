// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CodePlacement.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DominanceOrder = ILGPUC.IR.Analyses.ReversePostOrder<
    ILGPUC.IR.MethodValues.BasicBlock>;
using PreDominators = ILGPUC.IR.Analyses.Dominators<ILGPUC.IR.MethodValues.Forwards>;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents a placed block containing all values in linear sequence.
/// The last value is always the block terminator.
/// </summary>
sealed class PlacedBlock
{
    #region Nested Types

    /// <summary>
    /// An enumerator to iterate over all placed values.
    /// </summary>
    /// <param name="placedBlock">The parent placed block.</param>
    internal struct Enumerator(PlacedBlock placedBlock)
    {
        private int _index = -1;

        /// <inheritdoc cref="IEnumerator.Current"/>
        public readonly Value<Method> Current => placedBlock[_index];

        /// <inheritdoc cref="IEnumerator.MoveNext()"/>
        public bool MoveNext() => ++_index < placedBlock.Count;
    }

    #endregion

    #region Instance

    private InlineList<Value<Method>> _values;

    /// <summary>
    /// Constructs a new placed block.
    /// </summary>
    /// <param name="basicBlock">The source basic block.</param>
    /// <param name="capacity">The initial capacity.</param>
    internal PlacedBlock(BasicBlock basicBlock, int capacity)
    {
        BasicBlock = basicBlock;
        _values = InlineList<Value<Method>>.Create(capacity);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the source basic block.
    /// </summary>
    public BasicBlock BasicBlock { get; }

    /// <summary>
    /// Returns the number of placed values in this block.
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// Returns the termination kind of this placed block.
    /// </summary>
    public BlockTerminationKind TerminationKind => BasicBlock.TerminationKind;

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The value index.</param>
    public Value<Method> this[int index] => _values[index];

    /// <summary>
    /// Returns all placed values as a read-only span.
    /// </summary>
    public ReadOnlySpan<Value<Method>> Values => _values.AsReadOnlySpan();

    #endregion

    #region Methods

    /// <summary>
    /// Adds a value to this placed block.
    /// </summary>
    /// <param name="value">The value to add.</param>
    internal void Add(Value<Method> value) => _values.Add(value);

    /// <summary>
    /// Returns an enumerator to iterate over all placed values.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    #endregion
}

/// <summary>
/// Represents a code placement analysis that places all values (including PureValues)
/// in a linear sequence per basic block, respecting data dependencies.
/// PureValues are placed as late as possible to minimize live spans.
/// </summary>
sealed class CodePlacement
{
    #region Nested Types

    /// <summary>
    /// An enumerator to iterate over all placed blocks.
    /// </summary>
    /// <param name="codePlacement">The parent code placement.</param>
    internal struct Enumerator(CodePlacement codePlacement)
    {
        private int _index = -1;

        /// <inheritdoc cref="IEnumerator.Current"/>
        public readonly PlacedBlock Current => codePlacement._placedBlocks[_index];

        /// <inheritdoc cref="IEnumerator.MoveNext()"/>
        public bool MoveNext() => ++_index < codePlacement._placedBlocks.Count;
    }

    #endregion

    #region Static

    /// <summary>
    /// Creates a new code placement analysis.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <param name="cfg">The optional CFG instance to use.</param>
    /// <returns>The created code placement analysis.</returns>
    public static CodePlacement Create(
        Method method,
        CFG<DominanceOrder, Forwards>? cfg = null) => new(method, cfg);

    #endregion

    #region Instance

    /// <summary>
    /// Stores all placed blocks in reverse post order.
    /// </summary>
    private InlineList<PlacedBlock> _placedBlocks;

    /// <summary>
    /// Maps basic blocks to their placed blocks.
    /// </summary>
    private readonly ValueMap<Method, BasicBlock, PlacedBlock> _blockMap;

    /// <summary>
    /// Tracks which pure values have been placed.
    /// </summary>
    private readonly ValueSet<Method, PureValue> _placedPureValues;

    /// <summary>
    /// Maps pure values to their placement blocks (computed lazily).
    /// </summary>
    private readonly ValueMap<Method, PureValue, BasicBlock> _pureValuePlacements;

    /// <summary>
    /// Maps basic blocks to the set of pure values that should be placed there.
    /// </summary>
    private readonly ValueMap<Method, BasicBlock, List<PureValue>> _blockToPureValues;

    /// <summary>
    /// Constructs a new code placement analysis.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <param name="cfg">An existing CFG instance to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private CodePlacement(Method method, CFG<DominanceOrder, Forwards>? cfg)
    {
        method.Assert(cfg is null || cfg.Root.Block == method.EntryBlock);

        Method = method;
        Dominators = (cfg ?? method.Blocks.CreateCFG()).CreateDominators();

        _placedBlocks = InlineList<PlacedBlock>.Create(method.Blocks.Count);
        _blockMap = method.CreateMap<BasicBlock, PlacedBlock>();
        _placedPureValues = method.CreateSet<PureValue>();
        _pureValuePlacements = method.CreateMap<PureValue, BasicBlock>();
        _blockToPureValues = method.CreateMap<BasicBlock, List<PureValue>>();

        // First pass: compute optimal placement blocks for all pure values
        ComputePureValuePlacements();

        // Second pass: place all values in order
        PlaceAllValues();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the analyzed method.
    /// </summary>
    public Method Method { get; }

    /// <summary>
    /// Returns the dominators of the analyzed method.
    /// </summary>
    public PreDominators Dominators { get; }

    /// <summary>
    /// Returns the number of placed blocks.
    /// </summary>
    public int Count => _placedBlocks.Count;

    /// <summary>
    /// Gets the placed block at the specified index.
    /// </summary>
    /// <param name="index">The block index (in reverse post order).</param>
    public PlacedBlock this[int index] => _placedBlocks[index];

    #endregion

    #region Methods

    /// <summary>
    /// Computes the optimal placement block for all pure values.
    /// Uses dominators to place values as late as possible while respecting
    /// data dependencies.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ComputePureValuePlacements()
    {
        // Process all blocks and their values to find pure values
        foreach (var block in Method.Blocks)
        {
            foreach (BasicBlockValue bbValue in block)
            {
                // Process all operands of this basic block value
                foreach (var operand in bbValue.Values)
                {
                    if (operand is PureValue pureValue)
                        ComputePureValuePlacement(pureValue, block);
                }
            }

            if (block.TerminationValue is PureValue terminationValue)
                ComputePureValuePlacement(terminationValue, block);
        }
    }

    /// <summary>
    /// Recursively computes the placement for a pure value and its dependencies.
    /// </summary>
    /// <param name="pureValue">The pure value to place.</param>
    /// <param name="useBlock">The block where the value is used.</param>
    /// <returns>The computed placement block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private BasicBlock ComputePureValuePlacement(PureValue pureValue, BasicBlock useBlock)
    {
        // Check if we already computed the placement
        if (_pureValuePlacements.TryGetValue(pureValue, out var existingPlacement))
        {
            // Update placement to be the common dominator of existing and new use,
            // but never above the earliest valid block (operand constraint)
            var newPlacement = Dominators.GetImmediateCommonDominator(
                existingPlacement,
                useBlock);
            if (newPlacement != existingPlacement)
            {
                newPlacement = EnforceOperandConstraint(
                    pureValue, newPlacement, useBlock);
                _pureValuePlacements[pureValue] = newPlacement;
            }
            return newPlacement;
        }

        // Compute earliest valid placement: the deepest block where ALL
        // operands are available. We push down from entry toward the deepest
        // operand definition, since an operand defined in block B is only
        // available in blocks dominated by B.
        var earliestBlock = Method.EntryBlock;

        foreach (var operand in pureValue.Values)
        {
            BasicBlock operandBlock;
            if (operand is PureValue childPure)
            {
                // Recursively compute placement for child pure values
                operandBlock = ComputePureValuePlacement(childPure, useBlock);
            }
            else if (operand is BasicBlockValue bbOperand)
            {
                // Skip operands whose block is not in this method's CFG (e.g.,
                // references to External methods' entry blocks).
                if (bbOperand.BasicBlock.Method != Method)
                    continue;
                operandBlock = bbOperand.BasicBlock;
            }
            else
            {
                // Parameters are available in the entry block, so no constraint.
                // PhiValues are associated with basic blocks and are implicitly
                // placed accordingly.
                continue;
            }

            // Push earliestBlock deeper if this operand is in a deeper block
            if (Dominators.Dominates(earliestBlock, operandBlock))
                earliestBlock = operandBlock;
            else if (!Dominators.Dominates(operandBlock, earliestBlock))
                // Defensive: divergent branches — fall back to use site
                earliestBlock = useBlock;
        }

        // Compute latest placement, constrained to not go above earliestBlock
        var placementBlock = ComputeLatestPlacement(pureValue, earliestBlock);

        _pureValuePlacements.Add(pureValue, placementBlock);

        // Add to the reverse map: this block should contain this pure value
        if (!_blockToPureValues.TryGetValue(placementBlock, out var pureList))
        {
            pureList = new List<PureValue>(4);
            _blockToPureValues.Add(placementBlock, pureList);
        }
        pureList.Add(pureValue);

        return placementBlock;
    }

    /// <summary>
    /// Ensures a proposed placement doesn't move above any operand's definition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private BasicBlock EnforceOperandConstraint(
        PureValue pureValue,
        BasicBlock proposed,
        BasicBlock useBlock)
    {
        foreach (var operand in pureValue.Values)
        {
            BasicBlock operandBlock;
            if (operand is BasicBlockValue bbOp && bbOp.BasicBlock.Method == Method)
                operandBlock = bbOp.BasicBlock;
            else if (operand is PureValue pvOp
                && _pureValuePlacements.TryGetValue(pvOp, out var pvBlock))
                operandBlock = pvBlock;
            else
                continue;

            // If proposed placement is strictly above this operand, push it down
            if (Dominators.Dominates(proposed, operandBlock)
                && proposed != operandBlock)
            {
                proposed = operandBlock;
            }
            else if (!Dominators.Dominates(operandBlock, proposed)
                && proposed != operandBlock)
            {
                // Divergent — fall back to use site
                proposed = useBlock;
            }
        }
        return proposed;
    }

    /// <summary>
    /// Computes the latest possible placement for a pure value based on its uses.
    /// </summary>
    /// <param name="pureValue">The pure value.</param>
    /// <param name="earliestBlock">The earliest block where it can be placed.</param>
    /// <returns>The latest valid placement block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private BasicBlock ComputeLatestPlacement(
        PureValue pureValue,
        BasicBlock earliestBlock)
    {
        // Get the common dominator of all use sites
        var latestBlock = earliestBlock;

        foreach (Use use in pureValue.Uses)
        {
            BasicBlock useBlock;
            if (use.Target is BasicBlockValue bbValue)
            {
                // Skip uses whose block is not in this method's CFG (e.g.,
                // references from External methods' entry blocks).
                if (bbValue.BasicBlock.Method != Method)
                    continue;

                // Handle phi values specially - the use is in the predecessor block
                if (use.Target is PhiValue phiValue)
                    useBlock = phiValue.Sources[use.Index];
                else
                    useBlock = bbValue.BasicBlock;
            }
            else if (use.Target is PureValue pureUse)
            {
                // For pure value uses, get the computed placement of the user
                if (_pureValuePlacements.TryGetValue(pureUse, out var pureUsePlacement))
                    useBlock = pureUsePlacement;
                else
                    useBlock = earliestBlock;
            }
            else
            {
                continue;
            }

            latestBlock = Dominators.GetImmediateCommonDominator(latestBlock, useBlock);
        }

        // Ensure we don't place before the earliest valid block
        if (!Dominators.Dominates(earliestBlock, latestBlock))
            return earliestBlock;

        return latestBlock;
    }

    /// <summary>
    /// Places all values in their respective blocks in the correct order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PlaceAllValues()
    {
        // Process blocks in reverse post order
        var placedSet = Method.CreateSet<BasicBlockValue>();
        foreach (var block in Method.Blocks)
        {
            // Estimate capacity: original values + some pure values
            var placedBlock = new PlacedBlock(block, block.Count * 2);
            _placedBlocks.Add(placedBlock);
            _blockMap.Add(block, placedBlock);

            // Place all values for this block
            PlaceBlockValues(block, placedBlock, placedSet);
        }
    }

    /// <summary>
    /// Places all values belonging to a specific block.
    /// </summary>
    /// <param name="block">The source basic block.</param>
    /// <param name="placedBlock">The target placed block.</param>
    /// <param name="placedSet">The set of placed basic block values.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PlaceBlockValues(
        BasicBlock block,
        PlacedBlock placedBlock,
        ValueSet<Method, BasicBlockValue> placedSet)
    {
        // Process all basic block values from the chain
        foreach (var bbValue in block.BasicBlockValues)
        {
            PlacePureValueDependencies(bbValue, block, placedBlock);
            placedBlock.Add(bbValue);
            placedSet.Add(bbValue);
        }

        // Ensure all pure values assigned to this block are placed
        if (_blockToPureValues.TryGetValue(block, out var assignedPureValues))
        {
            foreach (var pureValue in assignedPureValues)
                PlacePureValue(pureValue, block, placedBlock);
        }

        // Handle termination-related values (condition, return value).
        // These may be orphaned BasicBlockValues not in the chain.
        if (block.TerminationValue is PureValue pvTerm)
        {
            PlacePureValue(pvTerm, block, placedBlock);
        }
        else if (block.TerminationValue is BasicBlockValue bbTerm
            && !placedSet.Contains(bbTerm))
        {
            PlaceOrphanedValue(bbTerm, block, placedBlock, placedSet);
        }

        // Handle orphaned phi arguments — phi values reference operands
        // that may not be in any block's value chain after transformation.
        foreach (var phi in block.PhiValues)
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.Arguments[i] is BasicBlockValue bbArg
                    && !placedSet.Contains(bbArg))
                {
                    PlaceOrphanedValue(bbArg, block, placedBlock, placedSet);
                }
            }
        }
    }

    /// <summary>
    /// Places all pure value dependencies of a value that should be in this block.
    /// </summary>
    /// <param name="value">The value whose dependencies to place.</param>
    /// <param name="block">The current block.</param>
    /// <param name="placedBlock">The target placed block.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PlacePureValueDependencies(
        Value value,
        BasicBlock block,
        PlacedBlock placedBlock)
    {
        foreach (var operand in value.Values)
        {
            if (operand is PureValue pureValue)
                PlacePureValue(pureValue, block, placedBlock);
        }
    }

    /// <summary>
    /// Recursively places a BasicBlockValue and all its BasicBlockValue
    /// operands that are not already placed. Handles "orphaned" values
    /// left by transformation (e.g., termination conditions or phi
    /// arguments not in the block's linked list).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PlaceOrphanedValue(
        BasicBlockValue value,
        BasicBlock block,
        PlacedBlock placedBlock,
        ValueSet<Method, BasicBlockValue> placedSet)
    {
        if (!placedSet.Add(value))
            return;

        // Recursively place BasicBlockValue operands first (depth-first)
        foreach (var operand in value.Values)
        {
            if (operand is BasicBlockValue bbOp && !placedSet.Contains(bbOp))
                PlaceOrphanedValue(bbOp, block, placedBlock, placedSet);
            else if (operand is PureValue pvOp)
                PlacePureValue(pvOp, block, placedBlock);
        }

        PlacePureValueDependencies(value, block, placedBlock);
        placedBlock.Add(value);
    }

    /// <summary>
    /// Places a pure value and its dependencies if it belongs in this block.
    /// </summary>
    /// <param name="pureValue">The pure value to potentially place.</param>
    /// <param name="block">The current block.</param>
    /// <param name="placedBlock">The target placed block.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PlacePureValue(
        PureValue pureValue,
        BasicBlock block,
        PlacedBlock placedBlock)
    {
        // Check if already placed
        if (_placedPureValues.Contains(pureValue))
            return;

        // Check if this pure value should be placed in this block
        if (!_pureValuePlacements.TryGetValue(pureValue, out var placementBlock))
            placementBlock = block;

        if (placementBlock != block)
        {
            // This value should be placed in a different block.
            // It will be placed when we process its assigned block.
            return;
        }

        // First, recursively place all pure value dependencies
        foreach (var operand in pureValue.Values)
        {
            if (operand is PureValue childPure)
                PlacePureValue(childPure, block, placedBlock);
        }

        // Place the pure value itself
        _placedPureValues.Add(pureValue);
        placedBlock.Add(pureValue);
    }

    /// <summary>
    /// Gets the placed block for the given basic block.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <returns>The placed block containing all values in order.</returns>
    public PlacedBlock GetPlacedBlock(BasicBlock block) => _blockMap[block];

    /// <summary>
    /// Tries to get the placed block for the given basic block.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <param name="placedBlock">The placed block if found.</param>
    /// <returns>True if the placed block was found.</returns>
    public bool TryGetPlacedBlock(
        BasicBlock block,
        out PlacedBlock placedBlock) =>
        _blockMap.TryGetValue(block, out placedBlock!);

    /// <summary>
    /// Returns an enumerator to iterate over all placed blocks in reverse post order.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    #endregion
}

/// <summary>
/// Extension methods for code placement analysis.
/// </summary>
static class CodePlacementExtensions
{
    /// <summary>
    /// Creates a new code placement analysis for the given method.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <param name="cfg">The optional CFG to use.</param>
    /// <returns>The created code placement analysis.</returns>
    public static CodePlacement CreateCodePlacement(
        this Method method,
        CFG<DominanceOrder, Forwards>? cfg = null) =>
        CodePlacement.Create(method, cfg);

    /// <summary>
    /// Creates a new code placement analysis for the given basic block collection.
    /// </summary>
    /// <param name="blocks">The blocks to analyze.</param>
    /// <returns>The created code placement analysis.</returns>
    public static CodePlacement CreateCodePlacement(
        this BasicBlockCollection<DominanceOrder, Forwards> blocks) =>
        CodePlacement.Create(blocks.Method, cfg: null);
}
