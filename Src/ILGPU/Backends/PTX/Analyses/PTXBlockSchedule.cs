﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXBlockSchedule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System.Collections.Immutable;

namespace ILGPU.Backends.PTX.Analyses
{
    /// <summary>
    /// Represents a PTX-specific block schedule.
    /// </summary>
    public abstract class PTXBlockSchedule
    {
        #region Instance

        /// <summary>
        /// Constructs a new PTX block schedule.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="blocks">The underlying block collection.</param>
        protected PTXBlockSchedule(
            BasicBlock entryBlock,
            ImmutableArray<BasicBlock> blocks)
        {
            EntryBlock = entryBlock;
            Blocks = blocks;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        /// <summary>
        /// Returns all blocks.
        /// </summary>
        public ImmutableArray<BasicBlock> Blocks { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new phi bindings mapping.
        /// </summary>
        /// <typeparam name="TAllocator">The custom allocator type.</typeparam>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>The created phi bindings.</returns>
        public abstract PhiBindings ComputePhiBindings<TAllocator>(TAllocator allocator)
            where TAllocator : IPhiBindingAllocator;

        /// <summary>
        /// Returns true if the given <paramref name="successor"/> is an implicit
        /// successor of the <paramref name="source"/> block.
        /// </summary>
        /// <param name="source">The source block.</param>
        /// <param name="successor">The target successor to jump to.</param>
        /// <returns>True, if the given successor in an implicit branch target.</returns>
        public abstract bool IsImplicitSuccessor(
            BasicBlock source,
            BasicBlock successor);

        /// <summary>
        /// Returns true if the given block needs an explicit branch target.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block needs an explicit branch target.</returns>
        public abstract bool NeedBranchTarget(BasicBlock block);

        /// <summary>
        /// Returns an enumerator to iterate over all blocks in the underlying
        /// collection.
        /// </summary>
        public ImmutableArray<BasicBlock>.Enumerator GetEnumerator() =>
            Blocks.GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Represents a PTX-specific block schedule.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public abstract class PTXBlockSchedule<TOrder, TDirection> : PTXBlockSchedule
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Instance

        /// <summary>
        /// Constructs a new PTX block schedule.
        /// </summary>
        /// <param name="blocks">The underlying block collection.</param>
        protected PTXBlockSchedule(in BasicBlockCollection<TOrder, TDirection> blocks)
            : base(blocks.EntryBlock, blocks.ToImmutableArray())
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public BasicBlockCollection<TOrder, TDirection> BasicBlockCollection =>
            new BasicBlockCollection<TOrder, TDirection>(EntryBlock, Blocks);

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new phi bindings mapping.
        /// </summary>
        /// <typeparam name="TAllocator">The custom allocator type.</typeparam>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>The created phi bindings.</returns>
        public override PhiBindings ComputePhiBindings<TAllocator>(
            TAllocator allocator) =>
            PhiBindings.Create(BasicBlockCollection, allocator);

        #endregion
    }

    /// <summary>
    /// Extensions methods for the <see cref="PTXBlockSchedule"/> class.
    /// </summary>
    public static partial class PTXBlockScheduleExtensions { }
}
