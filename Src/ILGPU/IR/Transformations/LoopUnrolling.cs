// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopUnrolling.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Loop = ILGPU.IR.Analyses.Loops<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>.Node;
using LoopInfo = ILGPU.IR.Analyses.LoopInfo<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using LoopInfos = ILGPU.IR.Analyses.LoopInfos<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using Loops = ILGPU.IR.Analyses.Loops<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Unrolls loops that rely on simple induction variables.
    /// </summary>
    public sealed class LoopUnrolling : UnorderedTransformation
    {
        #region Constants

        /// <summary>
        /// Represents the default maximum unroll factor.
        /// </summary>
        public const int DefaultMaxUnrollFactor = 32;

        #endregion

        #region Nested Types

        /// <summary>
        /// Remaps loop-specific target blocks and phi-value arguments.
        /// </summary>
        private readonly struct LoopRemapper :
            PhiValue.IArgumentRemapper,
            TerminatorValue.ITargetRemapper
        {
            public LoopRemapper(
                BasicBlock source,
                BasicBlock target,
                Value targetValue = null)
            {
                Source = source;
                Target = target;
                TargetValue = targetValue;
            }

            /// <summary>
            /// The loop entry.
            /// </summary>
            public BasicBlock Source { get; }

            /// <summary>
            /// The temporary predecessor branch.
            /// </summary>
            public BasicBlock Target { get; }

            /// <summary>
            /// Returns the target value to map phi operands to (if any).
            /// </summary>
            public Value TargetValue { get; }

            /// <summary>
            /// Returns true if the given span contains the loop entry.
            /// </summary>
            public readonly bool CanRemap(in ReadOnlySpan<BasicBlock> blocks) =>
                blocks.Contains(Source, new BasicBlock.Comparer());

            /// <summary>
            /// Returns true if the given phi value references the loop entry.
            /// </summary>
            public readonly bool CanRemap(PhiValue phiValue) =>
                CanRemap(phiValue.Sources);

            /// <summary>
            /// Remaps the given block to the target block in the case of the source
            /// block. It returns the given block otherwise.
            /// </summary>
            public readonly BasicBlock Remap(BasicBlock block) =>
                block == Source ? Target : block;

            /// <summary>
            /// Returns always true and remaps the new block using
            /// <see cref="Remap(BasicBlock)"/>.
            /// </summary>
            public readonly bool TryRemap(
                PhiValue phiValue,
                BasicBlock block,
                out BasicBlock newBlock)
            {
                newBlock = Remap(block);
                return true;
            }

            /// <summary>
            /// Remaps the given value to the target value (if defined).
            /// </summary>
            public readonly Value RemapValue(
                PhiValue phiValue,
                BasicBlock updatedBlock,
                Value value) =>
                updatedBlock == Target ? TargetValue : value;
        }

        /// <summary>
        /// Specializes loop bodies.
        /// </summary>
        private ref struct LoopSpecializer
        {
            #region Instance

            /// <summary>
            /// Maps original phi values to new target values that have to be used
            /// instead in the remainder of the program.
            /// </summary>
            private readonly Dictionary<PhiValue, Value> phiMapping;

            /// <summary>
            /// Maps original values to new target values that have to be used instead
            /// in the remainder of the program.
            /// </summary>
            private InlineList<(Value Source, Value Target)> valueMapping;

            /// <summary>
            /// All blocks in the scope of this loop in RPO.
            /// </summary>
            private readonly BasicBlockCollection<ReversePostOrder, Forwards> blocks;

            /// <summary>
            /// All (potentially) patched phi values.
            /// </summary>
            private readonly ReadOnlySpan<(PhiValue phi, Value outsideOperand)> phiValues;

            public LoopSpecializer(
                Method.Builder builder,
                LoopInfo<ReversePostOrder, Forwards> loopInfo,
                InductionVariable variable,
                bool fullUnrollMode)
            {
                Builder = builder;
                BlockBuilder = builder[loopInfo.Header];
                Variable = variable;
                phiValues = loopInfo.PhiValues;
                phiMapping = new Dictionary<PhiValue, Value>(phiValues.Length);

                blocks = loopInfo.ComputeOrderedBodyBlocks();
                bool hasOtherTarget = variable.BreakBranch.TryGetOtherBranchTarget(
                    loopInfo.Exit,
                    out var loopBody);
                variable.BreakBranch.Assert(hasOtherTarget);
                LoopBody = loopBody;
                BackEdge = loopInfo.BackEdge;

                // Determine all values in the body of the loop that require value
                // updates
                valueMapping = InlineList<(Value, Value)>.Create(phiValues.Length);
                var bodyBlockSet = blocks.ToSet();
                foreach (Value value in blocks.Values)
                {
                    // Skip phi values since they will be handled separately
                    if (value is PhiValue || value.Uses.AllIn(bodyBlockSet))
                        continue;
                    valueMapping.Add((value, value));
                }

                // Check whether we will fully unroll the loop
                if (fullUnrollMode)
                {
                    // If yes, we will use all init values of all reduction phis
                    VariableInitValue = Variable.Init;
                    LinkPhisToInitValue();
                }
                else
                {
                    // If no, we have to preserve the original phi values
                    VariableInitValue = Variable.Phi;
                    LinkPhisToPhis();
                }
            }

            /// <summary>
            /// Links all phi values to their instances.
            /// </summary>
            private readonly void LinkPhisToPhis()
            {
                foreach (var (phi, _) in phiValues)
                    phiMapping[phi] = phi;
            }

            /// <summary>
            /// Links all phi values to their outside (init) operands.
            /// </summary>
            private readonly void LinkPhisToInitValue()
            {
                foreach (var (phi, initValue) in phiValues)
                    phiMapping[phi] = initValue;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent method builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns the current block builder.
            /// </summary>
            public BasicBlock.Builder BlockBuilder { get; }

            /// <summary>
            /// Returns the associated variable.
            /// </summary>
            public InductionVariable Variable { get; }

            /// <summary>
            /// Returns the associated phi value representing the induction variable.
            /// </summary>
            public readonly PhiValue PhiVariable => Variable.Phi;

            /// <summary>
            /// Returns the block that is reachable from the break condition.
            /// </summary>
            public BasicBlock LoopBody { get; }

            /// <summary>
            /// Returns the block containing the back-edge branch.
            /// </summary>
            public BasicBlock BackEdge { get; }

            /// <summary>
            /// Returns the init value to update the induction variable.
            /// </summary>
            public Value VariableInitValue { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Creates a rebuilder value mapping including all currently known value
            /// updates.
            /// </summary>
            /// <param name="initValue">The current init value to use.</param>
            private readonly Dictionary<Value, Value> CreateValueRebuilderMapping(
                Value initValue)
            {
                // Build a value remapping for all registered values
                var result = new Dictionary<Value, Value>(phiValues.Length + 1)
                {
                    // Replace the actual induction variable with the updated value
                    [PhiVariable] = initValue
                };

                // Map all remaining phi values
                foreach (var (phi, _) in phiValues)
                {
                    // Replace the remapped phi with the outside operand representing
                    // its initial value
                    result[phi] = phiMapping[phi];
                }

                return result;
            }

            /// <summary>
            /// Specializes a single loop iteration.
            /// </summary>
            /// <param name="exitBlock">The current exit block to jump to.</param>
            /// <param name="initValue">The current init value to use.</param>
            /// <returns>The new entry and exit blocks.</returns>
            public (BasicBlock.Builder entry, BasicBlock.Builder exit)
                SpecializeLoop(
                BasicBlock exitBlock,
                Value initValue)
            {
                // Setup a temporary exit block that links the new blocks with the other
                // parts of the program
                var currentExit = Builder.CreateBasicBlock(
                    exitBlock.Location);

                // Rebuild the affected parts of the program
                var valueRebuilderMapping = CreateValueRebuilderMapping(initValue);
                var rebuilder = IRRebuilder.Create(
                    Builder,
                    valueRebuilderMapping,
                    blocks,
                    new LoopRemapper(exitBlock, currentExit));
                rebuilder.Rebuild();

                // Update all phi mappings
                foreach (var (phi, _) in phiValues)
                {
                    // Get the corresponding phi value for the back-edge predecessor
                    var backEdgeValue = phi.GetValue(BackEdge);
                    phi.AssertNotNull(backEdgeValue);
                    phiMapping[phi] = rebuilder[backEdgeValue];
                }
                phiMapping[PhiVariable] = rebuilder[Variable.Update];

                // Update all values to track
                for (int i = 0, e = valueMapping.Count; i < e; ++i)
                {
                    var source = valueMapping[i].Source;
                    valueMapping[i] = (source, rebuilder[source]);
                }

                // Remove the back-edge branch and simplify the exit branch to form a
                // straight-line piece of code
                var backEdgeBlock = rebuilder[BackEdge];
                backEdgeBlock.CreateBranch(
                    backEdgeBlock.Terminator.Location,
                    currentExit);

                return (rebuilder.EntryBlock, currentExit);
            }

            /// <summary>
            /// Finishes the loop specialization phase by wiring all phi values.
            /// </summary>
            public readonly void RewirePhis(BasicBlock backEdgeBlock)
            {
                // Create a new phi value for every registered phi
                foreach (var entry in phiMapping)
                {
                    entry.Key.RemapArguments(
                        Builder,
                        new LoopRemapper(
                            BackEdge,
                            backEdgeBlock,
                            entry.Value));
                }
            }

            /// <summary>
            /// Replace all loop-specific phi values.
            /// </summary>
            public readonly void ReplacePhis()
            {
                foreach (var entry in phiMapping)
                    entry.Key.Replace(entry.Value);
            }

            /// <summary>
            /// Replace all loop-specific values.
            /// </summary>
            public readonly void ReplaceValues()
            {
                foreach (var entry in valueMapping)
                    entry.Source.Replace(entry.Target);
            }

            /// <summary>
            /// Clears all blocks in the body of the loop.
            /// </summary>
            public readonly void ClearBody()
            {
                foreach (var block in blocks)
                    Builder[block].Clear();
            }

            #endregion
        }

        /// <summary>
        /// Applies the unrolling transformation to all loops.
        /// </summary>
        private struct LoopProcessor : Loops.ILoopProcessor
        {
            private readonly LoopInfos loopInfos;

            public LoopProcessor(
                in LoopInfos<ReversePostOrder, Forwards> infos,
                Method.Builder builder,
                int maxUnrollFactor)
            {
                loopInfos = infos;
                Builder = builder;
                MaxUnrollFactor = maxUnrollFactor;
                Applied = false;
            }

            /// <summary>
            /// Returns the parent method builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns the maximum unrolling factor.
            /// </summary>
            public int MaxUnrollFactor { get; }

            /// <summary>
            /// Returns true if the loop processor could be applied.
            /// </summary>
            public bool Applied { get; private set; }

            /// <summary>
            /// Applies the unrolling transformation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Process(Loop loop) =>
                Applied |= TryUnroll(
                    Builder,
                    loop,
                    loopInfos,
                    MaxUnrollFactor);
        }

        #endregion

        #region Static

        /// <summary>
        /// Unrolls the given loop based on the unroll and iteration factors.
        /// </summary>
        private static void UnrollLoop(
            Method.Builder builder,
            LoopInfo loopInfo,
            InductionVariable inductionVariable,
            in InductionVariableBounds bounds,
            int unrolls,
            int iterations,
            int update)
        {
            var entryBlock = loopInfo.Entry;
            var exitBlock = loopInfo.Exit;

            // Create a new entry block for the whole loop
            var current = builder.CreateBasicBlock(entryBlock.Location);
            var newBodyEntry = current;

            // Specialize the body of the loop
            var loopSpecializer = new LoopSpecializer(
                builder,
                loopInfo,
                inductionVariable,
                iterations < 2);

            // Unroll the loop until we reach the maximum unrolling factor
            for (int i = 0; i < unrolls; ++i)
            {
                // Setup a proper start value
                Value startValue = current.CreatePrimitiveValue(
                    bounds.Init.Location,
                    bounds.Init.BasicValueType,
                    i * update);
                startValue = current.CreateArithmetic(
                    bounds.UpdateValue.Location,
                    loopSpecializer.VariableInitValue,
                    startValue,
                    bounds.UpdateOperation.Kind);

                // Specialize the whole loop and wire the blocks
                var (loopEntry, loopExit) = loopSpecializer.SpecializeLoop(
                    exitBlock,
                    startValue);

                // Link to the new entry block
                current.CreateBranch(loopEntry.BasicBlock.Location, loopEntry);
                current = loopExit;
            }

            // Check whether we still need a loop
            if (iterations > 1)
            {
                // Remap branch to the new body entry
                var headerBuilder = builder[loopInfo.Header];
                var branch = loopInfo.Header.GetTerminatorAs<Branch>();
                branch.RemapTargets(
                    headerBuilder,
                    new LoopRemapper(loopInfo.Body, newBodyEntry));

                // Link the current block to the original header block
                current.CreateBranch(exitBlock.Location, loopInfo.Header);

                // Preserve all phis and rewire their operands
                loopSpecializer.RewirePhis(current);

                // Replace all values used outside of the loop with their computed values
                loopSpecializer.ReplaceValues();
            }
            else
            {
                // Wire the current entry block
                var entryBuilder = builder[entryBlock];
                var branch = entryBlock.GetTerminatorAs<Branch>();
                branch.RemapTargets(
                    entryBuilder,
                    new LoopRemapper(loopInfo.Header, newBodyEntry));

                // Link the current block to the original exit block
                current.CreateBranch(exitBlock.Location, exitBlock);

                // Replace all phi values with their computed values
                loopSpecializer.ReplacePhis();

                // Replace all values with used outside of the loop with their computed
                // values
                loopSpecializer.ReplaceValues();

                // Clear the loop header
                builder[loopInfo.Header].Clear();

                // Clear all old body blocks to remove all old uses
                loopSpecializer.ClearBody();
            }
        }

        /// <summary>
        /// Tries to unroll the given loop.
        /// </summary>
        private static bool TryUnroll(
            Method.Builder builder,
            Loop loop,
            LoopInfos loopInfos,
            int maxUnrollFactor)
        {
            // Try to find a loop information entry and ensure a simple loop for now
            if (!loopInfos.TryGetInfo(loop, out var loopInfo) ||
                loopInfo.InductionVariables.Length != 1)
            {
                return false;
            }

            // Check and verify the loop bounds of the induction variable
            var inductionVariable = loopInfo.InductionVariables[0];
            if (!inductionVariable.TryResolveBounds(out var bounds) ||
                inductionVariable.BreakBranch.NumTargets != 2)
            {
                return false;
            }

            // Try to compute a constant (compile-time known) trip count
            var tripCount = bounds.TryGetTripCount(out var intBounds);
            if (!tripCount.HasValue ||
                !intBounds.update.HasValue ||
                bounds.UpdateOperation.Kind != BinaryArithmeticKind.Add &&
                bounds.UpdateOperation.Kind != BinaryArithmeticKind.Sub)
            {
                return false;
            }

            // If trip count is 0, leave out loop completely
            if (tripCount.Value == 0)
                return true;

            // Compute the unroll factor and the number of iterations to use
            var (unrolls, iterations) = ComputeUnrollFactor(
                tripCount.Value,
                maxUnrollFactor);
            // Skip loops that cannot be properly unrolled
            if (unrolls < 2 && iterations > 1)
                return false;

            UnrollLoop(
                builder,
                loopInfo,
                inductionVariable,
                bounds,
                unrolls,
                iterations,
                intBounds.update.Value);
            return true;
        }

        /// <summary>
        /// Computes the unroll factor and the number of iterations.
        /// </summary>
        private static (int unrolls, int iterations) ComputeUnrollFactor(
            int tripCount,
            int maxUnrollFactor)
        {
            // TODO: improve this heuristic and its computation :)

            // Fully unroll small loops
            if (tripCount <= maxUnrollFactor)
                return (tripCount, 1);

            // Try to find an appropriate divisor
            for (int unrolls = maxUnrollFactor; unrolls > 1; unrolls >>= 1)
            {
                if (tripCount % unrolls > 0)
                    continue;

                // Compute the number of remaining iterations
                return (unrolls, tripCount / unrolls);
            }

            return (1, tripCount);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new loop unrolling transformation using the default maximum
        /// unroll factor.
        /// </summary>
        public LoopUnrolling()
            : this(DefaultMaxUnrollFactor)
        { }

        /// <summary>
        /// Constructs a new loop unrolling transformation.
        /// </summary>
        /// <param name="maxUnrollFactor">The maximum unroll factor.</param>
        public LoopUnrolling(int maxUnrollFactor)
        {
            if (maxUnrollFactor < 1)
                throw new ArgumentOutOfRangeException(nameof(maxUnrollFactor));
            MaxUnrollFactor = maxUnrollFactor;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the maximum unrolling factor to use.
        /// </summary>
        public int MaxUnrollFactor { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the loop unrolling transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var cfg = builder.SourceBlocks.CreateCFG();
            var loops = cfg.CreateLoops();
            var loopInfos = loops.CreateLoopInfos();

            // We change the control-flow structure during the transformation but
            // need to get information about previous predecessors and successors
            builder.AcceptControlFlowUpdates(accept: true);

            return loops.ProcessLoops(new LoopProcessor(
                loopInfos,
                builder,
                MaxUnrollFactor)).Applied;
        }

        #endregion
    }
}
