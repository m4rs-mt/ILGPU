// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlockCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// An abstract block collection with a particular control-flow direction.
/// </summary>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
interface IBasicBlockCollection<TDirection> :
    IControlFlowAnalysisSource<TDirection>
    where TDirection : struct, IControlFlowDirection
{
    /// <summary>
    /// Returns the number of blocks.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// A collection of basic blocks following a particular order.
/// </summary>
/// <typeparam name="TOrder">The current order.</typeparam>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
readonly struct BasicBlockCollection<TOrder, TDirection> :
    IBasicBlockCollection<TDirection>,
    IControlFlowAnalysisSource<TDirection>,
    IGenerationObject,
    IDumpable
    where TOrder : struct, ITraversalOrder<BasicBlock>
    where TDirection : struct, IControlFlowDirection
{
    #region Nested Types

    /// <summary>
    /// Enumerates all basic blocks in the underlying default order.
    /// </summary>
    internal struct Enumerator(ImmutableArray<BasicBlock> blockArray)
    {
        private TraversalEnumerationState _state = TOrder.Init(blockArray);

        /// <summary>
        /// Returns the current basic block.
        /// </summary>
        public readonly BasicBlock Current => blockArray[_state.Index];

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => TOrder.MoveNext(blockArray, ref _state);
    }

    #endregion

    #region Instance

    private readonly ImmutableArray<BasicBlock> _blocks;

    /// <summary>
    /// Constructs a new block collection.
    /// </summary>
    /// <param name="entryBlock">The entry block.</param>
    /// <param name="blockReferences">The source blocks.</param>
    public BasicBlockCollection(
        BasicBlock entryBlock,
        ImmutableArray<BasicBlock> blockReferences)
    {
        entryBlock.AssertNotNull(entryBlock);

        EntryBlock = entryBlock;
        _blocks = blockReferences;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the entry block.
    /// </summary>
    public BasicBlock EntryBlock { get; }

    /// <summary>
    /// Returns the parent method.
    /// </summary>
    public Method Method => EntryBlock.Method;

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation => EntryBlock.Generation;

    /// <summary>
    /// Returns the number of blocks.
    /// </summary>
    public int Count => _blocks.Length;

    /// <summary>
    /// Returns the i-th basic block in this collection.
    /// </summary>
    public BasicBlock this[int index] => _blocks[index];

    #endregion

    #region Methods

    /// <summary>
    /// Asserts that there is a unique exit block.
    /// </summary>
    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void AssertUniqueExitBlock()
    {
        BasicBlock? exitBlock = null;

        // Traverse all blocks to find a block without a successor
        foreach (var block in this)
        {
            if (block.GetSuccessors<TDirection>().Length < 1)
            {
                EntryBlock.Assert(exitBlock is null);
                exitBlock = block;
            }
        }
        EntryBlock.Assert(exitBlock is not null);
    }

    /// <summary>
    /// Computes the exit block.
    /// </summary>
    /// <returns>The exit block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public BasicBlock FindExitBlock()
    {
        AssertUniqueExitBlock();

        // Traverse all blocks to find a block without a successor
        foreach (var block in this)
        {
            if (block.GetSuccessors<TDirection>().Length < 1)
                return block;
        }

        // Unreachable
        throw EntryBlock.GetInvalidOperationException();
    }

    /// <summary>
    /// Executes the given visitor for each value in this collection.
    /// </summary>
    /// <typeparam name="TValue">The value to match.</typeparam>
    /// <param name="callback">The callback.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachValue<TValue>(Action<TValue> callback)
        where TValue : Value<Method>
    {
        ValueSet<Method, TValue>? set = null;
        ForEachValue(callback, ref set);
    }

    /// <summary>
    /// Executes the given visitor for each value in this collection.
    /// </summary>
    /// <typeparam name="TValue">The value to match.</typeparam>
    /// <param name="callback">The callback.</param>
    /// <param name="set">The value set to ensure values are visited once.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachValue<TValue>(
        Action<TValue> callback,
        ref ValueSet<Method, TValue>? set)
        where TValue : Value<Method>
    {
        foreach (var block in this)
            block.ForEachValue(callback, ref set);
    }

    /// <summary>
    /// Returns the underlying immutable block array.
    /// </summary>
    /// <returns>The underlying block array.</returns>
    public ImmutableArray<BasicBlock> AsImmutable() => _blocks;

    /// <summary>
    /// Returns underlying immutable block array as read only memory.
    /// </summary>
    /// <returns>The underlying block array as read only memory.</returns>
    public ReadOnlyMemory<BasicBlock> AsReadOnlyMemory() => _blocks.AsMemory();

    /// <summary>
    /// Converts this collection into a hash set.
    /// </summary>
    /// <returns>The created set.</returns>
    public ValueSet<Method, BasicBlock> ToSet() => ToSet(static _ => true);

    /// <summary>
    /// Converts this collection into a hash set that contains all elements for
    /// which the given predicate evaluates to true.
    /// </summary>
    /// <param name="predicate">The predicate instance.</param>
    /// <returns>The created set.</returns>
    public ValueSet<Method, BasicBlock> ToSet(Predicate<BasicBlock> predicate)
    {
        var result = CreateSet();
        foreach (var block in this)
            if (predicate(block)) result.Add(block);
        return result;
    }

    /// <summary>
    /// Changes the order of this collection.
    /// </summary>
    /// <typeparam name="TOtherOrder">The collection order.</typeparam>
    /// <returns>The newly ordered collection.</returns>
    public BasicBlockCollection<TOtherOrder, TDirection>
        AsOrder<TOtherOrder>()
        where TOtherOrder :
            struct,
            ITraversalOrder<BasicBlock>,
            ICompatibleTraversalOrder<BasicBlock, TOrder> =>
        new(EntryBlock, _blocks);

    /// <summary>
    /// Changes the direction of this collection.
    /// </summary>
    /// <typeparam name="TOtherDirection">The other direction.</typeparam>
    /// <returns>The newly ordered collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BasicBlockCollection<TOrder, TOtherDirection>
        ChangeDirection<TOtherDirection>()
        where TOtherDirection : struct, IControlFlowDirection =>
        ChangeOrder<TOrder, TOtherDirection>();

    /// <summary>
    /// Changes the order of this collection.
    /// </summary>
    /// <typeparam name="TOtherOrder">The collection order.</typeparam>
    /// <typeparam name="TOtherDirection">The control-flow direction.</typeparam>
    /// <remarks>
    /// Note that this function uses successor/predecessor links on all basic blocks.
    /// </remarks>
    /// <returns>The newly ordered collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BasicBlockCollection<TOtherOrder, TOtherDirection>
        ChangeOrder<
        TOtherOrder,
        TOtherDirection>()
        where TOtherOrder : struct, ITraversalOrder<BasicBlock>
        where TOtherDirection : struct, IControlFlowDirection =>
        Traverse<
            TOtherOrder,
            TOtherDirection,
            BasicBlock.SuccessorsProvider<TOtherDirection>>();

    /// <summary>
    /// Traverses this collection using the new order and direction.
    /// </summary>
    /// <typeparam name="TOtherOrder">The collection order.</typeparam>
    /// <typeparam name="TOtherDirection">The control-flow direction.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <remarks>
    /// Note that this function uses successor/predecessor links on all basic blocks.
    /// </remarks>
    /// <returns>The newly ordered collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public BasicBlockCollection<TOtherOrder, TOtherDirection>
        Traverse<
        TOtherOrder,
        TOtherDirection,
        TSuccessorProvider>()
        where TOtherOrder : struct, ITraversalOrder<BasicBlock>
        where TOtherDirection : struct, IControlFlowDirection
        where TSuccessorProvider :
            struct,
            ITraversalSuccessorsProvider<BasicBlock, TOtherDirection>
    {
        // Determine the new entry block using TOtherDirection direction, so that e.g.
        // ChangeDirection<Backwards>() correctly starts from the exit block.
        var newEntryBlock = TOtherDirection.GetEntryBlock<
            BasicBlockCollection<TOrder, TDirection>,
            TDirection>(this);

        // Compute new block order
        var newBlocks = ImmutableArray.CreateBuilder<BasicBlock>(Count);
        var visitor = new TraversalCollectionVisitor<
            BasicBlock,
            ImmutableArray<BasicBlock>.Builder>(newBlocks);
        TOrder.Traverse<
            ValueSet<Method, BasicBlock>,
            TraversalCollectionVisitor<BasicBlock, ImmutableArray<BasicBlock>.Builder>,
            TSuccessorProvider,
            TOtherDirection>(
                newEntryBlock,
                Method.CreateSet<BasicBlock>(),
                ref visitor);

        // Return updated block collection
        return new BasicBlockCollection<TOtherOrder, TOtherDirection>(
            newEntryBlock,
            newBlocks.ToImmutable());
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Constructs a new inline list of all blocks in the corresponding order.
    /// </summary>
    /// <returns>The created list of blocks.</returns>
    public InlineList<BasicBlock> ToList()
    {
        var result = InlineList<BasicBlock>.Create(Count);
        foreach (var block in this)
            result.Add(block);
        return result;
    }

    /// <summary>
    /// Constructs a new block set.
    /// </summary>
    /// <returns>The created block set.</returns>
    public ValueSet<Method, BasicBlock> CreateSet() => Method.CreateSet<BasicBlock>();

    /// <summary>
    /// Constructs a new block set list.
    /// </summary>
    /// <returns>The created block set list.</returns>
    public ValueSetList<Method, BasicBlock> CreateSetList() =>
        Method.CreateSetList<BasicBlock>();

    /// <summary>
    /// Constructs a new block map.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="provider">The initial value provider.</param>
    /// <returns>The created block map.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueMap<Method, BasicBlock, T> CreateMap<T>(
        Func<BasicBlock, int, T?>? provider = null)
    {
        var mapping = Method.CreateMap<BasicBlock, T>();
        if (provider is null) return mapping;

        int blockIndex = 0;
        foreach (var block in this)
        {
            var value = provider(block, blockIndex++);
            if (value is not null) mapping.Add(block, value);
        }

        return mapping;
    }

    /// <summary>
    /// Gathers all phi source blocks.
    /// </summary>
    /// <returns>All phi value source blocks.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueSet<Method, BasicBlock> ComputePhiSources()
    {
        var result = CreateSet();
        ForEachValue<PhiValue>(phiValue =>
        {
            foreach (var source in phiValue.Sources)
                result.Add(source);
        });
        return result;
    }

    /// <summary>
    /// Gathers all phis.
    /// </summary>
    /// <returns>All phi value source blocks.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueSet<Method, PhiValue> ComputePhis()
    {
        var result = Method.CreateSet<PhiValue>();
        ForEachValue<PhiValue>(phiValue => result.Add(phiValue));
        return result;
    }

    /// <summary>
    /// Dumps all blocks in this collection to the given text writer.
    /// </summary>
    /// <param name="textWriter">The text writer.</param>
    public void Dump(TextWriter textWriter)
    {
        foreach (var block in this)
            block.Dump(textWriter);
    }

    /// <summary>
    /// Returns an enumerator to enumerate all attached blocks.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public Enumerator GetEnumerator() => new(_blocks);

    #endregion
}
