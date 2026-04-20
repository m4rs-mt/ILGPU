// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlockTransform.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Rewriting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// A transformation of a basic block.
/// </summary>
sealed class BasicBlockTransform :
    BasicBlockBuilder,
    IBasicBlockRewriter,
    ITransform,
    ITypeRewriter
{
    private readonly MethodTransform _parent;
    internal HashSet<BasicBlock>? _mergedBlocks;

    /// <summary>
    /// Values created in this block's builder as cross-block side effects
    /// (i.e., during another block's DemandRewriteBlock). These are drained
    /// by <see cref="DrainPendingValues"/> after all DemandRewriteBlocks
    /// have run in CompleteRewrite.
    /// </summary>
    internal List<BasicBlockValue>? _pendingValues;

    /// <summary>
    /// Constructs a new basic block transform.
    /// </summary>
    /// <param name="parent">The parent method transform.</param>
    /// <param name="oldBlock">The old basic block.</param>
    /// <param name="newBlock">The new basic block.</param>
    public BasicBlockTransform(
        MethodTransform parent,
        BasicBlock newBlock,
        BasicBlock? oldBlock = null)
        : base(parent, newBlock)
    {
        _parent = parent;
        OldBasicBlock = oldBlock ?? newBlock;

        Last = oldBlock?.LastValue;
    }

    /// <inheritdoc/>
    BasicBlock IBasicBlockRewriter.BasicBlock => BasicBlock;

    /// <inheritdoc/>
    public BasicBlock OldBasicBlock { get; }

    /// <inheritdoc/>
    public Method OldMethod => OldBasicBlock.Method;

    /// <inheritdoc/>
    BasicBlockBuilder IRewriter<BasicBlockBuilder>.Builder => this;

    /// <inheritdoc/>
    public bool IsRemapped(BasicBlockValue basicBlockValue) =>
        _parent.IsRemapped(basicBlockValue);

    /// <inheritdoc/>
    public bool IsReplacedOrRemoved(Value? oldValue) =>
        _parent.IsReplacedOrRemoved(oldValue);

    /// <inheritdoc/>
    public bool TryGetReplaced(Value? oldValue, out Value? newValue) =>
        _parent.TryGetReplaced(oldValue, out newValue);

    /// <inheritdoc/>
    public Value? Rewrite(Value oldValue) => _parent.Rewrite(oldValue);

    /// <inheritdoc/>
    public TypeValue Rewrite(TypeValue oldValue) => _parent.Rewrite(oldValue);

    /// <inheritdoc/>
    public T RewriteAs<T>(Value oldValue) where T : Value =>
        Rewrite(oldValue).AsNotNullCast<T>();

    /// <inheritdoc/>
    public FieldSpan Rewrite(StructureType structureType, FieldSpan fieldSpan) =>
        _parent.Rewrite(structureType, fieldSpan);

    /// <inheritdoc/>
    public bool TryGetRelinked(
        BasicBlockValue basicBlockValue,
        [NotNullWhen(true)] out BasicBlockValue? newPreviousValue) =>
        _parent.TryGetRelinked(basicBlockValue, out newPreviousValue);

    /// <inheritdoc/>
    public void CopyTerminationFrom(
        BasicBlock basicBlock,
        BasicBlock? returnBlock = null) =>
        basicBlock.CopyTerminationTo(this, returnBlock);

    /// <summary>
    /// Replaces the given value with the given value.
    /// </summary>
    /// <param name="oldValue">The value to be replaced.</param>
    /// <param name="newValue">The value to replace.</param>
    public void Replace(Value oldValue, Value? newValue) =>
        _parent.Replace(oldValue, newValue);

    /// <summary>
    /// Registers a block whose values should be accepted by this block's
    /// <see cref="DemandRewriteBlock{TSet}"/>. Used by SimplifyControlFlow
    /// when merging chains: the merged block's new block is added here so
    /// the same-block filter doesn't reject its values.
    /// </summary>
    internal void AddMergedBlock(BasicBlock block) =>
        (_mergedBlocks ??= []).Add(block);

    /// <summary>
    /// Demand-driven rewrite of all non-phi basic block values in this block.
    /// Collects values from the old chain, resolves each via the parent's
    /// memoized <see cref="MethodTransform.Rewrite(Value)"/>, and reconstructs
    /// the Previous chain with only same-block, current-gen values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void DemandRewriteBlock<TSet>(TSet visited)
        where TSet : ISet<BasicBlockValue>
    {
        // Incorporate pending values from cross-block side effects.
        // These were created by previously-processed blocks' rewrites
        // and queued for this block. Link them into the chain before
        // the old-chain walk so they participate in normal rewriting.
        if (_pendingValues is not null && _pendingValues.Count > 0)
        {
            foreach (var pending in _pendingValues)
            {
                if (pending.BasicBlock == BasicBlock)
                {
                    pending.Previous = Last;
                    Last = pending;
                }
            }
            _pendingValues = null;
        }

        // Collect BBValues from old block chain (reverse order: Last→first)
        visited.Clear();
        var allValues = InlineList<BasicBlockValue>.Create(OldBasicBlock.NumValues * 2);
        var current = Last;
        while (current is not null && visited.Add(current))
        {
            allValues.Add(current);
            current = (TryGetRelinked(current, out var newPrev)
                ? (newPrev != current ? newPrev : null)
                : current.Previous) as BasicBlockValue;
        }

        // Process in forward order
        allValues.Reverse();
        var finalValues = InlineList<BasicBlockValue>.Create(allValues.Count);
        visited.Clear(); // reuse for deduplication

        // Build set of chain member IDs. Values that were physically in the
        // block's chain (via Previous/TryGetRelinked) should always be
        // accepted into finalValues, even if their rewritten BasicBlock
        // differs (e.g., values from blocks merged by SimplifyControlFlow
        // in a prior pass). Transitive dependencies (created during Rewrite
        // cascade) are NOT chain members and still use the same-block filter.
        var chainMemberIds = new HashSet<ValueId>(allValues.Count);
        foreach (var v in allValues)
            chainMemberIds.Add(v.Id);

        // Share chain member IDs with RebuildAndMemoize so it can
        // redirect value creation to the demand block for chain members.
        _parent._currentChainMemberIds = chainMemberIds;

        // Set the block override so RebuildAndMemoize creates values
        // from merged blocks in THIS block (not the merged block).
        _parent._overrideBlockTransform = _mergedBlocks is not null ? this : null;
        _parent._currentDemandBlock = this;

        foreach (var bbValue in allValues)
        {
            // Skip phi values — handled by PhiRewriter
            if (bbValue is PhiValue)
                continue;

            // Demand-driven rewrite with chain isolation.
            // PushLastValue(null) prevents side-effect values from
            // contaminating the chain walk. But Pop discards them.
            // Capture same-block side effects before Pop.
            // Track pre-existing replacement so we can avoid duplicating
            // values that were mapped by inlining (return values, etc.).
            bool wasPreReplaced = _parent.TryGetReplaced(bbValue, out _);
            var scope = PushLastValue(null);
            var result = _parent.Rewrite(bbValue);
            var sideEffectLast = Last; // Values created during Rewrite
            scope.Pop();

            // Collect same-block side effects that were created during
            // Rewrite (e.g., an alloca created as a cascade when
            // rewriting a Store's target). These form a chain from
            // null → ... → sideEffectLast (since we pushed null).
            if (sideEffectLast is not null)
            {
                BasicBlockValue? se = sideEffectLast;
                while (se is not null)
                {
                    if (se is not PhiValue
                        && se.BasicBlock == BasicBlock
                        && se != result
                        && visited.Add(se))
                    {
                        finalValues.Add(se);
                    }
                    se = se.Previous as BasicBlockValue;
                }
            }

            // Collect into final chain. For chain members whose result
            // is from a different block, create a fresh copy in THIS
            // block to avoid dual-chain membership — but only for
            // values that were rebuilt, not for values whose result
            // came from a pre-existing replacement (e.g. inlined return
            // values). Duplicating replaced values creates stale reads.
            if (result is BasicBlockValue resultBB
                && resultBB is not PhiValue)
            {
                if (resultBB.BasicBlock != BasicBlock
                    && chainMemberIds.Contains(bbValue.Id)
                    && !wasPreReplaced)
                {
                    // Duplicate the value into this block's builder
                    var dupScope = PushLastValue(null);
                    var dup = resultBB.Rewrite(this);
                    dupScope.Pop();
                    if (dup is BasicBlockValue dupBB && visited.Add(dupBB))
                    {
                        _parent.Replace(result, dup);
                        finalValues.Add(dupBB);
                    }
                }
                else if (resultBB.BasicBlock == BasicBlock
                    && visited.Add(resultBB))
                {
                    finalValues.Add(resultBB);
                }
            }
            else if (result is PureValue resultPV)
            {
                // When a chain member rewrites to a PureValue (e.g., an
                // inlined method call replaced by its return value), the
                // PureValue's BasicBlockValue operands need to be in this
                // block's chain. Collect them recursively.
                CollectBBVDependencies(resultPV, finalValues, visited);
            }
        }
        _parent._overrideBlockTransform = null;
        _parent._currentDemandBlock = null;
        _parent._currentChainMemberIds = null;

        // Reconstruct Previous chain
        Last = null;
        BasicBlockValue? prev = null;
        foreach (var v in finalValues)
        {
            v.Previous = prev;
            v.BasicBlock = BasicBlock;
            Last = v;
            prev = v;
        }
    }

    /// <summary>
    /// Recursively collects BasicBlockValue operands from a PureValue tree
    /// that belong to this block's method. Used when a chain member rewrites
    /// to a PureValue (e.g., inlined method return value) whose dependencies
    /// (MethodCalls, etc.) need to be in this block's chain.
    /// </summary>
    internal void CollectBBVDependencies<TSet>(
        PureValue pv,
        InlineList<BasicBlockValue> finalValues,
        TSet visited)
        where TSet : ISet<BasicBlockValue>
    {
        foreach (var operand in pv.Values)
        {
            if (operand is BasicBlockValue bbOp
                && bbOp.BasicBlock.Method == BasicBlock.Method
                && visited.Add(bbOp))
            {
                finalValues.Add(bbOp);
            }
            if (operand is PureValue childPV)
                CollectBBVDependencies(childPV, finalValues, visited);
        }
    }

    /// <summary>
    /// Appends any pending values (cross-block side effects) to the end of
    /// this block's chain. Called by CompleteRewrite after all blocks have
    /// been demand-rewritten, to catch values that were created here by
    /// other blocks' rewrite cascades.
    /// </summary>
    internal void DrainPendingValues()
    {
        if (_pendingValues is null || _pendingValues.Count == 0)
            return;

        // Build set of values already in chain to prevent duplicates
        var inChain = new HashSet<ValueId>();
        for (var v = Last; v is not null; v = v.Previous as BasicBlockValue)
        {
            if (!inChain.Add(v.Id))
                break; // cycle guard
        }

        foreach (var pending in _pendingValues)
        {
            if (pending.BasicBlock == BasicBlock
                && !inChain.Contains(pending.Id))
            {
                pending.Previous = Last;
                pending.BasicBlock = BasicBlock;
                Last = pending;
                inChain.Add(pending.Id);
            }
        }
        _pendingValues = null;
    }

    /// <summary>
    /// Rewrites phi values and termination for this block. Must be called after
    /// <see cref="PhiRewriter.Finish{TRewriter}"/> so that phi arguments are wired
    /// and the replacement map is fully populated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal BasicBlock RewriteTermination<TSet>(TSet visited)
        where TSet : ISet<BasicBlockValue>
    {
        // Process phi values — resolve any replacements and reconstruct chain
        visited.Clear();
        var phiValues = InlineList<PhiValue>.Create(4);
        var phiCurrent = LastPhiValue;
        while (phiCurrent is not null && visited.Add(phiCurrent))
        {
            phiValues.Add(phiCurrent);
            phiCurrent = phiCurrent.Previous as PhiValue;
        }
        phiValues.Reverse();

        LastPhiValue = null;
        PhiValue? phiPrev = null;
        visited.Clear(); // reuse for deduplication
        foreach (var phi in phiValues)
        {
            var resolved = _parent.Rewrite(phi);
            if (resolved is PhiValue resolvedPhi
                && visited.Add(resolvedPhi))
            {
                // Skip phis that already belong to a different current-gen
                // block. When replacement chains cross block boundaries
                // (e.g. after IfConversion or Inliner merges), a resolved
                // phi may belong to another block's builder. Claiming it
                // here would corrupt its BasicBlock and create cross-block
                // phi chain contamination.
                if (resolvedPhi.BasicBlock != BasicBlock
                    && resolvedPhi.BasicBlock.Generation == Generation)
                {
                    continue;
                }

                resolvedPhi.Previous = phiPrev;
                resolvedPhi.BasicBlock = BasicBlock;
                LastPhiValue = resolvedPhi;
                phiPrev = resolvedPhi;
            }
        }

        // Rewrite termination value — Return blocks must always have a
        // non-null termination value. If Rewrite returns null (e.g. because
        // SSACleanup removed the return value), fall back to UndefinedValue.
        if (TerminationValue is not null)
        {
            TerminationValue = Rewrite(TerminationValue)
                ?? ModuleBuilder.UndefinedValue;
        }

        FixStalePreviousPointers(visited);

        return BasicBlock;
    }

    /// <summary>
    /// Walks the Previous chains of both phi and non-phi value lists and
    /// fixes BasicBlock references and skips old-gen values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FixStalePreviousPointers<TSet>(TSet visited)
        where TSet : ISet<BasicBlockValue>
    {
        visited.Clear();
        FixChainSegment(Last, visited);
        FixChainSegment(LastPhiValue, visited);
    }

    /// <summary>
    /// Walks a Previous chain starting from <paramref name="tail"/> and
    /// skips over any values from an older generation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FixChainSegment<TSet>(BasicBlockValue? tail, TSet visited)
        where TSet : ISet<BasicBlockValue>
    {
        var current = tail;
        while (current is not null && visited.Add(current))
        {
            var prev = current.Previous;
            // Skip over old-gen values in the Previous chain
            int innerLimit = 0;
            while (prev is not null && prev.Generation != Generation)
            {
                this.Assert(
                    ++innerLimit <= MaxChainLength,
                    $"[FixChainSegment] Inner loop cycle: current={current}, " +
                    $"prev={prev}, gen={prev.Generation}, expected={Generation}");
                prev = prev.Previous;
            }
            if (prev != current.Previous)
                current.Previous = prev;
            // Fix BasicBlock if it references an old-gen block
            if (current.BasicBlock.Generation != Generation)
                current.BasicBlock = BasicBlock;
            current = prev;
        }
    }

    /// <summary>
    /// Updates the last phi entry based on the given chain start.
    /// </summary>
    /// <param name="chainStart">The start of the chain.</param>
    internal void UpdateLastPhiFromChain(PhiValue chainStart)
    {
        var current = chainStart;
        for (; current is not null && current.Next is not null;
            current = current.Next.AsNotNullCast<PhiValue>()) ;
        LastPhiValue = current;
    }

    /// <summary>
    /// Updates the last entry based on the given chain start.
    /// </summary>
    /// <param name="chainStart">The start of the chain.</param>
    internal void UpdateLastFromChain(BasicBlockValue chainStart)
    {
        var current = chainStart;
        for (; current is not null && current.Next is not null; current = current.Next) ;
        Last = current;
    }

    /// <summary>
    /// Updates the last entry based on the given end.
    /// </summary>
    /// <param name="end">The end of the current chain.</param>
    internal void UpdateLastByTerminatingAfter(BasicBlockValue end) => Last = end;

}
