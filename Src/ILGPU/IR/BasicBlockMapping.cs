// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlockMapping.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable CS0282 // There is no defined ordering between fields in
// multiple declarations of partial struct

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a set of basic blocks.
    /// </summary>
    public partial struct BasicBlockSet
    {
        #region Constants

        /// <summary>
        /// The number of elements per bucket.
        /// </summary>
        private const int NumElementsPerBucket = sizeof(ulong) * 8;

        /// <summary>
        /// The number of default elements.
        /// </summary>
        internal const int NumDefaultElements = NumElementsPerBucket * 4;

        #endregion

        #region Static

        /// <summary>
        /// Creates a new block set.
        /// </summary>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSet Create<TOrder, TDirection>(
            in BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            new BasicBlockSet(blocks.EntryBlock, blocks.Count);

        /// <summary>
        /// Creates a new block set.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSet Create(BasicBlock entryBlock) =>
            new BasicBlockSet(entryBlock, NumDefaultElements);

        /// <summary>
        /// Computes the bucket index and the bit mask.
        /// </summary>
        /// <param name="index">The block index.</param>
        /// <param name="bitMask">The resulting bit mask.</param>
        /// <returns>The bucket index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeBucketIndex(int index, out ulong bitMask)
        {
            bitMask = 1UL << (index % NumElementsPerBucket);
            return index / NumElementsPerBucket;
        }

        #endregion

        #region Instance

        private ulong[] visited;

        /// <summary>
        /// Constructs a new block set.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="numBlocks">The initial number of blocks.</param>
        internal BasicBlockSet(BasicBlock entryBlock, int numBlocks)
            : this()
        {
            EntryBlock = entryBlock;
            visited = new ulong[IntrinsicMath.DivRoundUp(numBlocks, 64)];

            InitBlockSet();
        }

        /// <summary>
        /// Initializes the debug part of this block set.
        /// </summary>
        partial void InitBlockSet();

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
        /// Asserts that the given block has been added to the set or not.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <param name="added">True, whether the block has been added.</param>
        readonly partial void AssertAdd(BasicBlock block, bool added);

        /// <summary>
        /// Adds the given block to this set.
        /// </summary>
        /// <param name="block">The block to add.</param>
        /// <returns>True, if the block has been added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(BasicBlock block)
        {
            block.Assert(block.Method == Method && block.BlockIndex >= 0);
            int bucketIndex = ComputeBucketIndex(block.BlockIndex, out ulong bitMask);
            if (bucketIndex >= visited.Length)
            {
                Array.Resize(
                    ref visited,
                    Math.Max(bucketIndex + 1, visited.Length * 2));
            }

            ref var entry = ref visited[bucketIndex];
            bool added = (entry & bitMask) == 0;
            entry |= bitMask;
            AssertAdd(block, added);
            return added;
        }

        /// <summary>
        /// Asserts that the given block is contained in the set or not.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <param name="contained">True, whether the block is contained.</param>
        readonly partial void AssertContained(BasicBlock block, bool contained);

        /// <summary>
        /// Returns true if the given block is contained in this set.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block is contained in this set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(BasicBlock block)
        {
            block.Assert(block.Method == Method && block.BlockIndex >= 0);
            int bucketIndex = ComputeBucketIndex(block.BlockIndex, out ulong bitMask);
            bool contained = bucketIndex < visited.Length &&
                (visited[bucketIndex] & bitMask) != 0;
            AssertContained(block, contained);
            return contained;
        }

        /// <summary>
        /// Asserts that the given block has been removed from the set or not.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <param name="removed">True, whether the block has been removed.</param>
        readonly partial void AssertRemoved(BasicBlock block, bool removed);

        /// <summary>
        /// Removes the given block from this set and returns true if the block has been
        /// removed.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <returns>True, whether the block has been removed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(BasicBlock block)
        {
            block.Assert(block.Method == Method && block.BlockIndex >= 0);
            int bucketIndex = ComputeBucketIndex(block.BlockIndex, out ulong bitMask);
            if (bucketIndex >= visited.Length)
            {
                AssertRemoved(block, false);
                return false;
            }

            ref var entry = ref visited[bucketIndex];
            bool removed = (entry & bitMask) != 0;
            entry &= ~bitMask;
            AssertRemoved(block, removed);
            return removed;
        }

        /// <summary>
        /// Asserts that the set has been cleared.
        /// </summary>
        readonly partial void AssertCleared();

        /// <summary>
        /// Removes all elements from this set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(visited, 0, visited.Length);
            AssertCleared();
        }

        #endregion
    }

    /// <summary>
    /// Represents a set list of basic blocks.
    /// </summary>
    public struct BasicBlockSetList : IReadOnlyList<BasicBlock>
    {
        #region Static

        /// <summary>
        /// Creates a new block set.
        /// </summary>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block set.</returns>
        public static BasicBlockSetList Create<TOrder, TDirection>(
            in BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
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
    /// A value provider for each block in a collection.
    /// </summary>
    /// <typeparam name="T">The map value type.</typeparam>
    public interface IBasicBlockMapValueProvider<T>
    {
        /// <summary>
        /// Extracts the value from the given block.
        /// </summary>
        /// <param name="block">The source block.</param>
        /// <param name="traversalIndex">The current traversal index.</param>
        /// <returns>The extracted value.</returns>
        T GetValue(BasicBlock block, int traversalIndex);
    }

    /// <summary>
    /// A map provider that returns the traversal index of each block.
    /// </summary>
    public readonly struct BasicBlockMapTraversalIndexProvider :
        IBasicBlockMapValueProvider<int>
    {
        /// <summary>
        /// Returns the value of <paramref name="traversalIndex"/>.
        /// </summary>
        public readonly int GetValue(BasicBlock block, int traversalIndex) =>
            traversalIndex;
    }

    /// <summary>
    /// A mapping of basic block to values.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of the current entity")]
    public partial struct BasicBlockMap<T> : IReadOnlyCollection<(BasicBlock, T)>
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
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created block map.</returns>
        public static BasicBlockMap<T> Create<TOrder, TDirection>(
            in BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            new BasicBlockMap<T>(blocks.EntryBlock, blocks.Count);

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <returns>The created block map.</returns>
        public static BasicBlockMap<T> Create(BasicBlock entryBlock) =>
            new BasicBlockMap<T>(entryBlock, BasicBlockSet.NumDefaultElements);

        #endregion

        #region Instance

        private (BasicBlock, T)[] values;

        /// <summary>
        /// Constructs a new block map.
        /// </summary>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="numBlocks">The number of blocks.</param>
        private BasicBlockMap(BasicBlock entryBlock, int numBlocks)
            : this()
        {
            EntryBlock = entryBlock;
            values = new (BasicBlock, T)[numBlocks];
            Count = 0;

            InitBlockMap();
        }

        /// <summary>
        /// Initializes the debug part of this block map.
        /// </summary>
        partial void InitBlockMap();

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
            readonly get => GetItemRef(block);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                int index = UpdateValueMap(block);
                if (values[index].Item1 is null)
                {
                    AssertAdd(block, value, true);
                    ++Count;
                }
                else
                {
                    AssertAdd(block, value, false);
                }
                values[index] = (block, value);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Asserts that the given block has been added to map set or not.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <param name="value">The value.</param>
        /// <param name="added">True, whether the block has been added.</param>
        readonly partial void AssertAdd(
            BasicBlock block,
            in T value,
            bool added);

        /// <summary>
        /// Returns an immutable reference to the associated value.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <returns>The reference to the associated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref readonly T GetItemRef(BasicBlock block)
        {
            int index = block.BlockIndex;
            EntryBlock.Assert(index < values.Length);
            ref var tupleRef = ref values[index];
            EntryBlock.AssertNotNull(tupleRef.Item1);
            AssertContained(block, tupleRef.Item2, true);
            return ref tupleRef.Item2;
        }

        /// <summary>
        /// Asserts that the given block is contained in the set or not.
        /// </summary>
        /// <param name="block">The basic block.</param>
        /// <param name="value">The value (if any).</param>
        /// <param name="contained">True, whether the block is contained.</param>
        readonly partial void AssertContained(
            BasicBlock block,
            in T value,
            bool contained);

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
        public void Add(BasicBlock block, T value)
        {
            bool success = TryAdd(block, value);
            Method.Assert(success);
        }

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
                AssertContained(block, entry.Item2, true);
                return false;
            }

            ++Count;
            entry = (block, value);
            AssertAdd(block, value, true);
            return true;
        }

        /// <summary>
        /// Tries to get a stored value for the given block.
        /// </summary>
        /// <param name="block">The block to lookup.</param>
        /// <param name="value">The stored value (if any).</param>
        /// <returns>True, if the block could be found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(BasicBlock block, [NotNullWhen(true)] out T? value)
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
            if (index >= values.Length)
                return false;
            ref var entry = ref values[index];
            bool contained = entry.Item1 != null;
            AssertContained(block, entry.Item2, contained);
            return contained;
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

        /// <summary>
        /// Asserts that the map has been cleared.
        /// </summary>
        readonly partial void AssertCleared();

        /// <summary>
        /// Clears this map by removing all elements.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(values, 0, values.Length);
            Count = 0;

            AssertCleared();
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

    //
    // Debugging implementations
    //

#if DEBUG
    partial struct BasicBlockSet
    {
        #region Instance

        /// <summary>
        /// The debugging block set.
        /// </summary>
        private HashSet<BasicBlock> blockSet;

        /// <summary cref="InitBlockSet"/>
        [MemberNotNull(nameof(blockSet))]
        partial void InitBlockSet() => blockSet = new(new BasicBlock.Comparer());

        #endregion

        #region Methods

        /// <summary cref="AssertAdd(BasicBlock, bool)"/>
        readonly partial void AssertAdd(BasicBlock block, bool added) =>
            EntryBlock.Assert(blockSet.Add(block) == added);

        /// <summary cref="AssertContained(BasicBlock, bool)"/>
        readonly partial void AssertContained(BasicBlock block, bool contained) =>
            EntryBlock.Assert(blockSet.Contains(block) == contained);

        /// <summary cref="AssertRemoved(BasicBlock, bool)"/>
        readonly partial void AssertRemoved(BasicBlock block, bool removed) =>
            EntryBlock.Assert(blockSet.Remove(block) == removed);

        /// <summary cref="AssertCleared()"/>
        readonly partial void AssertCleared()
        {
            blockSet.Clear();
            EntryBlock.Assert(!HasAny && blockSet.Count == 0);
        }

        #endregion
    }

    partial struct BasicBlockMap<T>
    {
        #region Instance

        /// <summary>
        /// The debugging block set.
        /// </summary>
        private Dictionary<BasicBlock, T> blockMap;

        /// <summary cref="InitBlockMap"/>
        [MemberNotNull(nameof(blockMap))]
        partial void InitBlockMap() => blockMap =
            new Dictionary<BasicBlock, T>(new BasicBlock.Comparer());

        #endregion

        #region Methods

        /// <summary cref="AssertAdd(BasicBlock, in T, bool)"/>
        readonly partial void AssertAdd(
            BasicBlock block,
            in T value,
            bool added)
        {
            EntryBlock.Assert(blockMap.ContainsKey(block) == !added);
            blockMap[block] = value;
        }

        /// <summary cref="AssertContained(BasicBlock, in T, bool)"/>
        readonly partial void AssertContained(
            BasicBlock block,
            in T value,
            bool contained)
        {
            bool found = blockMap.TryGetValue(block, out var storedValue);
            EntryBlock.Assert(contained == found);
            if (contained)
                EntryBlock.Assert(Equals(value, storedValue));
        }

        /// <summary cref="AssertCleared()"/>
        readonly partial void AssertCleared()
        {
            blockMap.Clear();
            EntryBlock.Assert(Count == blockMap.Count);
        }

        #endregion
    }
#endif
}

#pragma warning restore CS0282 // There is no defined ordering between fields in
// multiple declarations of partial struct
