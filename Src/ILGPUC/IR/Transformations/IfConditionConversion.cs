// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IfConditionConversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Folds single-entry, acyclic, side-effect-free conditional sub-CFGs (short-circuit
/// branch chains, nested and/or shapes, internal diamonds) into compound boolean
/// expressions, predicated selects, and a minimal branch ladder.
/// </summary>
/// <remarks>
/// <para>
/// A region consists of an entry block terminating in a two-way conditional branch
/// plus inner blocks that carry no regular values (phis are allowed), terminate in
/// two-way conditional or unconditional branches, and are reachable only from within
/// the region. Any successor failing this test is an exit; a region may have any
/// number of exits <c>N &gt;= 1</c>.
/// </para>
/// <para>
/// The fold rewires the region to a ladder of <c>N - 1</c> conditional branches whose
/// conditions are backward reachability predicates (memoized
/// <see cref="PureValues.Construction.PureValueBuilder.CreatePredicate"/> networks with
/// don't-care pruning of already-dispatched exits). Phis at internal merge points and
/// exit phis fed by the region are converted to predicated-select networks over
/// memoized forward edge predicates.
/// </para>
/// <para>
/// Legality: inner blocks are empty (side-effect freedom), the region is acyclic, and
/// no newly speculated pure DAG may contain an integer div/rem whose divisor is not a
/// non-zero constant (speculating a division above its zero-guard would fault on the
/// CPU backend). The block and node budgets are compile-time/codegen-size safety
/// valves, not a cost model.
/// </para>
/// <para>
/// Must run before <see cref="IfConversion"/>: folding produces the single-branch
/// diamonds that IfConversion then predicates.
/// </para>
/// </remarks>
/// <param name="args">The transformation arguments.</param>
/// <param name="maxInnerBlocks">
/// The maximum number of inner blocks per region (safety valve).
/// </param>
/// <param name="maxSpeculatedValues">
/// The maximum number of newly unconditionally evaluated pure values per region
/// (safety valve against pathological predicate jungles).
/// </param>
sealed class IfConditionConversion(
    TransformationArgs args,
    int maxInnerBlocks = 64,
    int maxSpeculatedValues = 256) :
    Transformation<ValueMap<Method, BasicBlock, IfConditionConversion.RegionInfo>>(args)
{
    #region Region model

    /// <summary>
    /// Describes an accepted foldable region.
    /// </summary>
    internal sealed class RegionInfo
    {
        /// <summary>The entry block (two-way conditional terminator).</summary>
        public required BasicBlock Entry { get; init; }

        /// <summary>All region blocks: the entry plus all inner blocks.</summary>
        public required HashSet<BasicBlock> Blocks { get; init; }

        /// <summary>Region blocks in region reverse post order (entry first).</summary>
        public List<BasicBlock> BlocksInRpo { get; set; } = [];

        /// <summary>All exit blocks in deterministic discovery order.</summary>
        public required List<BasicBlock> Exits { get; init; }
    }

    #endregion

    #region Detection

    /// <summary>
    /// Finds all accepted, disjoint regions of the given method in RPO entry order.
    /// Pure analysis without any rewriting — exposed for unit testing.
    /// </summary>
    /// <param name="method">The (sealed) method to analyze.</param>
    /// <param name="maxInnerBlocks">The inner-block budget.</param>
    /// <param name="maxSpeculatedValues">The speculated-node budget.</param>
    /// <returns>All accepted regions.</returns>
    internal static List<RegionInfo> FindRegions(
        Method method,
        int maxInnerBlocks,
        int maxSpeculatedValues)
    {
        var regions = new List<RegionInfo>();
        var claimed = new HashSet<BasicBlock>();

        // method.Blocks yields RPO: outer entries claim first.
        foreach (var entry in method.Blocks)
        {
            if (claimed.Contains(entry) ||
                entry.TerminationKind != BlockTerminationKind.Conditional)
            {
                continue;
            }

            var region = TryGrowRegion(entry, claimed, maxInnerBlocks);
            if (region is null ||
                !ComputeRegionOrder(region) ||
                !IsProfitable(region) ||
                !CheckSpeculationSafety(region, maxSpeculatedValues))
            {
                continue;
            }

            regions.Add(region);
            claimed.UnionWith(region.Blocks);
        }
        return regions;
    }

    /// <summary>
    /// Grows the maximal admissible region from the given entry via BFS,
    /// classifying every reached block before insertion. Inner blocks must be
    /// empty (no regular values; phis allowed), terminate in a conditional or
    /// unconditional branch, and have all predecessors inside the region.
    /// Candidates that fail only the predecessor test are deferred and re-tested
    /// once the queue drains (standard worklist refinement). Everything else is
    /// an exit.
    /// </summary>
    private static RegionInfo? TryGrowRegion(
        BasicBlock entry,
        HashSet<BasicBlock> claimed,
        int maxInnerBlocks)
    {
        var blocks = new HashSet<BasicBlock> { entry };
        var discovered = new List<BasicBlock>();
        var discoveredSet = new HashSet<BasicBlock> { entry };
        var queue = new Queue<BasicBlock>();
        var deferred = new List<BasicBlock>();

        void DiscoverSuccessors(BasicBlock block)
        {
            foreach (var successor in block.Successors)
            {
                if (!discoveredSet.Add(successor))
                    continue;
                discovered.Add(successor);
                queue.Enqueue(successor);
            }
        }

        static bool AllPredecessorsInRegion(
            BasicBlock block,
            HashSet<BasicBlock> region)
        {
            foreach (var predecessor in block.Predecessors)
            {
                if (!region.Contains(predecessor))
                    return false;
            }
            return true;
        }

        DiscoverSuccessors(entry);

        int innerCount = 0;
        while (true)
        {
            if (queue.Count == 0)
            {
                // Re-test deferred candidates now that the region has grown.
                bool changed = false;
                var stillDeferred = new List<BasicBlock>();
                foreach (var candidate in deferred)
                {
                    if (AllPredecessorsInRegion(candidate, blocks))
                    {
                        queue.Enqueue(candidate);
                        changed = true;
                    }
                    else
                    {
                        stillDeferred.Add(candidate);
                    }
                }
                deferred = stillDeferred;
                if (!changed)
                    break;
                continue;
            }

            var block = queue.Dequeue();
            if (blocks.Contains(block))
                continue;

            // Structural inner test. Claimed blocks of earlier regions can never
            // become inner of this region (disjointness) — treat them as
            // exits defensively. FirstValue is the idiomatic emptiness check.
            if (claimed.Contains(block) ||
                block.FirstValue is not null ||
                (block.TerminationKind != BlockTerminationKind.Conditional &&
                 block.TerminationKind != BlockTerminationKind.Unconditional))
            {
                // Exit — classified at the end via discovered \ blocks.
                continue;
            }

            if (!AllPredecessorsInRegion(block, blocks))
            {
                deferred.Add(block);
                continue;
            }

            // Inner block.
            if (++innerCount > maxInnerBlocks)
                return null;
            blocks.Add(block);
            DiscoverSuccessors(block);
        }

        var exits = new List<BasicBlock>();
        foreach (var block in discovered)
        {
            if (!blocks.Contains(block))
                exits.Add(block);
        }

        // A region without exits cannot occur in an acyclic shape; reject
        // defensively (cyclic shapes are rejected by the RPO check anyway).
        return exits.Count == 0
            ? null
            : new RegionInfo { Entry = entry, Blocks = blocks, Exits = exits };
    }

    /// <summary>
    /// Verifies acyclicity of the region subgraph and computes a deterministic
    /// region RPO (Kahn's algorithm seeded at the entry). Rejects back edges,
    /// including edges from region blocks back to the entry.
    /// </summary>
    private static bool ComputeRegionOrder(RegionInfo region)
    {
        var inDegree = new Dictionary<BasicBlock, int>(region.Blocks.Count);
        foreach (var block in region.Blocks)
            inDegree[block] = 0;
        foreach (var block in region.Blocks)
        {
            foreach (var successor in block.Successors)
            {
                if (region.Blocks.Contains(successor))
                    ++inDegree[successor];
            }
        }

        // An internal edge into the entry is a back edge.
        if (inDegree[region.Entry] != 0)
            return false;

        var order = new List<BasicBlock>(region.Blocks.Count);
        var queue = new Queue<BasicBlock>();
        queue.Enqueue(region.Entry);
        while (queue.Count > 0)
        {
            var block = queue.Dequeue();
            order.Add(block);
            foreach (var successor in block.Successors)
            {
                if (region.Blocks.Contains(successor) && --inDegree[successor] == 0)
                    queue.Enqueue(successor);
            }
        }

        if (order.Count != region.Blocks.Count)
            return false;

        region.BlocksInRpo = order;
        return true;
    }

    /// <summary>
    /// The profitability guard: fold iff the fold strictly reduces the
    /// number of conditional branches, an inner block carries a phi, an exit phi
    /// consolidates two or more region sources, or the region has a single exit
    /// (the branch disappears entirely).
    /// </summary>
    private static bool IsProfitable(RegionInfo region)
    {
        int numExits = region.Exits.Count;
        if (numExits == 1)
            return true;

        int conditionalBlocks = 0;
        foreach (var block in region.Blocks)
        {
            if (block.TerminationKind == BlockTerminationKind.Conditional)
                ++conditionalBlocks;
            if (block != region.Entry && block.FirstPhiValue is not null)
                return true;
        }
        if (conditionalBlocks > numExits - 1)
            return true;

        foreach (var exit in region.Exits)
        {
            foreach (PhiValue phi in exit.PhiValues)
            {
                int regionSources = 0;
                foreach (var source in phi.Sources)
                {
                    if (region.Blocks.Contains(source) && ++regionSources >= 2)
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Walks every newly unconditionally evaluated pure DAG — non-entry branch
    /// conditions, inner-phi arguments, and exit-phi region arguments — with one
    /// memoized DFS. Rejects the region if the walk finds an integer div/rem
    /// whose divisor is not a non-zero constant (hard legality rule) or if
    /// the total number of speculated nodes exceeds the budget. Values
    /// reachable from the entry's own condition were already unconditional and
    /// are skipped.
    /// </summary>
    private static bool CheckSpeculationSafety(
        RegionInfo region,
        int maxSpeculatedValues)
    {
        // Values that are evaluated unconditionally anyway once the entry runs.
        var alreadyUnconditional = new HashSet<Value>();
        CollectPureValues(region.Entry.TerminationCondition, alreadyUnconditional);

        var visited = new HashSet<Value>();
        var pending = new Stack<Value>();
        int speculatedCount = 0;

        // Iterative DFS (explicit stack: pure DAGs can be deep enough to
        // overflow the thread stack on Debug-mode bounds-check chains).
        bool WalkSpeculated(Value? root)
        {
            if (root is not null)
                pending.Push(root);
            while (pending.Count > 0)
            {
                // Non-pure operands (phis, parameters, loads defined before
                // the entry) are not speculated — they execute in their own
                // blocks, which dominate the region by SSA dominance.
                if (pending.Pop() is not PureValue pureValue)
                    continue;
                if (alreadyUnconditional.Contains(pureValue) ||
                    !visited.Add(pureValue))
                {
                    continue;
                }

                if (pureValue is BinaryArithmeticValue binary &&
                    (binary.Kind == BinaryArithmeticKind.Div ||
                     binary.Kind == BinaryArithmeticKind.Rem) &&
                    binary.BasicValueType.IsInt() &&
                    !(binary.Right is PrimitiveValue divisor &&
                      divisor.RawValue != 0))
                {
                    // Speculating a potentially faulting integer division above
                    // its guard is a miscompile — never a budget question.
                    return false;
                }

                if (pureValue is not PrimitiveValue)
                    ++speculatedCount;

                foreach (var operand in pureValue.Values)
                    pending.Push(operand);
            }
            return true;
        }

        foreach (var block in region.Blocks)
        {
            if (block == region.Entry)
                continue;
            if (block.TerminationKind == BlockTerminationKind.Conditional &&
                !WalkSpeculated(block.TerminationCondition))
            {
                return false;
            }
            foreach (PhiValue phi in block.PhiValues)
            {
                foreach (var argument in phi.Arguments)
                {
                    if (!WalkSpeculated(argument))
                        return false;
                }
            }
        }

        foreach (var exit in region.Exits)
        {
            foreach (PhiValue phi in exit.PhiValues)
            {
                for (int i = 0; i < phi.NumArguments; ++i)
                {
                    if (region.Blocks.Contains(phi.Sources[i]) &&
                        !WalkSpeculated(phi.Arguments[i]))
                    {
                        return false;
                    }
                }
            }
        }

        return speculatedCount <= maxSpeculatedValues;
    }

    /// <summary>
    /// Collects all pure values reachable from the given root into the set.
    /// Iterative (deep pure DAGs must not overflow the thread stack).
    /// </summary>
    private static void CollectPureValues(Value? root, HashSet<Value> target)
    {
        var pending = new Stack<Value>();
        if (root is not null)
            pending.Push(root);
        while (pending.Count > 0)
        {
            if (pending.Pop() is not PureValue pureValue || !target.Add(pureValue))
                continue;
            foreach (var operand in pureValue.Values)
                pending.Push(operand);
        }
    }

    #endregion

    #region Intermediate

    /// <inheritdoc/>
    protected override ValueMap<Method, BasicBlock, RegionInfo> CreateIntermediate(
        ModuleTransform transform,
        Method method)
    {
        var result = method.CreateMap<BasicBlock, RegionInfo>();
        foreach (var region in FindRegions(method, maxInnerBlocks, maxSpeculatedValues))
            result.Add(region.Entry, region);
        return result;
    }

    #endregion

    #region Transformation

    /// <summary>
    /// Per-region rewriting context carrying the memoized predicate families:
    /// forward block/edge predicates (for select networks) and lazily
    /// rewritten branch conditions shared by the backward reachability
    /// predicates.
    /// </summary>
    private sealed class RegionContext(
        MethodTransform transform,
        BasicBlockTransform builder,
        RegionInfo info)
    {
        private readonly Dictionary<BasicBlock, Value> _conditions = new();
        private readonly Dictionary<BasicBlock, Value?> _blockPredicates = new();
        private readonly Dictionary<(BasicBlock, BasicBlock), Value> _edgePredicates =
            new();
        private Value? _trueConstant;
        private Value? _falseConstant;

        /// <summary>The region description.</summary>
        public RegionInfo Info { get; } = info;

        /// <summary>The parent method transform.</summary>
        public MethodTransform Transform { get; } = transform;

        /// <summary>The entry block transform (used as the pure-value builder).</summary>
        public BasicBlockTransform Builder { get; } = builder;

        /// <summary>The new-generation block that branches to each exit.</summary>
        public List<BasicBlock> LadderSources { get; } = [];

        private Value TrueConstant =>
            _trueConstant ??= Builder.CreatePrimitiveValue(Info.Entry.Location, true);

        private Value FalseConstant =>
            _falseConstant ??= Builder.CreatePrimitiveValue(Info.Entry.Location, false);

        /// <summary>
        /// Returns the rewritten branch condition of the given region block.
        /// Lazily rewritten so that references to already-converted region phis
        /// resolve to their select networks.
        /// </summary>
        private Value GetCondition(BasicBlock block)
        {
            if (_conditions.TryGetValue(block, out var condition))
                return condition;
            condition = Transform
                .Rewrite(block.TerminationCondition.AsNotNull())
                .AsNotNull();
            _conditions[block] = condition;
            return condition;
        }

        /// <summary>
        /// Returns the forward block predicate P(B) — true iff control starting
        /// at the entry reaches B. Returns null for the entry (constant true).
        /// Memoized per block: O(E_R) nodes in total.
        /// </summary>
        private Value? GetBlockPredicate(BasicBlock block)
        {
            if (block == Info.Entry)
                return null;
            if (_blockPredicates.TryGetValue(block, out var predicate))
                return predicate;

            Value? result = null;
            foreach (var predecessor in block.Predecessors)
            {
                var edge = GetEdgePredicate(predecessor, block);
                result = result is null
                    ? edge
                    : Builder.CreateArithmetic(
                        block.Location,
                        result,
                        edge,
                        BinaryArithmeticKind.Or,
                        ArithmeticFlags.Unsigned).AsNotNull();
            }
            _blockPredicates[block] = result;
            return result.AsNotNull();
        }

        /// <summary>
        /// Returns the forward edge predicate Q(p→s) — true iff the execution
        /// traverses the given region edge. Memoized per edge.
        /// </summary>
        public Value GetEdgePredicate(BasicBlock predecessor, BasicBlock successor)
        {
            if (_edgePredicates.TryGetValue((predecessor, successor), out var cached))
                return cached;

            Value result;
            if (predecessor.TerminationKind == BlockTerminationKind.Unconditional)
            {
                // The entry always terminates conditionally, so the block
                // predicate of an unconditional predecessor is never "true".
                result = GetBlockPredicate(predecessor).AsNotNull();
            }
            else
            {
                var location = predecessor.Location;
                var condition = GetCondition(predecessor);
                predecessor.Assert(
                    predecessor.Successors[0] == successor ||
                    predecessor.Successors[1] == successor);
                var polarity = predecessor.Successors[0] == successor
                    ? condition
                    : Builder.CreateArithmetic(
                        location,
                        condition,
                        UnaryArithmeticKind.Not).AsNotNull();
                var blockPredicate = GetBlockPredicate(predecessor);
                result = blockPredicate is null
                    ? polarity
                    : Builder.CreateArithmetic(
                        location,
                        blockPredicate,
                        polarity,
                        BinaryArithmeticKind.And,
                        ArithmeticFlags.Unsigned).AsNotNull();
            }

            _edgePredicates[(predecessor, successor)] = result;
            return result;
        }

        /// <summary>
        /// Returns true if the given region edge is reached exclusively through
        /// the false polarity of a conditional branch (following single-in-edge
        /// forwarding chains; terminates by region acyclicity). Used to pick a
        /// select-network default arm whose edge predicate would materialize a
        /// negation.
        /// </summary>
        private static bool IsNegativeEdge(BasicBlock predecessor, BasicBlock successor)
        {
            if (predecessor.TerminationKind == BlockTerminationKind.Unconditional)
            {
                return predecessor.Predecessors.Length == 1 &&
                    IsNegativeEdge(predecessor.Predecessors[0], predecessor);
            }
            return predecessor.Successors[1] == successor &&
                predecessor.Successors[0] != successor;
        }

        /// <summary>
        /// Builds the nested select network over the phi's (filtered) arguments:
        /// Q_1 ? v_1 : (Q_2 ? v_2 : … : v_k). Any argument may serve as the
        /// default arm (the edge predicates are pairwise disjoint and jointly
        /// exhaustive); an argument arriving over a false-polarity
        /// edge is preferred so that its negated edge predicate never
        /// materializes — plain diamonds then fold to the same canonical
        /// <c>select(cond, trueValue, falseValue)</c> that
        /// <see cref="IfConversion"/> emits.
        /// </summary>
        public Value? BuildSelectNetwork(
            PhiValue phi,
            BasicBlock mergeBlock,
            System.Func<RegionInfo, BasicBlock, bool> sourceFilter)
        {
            var pairs = new List<(BasicBlock Source, Value Argument)>();
            for (int i = 0; i < phi.NumArguments; ++i)
            {
                var source = phi.Sources[i];
                if (!sourceFilter(Info, source))
                    continue;
                var argument = Transform.Rewrite(phi.Arguments[i]);
                if (argument is null)
                    continue;
                pairs.Add((source, argument));
            }
            if (pairs.Count == 0)
                return null;

            int defaultIndex = pairs.Count - 1;
            for (int i = pairs.Count - 1; i >= 0; --i)
            {
                if (IsNegativeEdge(pairs[i].Source, mergeBlock))
                {
                    defaultIndex = i;
                    break;
                }
            }

            var result = pairs[defaultIndex].Argument;
            for (int i = pairs.Count - 1; i >= 0; --i)
            {
                if (i == defaultIndex)
                    continue;
                var (source, argument) = pairs[i];
                var edge = GetEdgePredicate(source, mergeBlock);
                result = Builder.CreatePredicate(
                    phi.Location,
                    edge,
                    argument,
                    result).AsNotNull();
            }
            return result;
        }

        /// <summary>
        /// Computes the residual exit condition C'(X) = R'_E(X): the backward
        /// reachability predicate of the target exit with all already-dispatched
        /// exits treated as don't-cares. One fresh memoized post-order
        /// traversal per exit; null represents don't-care during resolution.
        /// </summary>
        public Value? ComputeResidualCondition(
            BasicBlock target,
            HashSet<BasicBlock> dispatched)
        {
            var memo = new Dictionary<BasicBlock, Value?>(Info.Blocks.Count);

            Value? Residual(BasicBlock block)
            {
                if (block == target)
                    return TrueConstant;
                if (!Info.Blocks.Contains(block))
                {
                    // Exit: dispatched exits are don't-cares, others contribute
                    // constant false (vanishing via the constant-arm folds).
                    return dispatched.Contains(block) ? null : FalseConstant;
                }
                if (memo.TryGetValue(block, out var cached))
                    return cached;

                Value? result;
                if (block.TerminationKind == BlockTerminationKind.Unconditional)
                {
                    result = Residual(block.Successors[0]);
                }
                else
                {
                    var onTrue = Residual(block.Successors[0]);
                    var onFalse = Residual(block.Successors[1]);
                    result = onTrue is null
                        ? onFalse
                        : onFalse is null
                            ? onTrue
                            : Builder.CreatePredicate(
                                block.Location,
                                GetCondition(block),
                                onTrue,
                                onFalse).AsNotNull();
                }
                memo[block] = result;
                return result;
            }

            return Residual(Info.Entry);
        }

        /// <summary>The fresh ladder blocks L_2 … L_{N-1}.</summary>
        private readonly List<BasicBlockTransform> _ladder = [];

        /// <summary>
        /// Allocates the fresh ladder blocks and populates
        /// <see cref="LadderSources"/> per exit — WITHOUT creating any
        /// terminators. Termination is deferred to
        /// <see cref="TerminateLadder"/> so that exit-phi conversion knows the
        /// new predecessor of each exit before any condition is rewritten
        /// (see the ordering contract in <c>OnTransform</c>).
        /// </summary>
        public void CreateLadderBlocks()
        {
            var exits = Info.Exits;
            int numExits = exits.Count;
            var location = Info.Entry.Location;

            for (int i = 2; i <= numExits - 1; ++i)
            {
                _ladder.Add(Transform
                    .CreateBasicBlock(location, "icc_ladder")
                    .AsNotNullCast<BasicBlockTransform>());
            }

            LadderSources.Add(Builder.BasicBlock);
            if (numExits >= 2)
            {
                for (int i = 2; i <= numExits - 1; ++i)
                    LadderSources.Add(_ladder[i - 2].BasicBlock);
                LadderSources.Add(StepBuilder(numExits - 1).BasicBlock);
            }
        }

        private BasicBlockTransform StepBuilder(int step) =>
            step == 1 ? Builder : _ladder[step - 2];

        /// <summary>
        /// Terminates the entry and the ladder blocks: N-1 two-way
        /// branches; the last exit is the unconditional fall-through (its
        /// predicate is the complement by partition). Must run after all phi
        /// conversion — the rewritten conditions must resolve region phis to
        /// their final replacements.
        /// </summary>
        public void TerminateLadder()
        {
            var exits = Info.Exits;
            int numExits = exits.Count;

            if (numExits == 1)
            {
                // The branch disappears entirely; the region reduces to the
                // select networks built during phi conversion.
                Builder.CreateUnconditionalTermination(
                    Transform.RewriteAs<BasicBlock>(exits[0]));
                return;
            }

            var dispatched = new HashSet<BasicBlock>();
            for (int step = 1; step <= numExits - 1; ++step)
            {
                var exit = exits[step - 1];
                // Defensive: an all-don't-care residual cannot occur for a
                // reachable exit; fall back to constant false (folds to an
                // unconditional branch to the next step).
                var condition =
                    ComputeResidualCondition(exit, dispatched) ?? FalseConstant;
                var trueTarget = Transform.RewriteAs<BasicBlock>(exit);
                var falseTarget = step == numExits - 1
                    ? Transform.RewriteAs<BasicBlock>(exits[numExits - 1])
                    : _ladder[step - 1].BasicBlock;
                StepBuilder(step).CreateConditionalTermination(
                    condition,
                    trueTarget,
                    falseTarget);
                dispatched.Add(exit);
            }
        }
    }

    /// <summary>
    /// The conversion plan of one exit phi that is fed by at least one region.
    /// A null <see cref="Builder"/> marks a trivial plan (a single incoming
    /// value): the phi is replaced by that value directly — no pass in this
    /// pipeline may leave a single-source phi behind, because
    /// <see cref="SimplifyControlFlow"/> merges single-predecessor blocks into
    /// their predecessor (its phi-source set only protects source blocks, not
    /// phi-carrying blocks), which would strand the phi in the merged block
    /// with its own block as source.
    /// </summary>
    private sealed class ExitPhiPlan
    {
        /// <summary>The old phi.</summary>
        public required PhiValue Phi { get; init; }

        /// <summary>The rewritten phi type.</summary>
        public required TypeValue NewType { get; init; }

        /// <summary>
        /// The pre-registered replacement phi builder, or null for a trivial
        /// plan.
        /// </summary>
        public PhiValue.Builder? Builder { get; init; }

        /// <summary>
        /// The regions contributing at least one argument, with the ladder
        /// block that feeds this exit.
        /// </summary>
        public required List<(RegionContext Context, BasicBlock Source)>
            Contributing
        { get; init; }

        /// <summary>The argument indices with non-region sources.</summary>
        public required List<int> ExternalIndices { get; init; }
    }

    /// <inheritdoc/>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        var intermediate = GetIntermediate(transform);
        if (intermediate.IsEmpty)
            return;

        // ORDERING CONTRACT. Replacing a phi after a Transform.Rewrite already
        // demand-reached it bakes the PhiRewriter placeholder into the
        // rewritten DAG; the later Replace orphans that placeholder
        // (PhiRewriter.Finish cannot patch operands already built), leaving
        // stale dangling values. Therefore every phi replacement must be
        // registered BEFORE any rewrite can reach the phi:
        //   (0) allocate ladder blocks (no terminators, no rewriting);
        //   (1) pre-register the replacement phis of all kept exit phis
        //       (structural plan only, no rewriting) — these are the only
        //       replaced phis reachable through loop back edges, i.e. from
        //       DAGs of blocks that precede them in RPO;
        //   (2) convert inner phis and trivial exit phis in method RPO — all
        //       values these conversions rewrite are defined in dominating
        //       blocks (already converted) or are pre-registered phis;
        //   (3) wire the kept exit phis (their region arguments may reference
        //       inner phis via back edges — now final);
        //   (4) terminate entry/ladder blocks (conditions see final phis).

        // Region iteration order is the RPO insertion order of CreateIntermediate.
        var contexts = new List<RegionContext>(intermediate.Count);
        var innerToContext = new Dictionary<BasicBlock, RegionContext>();
        foreach (var (entry, info) in intermediate)
        {
            var context = new RegionContext(
                transform,
                transform.GetBasicBlockTransform(entry),
                info);
            contexts.Add(context);
            foreach (var block in info.Blocks)
            {
                if (block != info.Entry)
                    innerToContext[block] = context;
            }
        }

        // Step 0: allocate ladder blocks.
        foreach (var context in contexts)
            context.CreateLadderBlocks();

        // Group exits across regions — a block that is an exit of several
        // regions has its phis rewritten once, jointly.
        var exitOrder = new List<BasicBlock>();
        var exitSources =
            new Dictionary<
                BasicBlock,
                List<(RegionContext Context, BasicBlock Source)>>();
        foreach (var context in contexts)
        {
            for (int i = 0; i < context.Info.Exits.Count; ++i)
            {
                var exit = context.Info.Exits[i];
                if (!exitSources.TryGetValue(exit, out var sources))
                {
                    sources = [];
                    exitSources[exit] = sources;
                    exitOrder.Add(exit);
                }
                sources.Add((context, context.LadderSources[i]));
            }
        }

        // Step 1: plan all exit phis and pre-register the kept replacements.
        var exitPlans = new Dictionary<BasicBlock, List<ExitPhiPlan>>();
        foreach (var exit in exitOrder)
        {
            var plans = PlanExitPhis(transform, exit, exitSources[exit]);
            if (plans.Count > 0)
                exitPlans[exit] = plans;
        }

        // Step 2: phi conversion sweep in method RPO.
        foreach (var block in transform.OldMethod.Blocks)
        {
            if (innerToContext.TryGetValue(block, out var context))
            {
                foreach (PhiValue phi in block.PhiValues)
                {
                    var select = context.BuildSelectNetwork(
                        phi,
                        block,
                        static (_, _) => true);
                    if (select is not null)
                        transform.Replace(phi, select);
                }
            }
            else if (exitPlans.TryGetValue(block, out var plans))
            {
                foreach (var plan in plans)
                {
                    if (plan.Builder is null)
                        ConvertTrivialExitPhi(transform, block, plan);
                }
            }
        }

        // Step 3: wire the kept exit phis.
        foreach (var exit in exitOrder)
        {
            if (!exitPlans.TryGetValue(exit, out var plans))
                continue;
            foreach (var plan in plans)
            {
                if (plan.Builder is not null)
                    WireExitPhi(transform, exit, plan);
            }
        }

        // Step 4: fold each region into its branch ladder.
        foreach (var context in contexts)
            context.TerminateLadder();
    }

    /// <summary>
    /// Plans the conversion of every region-fed phi of the given exit block
    /// (grouped across all regions exiting there) and pre-registers the
    /// replacement phis of all kept plans — structural analysis only, no value
    /// rewriting (ordering contract, <c>OnTransform</c>). A phi is kept iff at
    /// least two incoming values remain (merged region arguments count one per
    /// contributing region); otherwise the plan is trivial and the phi becomes
    /// its single incoming value in the RPO sweep. The common trivial case is
    /// N == 1 value diamonds folding to a single select fed from the entry.
    /// </summary>
    private static List<ExitPhiPlan> PlanExitPhis(
        MethodTransform transform,
        BasicBlock exit,
        List<(RegionContext Context, BasicBlock Source)> sources)
    {
        var plans = new List<ExitPhiPlan>();
        if (exit.FirstPhiValue is null)
            return plans;

        var exitTransform = transform.GetBasicBlockTransform(exit);
        foreach (PhiValue phi in exit.PhiValues)
        {
            var contributing = new List<(RegionContext, BasicBlock)>();
            var externals = new List<int>();
            for (int i = 0; i < phi.NumArguments; ++i)
            {
                var source = phi.Sources[i];
                bool isRegionSource = false;
                foreach (var (context, ladderSource) in sources)
                {
                    if (!context.Info.Blocks.Contains(source))
                        continue;
                    isRegionSource = true;
                    if (!contributing.Contains((context, ladderSource)))
                        contributing.Add((context, ladderSource));
                    break;
                }
                if (!isRegionSource)
                    externals.Add(i);
            }
            if (contributing.Count == 0)
                continue;

            var newType = transform.Rewrite(phi.Type);
            PhiValue.Builder? builder = null;
            int total = contributing.Count + externals.Count;
            if (total >= 2)
            {
                builder = exitTransform.CreatePhi(phi.Location, newType, total);
                transform.Replace(phi, builder.PhiValue);
            }
            plans.Add(new ExitPhiPlan
            {
                Phi = phi,
                NewType = newType,
                Builder = builder,
                Contributing = contributing,
                ExternalIndices = externals,
            });
        }
        return plans;
    }

    /// <summary>
    /// Replaces a trivially-planned exit phi (a single incoming value: exactly
    /// one contributing region and no external sources) by its merged region
    /// value. Runs at the exit's position in the RPO sweep.
    /// </summary>
    private static void ConvertTrivialExitPhi(
        MethodTransform transform,
        BasicBlock exit,
        ExitPhiPlan plan)
    {
        var (context, _) = plan.Contributing[0];
        var merged = context.BuildSelectNetwork(
            plan.Phi,
            exit,
            static (info, source) => info.Blocks.Contains(source));
        if (merged is not null)
            transform.Replace(plan.Phi, merged);
    }

    /// <summary>
    /// Wires a kept (pre-registered) exit phi: one merged select-network
    /// argument per contributing region, fed from the region's ladder source
    /// block, plus all external arguments unchanged.
    /// </summary>
    private static void WireExitPhi(
        MethodTransform transform,
        BasicBlock exit,
        ExitPhiPlan plan)
    {
        var phi = plan.Phi;
        var builder = plan.Builder.AsNotNull();

        // One merged select-network argument per contributing region.
        var mergedByContext = new Dictionary<RegionContext, Value?>();
        foreach (var (context, _) in plan.Contributing)
        {
            mergedByContext[context] = context.BuildSelectNetwork(
                phi,
                exit,
                static (info, source) => info.Blocks.Contains(source));
        }

        // Emit the arguments in the phi's original argument order (a region's
        // merged argument takes the slot of its first original argument) —
        // downstream rewriters expect conventional ordering; in particular,
        // moving a loop-carried self-referential argument to the front trips
        // the demand-driven phi rebuild in InferAddressSpaces.
        var emitted = new HashSet<RegionContext>();
        for (int i = 0; i < phi.NumArguments; ++i)
        {
            var source = phi.Sources[i];
            RegionContext? owner = null;
            BasicBlock? ladderSource = null;
            foreach (var (context, contextSource) in plan.Contributing)
            {
                if (context.Info.Blocks.Contains(source))
                {
                    owner = context;
                    ladderSource = contextSource;
                    break;
                }
            }

            if (owner is null)
            {
                // External argument, kept as-is.
                if (transform.Rewrite(source) is not BasicBlock newSource)
                    continue;
                var argument = transform.Rewrite(phi.Arguments[i]);
                if (argument is null)
                    continue;
                VerifyExitPhiArgument(phi, argument, plan);
                builder.AddArgument(newSource, argument);
            }
            else if (emitted.Add(owner))
            {
                var merged = mergedByContext[owner];
                if (merged is null)
                    continue;
                VerifyExitPhiArgument(phi, merged, plan);
                builder.AddArgument(ladderSource.AsNotNull(), merged);
            }
        }

        builder.Seal();
    }

    /// <summary>
    /// Throws if an argument of a rewired exit phi does not have the phi type:
    /// skipping it would silently drop an incoming value.
    /// </summary>
    private static void VerifyExitPhiArgument(
        PhiValue phi,
        Value argument,
        ExitPhiPlan plan)
    {
        if (argument.Type.Equals(plan.NewType))
            return;
        throw phi.Location.GetInvalidOperationException(
            $"Exit phi argument {argument} does not have the phi type " +
            $"{plan.NewType}");
    }

    #endregion
}
