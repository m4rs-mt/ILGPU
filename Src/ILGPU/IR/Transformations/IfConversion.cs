// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IfConversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlockCollection = ILGPU.IR.BasicBlockCollection<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
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
            private HashSet<BasicBlock> Gathered { get; set; }

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
                if (current.HasSideEffects() || !Gathered.Add(current))
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
                var gathered = Gathered;
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
}
