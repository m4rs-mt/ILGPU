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

using ILGPU.IR.Analyses.ControlFlowDirection;
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
    /// Provides successors for a given basic block.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <typeparam name="TSuccessors">The collection type.</typeparam>
    public interface ITraversalSuccessorsProvider<TDirection, TSuccessors>
        where TDirection : struct, IControlFlowDirection
        where TSuccessors : IReadOnlyCollection<BasicBlock>
    {
        /// <summary>
        /// Returns or computes successors of the given basic block.
        /// </summary>
        /// <param name="basicBlock">The source basic block.</param>
        /// <returns>The returned successor collection.</returns>
        TSuccessors GetSuccessors(BasicBlock basicBlock);
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
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TSuccessors">The collection type.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        void Traverse<TTargetList, TSuccessorProvider, TDirection, TSuccessors>(
            BasicBlock entryBlock,
            TTargetList target,
            TSuccessorProvider successorProvider)
            where TTargetList : IList<BasicBlock>
            where TSuccessorProvider :
                ITraversalSuccessorsProvider<TDirection, TSuccessors>
            where TDirection : struct, IControlFlowDirection
            where TSuccessors : IReadOnlyList<BasicBlock>;
    }

    /// <summary>
    /// Another view that is compatible with the current type without requiring a new
    /// computation.
    /// </summary>
    /// <typeparam name="TOther">The other view.</typeparam>
    [SuppressMessage(
        "Design",
        "CA1040:Avoid empty interfaces",
        Justification = "Used for generic constraints")]
    public interface ICompatibleTraversalOrder<TOther>
        where TOther : struct, ITraversalOrder
    { }

    /// <summary>
    /// A helper class for traversal.
    /// </summary>
    static class TraversalOrder
    {
        /// <summary>
        /// Specifies the default initial stack size.
        /// </summary>
        public const int InitStackSize = 16;

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
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TSuccessors">The collection type.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        public readonly void Traverse<
            TTargetList,
            TSuccessorProvider,
            TDirection,
            TSuccessors>(
            BasicBlock entryBlock,
            TTargetList target,
            TSuccessorProvider successorProvider)
            where TTargetList : IList<BasicBlock>
            where TSuccessorProvider :
                ITraversalSuccessorsProvider<TDirection, TSuccessors>
            where TDirection : struct, IControlFlowDirection
            where TSuccessors : IReadOnlyList<BasicBlock>
        {
            var visited = BasicBlockSet.Create(entryBlock);
            var stack = new Stack<BasicBlock>(TraversalOrder.InitStackSize);
            var currentBlock = entryBlock;

            while (true)
            {
                if (visited.Add(currentBlock))
                {
                    target.Add(currentBlock);
                    var successors = successorProvider.GetSuccessors(currentBlock);
                    if (successors.Count > 0)
                    {
                        for (int i = successors.Count - 1; i >= 1; --i)
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
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TSuccessors">The collection type.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <param name="target">The target list.</param>
        /// <returns>The created traversal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Traverse<
            TTargetList,
            TSuccessorProvider,
            TDirection,
            TSuccessors>(
            BasicBlock entryBlock,
            TTargetList target,
            TSuccessorProvider successorProvider)
            where TTargetList : IList<BasicBlock>
            where TSuccessorProvider :
                ITraversalSuccessorsProvider<TDirection, TSuccessors>
            where TDirection : struct, IControlFlowDirection
            where TSuccessors : IReadOnlyList<BasicBlock>
        {
            var preOrder = new PreOrder();
            preOrder.Traverse<TTargetList, TSuccessorProvider, TDirection, TSuccessors>(
                entryBlock,
                target,
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
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TSuccessors">The collection type.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        public readonly void Traverse<
            TTargetList,
            TSuccessorProvider,
            TDirection,
            TSuccessors>(
            BasicBlock entryBlock,
            TTargetList target,
            TSuccessorProvider successorProvider)
            where TTargetList : IList<BasicBlock>
            where TSuccessorProvider :
                ITraversalSuccessorsProvider<TDirection, TSuccessors>
            where TDirection : struct, IControlFlowDirection
            where TSuccessors : IReadOnlyList<BasicBlock>
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
                if (current.Child >= successors.Count)
                {
                    target.Add(currentBlock);
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
        /// <typeparam name="TTargetList">The target list type.</typeparam>
        /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TSuccessors">The collection type.</typeparam>
        /// <param name="entryBlock">The entry block.</param>
        /// <param name="target">The target list.</param>
        /// <param name="successorProvider">The successor provider.</param>
        /// <returns>The created traversal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Traverse<
            TTargetList,
            TSuccessorProvider,
            TDirection,
            TSuccessors>(
            BasicBlock entryBlock,
            TTargetList target,
            TSuccessorProvider successorProvider)
            where TTargetList : IList<BasicBlock>
            where TSuccessorProvider :
                ITraversalSuccessorsProvider<TDirection, TSuccessors>
            where TDirection : struct, IControlFlowDirection
            where TSuccessors : IReadOnlyList<BasicBlock>
        {
            var postOrder = new PostOrder();
            postOrder.Traverse<TTargetList, TSuccessorProvider, TDirection, TSuccessors>(
                entryBlock,
                target,
                successorProvider);
        }
    }
}
