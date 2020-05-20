// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: TraversalOrder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.Duplicates;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses.TraversalOrders
{
    /// <summary>
    /// A enumeration state of a generic traversal.
    /// </summary>
    public struct TraversalEnumerationState
    {
        /// <summary>
        /// The current enumeration index.
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// A generic traversal order.
    /// </summary>
    public interface ITraversalOrder
    {
        /// <summary>
        /// Initializes a new enumeration state.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        TraversalEnumerationState Init<TCollection>(TCollection blocks)
            where TCollection : IReadOnlyList<BasicBlock>;

        /// <summary>
        /// Tries to move the state to the next block.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        bool MoveNext<TCollection>(
            TCollection blocks,
            ref TraversalEnumerationState state)
            where TCollection : IReadOnlyList<BasicBlock>;
    }

    /// <summary>
    /// A more specific order that should be used in the scope of type constraints.
    /// </summary>
    public interface ITraversalOrderProvider : ITraversalOrder
    {
        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TDuplicates">The duplicate specification.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <returns>The created traversal.</returns>
        void Traverse<TTargetList, TDuplicates>(
            BasicBlock entryBlock,
            TTargetList target)
            where TTargetList : IList<BasicBlock>
            where TDuplicates : struct, IDuplicates<BasicBlock>;
    }

    /// <summary>
    /// A view that has an associated traversal order.
    /// </summary>
    /// <typeparam name="TOrderProvider">The order provider type.</typeparam>
    public interface ITraversalOrderView<TOrderProvider> :
        ITraversalOrder,
        ICompatibleTraversalView<TOrderProvider>
        where TOrderProvider : struct, ITraversalOrderProvider
    { }

    /// <summary>
    /// Another view that is compatible with the current type without requiring a new
    /// computation.
    /// </summary>
    /// <typeparam name="TOther">The other view.</typeparam>
    [SuppressMessage(
        "Design",
        "CA1040:Avoid empty interfaces",
        Justification = "Used for generic constraints")]
    public interface ICompatibleTraversalView<TOther>
        where TOther : struct, ITraversalOrder
    { }

    /// <summary>
    /// A helper class for traversal.
    /// </summary>
    static class TraversalOrder
    {
        /// <summary>
        /// Initializes a forwards enumeration state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TraversalEnumerationState ForwardsInit() => default;

        /// <summary>
        /// Tries to move a forwards state to the next block.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ForwardsMoveNext<TCollection>(
            TCollection blocks,
            ref TraversalEnumerationState state)
            where TCollection : IReadOnlyList<BasicBlock> =>
            ++state.Index < blocks.Count;

        /// <summary>
        /// Initializes a backwards enumeration state.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TraversalEnumerationState BackwardsInit<TCollection>(
            TCollection blocks)
            where TCollection : IReadOnlyList<BasicBlock> =>
            new TraversalEnumerationState()
            {
                Index = blocks.Count
            };

        /// <summary>
        /// Tries to move a backwards state to the next block.
        /// </summary>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BackwardsMoveNext(ref TraversalEnumerationState state) =>
            --state.Index >= 0;

        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TOrder">The order type.</typeparam>
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <param name="order">The current order.</param>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <returns>The created traversal.</returns>
        public static void Traverse<TOrder, TTargetList>(
            this TOrder order,
            BasicBlock entryBlock,
            TTargetList target)
            where TOrder : ITraversalOrderProvider
            where TTargetList : IList<BasicBlock> =>
            order.Traverse<TTargetList, NoDuplicates<BasicBlock>>(
                entryBlock,
                target);
    }

    /// <summary>
    /// Enumerates all basic blocks in pre order.
    /// </summary>
    public readonly struct PreOrder :
        ITraversalOrderProvider,
        ITraversalOrderView<PreOrder>,
        ICompatibleTraversalView<ReversePreOrder>
    {
        /// <summary>
        /// Initializes a new enumeration state.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TraversalEnumerationState Init<TCollection>(TCollection blocks)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.ForwardsInit();

        /// <summary>
        /// Tries to move the state to the next block.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool MoveNext<TCollection>(
            TCollection blocks,
            ref TraversalEnumerationState state)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.ForwardsMoveNext(blocks, ref state);

        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TDuplicates">The duplicate specification.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <returns>The created traversal.</returns>
        public readonly void Traverse<TTargetList, TDuplicates>(
            BasicBlock entryBlock,
            TTargetList target)
            where TTargetList : IList<BasicBlock>
            where TDuplicates : struct, IDuplicates<BasicBlock>
        {
            TDuplicates duplicates = default;

            var visited = new HashSet<BasicBlock>();
            var processed = new Stack<BasicBlock>(16);
            var currentBlock = entryBlock;

            while (true)
            {
                if (visited.Add(currentBlock))
                    target.Add(currentBlock);
                else
                    duplicates.AddAlreadyVisitedItem(target, currentBlock);

                var successors = currentBlock.Successors;
                if (successors.Length > 0)
                {
                    for (int i = successors.Length - 1; i >= 1; --i)
                        processed.Push(successors[i]);
                    currentBlock = successors[0];
                }
                else
                {
                    if (processed.Count < 1)
                        break;
                    currentBlock = processed.Pop();
                }
            }
        }
    }

    /// <summary>
    /// Enumerates all basic blocks in reverse pre order.
    /// </summary>
    public readonly struct ReversePreOrder : ITraversalOrderView<PreOrder>
    {
        /// <summary>
        /// Initializes a new enumeration state.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TraversalEnumerationState Init<TCollection>(TCollection blocks)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.BackwardsInit(blocks);

        /// <summary>
        /// Tries to move the state to the next block.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool MoveNext<TCollection>(
            TCollection blocks,
            ref TraversalEnumerationState state)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.BackwardsMoveNext(ref state);
    }

    /// <summary>
    /// Enumerates all basic blocks in post order.
    /// </summary>
    public readonly struct PostOrder :
        ITraversalOrderProvider,
        ITraversalOrderView<PostOrder>,
        ICompatibleTraversalView<ReversePostOrder>
    {
        /// <summary>
        /// Initializes a new enumeration state.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TraversalEnumerationState Init<TCollection>(TCollection blocks)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.ForwardsInit();

        /// <summary>
        /// Tries to move the state to the next block.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool MoveNext<TCollection>(
            TCollection blocks,
            ref TraversalEnumerationState state)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.ForwardsMoveNext(blocks, ref state);

        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TDuplicates">The duplicate specification.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <returns>The created traversal.</returns>
        public readonly void Traverse<TTargetList, TDuplicates>(
            BasicBlock entryBlock,
            TTargetList target)
            where TTargetList : IList<BasicBlock>
            where TDuplicates : struct, IDuplicates<BasicBlock>
        {
            TDuplicates duplicates = default;

            var visited = new HashSet<BasicBlock>();
            var processed = new Stack<(BasicBlock, int)>(16);
            var current = (Block: entryBlock, Child: 0);

            while (true)
            {
                var currentBlock = current.Block;

                if (current.Child == 0)
                {
                    if (!visited.Add(currentBlock))
                    {
                        duplicates.AddAlreadyVisitedItem(target, current.Block);
                        goto next;
                    }
                }

                if (current.Child >= currentBlock.Successors.Length)
                {
                    target.Add(currentBlock);
                    goto next;
                }
                else
                {
                    processed.Push((current.Block, current.Child + 1));
                    current = (current.Block.Successors[current.Child], 0);
                }

                continue;
                next:
                if (processed.Count < 1)
                    break;
                current = processed.Pop();
            }
        }
    }

    /// <summary>
    /// Enumerates all basic blocks in reverse post order.
    /// </summary>
    public readonly struct ReversePostOrder : ITraversalOrderView<PostOrder>
    {
        /// <summary>
        /// Initializes a new enumeration state.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TraversalEnumerationState Init<TCollection>(TCollection blocks)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.BackwardsInit(blocks);

        /// <summary>
        /// Tries to move the state to the next block.
        /// </summary>
        /// <param name="blocks">The list of blocks to enumerate.</param>
        /// <param name="state">The current enumeration state.</param>
        /// <returns>True, if there is a next block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool MoveNext<TCollection>(
            TCollection blocks,
            ref TraversalEnumerationState state)
            where TCollection : IReadOnlyList<BasicBlock> =>
            TraversalOrder.BackwardsMoveNext(ref state);
    }
}
