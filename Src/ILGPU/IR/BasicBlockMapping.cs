// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: BasicBlockMapping.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.TraversalOrders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a set of basic blocks.
    /// </summary>
    public struct BasicBlockSet
    {
        #region Static

        /// <summary>
        /// Creates a new block set.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSet Create<TOrder>(
            in BasicBlockCollection<TOrder> blocks)
            where TOrder : struct, ITraversalOrder =>
            new BasicBlockSet(blocks.EntryBlock, blocks.Count);

        #endregion

        #region Instance

        private ulong[] visited;

        /// <summary>
        /// Constructs a new block set.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="numBlocks">The initial number of blocks.</param>
        internal BasicBlockSet(BasicBlock entryBlock, int numBlocks)
        {
            EntryBlock = entryBlock;
            visited = new ulong[IntrinsicMath.DivRoundUp(numBlocks, 64)];
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
        /// Returns true if this set has at least one element.
        /// </summary>
        public readonly bool HasAny
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                foreach (var entry in visited)
                {
                    if (entry != 0UL)
                        return true;
                }
                return false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given block to this set.
        /// </summary>
        /// <param name="block">The block to add.</param>
        /// <returns>True, if the block has been added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(BasicBlock block)
        {
            block.Assert(block.Method == Method && block.BlockIndex >= 0);
            int index = block.BlockIndex;
            int setIndex = index / 32;
            ulong bitMask = 1UL << (index % 32);

            if (setIndex >= visited.Length)
                Array.Resize(ref visited, Math.Max(setIndex + 1, visited.Length * 2));
            ref var entry = ref visited[setIndex];

            bool added = (entry & bitMask) == 0;
            entry |= bitMask;
            return added;
        }

        /// <summary>
        /// Returns true if the given block is contained in this set.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block is contained in this set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(BasicBlock block)
        {
            block.Assert(block.Method == Method && block.BlockIndex >= 0);
            int index = block.BlockIndex;
            int setIndex = index / 32;
            ulong bitMask = 1UL << (index % 32);
            return setIndex < visited.Length && (visited[setIndex] & bitMask) != 0;
        }

        #endregion
    }

    /// <summary>
    /// Represents a set list of basic blocks.
    /// </summary>
    [SuppressMessage(
        "Naming",
        "CA1710:Identifiers should have correct suffix",
        Justification = "The collection ends in list")]
    public struct BasicBlockSetList : IReadOnlyList<BasicBlock>
    {
        #region Static

        /// <summary>
        /// Creates a new block set.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSetList Create<TOrder>(
            in BasicBlockCollection<TOrder> blocks)
            where TOrder : struct, ITraversalOrder =>
            new BasicBlockSetList(
                BasicBlockSet.Create(blocks),
                blocks.Count);

        #endregion

        #region Instance

        private readonly List<BasicBlock> blocks;
        private BasicBlockSet set;

        /// <summary>
        /// Constructs a new block set.
        /// </summary>
        /// <param name="blockSet">The block set.</param>
        /// <param name="numBlocks">The initial number of blocks.</param>
        private BasicBlockSetList(in BasicBlockSet blockSet, int numBlocks)
        {
            set = blockSet;
            blocks = new List<BasicBlock>(numBlocks);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public readonly Method Method => set.Method;

        /// <summary>
        /// Returns the number of elements in this set.
        /// </summary>
        public readonly int Count => blocks.Count;

        /// <summary>
        /// Returns the i-th basic block.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <returns></returns>
        public readonly BasicBlock this[int index] => blocks[index];

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given block to this set.
        /// </summary>
        /// <param name="block">The block to add.</param>
        /// <returns>True, if the block has been added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(BasicBlock block)
        {
            bool result = set.Add(block);
            if (result)
                blocks.Add(block);
            return result;
        }

        /// <summary>
        /// Returns true if the given block is contained in this set.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block is contained in this set.</returns>
        public readonly bool Contains(BasicBlock block) => set.Contains(block);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all attached blocks.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public readonly List<BasicBlock>.Enumerator GetEnumerator() =>
            blocks.GetEnumerator();

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

    /// <summary>
    /// A mapping of basic block to values.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    [SuppressMessage(
        "Naming",
        "CA1710:Identifiers should have correct suffix",
        Justification = "The collection ends in map")]
    public struct BasicBlockMap<T> : IReadOnlyCollection<(BasicBlock, T)>
    {
        #region Nested Types

        /// <summary>
        /// Enumerates all block value pairs.
        /// </summary>
        public struct Enumerator : IEnumerator<(BasicBlock, T)>
        {
            private readonly (BasicBlock, T)[] values;
            private int index;
            private int elementIndex;

            internal Enumerator(in BasicBlockMap<T> map)
            {
                values = map.values;
                index = -1;
                elementIndex = -1;
                Count = map.Count;
            }

            /// <summary>
            /// The number of elements in the collection.
            /// </summary>
            public int Count { get; }

            /// <summary>
            /// Returns the current basic block.
            /// </summary>
            public (BasicBlock, T) Current => values[index];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (++elementIndex >= Count)
                    return false;

                for (++index; index < values.Length; ++index)
                {
                    if (values[index].Item1 != null)
                        return true;
                }
                return false;
            }

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        #region Static

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block map.</returns>
        public static BasicBlockMap<T> Create<TOrder>(
            in BasicBlockCollection<TOrder> blocks)
            where TOrder : struct, ITraversalOrder =>
            new BasicBlockMap<T>(blocks.EntryBlock, blocks.Count);

        #endregion

        #region Instance

        private (BasicBlock, T)[] values;

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="numBlocks">The number of blocks.</param>
        private BasicBlockMap(BasicBlock entryBlock, int numBlocks)
        {
            EntryBlock = entryBlock;
            values = new (BasicBlock, T)[numBlocks];
            Count = 0;
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
        /// Returns the number of elements in this set.
        /// </summary>
        public int Count { readonly get; private set; }

        /// <summary>
        /// Returns the value associated to the given block.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>The value associated to the given block.</returns>
        public T this[BasicBlock block]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int index = block.BlockIndex;
                EntryBlock.Assert(index < values.Length);
                var (entryBlock, value) = values[index];
                EntryBlock.AssertNotNull(entryBlock);
                return value;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                int index = UpdateValueMap(block);
                if (values[index].Item1 is null)
                    ++Count;
                values[index] = (block, value);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the value map to store the given block.
        /// </summary>
        /// <param name="block">The block to store in this map.</param>
        /// <returns>The block index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int UpdateValueMap(BasicBlock block)
        {
            block.Assert(block.Method == Method && block.BlockIndex >= 0);
            int index = block.BlockIndex;
            if (index >= values.Length)
                Array.Resize(ref values, Math.Max(index + 1, values.Length * 2));
            return index;
        }

        /// <summary>
        /// Adds the given block to this set.
        /// </summary>
        /// <param name="block">The block to add.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(BasicBlock block, T value) =>
            Method.Assert(TryAdd(block, value));

        /// <summary>
        /// Adds the given block to this set.
        /// </summary>
        /// <param name="block">The block to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>True, if the block has been added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(BasicBlock block, T value)
        {
            int index = UpdateValueMap(block);
            ref var entry = ref values[index];
            if (entry.Item1 != null)
            {
                Method.Assert(entry.Item1 == block);
                return false;
            }

            ++Count;
            entry = (block, value);
            return true;
        }

        /// <summary>
        /// Tries to get a stored value for the given block.
        /// </summary>
        /// <param name="block">The block to lookup.</param>
        /// <param name="value">The stored value (if any).</param>
        /// <returns>True, if the block could be found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(BasicBlock block, out T value)
        {
            value = default;
            int index = block.BlockIndex;
            if (index >= values.Length)
                return false;
            BasicBlock entry;
            (entry, value) = values[index];
            return entry != null;
        }

        /// <summary>
        /// Returns true if the given block is contained in this set.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block is contained in this set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(BasicBlock block)
        {
            int index = block.BlockIndex;
            return index < values.Length && values[index].Item1 != null;
        }

        /// <summary>
        /// Remaps the current values to other values.
        /// </summary>
        /// <typeparam name="TOther">The other element type.</typeparam>
        /// <param name="valueProvider">The value provider to map the values.</param>
        /// <returns>The created block mapping.</returns>
        public BasicBlockMap<TOther> Remap<TOther>(Func<T, TOther> valueProvider)
        {
            var result = new BasicBlockMap<TOther>(EntryBlock, values.Length);
            for (int i = 0, e = values.Length; i < e; ++i)
            {
                var (block, value) = values[i];
                if (block is null)
                    continue;
                result.values[i] = (block, valueProvider(value));
            }
            return result;
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all attached blocks.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Returns an enumerator to enumerator all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<(BasicBlock, T)> IEnumerable<(BasicBlock, T)>.GetEnumerator() =>
            GetEnumerator();

        /// <summary>
        /// Returns an enumerator to enumerator all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Helper methods for basic block mappings.
    /// </summary>
    static class BasicBlockMapping
    {
        /// <summary>
        /// Constructs a new block set.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSet CreateSet<TOrder>(
            this BasicBlockCollection<TOrder> blocks)
            where TOrder : struct, ITraversalOrder =>
            BasicBlockSet.Create(blocks);

        /// <summary>
        /// Constructs a new block set.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSetList CreateSetList<TOrder>(
            this BasicBlockCollection<TOrder> blocks)
            where TOrder : struct, ITraversalOrder =>
            BasicBlockSetList.Create(blocks);

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block map.</returns>
        public static BasicBlockMap<T> CreateMap<TOrder, T>(
            this BasicBlockCollection<TOrder> blocks)
            where TOrder : struct, ITraversalOrder =>
            BasicBlockMap<T>.Create(blocks);

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <param name="valueProvider">The initial value provider.</param>
        /// <returns>The created block map.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BasicBlockMap<T> CreateMap<TOrder, T>(
            this BasicBlockCollection<TOrder> blocks,
            Func<BasicBlock, int, T> valueProvider)
            where TOrder : struct, ITraversalOrder
        {
            var mapping = blocks.CreateMap<TOrder, T>();
            int blockIndex = 0;
            foreach (var block in blocks)
            {
                if (mapping.Contains(block))
                    continue;
                mapping.Add(block, valueProvider(block, blockIndex++));
            }
            return mapping;
        }
    }
}
