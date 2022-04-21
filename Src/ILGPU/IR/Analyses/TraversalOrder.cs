// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: TraversalOrder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    /// Provides successors for a given basic block.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public interface ITraversalSuccessorsProvider<TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        /// <summary>
        /// Returns or computes successors of the given basic block.
        /// </summary>
        /// <param name="basicBlock">The source basic block.</param>
        /// <returns>The returned successor collection.</returns>
        ReadOnlySpan<BasicBlock> GetSuccessors(BasicBlock basicBlock);
    }

    /// <summary>
    /// A general traversal visitor.
    /// </summary>
    public interface ITraversalVisitor
    {
        /// <summary>
        /// Visits the given block.
        /// </summary>
        /// <param name="block">The block to visit.</param>
        void Visit(BasicBlock block);
    }

    /// <summary>
    /// A generic collection visitor.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    public readonly struct TraversalCollectionVisitor<TCollection> :
        ITraversalVisitor
        where TCollection : ICollection<BasicBlock>
    {
        #region Instance

        /// <summary>
        /// Constructs a new collection visitor.
        /// </summary>
        /// <param name="collection">The target collection.</param>
        public TraversalCollectionVisitor(TCollection collection)
        {
            Collection = collection;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target collection to add the elements to.
        /// </summary>
        public TCollection Collection { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given block to the target collection.
        /// </summary>
        /// <param name="block">The block to add.</param>
        public readonly void Visit(BasicBlock block) => Collection.Add(block);

        #endregion
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

        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="visitor">The visitor instance.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        void Traverse<TVisitor, TSuccessorProvider, TDirection>(
            BasicBlock entryBlock,
            ref TVisitor visitor,
            in TSuccessorProvider successorProvider)
            where TVisitor : struct, ITraversalVisitor
            where TSuccessorProvider : struct, ITraversalSuccessorsProvider<TDirection>
            where TDirection : struct, IControlFlowDirection;
    }

    /// <summary>
    /// Another view that is compatible with the current type without requiring a new
    /// computation.
    /// </summary>
    /// <typeparam name="TOther">The other view.</typeparam>
    public interface ICompatibleTraversalOrder<TOther>
        where TOther : struct, ITraversalOrder
    { }

    /// <summary>
    /// A helper class for traversal.
    /// </summary>
    public static class TraversalOrder
    {
        /// <summary>
        /// Specifies the default initial stack size.
        /// </summary>
        public const int InitStackSize = 16;

        /// <summary>
        /// Initializes a forwards enumeration state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TraversalEnumerationState ForwardsInit() =>
            new TraversalEnumerationState()
            {
                Index = -1,
            };

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
        /// <typeparam name="TOrder">The current order type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="order">The current order instance.</param>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="count">The number of elements.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BasicBlockCollection<TOrder, TDirection>
            TraverseToCollection<TOrder, TSuccessorProvider, TDirection>(
            this TOrder order,
            int count,
            BasicBlock entryBlock,
            in TSuccessorProvider successorProvider)
            where TOrder : struct, ITraversalOrder
            where TSuccessorProvider : struct, ITraversalSuccessorsProvider<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            var newBlocks = ImmutableArray.CreateBuilder<BasicBlock>(count);
            var visitor = new TraversalCollectionVisitor<
                ImmutableArray<BasicBlock>.Builder>(newBlocks);

            order.Traverse<
                TraversalCollectionVisitor<ImmutableArray<BasicBlock>.Builder>,
                TSuccessorProvider,
                TDirection>(
                entryBlock,
                ref visitor,
                successorProvider);

            // Return new block collection
            return new BasicBlockCollection<TOrder, TDirection>(
                entryBlock,
                newBlocks.ToImmutable());
        }
    }

    /// <summary>
    /// Enumerates all basic blocks in pre order.
    /// </summary>
    public readonly struct PreOrder :
        ITraversalOrder,
        ICompatibleTraversalOrder<ReversePreOrder>
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
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="visitor">The visitor instance.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        public readonly void Traverse<
            TVisitor,
            TSuccessorProvider,
            TDirection>(
            BasicBlock entryBlock,
            ref TVisitor visitor,
            in TSuccessorProvider successorProvider)
            where TVisitor : struct, ITraversalVisitor
            where TSuccessorProvider : struct, ITraversalSuccessorsProvider<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            var visited = BasicBlockSet.Create(entryBlock);
            var stack = new Stack<BasicBlock>(TraversalOrder.InitStackSize);
            var currentBlock = entryBlock;

            while (true)
            {
                if (visited.Add(currentBlock))
                {
                    visitor.Visit(currentBlock);
                    var successors = successorProvider.GetSuccessors(currentBlock);
                    if (successors.Length > 0)
                    {
                        for (int i = successors.Length - 1; i >= 1; --i)
                            stack.Push(successors[i]);
                        currentBlock = successors[0];
                        continue;
                    }
                }

                if (stack.Count < 1)
                    break;
                currentBlock = stack.Pop();
            }
        }
    }

    /// <summary>
    /// Enumerates all basic blocks in reverse pre order.
    /// </summary>
    public readonly struct ReversePreOrder :
        ITraversalOrder,
        ICompatibleTraversalOrder<PreOrder>
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

        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <param name="visitor">The visitor instance.</param>
        /// <returns>The created traversal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Traverse<
            TVisitor,
            TSuccessorProvider,
            TDirection>(
            BasicBlock entryBlock,
            ref TVisitor visitor,
            in TSuccessorProvider successorProvider)
            where TVisitor : struct, ITraversalVisitor
            where TSuccessorProvider : struct, ITraversalSuccessorsProvider<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            var preOrder = new PreOrder();
            preOrder.Traverse<TVisitor, TSuccessorProvider, TDirection>(
                entryBlock,
                ref visitor,
                successorProvider);
        }
    }

    /// <summary>
    /// Enumerates all basic blocks in post order.
    /// </summary>
    public readonly struct PostOrder :
        ITraversalOrder,
        ICompatibleTraversalOrder<ReversePostOrder>
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
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="visitor">The visitor instance.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        public readonly void Traverse<
            TVisitor,
            TSuccessorProvider,
            TDirection>(
            BasicBlock entryBlock,
            ref TVisitor visitor,
            in TSuccessorProvider successorProvider)
            where TVisitor : struct, ITraversalVisitor
            where TSuccessorProvider : struct, ITraversalSuccessorsProvider<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            var visited = BasicBlockSet.Create(entryBlock);
            var stack = new Stack<(BasicBlock, int)>(TraversalOrder.InitStackSize);
            var current = (Block: entryBlock, Child: 0);

            while (true)
            {
                var currentBlock = current.Block;

                if (current.Child == 0)
                {
                    if (!visited.Add(currentBlock))
                        goto next;
                }

                var successors = successorProvider.GetSuccessors(currentBlock);
                if (current.Child >= successors.Length)
                {
                    visitor.Visit(currentBlock);
                    goto next;
                }
                else
                {
                    stack.Push((currentBlock, current.Child + 1));
                    current = (successors[current.Child], 0);
                }

                continue;
            next:
                if (stack.Count < 1)
                    break;
                current = stack.Pop();
            }
        }
    }

    /// <summary>
    /// Enumerates all basic blocks in reverse post order.
    /// </summary>
    public readonly struct ReversePostOrder :
        ITraversalOrder,
        ICompatibleTraversalOrder<PostOrder>
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

        /// <summary>
        /// Computes a traversal using the current order.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="visitor">The visitor instance.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Traverse<
            TVisitor,
            TSuccessorProvider,
            TDirection>(
            BasicBlock entryBlock,
            ref TVisitor visitor,
            in TSuccessorProvider successorProvider)
            where TVisitor : struct, ITraversalVisitor
            where TSuccessorProvider : struct, ITraversalSuccessorsProvider<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            var postOrder = new PostOrder();
            postOrder.Traverse<TVisitor, TSuccessorProvider, TDirection>(
                entryBlock,
                ref visitor,
                successorProvider);
        }
    }
}
