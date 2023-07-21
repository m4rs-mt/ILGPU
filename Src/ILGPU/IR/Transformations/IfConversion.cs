// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IfConversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BlockCollection = ILGPU.IR.BasicBlockCollection<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using Dominators = ILGPU.IR.Analyses.Dominators<
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Converts nested if/switch branches into value conditionals.
    /// </summary>
    public sealed class IfConversion : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Remaps if branch targets to new blocks in order to linearize all jump targets.
        /// </summary>
        private readonly struct IfBranchRemapper : TerminatorValue.ITargetRemapper
        {
            /// <summary>
            /// Constructs a new if branch remapper.
            /// </summary>
            /// <param name="postDominator">The common post dominator.</param>
            /// <param name="newTarget">The new target block.</param>
            public IfBranchRemapper(BasicBlock postDominator, BasicBlock newTarget)
            {
                PostDominator = postDominator;
                NewTarget = newTarget;
            }

            /// <summary>
            /// Returns the common post dominator.
            /// </summary>
            public BasicBlock PostDominator { get; }

            /// <summary>
            /// Returns the new target block.
            /// </summary>
            public BasicBlock NewTarget { get; }

            /// <summary>
            /// Returns true if one of the block is equal to the
            /// <see cref="PostDominator"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool CanRemap(in ReadOnlySpan<BasicBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (block == PostDominator)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Remaps the given block to the block <see cref="NewTarget"/> if the source
            /// block is equal to the <see cref="PostDominator"/>.
            /// </summary>
            public readonly BasicBlock Remap(BasicBlock block) =>
                block == PostDominator ? NewTarget : block;
        }

        /// <summary>
        /// A wrapper structure to encapsulate several basic block regions.
        /// </summary>
        private readonly struct Regions
        {
            #region Instance

            private readonly HashSet<BasicBlock>[] regions;

            /// <summary>
            /// Constructs a new regions wrapper.
            /// </summary>
            /// <param name="root">The root node.</param>
            /// <param name="numRegions">The number of attached regions.</param>
            public Regions(BasicBlock root, int numRegions)
            {
                regions = new HashSet<BasicBlock>[numRegions];

                Root = root;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated root block.
            /// </summary>
            public BasicBlock Root { get; }

            /// <summary>
            /// Returns the number of regions.
            /// </summary>
            public readonly int Count => regions.Length;

            /// <summary>
            /// Returns the i-th region.
            /// </summary>
            public readonly HashSet<BasicBlock> this[int regionIndex] =>
                regions[regionIndex];

            #endregion

            #region Methods

            /// <summary>
            /// Finds a particular case index via linear search.
            /// </summary>
            /// <param name="block">The phi-argument block.</param>
            /// <returns>The region index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int? FindRegion(BasicBlock block)
            {
                for (int i = 0, e = regions.Length; i < e; ++i)
                {
                    if (regions[i].Contains(block))
                        return i;
                }
                return null;
            }

            /// <summary>
            /// Adds a new region.
            /// </summary>
            /// <param name="index">The region index.</param>
            /// <param name="region">The region contents.</param>
            /// <param name="regionSize">The region size to adapt.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void AddRegion(
                int index,
                HashSet<BasicBlock> region,
                ref int regionSize)
            {
                // Check for degenerated cases in which we hit a critical edge
                if (region.Count < 1)
                {
                    region.Add(Root);
                    regionSize = Root.Count;
                }
                regions[index] = region;
            }

            #endregion
        }

        /// <summary>
        /// An analyzer to detect compatible if/switch branch constructions.
        /// </summary>
        private struct ConditionalAnalyzer
        {
            #region Static

            /// <summary>
            /// Verifies predecessors of all blocks.
            /// </summary>
            /// <param name="root">The current root node.</param>
            /// <param name="region">The current region.</param>
            /// <returns>True, if all predecessors can be safely converted.</returns>
            private static bool VerifyPredecessors(
                BasicBlock root,
                HashSet<BasicBlock> region)
            {
                foreach (var block in region)
                {
                    // Note that we have to query the successors since the current CFG
                    // has been created in backwards mode
                    foreach (var predecessor in block.Predecessors)
                    {
                        if (predecessor != root && !region.Contains(predecessor))
                            return false;
                    }
                }
                return true;
            }

            /// <summary>
            /// Returns true if the given set of phi values can be converted.
            /// </summary>
            /// <param name="phiValues">The phi values to convert.</param>
            /// <param name="regions">The current regions.</param>
            /// <returns>True, if the given set of phi values can be converted.</returns>
            private static bool CanConvertPhis(
                HashSet<PhiValue> phiValues,
                Regions regions)
            {
                var foundRegions = new HashSet<int>();
                foreach (var phiValue in phiValues)
                {
                    // Reject nodes which cannot be mapped to all predecessor regions
                    if (phiValue.Sources.Length != regions.Count)
                        return false;

                    foreach (var source in phiValue.Sources)
                    {
                        // Check for references to another part of the program
                        var regionIndex = regions.FindRegion(source);
                        if (regionIndex is null || !foundRegions.Add(regionIndex.Value))
                            return false;
                    }

                    foundRegions.Clear();
                }
                return true;
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new conditional analyzer.
            /// </summary>
            /// <param name="maxBlockSize">The maximum block size.</param>
            /// <param name="maxBlockDifference">
            /// The maximum block size difference.
            /// </param>
            /// <param name="blocks">The current blocks.</param>
            public ConditionalAnalyzer(
                int maxBlockSize,
                int maxBlockDifference,
                in BlockCollection blocks)
            {
                PostDominators = blocks.CreatePostDominators();
                MaxBlockSize = maxBlockSize;
                MaxBlockDifference = maxBlockDifference;
                Gathered = null;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the maximum block size.
            /// </summary>
            public int MaxBlockSize { get; }

            /// <summary>
            /// Returns the maximum block difference.
            /// </summary>
            public int MaxBlockDifference { get; }

            /// <summary>
            /// Returns the parent post dominators.
            /// </summary>
            public Dominators<Backwards> PostDominators { get; }

            /// <summary>
            /// Gets or sets the current set of gathered blocks.
            /// </summary>
            private HashSet<BasicBlock>? Gathered { get; set; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given block forms an if-statement that can be
            /// converted using the associated <see cref="ConditionalConverter"/>.
            /// </summary>
            /// <param name="block">The block to check.</param>
            /// <param name="converter"></param>
            /// <returns></returns>
            public bool CanConvert(BasicBlock block, out ConditionalConverter converter)
            {
                converter = default;
                var successors = block.CurrentSuccessors;
                if (successors.Length != 2)
                    return false;

                // Compute the common dominator of all successors
                var postDominator = PostDominators.GetImmediateCommonDominator(
                   successors);

                // Gather region information about nodes from all successors. Furthermore,
                // we can check whether the regions are distinct or share nodes.
                Gathered = new HashSet<BasicBlock>()
                {
                    // Assume that the current node has already been found to avoid loops
                    block,
                };
                var regions = new Regions(block, successors.Length);
                int minRegionSize = int.MaxValue;
                int maxRegionSize = 0;
                for (int i = 0, e = regions.Count; i < e; ++i)
                {
                    var region = new HashSet<BasicBlock>();
                    int regionSize = 0;
                    if (!GatherNodes(
                        successors[i],
                        postDominator,
                        region,
                        ref regionSize))
                    {
                        return false;
                    }

                    // Check for invalid predecessors that are not linked properly
                    if (!VerifyPredecessors(block, region))
                        return false;

                    // Register the current region
                    regions.AddRegion(i, region, ref regionSize);
                    minRegionSize = Math.Min(minRegionSize, regionSize);
                    maxRegionSize = Math.Max(maxRegionSize, regionSize);
                }

                // Check all instruction constraints
                if (maxRegionSize > MaxBlockSize ||
                    Math.Abs(maxRegionSize - minRegionSize) > MaxBlockDifference)
                {
                    return false;
                }

                // If we arrive here, we can be sure that each successor has its distinct
                // region that we may safely merge into one chain of nested blocks

                // Gather all phi values in the rest of the program that do not belong to
                // the gathered part of the program
                var phiValues = GatherPhis(block.Method);

                // Check for very rare cases in which a phi node is linked to a value
                // from other parts of the program or has multiple sources in one region
                if (!CanConvertPhis(phiValues, regions))
                    return false;

                // Create the actual converter that is used to convert all phi values
                var branch = block.GetTerminatorAs<ConditionalBranch>();
                converter = new ConditionalConverter(
                    branch,
                    postDominator,
                    phiValues,
                    regions);
                return true;
            }

            /// <summary>
            /// Gathers all nodes recursively that belong to a particular region.
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="postDominator">
            /// The common post dominator of all regions.
            /// </param>
            /// <param name="visited">The target set of visited nodes.</param>
            /// <param name="regionSize">The current region size.</param>
            /// <returns>True, if this region can be converted.</returns>
            private readonly bool GatherNodes(
                BasicBlock current,
                BasicBlock postDominator,
                HashSet<BasicBlock> visited,
                ref int regionSize)
            {
                // Check whether we have found an exit block
                if (current == postDominator || !visited.Add(current))
                    return true;

                // Reject blocks with side effects and check whether this is a node
                // that has been referenced by another region
                if (current.HasSideEffects() || !Gathered.AsNotNull().Add(current))
                    return false;

                // Adjust the current region size
                regionSize += current.Count;

                // Gather nodes from all successors
                foreach (var successor in current.Successors)
                {
                    if (!GatherNodes(
                        successor,
                        postDominator,
                        visited,
                        ref regionSize))
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>
            /// Gathers all <see cref="PhiValue"/> nodes that reference values from the
            /// region that we want to convert.
            /// </summary>
            /// <param name="method">The parent method.</param>
            /// <returns>
            /// The set of all <see cref="PhiValue"/> that could be found.
            /// </returns>
            private readonly HashSet<PhiValue> GatherPhis(Method method)
            {
                var phiValues = new HashSet<PhiValue>();
                var gathered = Gathered.AsNotNull();
                method.Blocks.ForEachValue<PhiValue>(phiValue =>
                {
                    foreach (var block in phiValue.Sources)
                    {
                        if (gathered.Contains(block))
                        {
                            phiValues.Add(phiValue);
                            break;
                        }
                    }
                });
                return phiValues;
            }

            #endregion
        }

        /// <summary>
        /// A conditional converter to perform the actual if/switch conversion into
        /// conditional value predicates.
        /// </summary>
        private readonly struct ConditionalConverter
        {
            #region Instance

            /// <summary>
            /// Constructs a new conditional converter.
            /// </summary>
            /// <param name="branch">The conditional branch node.</param>
            /// <param name="postDominator">The common post dominator.</param>
            /// <param name="phiValues">All phi values to convert.</param>
            /// <param name="regions"></param>
            public ConditionalConverter(
                ConditionalBranch branch,
                BasicBlock postDominator,
                HashSet<PhiValue> phiValues,
                Regions regions)
            {
                Branch = branch;
                PostDominator = postDominator;
                PhiValues = phiValues;
                Regions = regions;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the source branch.
            /// </summary>
            private ConditionalBranch Branch { get; }

            /// <summary>
            /// The post dominator block.
            /// </summary>
            private BasicBlock PostDominator { get; }

            /// <summary>
            /// Returns the set of all phi values that will be converted.
            /// </summary>
            private HashSet<PhiValue> PhiValues { get; }

            /// <summary>
            /// Returns all regions.
            /// </summary>
            private Regions Regions { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Converts all phi nodes to their conditional value counterparts.
            /// </summary>
            /// <param name="methodBuilder">The current builder.</param>
            private readonly void ConvertPhis(Method.Builder methodBuilder)
            {
                foreach (var phiValue in PhiValues)
                {
                    phiValue.Assert(phiValue.Count == 2);
                    var conditionalValues = ValueList.Empty;
                    conditionalValues.Resize(phiValue.Nodes.Length);
                    for (int i = 0, e = phiValue.Nodes.Length; i < e; ++i)
                    {
                        // Determine the conditional case to which the associated value
                        // belongs to
                        int conditionalCase = Regions.FindRegion(phiValue.Sources[i]) ??
                            throw PostDominator.GetInvalidOperationException();

                        // Get the actual condition value based on the associated phi ref
                        conditionalValues[conditionalCase] = phiValue[i].Resolve();
                    }

                    // Create the final condition
                    var builder = methodBuilder[phiValue.BasicBlock];
                    builder.SetupInsertPosition(phiValue);
                    var conditional = builder.CreatePredicate(
                        phiValue.Location,
                        Branch.Condition,
                        conditionalValues[0],
                        conditionalValues[1]);

                    // Replace the phi node
                    phiValue.Replace(conditional);
                    builder.Remove(phiValue);
                }
            }

            /// <summary>
            /// Converts all branches to a linear branch chain.
            /// </summary>
            /// <param name="methodBuilder">The current builder.</param>
            private readonly void ConvertBranches(Method.Builder methodBuilder)
            {
                // Wire initial branch to the first region
                var blockBuilder = methodBuilder[Branch.BasicBlock];
                blockBuilder.CreateBranch(Branch.Location, Branch.Targets[0]);

                // Linearize all regions
                for (int i = 0, e = Regions.Count - 1; i < e; ++i)
                    ConvertRegionBranches(methodBuilder, i, Branch.Targets[i + 1]);

                // Wire the last region with the last branch
                ConvertRegionBranches(methodBuilder, Regions.Count - 1, PostDominator);
            }

            /// <summary>
            /// Converts all branches inside the specified region.
            /// </summary>
            /// <param name="methodBuilder">The current builder.</param>
            /// <param name="regionIndex">The region index.</param>
            /// <param name="jumpTarget">The jump target.</param>
            private readonly void ConvertRegionBranches(
                Method.Builder methodBuilder,
                int regionIndex,
                BasicBlock jumpTarget)
            {
                foreach (var block in Regions[regionIndex])
                {
                    var terminator = block.GetTerminatorAs<Branch>();
                    if (terminator is null)
                        continue;
                    terminator.RemapTargets(
                        methodBuilder,
                        new IfBranchRemapper(PostDominator, jumpTarget));
                }
            }

            /// <summary>
            /// Converts all phi nodes and branches.
            /// </summary>
            /// <param name="methodBuilder">The current builder.</param>
            public readonly void Convert(Method.Builder methodBuilder)
            {
                ConvertPhis(methodBuilder);
                ConvertBranches(methodBuilder);
            }

            #endregion
        }

        #endregion

        #region Constants

        /// <summary>
        /// The default maximum block size measured in instructions.
        /// </summary>
        public const int DefaultMaxBlockSize = 8;

        /// <summary>
        /// The default maximum block difference between all branches measured in
        /// instructions.
        /// </summary>
        public const int DefaultMaxBlockDifference = 4;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new if/switch conversion transformation.
        /// </summary>
        public IfConversion()
            : this(DefaultMaxBlockSize, DefaultMaxBlockDifference)
        { }

        /// <summary>
        /// Constructs a new if/switch conversion transformation.
        /// </summary>
        /// <param name="maxBlockSize">The maximum block size.</param>
        /// <param name="maxBlockDifference">The maximum block size difference.</param>
        public IfConversion(
            int maxBlockSize,
            int maxBlockDifference)
        {
            if (maxBlockSize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBlockSize));
            if (maxBlockDifference < 0)
                throw new ArgumentOutOfRangeException(nameof(maxBlockDifference));

            MaxBlockSize = maxBlockSize;
            MaxBlockDifference = maxBlockDifference;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the maximum block size.
        /// </summary>
        public int MaxBlockSize { get; }

        /// <summary>
        /// Returns the maximum block difference.
        /// </summary>
        public int MaxBlockDifference { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Folds conditionals into uniform control flow using selects.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var blocks = builder.SourceBlocks;
            var conditionalAnalyzer = new ConditionalAnalyzer(
                MaxBlockSize,
                MaxBlockDifference,
                blocks);

            var converters = InlineList<ConditionalConverter>.Create(
                Math.Max(blocks.Count >> 4, 4));
            foreach (var block in blocks)
            {
                // Check whether we can convert the associated branch
                if (conditionalAnalyzer.CanConvert(block, out var converter))
                    converters.Add(converter);
            }

            // Convert all nodes and branches
            foreach (var converter in converters)
                converter.Convert(builder);

            return converters.Count > 0;
        }

        #endregion
    }

    /// <summary>
    /// Transforms and-also and or-else branch chains into efficient logical operations.
    /// </summary>
    public sealed class IfConditionConversion : UnorderedTransformation
    {
        #region Static

        /// <summary>
        /// Helper function to return <see cref="IfBranch"/> terminator of the given
        /// block.
        /// </summary>
        private static IfBranch GetIfBranch(BasicBlock block) =>
            block.GetTerminatorAs<IfBranch>();

        /// <summary>
        /// Merges two phi case values.
        /// </summary>
        /// <param name="currentValue">The current value.</param>
        /// <param name="caseValue">The case value to merge.</param>
        /// <returns>True, if both case values are compatible.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MergePhiCaseValue(ref Value? currentValue, Value caseValue)
        {
            var oldCaseValue = currentValue;
            currentValue = caseValue;
            return oldCaseValue is null || oldCaseValue == caseValue;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// The kind of a single block in the scope of this transformation.
        /// </summary>
        private enum BlockKind
        {
            /// <summary>
            /// An inner block that can be merged.
            /// </summary>
            Inner,

            /// <summary>
            /// An exit block that has to be preserved.
            /// </summary>
            Exit
        }

        /// <summary>
        /// Wraps a pair consisting of a true-case and a false-case block.
        /// </summary>
        private readonly struct CaseBlocks
        {
            #region Static

            /// <summary>
            /// Gets the primary true leaf that is used to created the merged branch.
            /// </summary>
            /// <param name="kinds">The set of all block kinds.</param>
            /// <param name="current">The current block.</param>
            /// <returns>The determined true block.</returns>
            private static BasicBlock GetTrueExit(
                in BasicBlockMap<BlockKind> kinds,
                BasicBlock current) =>
                kinds[current] == BlockKind.Exit
                ? current
                : GetTrueExit(kinds, GetIfBranch(current).TrueTarget);

            /// <summary>
            /// Gets the primary false leaf that is used to created the merged branch.
            /// </summary>
            /// <param name="kinds">The set of all block kinds.</param>
            /// <param name="trueBlock">The true block.</param>
            /// <returns>The determined false block.</returns>
            private static BasicBlock GetFalseExit(
                in BasicBlockMap<BlockKind> kinds,
                BasicBlock trueBlock)
            {
                foreach (var (block, kind) in kinds)
                {
                    if (block != trueBlock && kind == BlockKind.Exit)
                        return block;
                }

                // This cannot happen since there must be two leaf nodes
                throw trueBlock.GetInvalidOperationException();
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new case blocks instance.
            /// </summary>
            /// <param name="kinds">The current block kinds.</param>
            /// <param name="current">The current root block to start the search.</param>
            public CaseBlocks(in BasicBlockMap<BlockKind> kinds, BasicBlock current)
            {
                TrueBlock = GetTrueExit(kinds, current);
                FalseBlock = GetFalseExit(kinds, TrueBlock);
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the true block.
            /// </summary>
            public BasicBlock TrueBlock { get; }

            /// <summary>
            /// Returns the false block.
            /// </summary>
            public BasicBlock FalseBlock { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given block is the <see cref="TrueBlock"/>.
            /// </summary>
            /// <param name="block">The block to test.</param>
            /// <returns>
            /// True, if the given block is the <see cref="TrueBlock"/>.
            /// </returns>
            public readonly bool IsTrueBlock(BasicBlock block)
            {
                bool result = block == TrueBlock;
                block.Assert(result || block == FalseBlock);
                return result;
            }

            /// <summary>
            /// Returns true if the given block is either the <see cref="TrueBlock"/>
            /// or the <see cref="FalseBlock"/>.
            /// </summary>
            /// <param name="block">The block to test.</param>
            /// <returns>
            /// True, if the given block is either the <see cref="TrueBlock"/> or the
            /// <see cref="FalseBlock"/>.
            /// </returns>
            public readonly bool Contains(BasicBlock block) =>
                block == TrueBlock || block == FalseBlock;

            /// <summary>
            /// Asserts that the given value is contained in either the
            /// <see cref="TrueBlock"/> or the <see cref="FalseBlock"/>.
            /// </summary>
            /// <param name="value">The value to test.</param>
            public readonly void AssertInBlocks(Value value) =>
                value.Assert(Contains(value.BasicBlock));

            #endregion
        }

        /// <summary>
        /// A custom successors provider that stops processing as soon as it hits an
        /// block with kind <see cref="BlockKind.Exit"/>.
        /// </summary>
        private readonly struct SuccessorsProvider :
            ITraversalSuccessorsProvider<Forwards>
        {
            #region Instance

            /// <summary>
            /// Constructs a new successors provider.
            /// </summary>
            /// <param name="dominators">The dominators.</param>
            /// <param name="entryPoint">The current entry point.</param>
            /// <param name="maxNumInstructions">
            /// The maximum number of instructions in an inner block.
            /// </param>
            public SuccessorsProvider(
                Dominators dominators,
                BasicBlock entryPoint,
                int maxNumInstructions)
            {
                Dominators = dominators;
                EntryPoint = entryPoint;
                MaxNumInstructions = maxNumInstructions;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns all dominators.
            /// </summary>
            public Dominators Dominators { get; }

            /// <summary>
            /// Returns the current entry point.
            /// </summary>
            public BasicBlock EntryPoint { get; }

            /// <summary>
            /// Returns the maximum number of instructions in an inner block.
            /// </summary>
            public int MaxNumInstructions { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given block can be converted (an inner block).
            /// </summary>
            /// <param name="basicBlock">The block to test.</param>
            /// <param name="terminator">The resolved terminator (if any).</param>
            /// <returns>
            /// True, if the block can be considered to be an inner block.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool IsCompatibleBlock(
                BasicBlock basicBlock,
                [NotNullWhen(true)] out IfBranch? terminator) =>
                // The current block must have an IfBranch terminator
                (terminator = basicBlock.Terminator as IfBranch) != null &&
                // It must be dominated by the entry block in order to avoid rare cases
                // in which the current block is also reachable by other parts of the
                // program
                Dominators.Dominates(EntryPoint, basicBlock) &&
                // It must not exceed the max #instructions per block and must not have
                // any side effects
                basicBlock.Count <= MaxNumInstructions &&
                !basicBlock.HasSideEffects();

            /// <summary>
            /// Determines the block kind of the given block.
            /// </summary>
            /// <param name="basicBlock">The current block.</param>
            /// <param name="exitCounter">The current number of exit blocks.</param>
            /// <returns>The block kind.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly BlockKind GetBlockKind(
                BasicBlock basicBlock,
                ref int exitCounter)
            {
                if (IsCompatibleBlock(basicBlock, out var _))
                    return BlockKind.Inner;
                ++exitCounter;
                return BlockKind.Exit;
            }

            /// <summary>
            /// Returns all successors in the case of an inner block.
            /// </summary>
            /// <param name="basicBlock">The current basic block.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<BasicBlock> GetSuccessors(
                BasicBlock basicBlock) =>
                IsCompatibleBlock(basicBlock, out var terminator)
                ? terminator.Targets
                : new ReadOnlySpan<BasicBlock>();

            #endregion
        }

        /// <summary>
        /// Represents a kind predicate that filters blocks based on their kind.
        /// </summary>
        private readonly struct BlockKindPredicate : InlineList.IPredicate<BasicBlock>
        {
            /// <summary>
            /// Constructs a new kind predicate.
            /// </summary>
            /// <param name="kinds">All block kinds.</param>
            /// <param name="kindToInclude">The kind of blocks to include.</param>
            public BlockKindPredicate(
                in BasicBlockMap<BlockKind> kinds,
                BlockKind kindToInclude)
            {
                Kinds = kinds;
                KindToInclude = kindToInclude;
            }

            /// <summary>
            /// Returns the kind of blocks to include.
            /// </summary>
            public BlockKind KindToInclude { get; }

            /// <summary>
            /// Returns the map of all block kinds.
            /// </summary>
            private BasicBlockMap<BlockKind> Kinds { get; }

            /// <summary>
            /// Returns true if the kind of the given block is equal to
            /// <see cref="KindToInclude"/>.
            /// </summary>
            public readonly bool Apply(BasicBlock item) =>
                Kinds[item] == KindToInclude;
        }

        /// <summary>
        /// Skips duplicate entries pointing to the entry block.
        /// </summary>
        private struct PhiRemapper : PhiValue.IArgumentRemapper
        {
            public PhiRemapper(BasicBlock entryBlock)
            {
                EntryBlock = entryBlock;
                Added = false;
            }

            /// <summary>
            /// Returns the entry block to remap to.
            /// </summary>
            public BasicBlock EntryBlock { get; }

            /// <summary>
            /// Returns true if the <see cref="EntryBlock"/> has been already wired
            /// with the current block.
            /// </summary>
            public bool Added { get; private set; }

            /// <summary>
            /// Returns true and sets the value of <see cref="Added"/> to false.
            /// </summary>
            public bool CanRemap(PhiValue phiValue)
            {
                Added = false;
                return true;
            }

            /// <summary>
            /// Performs an identity mapping by filtering duplicate sources pointing to
            /// the <see cref="EntryBlock"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryRemap(
                PhiValue phiValue,
                BasicBlock block,
                out BasicBlock newBlock)
            {
                newBlock = block;
                if (block != EntryBlock)
                    return true;

                bool add = !Added;
                Added = true;
                return add;
            }

            /// <summary>
            /// Returns the input <paramref name="value"/>.
            /// </summary>
            public readonly Value RemapValue(
                PhiValue phiValue,
                BasicBlock updatedBlock,
                Value value) => value;
        }

        /// <summary>
        /// An analyzer to detect compatible (nested) if-branch conditions.
        /// </summary>
        private ref struct ConditionalAnalyzer
        {
            #region Instance

            private BasicBlockMap<BlockKind> kinds;

            /// <summary>
            /// Constructs a new conditional analyzer.
            /// </summary>
            /// <param name="blocks">The current block collection.</param>
            /// <param name="maxBlockSize">The maximum block size.</param>
            public ConditionalAnalyzer(BlockCollection blocks, int maxBlockSize)
            {
                kinds = blocks.CreateMap<BlockKind>();

                MaxNumBlocks = blocks.Count;
                MaxBlockSize = maxBlockSize;

                Dominators = blocks.CreateDominators();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the maximum number of all blocks.
            /// </summary>
            public int MaxNumBlocks { get; }

            /// <summary>
            /// Returns the maximum block size
            /// </summary>
            public int MaxBlockSize { get; }

            /// <summary>
            /// Returns the dominator analysis.
            /// </summary>
            public Dominators Dominators { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given block forms an if-statement that can be
            /// converted using the associated <see cref="ConditionalConverter"/>.
            /// </summary>
            /// <param name="methodBuilder">The current builder.</param>
            /// <param name="current">The block to check.</param>
            /// <param name="converter">The created converter (if any).</param>
            /// <returns>True, if the given block can be converted.</returns>
            public bool CanConvert(
                Method.Builder methodBuilder,
                BasicBlock current,
                out ConditionalConverter converter)
            {
                // Early exit for trivially not-supported block constructions
                converter = default;
                if (!(current.Terminator is IfBranch))
                    return false;

                // Try to determine an intermediate condition graph
                kinds.Clear();
                if (!Traverse(current, out var blocks))
                    return false;

                // Get the true and false-branch leaf nodes that are used to build the
                // conditional branch in the end
                var caseBlocks = new CaseBlocks(kinds, current);

                // Check all phi-value references and determine all phis that need
                // to be adjusted after folding all blocks
                if (GetLocalPhis(blocks, BlockKind.Inner).Count > 0)
                    return false;

                // Initialize the list of phi entries and find all phis to adapt
                var phis = ValueList.Create(2);
                if (!GatherPhiValues(blocks, caseBlocks, ref phis))
                    return false;

                // Create the converter to transform all compatible blocks
                converter = new ConditionalConverter(
                    methodBuilder,
                    kinds,
                    blocks,
                    phis,
                    caseBlocks);
                return true;
            }

            /// <summary>
            /// Traverses the control flow starting with the current block and tries to
            /// determine a set of blocks that can be merged.
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="blocks">The collection of convertible blocks.</param>
            /// <returns>
            /// True, if a set of blocks that can be merged could be found.
            /// </returns>
            private bool Traverse(BasicBlock current, out BlockCollection blocks)
            {
                // Resolve all blocks that can be merged
                var successorsProvider = new SuccessorsProvider(
                    Dominators,
                    current,
                    MaxBlockSize);
                blocks = new ReversePostOrder().TraverseToCollection<
                    ReversePostOrder,
                    SuccessorsProvider,
                    Forwards>(MaxNumBlocks, current, successorsProvider);

                // Early exit for incompatible block setups
                if (blocks.Count < 4)
                    return false;

                // Register all blocks while using their different kinds
                int numExitBlocks = 0;
                foreach (var block in blocks)
                {
                    kinds[block] = successorsProvider.GetBlockKind(
                        block,
                        ref numExitBlocks);
                }

                return numExitBlocks == 2;
            }

            /// <summary>
            /// Returns all local phi values that are stored in blocks with the
            /// specified <paramref name="blockKind"/>.
            /// </summary>
            /// <param name="blocks">The blocks to be converted.</param>
            /// <param name="blockKind">The target block kind.</param>
            /// <returns>The list of all phi values.</returns>
            private readonly Phis GetLocalPhis(
                in BlockCollection blocks,
                BlockKind blockKind)
            {
                var builder = Phis.CreateBuilder(blocks.Method);
                foreach (var block in blocks)
                {
                    if (kinds[block] == blockKind)
                        builder.Add(block);
                }
                return builder.Seal();
            }

            /// <summary>
            /// Gathers and checks all local phi values that need to be adapted.
            /// </summary>
            /// <param name="blocks">The blocks to be converted.</param>
            /// <param name="caseBlocks">Both case blocks.</param>
            /// <param name="phis">The list of phi values to adapt.</param>
            /// <returns>True, if all phi values are compatible.</returns>
            private readonly bool GatherPhiValues(
                in BlockCollection blocks,
                in CaseBlocks caseBlocks,
                ref ValueList phis)
            {
                // Get all phis in the exit block
                var exitPhis = GetLocalPhis(blocks, BlockKind.Exit);

                // Convert to a set of blocks including all inner blocks
                var innerBlocksSet = blocks.ToSet(
                    new BlockKindPredicate(kinds, BlockKind.Inner));

                Value? trueValue = null;
                Value? falseValue = null;
                foreach (var phi in exitPhis)
                {
                    // The phi must be located in one of our exit blocks
                    caseBlocks.AssertInBlocks(phi);

                    // Check whether this phi value has source that is not linked to our
                    // block set
                    if (!phi.Sources.Any(
                        BasicBlock.IsInCollectionPredicate.ToPredicate(innerBlocksSet)))
                    {
                        continue;
                    }

                    // Check whether all sources are linked to our internal blocks
                    bool isTrueBlock = caseBlocks.IsTrueBlock(phi.BasicBlock);
                    for (int i = 0, e = phi.Count; i < e; ++i)
                    {
                        if (!innerBlocksSet.Contains(phi.Sources[i]))
                            continue;

                        // Get the value for this predecessor
                        Value phiValue = phi[i];

                        // Check the case for this predecessor
                        bool merged = isTrueBlock
                            ? MergePhiCaseValue(ref trueValue, phiValue)
                            : MergePhiCaseValue(ref falseValue, phiValue);

                        // If we could not merge these case values, we have to skip the
                        // whole block list, since it contains unknown control flow
                        if (!merged)
                            return false;
                    }

                    // If we reach this point, the current phi value has to be adapted
                    phis.Add(phi);
                }
                return true;
            }

            #endregion
        }

        /// <summary>
        /// A conditional converter to perform the actual if/switch conversion into
        /// conditional value predicates.
        /// </summary>
        private ref struct ConditionalConverter
        {
            #region Instance

            /// <summary>
            /// Constructs a new conditional converter.
            /// </summary>
            /// <param name="builder">The parent builder.</param>
            /// <param name="kinds">The mapping of block kinds.</param>
            /// <param name="blocks">The block collection to be used.</param>
            /// <param name="phis">All phis to be adapted.</param>
            /// <param name="caseBlocks">Both case blocks.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConditionalConverter(
                Method.Builder builder,
                BasicBlockMap<BlockKind> kinds,
                BlockCollection blocks,
                ReadOnlySpan<ValueReference> phis,
                CaseBlocks caseBlocks)
            {
                Blocks = blocks;
                Kinds = kinds;

                Builder = builder;
                BlockBuilder = builder[blocks.EntryBlock];

                Phis = phis;
                CaseBlocks = caseBlocks;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns the main target builder used to emit all conditionals.
            /// </summary>
            public BasicBlock.Builder BlockBuilder { get; }

            /// <summary>
            /// Returns all blocks in this conditional graph.
            /// </summary>
            public BlockCollection Blocks { get; }

            /// <summary>
            /// Returns all block kinds.
            /// </summary>
            public BasicBlockMap<BlockKind> Kinds { get; }

            /// <summary>
            /// Returns the entry block of the current collections of blocks.
            /// </summary>
            public readonly BasicBlock EntryBlock => Blocks.EntryBlock;

            /// <summary>
            /// Returns all phi values that need to be adapted.
            /// </summary>
            public ReadOnlySpan<ValueReference> Phis { get; }

            /// <summary>
            /// Returns both case blocks.
            /// </summary>
            public CaseBlocks CaseBlocks { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given block should be maintained.
            /// </summary>
            private readonly bool IsBlockToKeep(BasicBlock block) =>
                Bitwise.Or(block == EntryBlock, CaseBlocks.Contains(block));

            /// <summary>
            /// Returns true if the given block is an exit block.
            /// </summary>
            private readonly bool IsExit(BasicBlock block) =>
                Kinds[block] == BlockKind.Exit;

            /// <summary>
            /// Converts the underlying conditional tree into a folded set of wired
            /// conditionals.
            /// </summary>
            public void Convert()
            {
                // Remember the original target location
                var terminator = GetIfBranch(EntryBlock);

                // Merge all blocks into one
                MergeBlocks();
                BlockBuilder.SetupInsertPositionToEnd();

                // Emit the actual condition
                CreateInnerCondition(terminator, null, null, out Value? condition);
                EntryBlock.AssertNotNull(condition.AsNotNull());

                // Create the actual branch condition
                BlockBuilder.CreateIfBranch(
                    terminator.Location,
                    condition.AsNotNull(),
                    CaseBlocks.TrueBlock,
                    CaseBlocks.FalseBlock);

                // Adapt all phis
                AdaptPhis();

                // Clear all other blocks to remove all obsolete uses
                ClearBlocks();

                // Replace the original terminator
                terminator.Replace(BlockBuilder.CreateUndefined());
            }

            /// <summary>
            /// Merges the given inner node into the given block builder.
            /// </summary>
            private void MergeBlocks()
            {
                foreach (var block in Blocks)
                {
                    // Skip the root block
                    if (IsBlockToKeep(block))
                        continue;

                    BlockBuilder.MergeBlock(block);
                }
            }

            /// <summary>
            /// Merges both conditions using the <paramref name="kind"/>.
            /// </summary>
            /// <param name="condition">The source condition (may be null).</param>
            /// <param name="newCondition">The new condition to merge.</param>
            /// <param name="kind">The arithmetic kind used to combine them.</param>
            /// <returns>
            /// The merged condition or <paramref name="newCondition"/>.
            /// </returns>
            private Value MergeCondition(
                Value? condition,
                Value newCondition,
                BinaryArithmeticKind kind) =>
                condition is null
                ? newCondition
                : (Value)BlockBuilder.CreateArithmetic(
                    condition.Location,
                    condition,
                    newCondition,
                    kind);

            /// <summary>
            /// Creates a merge intermediate condition that will be passed to the
            /// <see cref="CreateCondition(BasicBlock, Value, Value, out Value)"/> method.
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="condition">The source condition (may be null).</param>
            /// <param name="newCondition">The new condition to merge.</param>
            /// <param name="initialExitCondition">The current exit condition.</param>
            /// <param name="exitCondition">The exit condition to be updated.</param>
            private void CreateMergedCondition(
                BasicBlock current,
                Value? condition,
                Value newCondition,
                Value? initialExitCondition,
                out Value? exitCondition)
            {
                var merged = MergeCondition(
                    condition,
                    newCondition,
                    BinaryArithmeticKind.And);

                // Continue the traversal using the merged condition
                CreateCondition(
                    current,
                    merged,
                    initialExitCondition,
                    out exitCondition);
            }

            /// <summary>
            /// Creates and updates the exit condition in the case of a
            /// <see cref="CaseBlocks.TrueBlock" />
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="condition">The source condition (may be null).</param>
            /// <param name="initialExitCondition">The current exit condition.</param>
            /// <param name="exitCondition">The exit condition to be updated.</param>
            private void CreateExitCondition(
                BasicBlock current,
                Value condition,
                Value? initialExitCondition,
                out Value? exitCondition)
            {
                current.Assert(IsExit(current));

                // Skip non-true blocks since they will not contribute to the conditional
                // branch that will be emitted in the end
                if (!CaseBlocks.IsTrueBlock(current))
                {
                    exitCondition = initialExitCondition;
                    return;
                }

                // Append the current condition using a logical or to form clauses of
                // the form: (a & b & c) | (d & e & f) | ...
                exitCondition = MergeCondition(
                    initialExitCondition,
                    condition,
                    BinaryArithmeticKind.Or);
            }

            /// <summary>
            /// Creates conditions for inner blocks using recursion.
            /// </summary>
            /// <param name="terminator">The current terminator.</param>
            /// <param name="condition">The source condition (may be null).</param>
            /// <param name="initialExitCondition">The current exit condition.</param>
            /// <param name="exitCondition">The exit condition to be updated.</param>
            private void CreateInnerCondition(
                IfBranch terminator,
                Value? condition,
                Value? initialExitCondition,
                out Value? exitCondition)
            {
                // Determine the true and false conditions, as well as the different
                // branch targets
                var trueCondition = terminator.Condition;
                var falseCondition = trueCondition;
                var (trueTarget, falseTarget) = terminator.NotInvertedBranchTargets;

                // Simple optimization to avoid the generation on unnecessary operations
                bool emitFalseTarget = terminator.FalseTarget != CaseBlocks.FalseBlock;

                // Check whether we need to add another not here
                if (terminator.FalseTarget == CaseBlocks.TrueBlock)
                {
                    // Invert the true condition since the true branch target is on the
                    // RHS of the branch
                    trueCondition = BlockBuilder.CreateArithmetic(
                        trueCondition.Location,
                        trueCondition,
                        UnaryArithmeticKind.Not);
                    Utilities.Swap(ref trueTarget, ref falseTarget);
                }
                else if (emitFalseTarget)
                {
                    // Negate the false condition otherwise
                    falseCondition = BlockBuilder.CreateArithmetic(
                        falseCondition.Location,
                        falseCondition,
                        UnaryArithmeticKind.Not);
                }

                // Merge the true condition and continue with the true target
                CreateMergedCondition(
                    trueTarget,
                    condition,
                    trueCondition,
                    initialExitCondition,
                    out exitCondition);

                // If we have to emit a false target, continue with a recursive emission
                if (emitFalseTarget)
                {
                    // Merge the false condition and continue with the false target
                    initialExitCondition = exitCondition;

                    CreateMergedCondition(
                        falseTarget,
                        condition,
                        falseCondition,
                        initialExitCondition,
                        out exitCondition);
                }
            }

            /// <summary>
            /// Creates a condition for an exit or an inner block.
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="condition">The source condition (may be null).</param>
            /// <param name="initialExitCondition">The current exit condition.</param>
            /// <param name="exitCondition">The exit condition to be updated.</param>
            private void CreateCondition(
                BasicBlock current,
                Value condition,
                Value? initialExitCondition,
                out Value? exitCondition)
            {
                if (IsExit(current))
                {
                    // Create an exit-block condition
                    CreateExitCondition(
                        current,
                        condition,
                        initialExitCondition,
                        out exitCondition);
                }
                else
                {
                    // Create an inner-block condition
                    var terminator = GetIfBranch(current);
                    CreateInnerCondition(
                        terminator,
                        condition,
                        initialExitCondition,
                        out exitCondition);
                }
            }

            /// <summary>
            /// Clears all blocks that have been merged in order to release the uses.
            /// </summary>
            private void ClearBlocks()
            {
                foreach (var block in Blocks)
                {
                    if (IsBlockToKeep(block))
                        continue;

                    Builder[block].Clear();
                }
            }

            /// <summary>
            /// Adapts all phi sources to match the new control-flow structure.
            /// </summary>
            private void AdaptPhis()
            {
                // Initialize the remapper that maps inner blocks to the entry block
                var phiRemapper = new PhiRemapper(EntryBlock);
                foreach (PhiValue phi in Phis)
                {
                    // The phi must be located in one of our exit blocks
                    CaseBlocks.AssertInBlocks(phi);

                    // Remap the current phi
                    phi.RemapArguments(Builder, phiRemapper);
                }
            }

            #endregion
        }

        #endregion

        #region Constants

        /// <summary>
        /// The default maximum block size measured in instructions.
        /// </summary>
        public const int DefaultMaxBlockSize = 4;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new conditional conversion transformation using the default
        /// maximum block size.
        /// </summary>
        public IfConditionConversion()
            : this(DefaultMaxBlockSize)
        { }

        /// <summary>
        /// Constructs a new conditional conversion transformation.
        /// </summary>
        /// <param name="maxBlockSize">The maximum block size in instructions.</param>
        public IfConditionConversion(int maxBlockSize)
        {
            if (maxBlockSize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBlockSize));

            MaxBlockSize = maxBlockSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the maximum block size for merging in number of instructions.
        /// </summary>
        public int MaxBlockSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Applies to if-conditional conversion transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // We change the control-flow structure during the transformation but
            // need to get information about previous successors
            builder.AcceptControlFlowUpdates(accept: true);

            // Create the conditional analyzer to detect compatible block setups
            var blocks = builder.SourceBlocks;
            var analyzer = new ConditionalAnalyzer(blocks, MaxBlockSize);

            // Convert all ifs in post order
            bool applied = false;
            foreach (var block in blocks.AsOrder<PostOrder>())
            {
                // Skip blocks that have been converted or cannot be converted
                if (analyzer.CanConvert(builder, block, out var converter))
                {
                    // Apply the instantiated converter
                    converter.Convert();
                    applied = true;
                }
            }

            return applied;
        }

        #endregion
    }
}
