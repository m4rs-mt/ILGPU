// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: OptimizedPTXBlockSchedule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.PTX.Analyses
{
    /// <summary>
    /// Represents a optimized PTX-specific block schedule to place blocks.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public sealed class OptimizedPTXBlockSchedule<TOrder, TDirection> :
        PTXBlockSchedule<TOrder, TDirection>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// A specific successor provider that inverts the successors of all
        /// <see cref="IfBranch"/> terminators.
        /// </summary>
        private readonly struct SuccessorProvider :
            ITraversalSuccessorsProvider<Forwards>
        {
            /// <summary>
            /// Returns all successors in the default order except for
            /// <see cref="IfBranch"/> terminators. The successors of these terminators
            /// will be reversed to invert all if branch targets.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<BasicBlock> GetSuccessors(BasicBlock basicBlock)
            {
                var successors = basicBlock.Successors;
                if (basicBlock.Terminator is IfBranch ifBranch && ifBranch.IsInverted)
                {
                    var tempList = successors.ToInlineList();
                    tempList.Reverse();
                    successors = tempList;
                }

                return successors;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX block schedule.
        /// </summary>
        /// <param name="blocks">The underlying block collection.</param>
        internal OptimizedPTXBlockSchedule(
            in BasicBlockCollection<TOrder, TDirection> blocks)
            : base(blocks)
        {
            BlockIndices = blocks.CreateMap(new BasicBlockMapTraversalIndexProvider());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the block-traversal indices.
        /// </summary>
        public BasicBlockMap<int> BlockIndices { get; }

        /// <summary>
        /// Maps the given block to its traversal index.
        /// </summary>
        /// <param name="block">The block to map to its traversal index.</param>
        /// <returns>The associated traversal index.</returns>
        public int this[BasicBlock block] => BlockIndices[block];

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given <paramref name="successor"/> is an implicit
        /// successor of the <paramref name="source"/> block.
        /// </summary>
        /// <param name="source">The source block.</param>
        /// <param name="successor">The target successor to jump to.</param>
        /// <returns>True, if the given successor in an implicit branch target.</returns>
        public override bool IsImplicitSuccessor(
            BasicBlock source,
            BasicBlock successor) =>
            this[source] + 1 == this[successor];

        /// <summary>
        /// Returns true if the given block needs an explicit branch target.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block needs an explicit branch target.</returns>
        public override bool NeedBranchTarget(BasicBlock block)
        {
            // If there is more than one predecessor
            if (block.Predecessors.Length > 1)
                return true;

            // If there is less than one predecessor
            if (block.Predecessors.Length < 1)
                return false;

            // If there is exactly one predecessor, we have to check whether this block
            // can be reached via an implicit successor branch
            var pred = block.Predecessors[0];
            if (pred.Terminator is IfBranch ifBranch)
            {
                var (trueTarget, _) = ifBranch.NotInvertedBranchTargets;
                return block != trueTarget || !IsImplicitSuccessor(pred, block);
            }

            // Ensure that we are not removing labels from switch-based branch targets
            return !(pred.Terminator is UnconditionalBranch);
        }

        #endregion
    }

    public partial class PTXBlockScheduleExtensions
    {
        #region Nested Types

        /// <summary>
        /// A specific successor provider that inverts the successors of all
        /// <see cref="IfBranch"/> terminators.
        /// </summary>
        private readonly struct SuccessorProvider :
            ITraversalSuccessorsProvider<Forwards>
        {
            /// <summary>
            /// Returns all successors in the default order except for
            /// <see cref="IfBranch"/> terminators. The successors of these terminators
            /// will be reversed to invert all if branch targets.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<BasicBlock> GetSuccessors(BasicBlock basicBlock)
            {
                var successors = basicBlock.Successors;
                if (basicBlock.Terminator is IfBranch ifBranch && ifBranch.IsInverted)
                {
                    var tempList = successors.ToInlineList();
                    tempList.Reverse();
                    successors = tempList;
                }

                return successors;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new optimized block schedule using the given blocks.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The input blocks.</param>
        /// <returns>The created block schedule.</returns>
        public static PTXBlockSchedule CreatePTXScheduleToOptimize<TOrder, TDirection>(
            this BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            CreateSchedule(blocks.ChangeOrder<PreOrder, Forwards>());

        /// <summary>
        /// Creates a schedule from an already existing schedule.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The input blocks.</param>
        /// <returns>The created block schedule.</returns>
        public static PTXBlockSchedule CreateOptimizedPTXSchedule<TOrder, TDirection>(
            this BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            CreateSchedule(
                blocks.Traverse<PreOrder, Forwards, SuccessorProvider>(default));

        /// <summary>
        /// Creates an optimized PTX block schedule.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The input blocks.</param>
        /// <returns>The created block schedule.</returns>
        private static PTXBlockSchedule CreateSchedule<TOrder, TDirection>(
            in BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            new OptimizedPTXBlockSchedule<TOrder, TDirection>(blocks);

        #endregion
    }
}
