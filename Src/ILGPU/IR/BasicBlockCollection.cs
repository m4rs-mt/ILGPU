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

using ILGPU.IR.Analyses.Duplicates;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// An abstract collection of basic blocks following a particular order.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    public interface IBasicBlockCollection<TOrder>
        where TOrder : struct, ITraversalOrder
    {
        /// <summary>
        /// Computes an updated block order.
        /// </summary>
        /// <typeparam name="TOtherOrder">The collection order.</typeparam>
        /// <typeparam name="TOtherOrderProvider">
        /// The collection order provider.
        /// </typeparam>
        /// <typeparam name="TDuplicates">The duplicate specification.</typeparam>
        /// <returns>The newly ordered collection.</returns>
        BasicBlockCollection<TOtherOrder> ComputeBlockOrder<
            TOtherOrder,
            TOtherOrderProvider,
            TDuplicates>()
            where TOtherOrder : struct, ITraversalOrderView<TOtherOrderProvider>
            where TOtherOrderProvider : struct, ITraversalOrderProvider
            where TDuplicates : struct, IDuplicates<BasicBlock>;
    }

    /// <summary>
    /// A collection of basic blocks following a particular order.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    public readonly struct BasicBlockCollection<TOrder> : IReadOnlyList<BasicBlock>
        where TOrder : struct, ITraversalOrder
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
                [SuppressMessage(
                    "Style",
                    "IDE0044:Add readonly modifier",
                    Justification = "This instance variable will be modified")]
                private BasicBlockCollection<TOrder>.Enumerator blockEnumerator;
                private BasicBlock.Enumerator valueEnumerator;

                /// <summary>
                /// Constructs a new basic block enumerator.
                /// </summary>
                /// <param name="blocks">The parent blocks.</param>
                internal Enumerator(ImmutableArray<BasicBlock> blocks)
                {
                    blockEnumerator = new BasicBlockCollection<TOrder>.Enumerator(
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
            internal ValueCollection(in BasicBlockCollection<TOrder> blockCollection)
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
        /// Returns the entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        /// <summary>
        /// Returns the number of attached parameters.
        /// </summary>
        public readonly int Count => blocks.Length;

        /// <summary>
        /// Returns an abstract view on all values.
        /// </summary>
        public readonly ValueCollection Values => new ValueCollection(this);

        /// <summary>
        /// Returns the i-th block.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <returns>The resolved block.</returns>
        BasicBlock IReadOnlyList<BasicBlock>.this[int index] => blocks[index];

        #endregion

        #region Methods

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
        public readonly HashSet<BasicBlock> ToSet()
        {
            var result = new HashSet<BasicBlock>();
            foreach (var block in this)
                result.Add(block);
            return result;
        }

        /// <summary>
        /// Changes the order of this collection.
        /// </summary>
        /// <typeparam name="TOtherOrder">The collection order.</typeparam>
        /// <returns>The newly ordered collection.</returns>
        public readonly BasicBlockCollection<TOtherOrder> AsOrder<TOtherOrder>()
            where TOtherOrder :
                struct,
                ITraversalOrder,
                ICompatibleTraversalView<TOrder> =>
            new BasicBlockCollection<TOtherOrder>(EntryBlock, blocks);

        /// <summary>
        /// Changes the order of this collection.
        /// </summary>
        /// <typeparam name="TOtherOrder">The collection order.</typeparam>
        /// <typeparam name="TOtherOrderProvider">
        /// The collection order provider.
        /// </typeparam>
        /// <typeparam name="TDuplicates">The duplicate specification.</typeparam>
        /// <returns>The newly ordered collection.</returns>
        public readonly BasicBlockCollection<TOtherOrder> ComputeBlockOrder<
            TOtherOrder,
            TOtherOrderProvider,
            TDuplicates>()
            where TOtherOrder : struct, ITraversalOrderView<TOtherOrderProvider>
            where TOtherOrderProvider : struct, ITraversalOrderProvider
            where TDuplicates : struct, IDuplicates<BasicBlock>
        {
            if (EntryBlock is null)
                throw new InvalidOperationException();

            var newBlocks = ImmutableArray.CreateBuilder<BasicBlock>(Count);
            TOtherOrderProvider orderProvider = default;
            orderProvider.Traverse<
                ImmutableArray<BasicBlock>.Builder,
                TDuplicates>(EntryBlock, newBlocks);
            return new BasicBlockCollection<TOtherOrder>(
                EntryBlock,
                newBlocks.ToImmutable());
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
