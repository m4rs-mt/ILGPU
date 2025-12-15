// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodTransform.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using ILGPUC.IR.Rewriting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BlockCollection = ILGPUC.IR.MethodValues.BasicBlockCollection<
    ILGPUC.IR.Analyses.ReversePostOrder<ILGPUC.IR.MethodValues.BasicBlock>,
    ILGPUC.IR.MethodValues.Forwards>;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// A transformation of a method.
/// </summary>
sealed class MethodTransform :
    MethodBuilder,
    IMethodRewriter,
    IMethodPhiRewriter,
    IPureValueRewriter,
    ITypeRewriter,
    ITransform
{
    #region Nested Types

    /// <summary>
    /// Represents a method specialization request.
    /// </summary>
    /// <param name="Call">The call to specialize.</param>
    /// <param name="ExitBlock">The exit block to jump to.</param>
    readonly record struct MethodSpecialization(
        MethodCall Call,
        BasicBlockBuilder ExitBlock)
    {
        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => Call.BasicBlock;

        /// <summary>
        /// Returns all call argument values.
        /// </summary>
        public ReadOnlySpan<Value> Arguments => Call.Arguments;

        /// <summary>
        /// Returns the call target.
        /// </summary>
        public Method Target => Call.Target;

        /// <summary>
        /// Returns all parameters.
        /// </summary>
        public Method.ParameterCollection Parameters => Target.Parameters;
    }

    #endregion

    #region Instance

    private readonly Dictionary<BasicBlockValue, BasicBlockValue?> _prevMapping;
    private readonly ValueSet<Method, BasicBlock> _rewritten;
    private readonly ValueMap<Method, BasicBlock, BasicBlock> _newToOld;
    private readonly Lazy<CFG<ReversePostOrder<BasicBlock>, Forwards>> _cfg;
    private PhiRewriter _phiRewriter;
    private readonly Dictionary<BasicBlock, BasicBlockTransform> _splitBlocks = new(32);
    private readonly Dictionary<BasicBlock, BasicBlock> _phiSourceRedirects = new(32);
    private bool _skipPhiSourceRedirects;
    private readonly GlobalValueSet _resolvedIdentity;
    private readonly GlobalValueSet _currentlyResolving;
    internal BasicBlockTransform? _overrideBlockTransform;
    internal BasicBlockTransform? _currentDemandBlock;
    internal HashSet<ValueId>? _currentChainMemberIds;
    private bool _inlining;
    private bool _inPhiWiring;

    /// <summary>
    /// Creates a new method transform.
    /// </summary>
    /// <param name="parent">The parent method transform.</param>
    /// <param name="initializer">The method initializer to use.</param>
    /// <param name="oldMethod">The old method.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public MethodTransform(
        ModuleTransform parent,
        in ModuleValueInitializer initializer,
        Method oldMethod)
        : base(
            parent,
            initializer,
            oldMethod.Declaration.Rewrite(parent),
            initialBlockCapacity: oldMethod.Blocks.Count,
            setupEntryBuilder: false)
    {
        _prevMapping = new(oldMethod.NumValues);
        _resolvedIdentity = new(
            Generation,
            oldMethod.NumValues,
            new GenerationValidator(Generation, Generation.PreviousGeneration()));
        _currentlyResolving = new(
            Generation,
            numValues: 16,
            new GenerationValidator(Generation, Generation.PreviousGeneration()));
        _newToOld = Method.CreateMap<BasicBlock, BasicBlock>();
        ModuleTransform = parent;
        _phiRewriter = new(oldMethod.Blocks.Count);
        _cfg = new Lazy<CFG<ReversePostOrder<BasicBlock>, Forwards>>(() =>
            oldMethod.Blocks.CreateCFG());
        _rewritten = new(Method, oldMethod.Blocks.Count);

        OldMethod = oldMethod;

        // Replace method
        Replace(OldMethod, Method);

        // Pre-define all parameters
        foreach (var parameter in oldMethod.Parameters)
        {
            if (IsReplacedOrRemoved(parameter))
                continue;

            var newParameter = parameter.Rewrite(this);
            Replace(parameter, newParameter);
        }

        // Loop over all blocks and register them
        foreach (var block in oldMethod.Blocks)
        {
            // Skip blocks that are touched externally and will be handled separately
            if (IsReplacedOrRemoved(block))
                continue;

            // Define new block and store it
            var newBlock = CreateBasicBlockFromOldBlock(block);
            _newToOld.Add(newBlock.BasicBlock, block);
        }

        // Loop over all blocks and copy termination data
        foreach (var block in oldMethod.Blocks)
        {
            // Skip blocks that are touched externally and will be handled separately
            if (!TryGetReplaced(block, out var newBlock) || newBlock is not BasicBlock)
                continue;

            // Copy termination from old block to new block transform
            var blockTransform = GetBasicBlockTransform(block);
            blockTransform.CopyTerminationFrom(block);
        }

        // Setup our new entry builder
        EntryBuilder = GetBasicBlockTransform(oldMethod.EntryBlock);
    }

    /// <inheritdoc/>
    public Method OldMethod { get; }

    /// <summary>
    /// Returns the parent module transform.
    /// </summary>
    public ModuleTransform ModuleTransform { get; }

    /// <inheritdoc/>
    MethodBuilder IRewriter<MethodBuilder>.Builder => this;

    /// <inheritdoc/>
    ModuleBuilder ITypeRewriter.ModuleBuilder => ModuleTransform;

    /// <inheritdoc/>
    PureValueBuilder IRewriter<PureValueBuilder>.Builder => this;

    /// <summary>
    /// Returns the CFG of the old source method.
    /// </summary>
    public CFG<ReversePostOrder<BasicBlock>, Forwards> CFG => _cfg.Value;

    /// <summary>
    /// Returns true if the given basic block value has been remapped.
    /// </summary>
    /// <param name="basicBlockValue">The basic block value to test.</param>
    /// <returns>True if the given value has been remapped.</returns>
    public bool IsRemapped(BasicBlockValue basicBlockValue) =>
        _prevMapping.ContainsKey(basicBlockValue);

    /// <summary>
    /// Creates a new block transform.
    /// </summary>
    protected override BasicBlockBuilder CreateBasicBlockBuilder(
        BasicBlock basicBlock,
        BasicBlock? oldBlock)
    {
        var result = new BasicBlockTransform(this, basicBlock, oldBlock);

        if (oldBlock is not null)
        {
            // Register all phi values in this block
            _phiRewriter.ProcessBlock(this, result, oldBlock);

            // Replace old block
            Replace(oldBlock, basicBlock);
        }

        return result;
    }

    /// <summary>
    /// Returns the corresponding basic block transform.
    /// </summary>
    /// <param name="oldBasicBlock">
    /// The basic block for which to get a transform for.
    /// </param>
    /// <returns>The basic block transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BasicBlockTransform GetBasicBlockTransform(BasicBlock oldBasicBlock)
    {
        Generation.ValidatePreviousGeneration(oldBasicBlock);
        oldBasicBlock.Assert(oldBasicBlock.Method == OldMethod);

        if (!ModuleTransform.TryGetReplaced(oldBasicBlock, out var newBasicBlock))
            throw oldBasicBlock.GetInvalidOperationException();
        return this[newBasicBlock.AsNotNullCast<BasicBlock>()]
            .AsNotNullCast<BasicBlockTransform>();
    }

    /// <inheritdoc/>
    public bool IsReplacedOrRemoved(Value? oldValue) =>
        ModuleTransform.IsReplacedOrRemoved(oldValue);

    /// <inheritdoc/>
    public bool TryGetReplaced(Value? oldValue, out Value? newValue) =>
        ModuleTransform.TryGetReplaced(oldValue, out newValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Value? Rewrite(Value oldValue)
    {
        // Cycle detection: if we're already resolving this value, check for a
        // pre-registered replacement (e.g. phi placeholder from RebuildAndMemoize).
        // This breaks replacement chain cycles for loop phis: A→B where B's
        // operands reference A.
        if (!_currentlyResolving.Add(oldValue))
        {
            if (ModuleTransform.TryGetReplaced(oldValue, out var cycleReplacement)
                && cycleReplacement != oldValue
                && cycleReplacement is not null)
            {
                // Follow the replacement chain to the final value. During
                // RebuildAndMemoize the chain may have been extended
                // (e.g. phi_A → phi_B → new_placeholder) and we need
                // the latest entry.
                while (ModuleTransform.TryGetReplaced(cycleReplacement, out var next)
                    && next is not null
                    && next != cycleReplacement)
                {
                    cycleReplacement = next;
                }
                return cycleReplacement;
            }
            return oldValue;
        }

        try
        {
            Generation.ValidateCurrentOrPreviousGeneration(oldValue);

            // Step 1: Check replacement map — follow chain if replaced
            if (ModuleTransform.TryGetReplaced(oldValue, out var cached)
                && cached != oldValue)
            {
                return cached is null ? null : Rewrite(cached);
            }
            // Step 2: Identity cache — already confirmed up-to-date.
            // Guard with generation check: TypeValue.Equals uses structural
            // equality (same fields/offsets), so a Gen_N type can match a
            // Gen_N+1 type in the set. Only trust the cache for current-gen.
            if (oldValue.Generation == Generation
                && _resolvedIdentity.Contains(oldValue))
            {
                return oldValue;
            }

            // Step 3: BasicBlock (phi source) — check split redirect, else as-is
            if (oldValue is BasicBlock bb)
            {
                // When a block has been split by SpecializeMethodCall, phi sources
                // must redirect to the exit block (which inherited the termination
                // and is the actual predecessor of successor blocks).
                // However, successor edges must NOT follow these redirects — they
                // should point to the original (split) block, not the exit block.
                // _skipPhiSourceRedirects is set during successor resolution
                // (CopyTerminationFrom, Method.Rewrite entry resolution).
                if (!_skipPhiSourceRedirects
                    && _phiSourceRedirects.TryGetValue(bb, out var redirect))
                {
                    return Rewrite(redirect);
                }

                _resolvedIdentity.Add(oldValue);
                return oldValue;
            }

            // Step 4+5: Current-gen value — check if operands need updating.
            if (oldValue.Generation == Generation)
            {
                // Quick pre-check: if any direct operand is old-gen, this value
                // definitely needs rebuilding. Go directly to RebuildAndMemoize
                // to pre-register a placeholder BEFORE resolving arguments. This
                // avoids replacement-chain cycles during the operand-check loop
                // (e.g. phi_A → old_arg → replaced_by phi_B → old_arg →
                // replaced_by phi_A, where the cycle returns phi_A before it
                // has a placeholder registered).
                bool hasOldGenOperand = oldValue.Type is not null
                    && oldValue.Type.Generation != Generation;
                if (!hasOldGenOperand)
                {
                    foreach (var op in oldValue.Values)
                    {
                        if (op.Generation != Generation)
                        {
                            hasOldGenOperand = true;
                            break;
                        }
                    }
                }
                if (hasOldGenOperand)
                    return RebuildAndMemoize(oldValue);

                // All operands are current-gen. Do the full recursive check to
                // see if any operand is in a replacement chain.
                _resolvedIdentity.Add(oldValue);
                bool needsRebuild = false;
                foreach (var op in oldValue.Values)
                {
                    var resolved = Rewrite(op);
                    if (resolved != op) { needsRebuild = true; break; }
                }
                if (!needsRebuild)
                {
                    // Fix stale BasicBlock references on current-gen BasicBlockValues.
                    // Values created by converters during OnTransform inherit BasicBlock
                    // from their Previous (which may be old-gen), causing them to
                    // reference the old block/method.
                    if (oldValue is BasicBlockValue bbv
                        && bbv.BasicBlock.Generation != Generation
                        && TryGetReplaced(bbv.BasicBlock, out var newBlock)
                        && newBlock is BasicBlock nb)
                    {
                        bbv.BasicBlock = nb;
                    }
                    return oldValue;
                }
                // Remove optimistic marking — needs actual rebuild
                _resolvedIdentity.Remove(oldValue);
                return RebuildAndMemoize(oldValue);
            }

            // Step 6: Old-gen value — rebuild
            return RebuildAndMemoize(oldValue);

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[Rewrite] Failed for {oldValue.GetType().Name} " +
                $"Id={oldValue.Id} Gen={oldValue.Generation} " +
                $"CurrentGen={Generation}: {ex.Message}", ex);
        }
        finally
        {
            _currentlyResolving.Remove(oldValue);
        }
    }

    /// <summary>
    /// Dispatches rebuild by value type, registers the replacement, and returns
    /// the new value. Called for old-gen values and current-gen values whose
    /// operands have changed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RebuildAndMemoize(Value oldValue)
    {
        Value? newValue;

        if (oldValue is PureValue pv)
        {
            newValue = pv.Rewrite(this);
            Replace(oldValue, newValue);
            if (newValue is not null)
                _resolvedIdentity.Add(newValue);
            return newValue;
        }

        if (oldValue is BasicBlockValue bbv)
        {
            // Resolve the BBValue's block to a current-gen block.
            // The replacement chain may go through old-gen blocks when
            // CleanupEmptyBlock redirects block → successor (both old-gen).
            BasicBlockTransform blockTransform;
            Value? resolved = bbv.BasicBlock;
            while (resolved is BasicBlock bb && bb.Generation != Generation)
            {
                if (!ModuleTransform.TryGetReplaced(bb, out var next) || next == bb)
                {
                    resolved = null;
                    break;
                }
                resolved = next;
            }

            if (resolved is BasicBlock currentGenBlock
                && currentGenBlock.Generation == Generation)
            {
                // When DemandRewriteBlock is processing values from
                // merged blocks, redirect the value to be created in
                // the processing block (not the merged block). This
                // avoids dual-chain issues where a value appears in
                // both the merged block's chain and the target block's
                // chain.
                if (_overrideBlockTransform is not null
                    && currentGenBlock != _overrideBlockTransform.BasicBlock
                    && (_overrideBlockTransform._mergedBlocks?
                        .Contains(currentGenBlock) ?? false))
                {
                    blockTransform = _overrideBlockTransform;
                }
                else if (_currentDemandBlock is not null
                    && currentGenBlock != _currentDemandBlock.BasicBlock
                    && (bbv.BasicBlock == _currentDemandBlock.OldBasicBlock
                        || IsRelinkedChainMember(bbv)
                        || (_currentChainMemberIds?.Contains(bbv.Id) ?? false)))
                {
                    // This value's OLD block matches the demand block's OLD
                    // block, or was relinked via AppendTo, or is a chain
                    // member from a prior-pass merge. Redirect to the
                    // demand block so it's created in the correct builder.
                    blockTransform = _currentDemandBlock;
                }
                else
                {
                    blockTransform = this[currentGenBlock]
                        .AsNotNullCast<BasicBlockTransform>();
                }
            }
            else
            {
                // Block is unreachable or not resolvable — rewrite in entry
                blockTransform = EntryBuilder
                    .AsNotNullCast<BasicBlockTransform>();
            }

            // PhiValues need special rebuild: resolved arguments may have
            // different types than the phi's declared type (e.g., after
            // InferAddressSpaces changes address spaces). Standard
            // PhiValue.Rewrite asserts type equality via Equals. We
            // rebuild manually and skip arguments whose types diverge.
            if (bbv is PhiValue phiV)
            {
                var scope = blockTransform.PushLastValue(null);
                var newPhiType = Rewrite(phiV.Type) as TypeValue ?? phiV.Type;
                var phiBuilder = blockTransform.CreatePhi(
                    phiV.Location, newPhiType, phiV.NumArguments);

                // Pre-register replacement BEFORE resolving arguments.
                // This ensures self-referential loop phis (where an argument
                // maps back to this phi via the replacement chain) resolve to
                // the new placeholder rather than the old stale phi.
                Replace(oldValue, phiBuilder.PhiValue);
                _resolvedIdentity.Add(phiBuilder.PhiValue);

                for (int i = 0; i < phiV.NumArguments; i++)
                {
                    var source = Rewrite(phiV.Sources[i]);
                    if (source is not BasicBlock newSource) continue;

                    var argument = Rewrite(phiV.Arguments[i]);
                    if (argument is null) continue;

                    // Only add arguments whose type matches the phi type
                    // (structural equality via Equals). Arguments with
                    // divergent types are dropped — the phi may end up
                    // with fewer arguments, which is valid.
                    if (argument.Type.Equals(newPhiType))
                        phiBuilder.AddArgument(newSource, argument);
                }

                newValue = phiBuilder.Seal();
                scope.Pop();
                // If phi was simplified (e.g. single-argument → value),
                // update the replacement chain.
                if (newValue != phiBuilder.PhiValue)
                {
                    Replace(phiBuilder.PhiValue, newValue);
                    if (newValue is not null)
                        _resolvedIdentity.Add(newValue);
                }
                return newValue;
            }

            var converted = ModuleTransform.TryApplyBlockValueConverter(
                blockTransform, bbv);
            if (converted is not null)
            {
                newValue = converted;
                ReverseSideEffectChain(blockTransform);
            }
            else if (_inPhiWiring
                || (_currentDemandBlock is not null
                    && blockTransform == _currentDemandBlock))
            {
                newValue = bbv.Rewrite(blockTransform);
            }
            else
            {
                using (var _ = blockTransform.PushLastValue(null))
                    newValue = bbv.Rewrite(blockTransform);
            }

            Replace(oldValue, newValue);
            if (newValue is not null)
            {
                _resolvedIdentity.Add(newValue);

                // Detect cross-block side effects: queue for the target
                // block's chain repair. Skip during inlining — those side
                // effects are handled by SpecializeMethod's own rewrite,
                // and queuing them would create infinite inliner loops.
                if (newValue is BasicBlockValue newBBV
                    && _currentDemandBlock is not null
                    && !_inlining
                    && newBBV.BasicBlock != _currentDemandBlock.BasicBlock)
                {
                    blockTransform._pendingValues ??= new();
                    blockTransform._pendingValues.Add(newBBV);
                }
            }
            return newValue;
        }

        // Handle Parameters that aren't in the replacement map.
        // This can happen during inlining when the parameter's method
        // was transformed in a scoped replacement that has since been popped.
        //
        // NOTE: ReverseSideEffectChain below is used by the deferred BBV
        // converter path to fix the Previous chain ordering of side effects
        // created by multi-value converters (e.g., LowerGroupCollectives).
        if (oldValue is Parameter param)
        {
            // Try to find the replacement method and map to corresponding param
            if (ModuleTransform.TryGetReplaced(param.Method, out var newMethodValue)
                && newMethodValue is Method newMethod
                && param.Index < newMethod.NumParameters)
            {
                var newParam = newMethod.Parameters[param.Index];
                Replace(oldValue, newParam);
                return newParam;
            }

            // Fallback: if the parameter belongs to the method currently
            // being transformed, map directly to the new method's parameter.
            // This handles lambda captures that reference the enclosing
            // method's parameters — the method isn't in the replacement map
            // yet (registered after Seal), but its new parameters are
            // already created by CreateMethodTransform.
            if (param.Method == OldMethod
                && param.Index < Method.NumParameters)
            {
                var newParam = Method.Parameters[param.Index];
                Replace(oldValue, newParam);
                return newParam;
            }

            // The parameter's method hasn't been transformed yet in this
            // pass (methods are processed sequentially in RPO). Return
            // null — the MethodCall using this parameter will produce a
            // null argument, which is handled gracefully by downstream
            // code (the value is dead or will be re-resolved when the
            // method is eventually processed).
            Replace(oldValue, null);
            return null;
        }

        // Module-level value (Global, TypeValue, UndefinedValue, Method)
        newValue = ModuleTransform.Rewrite(oldValue);
        return newValue;
    }

    /// <summary>
    /// Reverses the Previous chain so that <see cref="BasicBlockTransform.
    /// DemandRewriteBlock{TSet}"/>'s backward walk yields side effects in
    /// creation (forward) order. After reversal, <c>Last</c> points to the
    /// first created value; walking backward from it traverses values in
    /// the order they were originally appended by the converter.
    /// </summary>
    /// <remarks>
    /// Called after a deferred BBV converter (see <see cref="Transformation.
    /// DeferBlockValueMapping"/>) creates a sequence of side-effect values
    /// (stores, barriers, loads, warp reduces). The builder appends each
    /// value to <c>Last</c>, forming a Previous chain in reverse creation
    /// order (last-created is <c>Last</c>, first-created is at the tail).
    /// DemandRewriteBlock collects side effects by walking <c>Last</c>
    /// backward — without reversal, they'd appear in reverse creation order
    /// in <c>finalValues</c>, misplacing barriers after the operations they
    /// guard.
    /// </remarks>
    private static void ReverseSideEffectChain(
        BasicBlockTransform blockTransform)
    {
        var last = blockTransform.Last;
        if (last is null)
            return;

        BasicBlockValue? prev = null;
        BasicBlockValue? current = last;
        while (current is not null)
        {
            var next = current.Previous;
            current.Previous = prev;
            prev = current;
            current = next;
        }
        // prev is now the first created value (was at the end of the original chain)
        // last is now the last created value (was at the start, now has Previous =
        // second-to-last) DemandRewriteBlock walks from Last backward via Previous, so
        // set Last to the first created value; the backward walk then yields forward
        // order.
        if (prev is not null)
            blockTransform.UpdateLastByTerminatingAfter(prev);
    }

    private bool CanRewriteSafely(Value? value, HashSet<ValueId>? visited = null)
    {
        if (value is null || value is PrimitiveValue || value is NullValue
            || value is UndefinedValue)
            return true;

        // Already mapped — safe
        if (TryGetReplaced(value, out _))
            return true;

        // Current generation — safe (already in the new module)
        if (value.Generation == Generation)
            return true;

        // Parameters: safe ONLY if their method has a mapping
        if (value is Parameter param)
            return ModuleTransform.TryGetReplaced(param.Method, out _);

        // Cycle guard
        visited ??= new();
        if (!visited.Add(value.Id))
            return true;

        // Check all operands recursively
        foreach (var operand in value.Values)
        {
            if (!CanRewriteSafely(operand, visited))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Rebuilds all blocks using the legacy demand-driven approach.
    /// Runs DemandRewriteBlock, DrainPendingValues, PhiRewriter.Finish,
    /// and RewriteTermination for each block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RebuildBlocksLegacy(
        BlockCollection blocks,
        ref PhiRewriter phiRewriter)
    {
        // Step 1: Demand-driven rewrite of all basic block values
        var visitedSet = new HashSet<BasicBlockValue>();
        foreach (var block in blocks)
        {
            if (_rewritten.Contains(block))
                continue;
            this[block]
                .AsNotNullCast<BasicBlockTransform>()
                .DemandRewriteBlock(visitedSet);
        }

        // Step 2: Drain pending values — cross-block side effects
        foreach (var block in blocks)
        {
            this[block]
                .AsNotNullCast<BasicBlockTransform>()
                .DrainPendingValues();
        }

        // Step 3: Wire phi arguments
        phiRewriter.Finish(this);

        // Step 4: Rewrite termination values and fix phi chains.
        // Set _currentDemandBlock per block so side-effect values
        // created during termination/phi resolution aren't orphaned.
        foreach (var block in blocks)
        {
            if (_rewritten.Contains(block))
                continue;
            var bt = this[block].AsNotNullCast<BasicBlockTransform>();
            _currentDemandBlock = bt;
            bt.RewriteTermination(visitedSet);
            _currentDemandBlock = null;
        }
    }

    /// <summary>
    /// Rebuilds a set of inlined blocks using the legacy approach.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RebuildInlinedBlocksLegacy(
        InlineList<BasicBlockTransform> builders,
        ref PhiRewriter phiRewriter)
    {
        var visitedSet = new HashSet<BasicBlockValue>();
        foreach (var builder in builders)
            builder.DemandRewriteBlock(visitedSet);

        // PhiRewriter.Finish uses BeginPhiWiring/EndPhiWiring callbacks
        // to set _currentDemandBlock per phi, preventing orphaning.
        phiRewriter.Finish(this);

        // Set _currentDemandBlock per block during RewriteTermination
        // to prevent orphaning from nested isolation.
        foreach (var builder in builders)
        {
            _currentDemandBlock = builder;
            var newBlock = builder.RewriteTermination(visitedSet);
            _currentDemandBlock = null;
            _rewritten.Add(newBlock);
        }
    }

    /// <summary>
    /// Rewrites the given method using demand-driven memoized resolution.
    /// </summary>
    /// <param name="method">The method to finish rewriting for.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void CompleteRewrite(Method method)
    {
        // Rewrite block references with _phiSourceRedirects disabled.
        _skipPhiSourceRedirects = true;
        var blocks = method.Rewrite(this);
        _skipPhiSourceRedirects = false;
        EntryBuilder = this[blocks.EntryBlock];

        RebuildBlocksLegacy(blocks, ref _phiRewriter);
    }

    /// <inheritdoc/>
    public T RewriteAs<T>(Value oldValue) where T : Value =>
        Rewrite(oldValue).AsNotNullCast<T>();

    /// <inheritdoc/>
    public FieldSpan Rewrite(StructureType structureType, FieldSpan fieldSpan) =>
        ModuleTransform.Rewrite(structureType, fieldSpan);

    /// <inheritdoc/>
    public TypeValue Rewrite(TypeValue oldValue) =>
        ModuleTransform.Rewrite(oldValue);

    /// <summary>
    /// Checks whether the given value is a relinked chain member — its
    /// predecessor was set via <c>_prevMapping</c> by AppendTo during
    /// SimplifyControlFlow block merging. This means the value is from
    /// a block that was merged into another block, and its original
    /// BasicBlock no longer matches the demand block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsRelinkedChainMember(BasicBlockValue bbv) =>
        _prevMapping.ContainsKey(bbv);

    /// <summary>
    /// Tries to get a relinked previous value for the given value.
    /// </summary>
    /// <param name="basicBlockValue">The old value.</param>
    /// <param name="newPreviousValue">The newly mapped value to link to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetRelinked(
        BasicBlockValue basicBlockValue,
        [NotNullWhen(true)] out BasicBlockValue? newPreviousValue)
    {
        newPreviousValue = null;
        if (basicBlockValue.Generation == Generation)
            return false;
        return _prevMapping.TryGetValue(basicBlockValue, out newPreviousValue);
    }

    /// <summary>
    /// Maps the given value from the previous generation to a newly assigned target
    /// block from the previous generation.
    /// </summary>
    /// <param name="block">The block to append to.</param>
    /// <param name="toAppend">The value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void AppendTo(BasicBlockTransform block, BasicBlockValue? toAppend)
    {
        if (toAppend is null) return;

        if (!AppendTo(block.Last, toAppend))
            block.UpdateLastFromChain(toAppend);

        // Register every subsequent value in the moved chain in
        // _prevMapping. AppendTo semantics move the whole .Next chain;
        // callers depend on IsRelinkedChainMember to know "this value
        // belongs to the destination block now" during block resolution
        // (see MethodTransform.Rewrite's _currentDemandBlock redirect).
        // Without entries for the rest of the chain, only the head shows
        // up as relinked — later values route to their original block,
        // triggering cross-block side-effect queueing into the wrong
        // block and duplicating values across blocks. That in turn
        // breaks codegen order when many calls are inlined from one
        // caller (e.g. GroupRadixSort's 32 bit iterations each inline
        // an ExtractRadixBits call, moving long chains per call).
        for (var current = toAppend.Next;
             current is not null;
             current = current.Next)
        {
            if (!_prevMapping.ContainsKey(current))
                _prevMapping[current] = current.Previous;
        }
    }

    /// <summary>
    /// Maps the given value from the previous generation to a newly assigned target
    /// block from the previous generation.
    /// </summary>
    /// <param name="source">The block value to append to.</param>
    /// <param name="toAppend">The value to append.</param>
    /// <returns>
    /// Returns true if the block of the source value has been updated with latest chain
    /// information.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool AppendTo(BasicBlockValue? source, BasicBlockValue? toAppend)
    {
        if (toAppend is null) return false;

        // Check for invalid mapping operations
        Generation.ValidatePreviousGeneration(source);
        Generation.ValidateCurrentOrPreviousGeneration(toAppend);

        // Register next entry
        _prevMapping[toAppend] = source;

        // Ensure last value is set appropriately
        if (source is null)
            return false;

        var blockTransform = GetBasicBlockTransform(source.BasicBlock);
        blockTransform.UpdateLastFromChain(toAppend);
        return true;
    }

    /// <summary>
    /// Replaces the given value with the given value.
    /// </summary>
    /// <param name="oldValue">The value to be replaced.</param>
    /// <param name="newValue">The value to replace.</param>
    public void Replace(Value oldValue, Value? newValue)
    {
        if (_inlining)
            ModuleTransform.ReplaceDirect(oldValue, newValue);
        else
            ModuleTransform.Replace(oldValue, newValue);
    }

    /// <summary>
    /// Specializes the given method using the call details provided.
    /// </summary>
    /// <param name="methodCall">
    /// The method call to specialize using the parameters provided.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Value? SpecializeMethodCall(MethodCall methodCall)
    {
        Generation.ValidatePreviousGeneration(methodCall);
        methodCall.Assert(methodCall.Scope == OldMethod);

        // Create new exit block
        var exitBuilder = CreateBasicBlock(
            methodCall.Location,
            $"{methodCall.Target.Name}_exit").AsNotNullCast<BasicBlockTransform>();

        // Get the source block. When multiple calls from the same old block
        // are inlined, the "current block" for subsequent calls is the exit
        // block from the previous inlining, not the original block.
        BasicBlockTransform currentBlockTransform;
        if (_splitBlocks.TryGetValue(methodCall.BasicBlock, out var splitTransform))
            currentBlockTransform = splitTransform;
        else
            currentBlockTransform = GetBasicBlockTransform(methodCall.BasicBlock);

        // Terminate method call chain
        currentBlockTransform.UpdateLastByTerminatingAfter(methodCall);

        // Move next instructions of the current block to the exit block and wire jump
        AppendTo(exitBuilder, toAppend: methodCall.Next);

        // The moved values still reference their original BasicBlock (the old
        // block from which the method call was split). When DemandRewriteBlock
        // later rewrites them, RebuildAndMemoize resolves the old block to its
        // new-gen version and creates the value there. The same-block filter
        // then rejects it because it's in the wrong block. Registering the
        // resolved new block as a "merged block" of the exit block makes
        // _overrideBlockTransform redirect value creation to the exit block.
        if (methodCall.Next is not null
            && TryGetReplaced(methodCall.BasicBlock, out var resolvedBlock)
            && resolvedBlock is BasicBlock resolvedBB)
        {
            exitBuilder.AddMergedBlock(resolvedBB);
        }

        // Pre-rewrite call arguments in the parent scope so that caller-
        // side values (e.g., data alloca → addrspacecast chain) are mapped
        // BEFORE the inlining scope is pushed. This prevents the mapping
        // from being discarded when the scope is popped, which would cause
        // CompleteRewrite to create a duplicate alloca (orphaning the
        // inlined code's reference). Only pre-rewrite arguments whose
        // entire dependency chain is resolvable — skip arguments that
        // depend on unmapped Parameters (e.g., lambda captures) to avoid
        // assertion failures in RebuildAndMemoize.
        //
        // Side-effect BBVs created during pre-rewrite (e.g., allocas
        // from operand resolution cascades) are captured and queued
        // to their owning block's _pendingValues so they're linked
        // into the chain during DemandRewriteBlock/DrainPendingValues.
        var specialization = new MethodSpecialization(methodCall, exitBuilder);
        var callArgs = specialization.Arguments;
        // Set _currentDemandBlock so RebuildAndMemoize skips its inner
        // PushLastValue(null)/Pop() isolation. This lets the outer scope
        // capture side-effect BBVs (allocas from operand cascades).
        _currentDemandBlock = currentBlockTransform;
        var preRewriteScope = currentBlockTransform.PushLastValue(null);
        for (int i = 0; i < callArgs.Length; i++)
        {
            if (CanRewriteSafely(callArgs[i]))
                Rewrite(callArgs[i]);
        }
        // Queue any BBVs created during pre-rewrite to their owning
        // block's _pendingValues for later chain integration.
        for (var se = currentBlockTransform.Last;
             se is BasicBlockValue seBBV;
             se = se.Previous)
        {
            if (seBBV is not PhiValue)
            {
                var owningBT = this[seBBV.BasicBlock]
                    .AsNotNullCast<BasicBlockTransform>();
                owningBT._pendingValues ??= new();
                owningBT._pendingValues.Add(seBBV);
            }
        }
        preRewriteScope.Pop();
        _currentDemandBlock = null;

        var returnValue = SpecializeMethod(specialization, currentBlockTransform);

        // Wire exit block to fit into the game. Disable _phiSourceRedirects
        // so that successor edges resolve to the original (split) blocks, not
        // their exit blocks from prior inlining processes.
        _skipPhiSourceRedirects = true;
        exitBuilder.CopyTerminationFrom(methodCall.BasicBlock);
        _skipPhiSourceRedirects = false;

        // Replace value with phi value or null (remove)
        Replace(methodCall, returnValue);

        // Track split: subsequent calls from the same old block use the exit
        _splitBlocks[methodCall.BasicBlock] = exitBuilder;

        // Redirect phi sources: after the split, the exit block is the actual
        // predecessor of the original block's successors. Phi sources pointing
        // to the pre-split block must resolve to the exit block instead.
        _phiSourceRedirects[currentBlockTransform.BasicBlock] = exitBuilder.BasicBlock;
        _resolvedIdentity.Remove(currentBlockTransform.BasicBlock);

        // Return potential return value
        return returnValue;
    }

    /// <summary>
    /// Specializes a method described by the current specialization.
    /// </summary>
    /// <param name="specialization">The method specialization.</param>
    /// <param name="sourceBlockTransform">
    /// The source block that should jump to the inlined entry.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? SpecializeMethod(
        in MethodSpecialization specialization,
        BasicBlockTransform sourceBlockTransform)
    {
        // Set inlining flag to use ReplaceDirect for all replacement
        // operations during method specialization. This prevents chain-
        // following corruption when the same method is inlined multiple
        // times (e.g., CLI.param → arg1 becomes CLI.param → arg2 AND
        // arg1 → arg2 without this flag).
        _inlining = true;

        // Clear stale resolved-identity entries for the target method's values.
        foreach (var block in specialization.Target.Blocks)
        {
            foreach (BasicBlockValue bbValue in block)
                _resolvedIdentity.Remove(bbValue);
            block.ForEachValue<PureValue>(pv =>
                _resolvedIdentity.Remove(pv));
        }

        // Push a replacement scope to isolate this inlining's mappings.
        // Merged back into the parent after processing to preserve the
        // mappings for later use (CompleteRewrite, etc.).
        ModuleTransform.PushReplacementScope();

        // Create method specialization setup
        var arguments = specialization.Arguments;
        for (int i = 0; i < arguments.Length; ++i)
        {
            Generation.ValidateCurrentOrPreviousGeneration(arguments[i]);
            Replace(specialization.Parameters[i], arguments[i]);
        }

        // Pass 1: Create all blocks and register replacements so that
        // CopyTerminationFrom can resolve all successor blocks.
        int numBlocks = specialization.Target.Blocks.Count;
        var phiRewriter = new PhiRewriter(numBlocks);
        var builders = InlineList<BasicBlockTransform>.Create(numBlocks);
        var blockMappings = InlineList<(BasicBlock Old, BasicBlock New)>.Create(
            numBlocks);
        var targetName = specialization.Target.Name;
        foreach (var block in specialization.Target.Blocks)
        {
            Generation.ValidateCurrentOrPreviousGeneration(block);

            // Define new block and map it accordingly (ReplaceDirect
            // is used via _inlining flag in CreateBasicBlockBuilder)
            var bbBuilder = CreateBasicBlockFromOldBlock(
                block,
                $"{targetName}_{block.Name}").AsNotNullCast<BasicBlockTransform>();
            Replace(block, bbBuilder.BasicBlock);

            // Collect block mapping so we can re-add it after popping
            // the scope (needed for phi source resolution)
            blockMappings.Add((block, bbBuilder.BasicBlock));

            // Register builder
            builders.Add(bbBuilder);
        }

        // Pass 2: Now that all blocks are registered, copy termination,
        // set up value chains, and register phi values.
        int blockIdx = 0;
        foreach (var block in specialization.Target.Blocks)
        {
            var bbBuilder = builders[blockIdx++];

            // Setup first phi value
            if (block.FirstPhiValue is not null)
                bbBuilder.UpdateLastPhiFromChain(block.FirstPhiValue);

            // Setup last value
            if (block.LastValue is not null)
                bbBuilder.UpdateLastFromChain(block.LastValue);

            // Copy termination while making sure returns jump to the pre-defined exit
            bbBuilder.CopyTerminationFrom(
                block,
                returnBlock: specialization.ExitBlock.BasicBlock);

            // Clear stale phi mappings from lower scopes (e.g., Phase 1's
            // base-scope mapping from CreateMethodTransform) so that
            // ProcessBlock's IsReplacedOrRemoved check doesn't skip phis
            // that need fresh inlined versions.
            foreach (BasicBlockValue bbv in block)
            {
                if (bbv is PhiValue)
                    ModuleTransform.RemoveReplacement(bbv);
            }

            // Register all phi values in this block
            phiRewriter.ProcessBlock(this, bbBuilder, block);
        }

        // Wire entry block branch using the provided source block
        sourceBlockTransform.CreateUnconditionalTermination(
            RewriteAs<BasicBlock>(specialization.Target.EntryBlock));

        // Rebuild all inlined blocks
        RebuildInlinedBlocksLegacy(builders, ref phiRewriter);

        // Rewrite termination value — if the inlined method's return is
        // UndefinedValue (void return or cleaned-up value), return null so
        // the call site is removed rather than replaced with UndefinedValue.
        var terminationValue = specialization.Target.ExitBlock.TerminationValue;
        var newTerminationValue = terminationValue is not null
            ? Rewrite(terminationValue)
            : null;

        if (newTerminationValue is ModuleValues.UndefinedValue)
            newTerminationValue = null;

        _inlining = false;

        // Pop and DISCARD this inlining's scope. Do not merge: merging
        // would leak callee-internal value mappings (e.g., calleeCall →
        // newCall) into the parent scope. When the same callee is inlined
        // multiple times, those stale mappings cause the second inlining
        // to reuse the first inlining's values instead of creating fresh
        // ones with the correct arguments. The return value mapping is set
        // separately AFTER the pop (via Replace(methodCall, returnValue)),
        // and the inlined blocks are already marked as _rewritten.
        ModuleTransform.PopReplacementScope();

        // Re-add block and parameter mappings that were discarded by the
        // pop. Block mappings are needed for phi source resolution during
        // CompleteRewrite. Parameter mappings are needed when phi values
        // in exit blocks reference the callee's parameters.
        foreach (var (oldBlock, newBlock) in blockMappings)
            ModuleTransform.ReplaceDirect(oldBlock, newBlock);
        for (int i = 0; i < arguments.Length; ++i)
            ModuleTransform.ReplaceDirect(
                specialization.Parameters[i], arguments[i]);

        // NOTE: The termination value mapping (terminationValue →
        // newTerminationValue) was in the inlining scope that was just
        // popped. It is NOT re-added here because doing so can cause
        // stale value resolution when the same target method is inlined
        // multiple times. The return value is propagated via the
        // Replace(methodCall, returnValue) call in SpecializeMethodCall.

        // Return exit block value (return value)
        return newTerminationValue;
    }

    /// <summary>
    /// Maps an old phi value to a new one.
    /// </summary>
    /// <param name="oldPhi">The old value to map to a new one.</param>
    /// <param name="newValue">The new value to map to.</param>
    /// <returns>The new value (if any).</returns>
    void IMethodPhiRewriter.Map(PhiValue oldPhi, Value? newValue) =>
        ModuleTransform.ReplaceDirect(oldPhi, newValue);

    /// <inheritdoc/>
    void IMethodPhiRewriter.BeginPhiWiring(BasicBlock phiBlock)
    {
        _inPhiWiring = true;
    }

    /// <inheritdoc/>
    void IMethodPhiRewriter.EndPhiWiring()
    {
        _inPhiWiring = false;
    }

    /// <summary>
    /// Seals the underlying method.
    /// </summary>
    /// <return>The created method.</return>
    internal override Method Seal() =>
        SealInternal(param =>
        {
            if (!TryGetReplaced(param, out var newValue))
                return true;

            return newValue == param;
        });

    #endregion
}
