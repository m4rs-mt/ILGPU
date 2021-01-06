// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: BasicBlockCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// An abstract block collection with a particular control-flow direction.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public interface IBasicBlockCollection<TDirection> :
        IControlFlowAnalysisSource<TDirection>
        where TDirection : IControlFlowDirection
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
    public readonly struct BasicBlockCollection<TOrder, TDirection> :
        IBasicBlockCollection<TDirection>,
        IReadOnlyCollection<BasicBlock>,
        IControlFlowAnalysisSource<TDirection>,
        IDumpable
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// Enumerates all basic blocks in the underlying default order.
        /// </summary>
        public struct Enumerator : IEnumerator<BasicBlock>
        {
            private static TOrder GetOrder() => default;

            private ImmutableArray<BasicBlock> blocks;
            private TraversalEnumerationState state;

            internal Enumerator(ImmutableArray<BasicBlock> blockArray)
            {
                blocks = blockArray;
                state = GetOrder().Init(blockArray);
            }

            /// <summary>
            /// Returns the current basic block.
            /// </summary>
            public BasicBlock Current => blocks[state.Index];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => GetOrder().MoveNext(blocks, ref state);

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// An abstract view on all values.
        /// </summary>
        public readonly struct ValueCollection : IEnumerable<BasicBlock.ValueEntry>
        {
            #region Nested Types

            /// <summary>
            /// Enumerates all nodes in all blocks.
            /// </summary>
            public struct Enumerator : IEnumerator<BasicBlock.ValueEntry>
            {
                private BasicBlockCollection<TOrder, TDirection>.Enumerator
                    blockEnumerator;
                private BasicBlock.Enumerator valueEnumerator;

                /// <summary>
                /// Constructs a new basic block enumerator.
                /// </summary>
                /// <param name="blocks">The parent blocks.</param>
                internal Enumerator(ImmutableArray<BasicBlock> blocks)
                {
                    blockEnumerator =
                        new BasicBlockCollection<TOrder, TDirection>.Enumerator(
                            blocks);

                    // There must be at least a single block
                    blockEnumerator.MoveNext();
                    valueEnumerator = blockEnumerator.Current.GetEnumerator();
                }

                /// <summary>
                /// Returns the current value and its parent basic block.
                /// </summary>
                public BasicBlock.ValueEntry Current => valueEnumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                void IDisposable.Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    while (true)
                    {
                        if (valueEnumerator.MoveNext())
                            return true;

                        // Try to move to the next function
                        if (!blockEnumerator.MoveNext())
                            return false;

                        valueEnumerator = blockEnumerator.Current.GetEnumerator();
                    }
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            /// <summary>
            /// The list of all blocks.
            /// </summary>
            private readonly ImmutableArray<BasicBlock> blocks;

            /// <summary>
            /// Constructs a new value collection.
            /// </summary>
            /// <param name="blockCollection">The parent blocks.</param>
            internal ValueCollection(
                in BasicBlockCollection<TOrder, TDirection> blockCollection)
            {
                blocks = blockCollection.blocks;
            }

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns a value enumerator.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(blocks);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<BasicBlock.ValueEntry>
                IEnumerable<BasicBlock.ValueEntry>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        #endregion

        #region Instance

        private readonly ImmutableArray<BasicBlock> blocks;

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
            blocks = blockReferences;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public readonly Method Method => EntryBlock.Method;

        /// <summary>
        /// Returns the parent base context.
        /// </summary>
        public readonly IRBaseContext BaseContext => Method.BaseContext;

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        /// <summary>
        /// Returns the number of blocks.
        /// </summary>
        public readonly int Count => blocks.Length;

        /// <summary>
        /// Returns an abstract view on all values.
        /// </summary>
        public readonly ValueCollection Values => new ValueCollection(this);

        #endregion

        #region Methods

        /// <summary>
        /// Asserts that there is a unique exit block.
        /// </summary>
        [Conditional("DEBUG")]
        public void AssertUniqueExitBlock()
        {
            BasicBlock exitBlock = null;

            // Traverse all blocks to find a block without a successor
            foreach (var block in this)
            {
                if (block.GetSuccessors<TDirection>().Length < 1)
                {
                    EntryBlock.Assert(exitBlock is null);
                    exitBlock = block;
                }
            }
            EntryBlock.Assert(exitBlock != null);
        }

        /// <summary>
        /// Computes the exit block.
        /// </summary>
        /// <returns>The exit block.</returns>
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
            EntryBlock.Assert(false);
            return null;
        }

        /// <summary>
        /// Executes the given visitor for each terminator is this collection.
        /// </summary>
        /// <typeparam name="TTerminatorValue">The terminator value to match.</typeparam>
        /// <param name="visitor">The visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ForEachTerminator<TTerminatorValue>(
            BasicBlock.ValueVisitor<TTerminatorValue> visitor)
            where TTerminatorValue : TerminatorValue
        {
            foreach (var block in this)
            {
                if (block.Terminator.Resolve() is TTerminatorValue terminator)
                    visitor(terminator);
            }
        }

        /// <summary>
        /// Executes the given visitor for each value in this collection.
        /// </summary>
        /// <typeparam name="TValue">The value to match.</typeparam>
        /// <param name="visitor">The visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ForEachValue<TValue>(
            BasicBlock.ValueVisitor<TValue> visitor)
            where TValue : Value
        {
            foreach (Value value in Values)
            {
                if (value is TValue matchedValue)
                    visitor(matchedValue);
            }
        }

        /// <summary>
        /// Returns the underlying immutable block array.
        /// </summary>
        /// <returns>The underlying block array.</returns>
        public readonly ImmutableArray<BasicBlock> AsImmutable() => blocks;

        /// <summary>
        /// Converts this collection into a hash set.
        /// </summary>
        /// <returns>The created set.</returns>
        public readonly HashSet<BasicBlock> ToSet() =>
            ToSet(new InlineList.TruePredicate<BasicBlock>());

        /// <summary>
        /// Converts this collection into a hash set that contains all elements for
        /// which the given predicate evaluates to true.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="predicate">The predicate instance.</param>
        /// <returns>The created set.</returns>
        public readonly HashSet<BasicBlock> ToSet<TPredicate>(TPredicate predicate)
            where TPredicate : InlineList.IPredicate<BasicBlock>
        {
            var result = new HashSet<BasicBlock>();
            foreach (var block in this)
            {
                if (predicate.Apply(block))
                    result.Add(block);
            }
            return result;
        }

        /// <summary>
        /// Changes the order of this collection.
        /// </summary>
        /// <typeparam name="TOtherOrder">The collection order.</typeparam>
        /// <returns>The newly ordered collection.</returns>
        public readonly BasicBlockCollection<TOtherOrder, TDirection>
            AsOrder<TOtherOrder>()
            where TOtherOrder :
                struct,
                ITraversalOrder,
                ICompatibleTraversalOrder<TOrder> =>
            new BasicBlockCollection<TOtherOrder, TDirection>(EntryBlock, blocks);

        /// <summary>
        /// Changes the direction of this collection.
        /// </summary>
        /// <typeparam name="TOtherDirection">The other direction.</typeparam>
        /// <returns>The newly ordered collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly BasicBlockCollection<TOrder, TOtherDirection>
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
        public readonly BasicBlockCollection<TOtherOrder, TOtherDirection>
            ChangeOrder<
            TOtherOrder,
            TOtherDirection>()
            where TOtherOrder : struct, ITraversalOrder
            where TOtherDirection : struct, IControlFlowDirection =>
            Traverse<
                TOtherOrder,
                TOtherDirection,
                BasicBlock.SuccessorsProvider<TOtherDirection>>(default);

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
        public readonly BasicBlockCollection<TOtherOrder, TOtherDirection>
            Traverse<
            TOtherOrder,
            TOtherDirection,
            TSuccessorProvider>(TSuccessorProvider successorProvider)
            where TOtherOrder : struct, ITraversalOrder
            where TOtherDirection : struct, IControlFlowDirection
            where TSuccessorProvider : ITraversalSuccessorsProvider<TOtherDirection>
        {
            // Determine the new entry block
            TOtherDirection direction = default;
            var newEntryBlock = direction.GetEntryBlock<
                BasicBlockCollection<TOrder, TDirection>,
                TDirection>(this);

            // Compute new block order
            var newBlocks = ImmutableArray.CreateBuilder<BasicBlock>(Count);
            TOtherOrder otherOrder = default;
            var visitor = new TraversalCollectionVisitor<
                ImmutableArray<BasicBlock>.Builder>(newBlocks);
            otherOrder.Traverse<
                TraversalCollectionVisitor<ImmutableArray<BasicBlock>.Builder>,
                TSuccessorProvider,
                TOtherDirection>(
                newEntryBlock,
                ref visitor,
                successorProvider);

            // Return updated block collection
            return new BasicBlockCollection<TOtherOrder, TOtherDirection>(
                newEntryBlock,
                newBlocks.ToImmutable());
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Constructs a new block set.
        /// </summary>
        /// <returns>The created block set.</returns>
        public readonly BasicBlockSet CreateSet() => BasicBlockSet.Create(this);

        /// <summary>
        /// Constructs a new block set list.
        /// </summary>
        /// <returns>The created block set list.</returns>
        public readonly BasicBlockSetList CreateSetList() =>
            BasicBlockSetList.Create(this);

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The created block map.</returns>
        public readonly BasicBlockMap<T> CreateMap<T>() =>
            BasicBlockMap<T>.Create(this);

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="valueProvider">The initial value provider.</param>
        /// <returns>The created block map.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly BasicBlockMap<T> CreateMap<T>(
            IBasicBlockMapValueProvider<T> valueProvider)
        {
            var mapping = CreateMap<T>();
            int blockIndex = 0;
            foreach (var block in this)
            {
                var value = valueProvider.GetValue(block, blockIndex++);
                mapping.Add(block, value);
            }

            return mapping;
        }

        #endregion

        #region IDumpable

        /// <summary>
        /// Dumps all blocks in this collection to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public void Dump(TextWriter textWriter)
        {
            foreach (var block in this)
                block.Dump(textWriter);
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all attached blocks.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(blocks);

        /// <summary>
        /// Returns an enumerator to enumerator all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() =>
            GetEnumerator();

        /// <summary>
        /// Returns an enumerator to enumerator all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
