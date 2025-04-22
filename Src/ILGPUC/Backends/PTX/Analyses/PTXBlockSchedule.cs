// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXBlockSchedule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses.ControlFlowDirection;
using ILGPUC.IR.Analyses.TraversalOrders;
using ILGPUC.IR.Values;
using System;
using System.Collections.Immutable;

namespace ILGPUC.Backends.PTX.Analyses;

/// <summary>
/// Represents a PTX-specific block schedule.
/// </summary>
/// <param name="entryBlock">The entry block.</param>
/// <param name="blocks">The underlying block collection.</param>
abstract class PTXBlockSchedule(
    BasicBlock entryBlock,
    ImmutableArray<BasicBlock> blocks)
{
    #region Properties

    /// <summary>
    /// Returns the entry block.
    /// </summary>
    public BasicBlock EntryBlock { get; } = entryBlock;

    /// <summary>
    /// Returns all blocks.
    /// </summary>
    public ImmutableArray<BasicBlock> Blocks { get; } = blocks;

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new phi bindings mapping.
    /// </summary>
    /// <param name="allocator">The allocator to use.</param>
    /// <returns>The created phi bindings.</returns>
    public abstract PhiBindings ComputePhiBindings(
        Action<BasicBlock, PhiValue> allocator);

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
/// <param name="blocks">The underlying block collection.</param>
abstract class PTXBlockSchedule<TOrder, TDirection>(
    in BasicBlockCollection<TOrder, TDirection> blocks) :
    PTXBlockSchedule(blocks.EntryBlock, [.. blocks])
    where TOrder : struct, ITraversalOrder
    where TDirection : struct, IControlFlowDirection
{
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
    /// <param name="allocator">The allocator to use.</param>
    /// <returns>The created phi bindings.</returns>
    public override PhiBindings ComputePhiBindings(
        Action<BasicBlock, PhiValue> allocator) =>
        PhiBindings.Create(BasicBlockCollection, allocator);

    #endregion
}

/// <summary>
/// Extensions methods for the <see cref="PTXBlockSchedule"/> class.
/// </summary>
static partial class PTXBlockScheduleExtensions { }
