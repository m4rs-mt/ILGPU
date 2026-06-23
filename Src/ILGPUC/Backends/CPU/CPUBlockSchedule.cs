// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUBlockSchedule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// A linear, loop-nested emission order of the blocks of a method for masked
/// (predicated) SIMD execution.
/// </summary>
/// <remarks>
/// Every block is emitted exactly once, after all of its forward predecessors.
/// The blocks of a loop form a contiguous range that starts with the loop
/// header and is wrapped in a C# loop, so that lanes executing different
/// paths reconverge at every block and all lanes inside a loop run the same
/// iteration. Each lane enters a block through the entry mask that the
/// terminators of its predecessors accumulate.
///
/// A loop entered through several headers (irreducible control flow) runs
/// while lanes wait at any of its headers. Every edge to one of its headers
/// is treated as a back edge, so lanes can enter the loop through different
/// headers and move between them in any iteration.
/// </remarks>
sealed class CPUBlockSchedule
{
    #region Nested Types

    /// <summary>
    /// An element of a schedule: either a single block or a nested loop.
    /// </summary>
    internal sealed class Item
    {
        /// <summary>
        /// Creates a block item.
        /// </summary>
        public Item(BasicBlock block)
        {
            _block = block;
            Items = [];
        }

        /// <summary>
        /// Creates a loop item.
        /// </summary>
        public Item(Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop)
        {
            Loop = loop;
            Items = [];
        }

        private readonly BasicBlock? _block;

        /// <summary>
        /// The block of a block item, or the first scheduled header of a loop
        /// item.
        /// </summary>
        public BasicBlock Block => _block ?? Items[0].Block;

        /// <summary>
        /// The loop of a loop item, null for block items.
        /// </summary>
        public Loops<ReversePostOrder<BasicBlock>, Forwards>.Node? Loop { get; }

        /// <summary>
        /// The nested items of a loop item (starting with its header).
        /// </summary>
        public List<Item> Items { get; }
    }

    #endregion

    #region Instance

    private readonly Dictionary<BasicBlock, int> _rpoIndex = [];
    private readonly Dictionary<BasicBlock, int> _linearIndex = [];
    private readonly HashSet<BasicBlock> _elidedMasks = [];

    /// <summary>
    /// Builds the schedule of the given method.
    /// </summary>
    public CPUBlockSchedule(Method method)
    {
        Method = method;
        Loops = method.CreateLoops();

        int index = 0;
        foreach (var block in method.Blocks)
            _rpoIndex[block] = index++;

        var allBlocks = new List<BasicBlock>();
        foreach (var block in method.Blocks)
            allBlocks.Add(block);
        Build(allBlocks, null, Items);
        Flatten(Items);
        ComputeElidedMasks();
    }

    #endregion

    #region Properties

    /// <summary>
    /// The scheduled method.
    /// </summary>
    public Method Method { get; }

    /// <summary>
    /// The loops of the method.
    /// </summary>
    public Loops<ReversePostOrder<BasicBlock>, Forwards> Loops { get; }

    /// <summary>
    /// The top-level items of the schedule.
    /// </summary>
    public List<Item> Items { get; } = [];

    /// <summary>
    /// All blocks in emission order.
    /// </summary>
    public List<BasicBlock> LinearOrder { get; } = [];

    #endregion

    #region Methods

    /// <summary>
    /// Returns true if the block is entered with the active mask of its only
    /// predecessor, which is emitted directly before it and branches to it
    /// unconditionally, so no separate entry mask is required.
    /// </summary>
    public bool HasElidedMask(BasicBlock block) => _elidedMasks.Contains(block);

    /// <summary>
    /// Returns true if the block is a header of a loop.
    /// </summary>
    /// <remarks>
    /// A header never belongs to a nested loop, so it is a header of its
    /// innermost loop.
    /// </remarks>
    public bool IsLoopHeader(BasicBlock block) =>
        Loops.TryGetLoop(block, out var loop) && IsHeaderOf(loop, block);

    /// <summary>
    /// Returns the innermost loop containing the block, if any.
    /// </summary>
    public Loops<ReversePostOrder<BasicBlock>, Forwards>.Node? GetLoop(
        BasicBlock block) =>
        Loops.GetLoop(block);

    /// <summary>
    /// Returns true if the block is one of the headers of the loop.
    /// </summary>
    public static bool IsHeaderOf(
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        BasicBlock block)
    {
        foreach (var header in loop.Headers)
        {
            if (header == block)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the item node that represents the block at the nesting level
    /// of <paramref name="loop"/>: the block itself or the outermost child
    /// loop of <paramref name="loop"/> that contains it.
    /// </summary>
    private object GetNode(
        BasicBlock block,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node? loop)
    {
        var current = Loops.GetLoop(block);
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node? node = null;
        while (current is not null && current != loop)
        {
            node = current;
            current = current.Parent;
        }
        return node is null ? block : node;
    }

    /// <summary>
    /// Topologically sorts the given region (the whole method or the members
    /// of a loop) with child loops collapsed into single nodes.
    /// </summary>
    /// <remarks>
    /// The loop analysis splits every strongly connected component into a
    /// loop, and the members of a loop without its headers into nested loops,
    /// so the collapsed region without the edges to its headers is acyclic and
    /// can always be sorted. Only the headers of a loop are entered from
    /// outside it, so a loop starts with one of its headers.
    /// </remarks>
    private void Build(
        List<BasicBlock> region,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node? loop,
        List<Item> items)
    {
        var regionSet = new HashSet<BasicBlock>(region);
        var nodes = new List<object>();
        var nodeSet = new HashSet<object>();
        foreach (var block in region)
        {
            var node = GetNode(block, loop);
            if (nodeSet.Add(node))
                nodes.Add(node);
        }

        // Count the forward edges between distinct nodes
        var inDegree = new Dictionary<object, int>();
        var successors = new Dictionary<object, List<object>>();
        foreach (var node in nodes)
        {
            inDegree[node] = 0;
            successors[node] = [];
        }
        foreach (var block in region)
        {
            var source = GetNode(block, loop);
            foreach (var succ in block.Successors)
            {
                // Edges to the headers of the loop are back edges
                if (!regionSet.Contains(succ) ||
                    loop is not null && IsHeaderOf(loop, succ))
                {
                    continue;
                }
                var target = GetNode(succ, loop);
                if (target == source || successors[source].Contains(target))
                    continue;
                successors[source].Add(target);
                inDegree[target]++;
            }
        }

        // Kahn's algorithm, preferring the lowest reverse post order index
        var ready = new List<object>();
        foreach (var node in nodes)
        {
            if (inDegree[node] == 0)
                ready.Add(node);
        }
        int emitted = 0;
        while (ready.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < ready.Count; i++)
            {
                if (GetOrderIndex(ready[i]) < GetOrderIndex(ready[best]))
                    best = i;
            }
            var node = ready[best];
            ready.RemoveAt(best);
            ++emitted;

            if (node is BasicBlock block)
            {
                items.Add(new Item(block));
            }
            else
            {
                var child = (Loops<ReversePostOrder<BasicBlock>, Forwards>.Node)node;
                var item = new Item(child);
                var members = new List<BasicBlock>();
                foreach (var b in Method.Blocks)
                {
                    if (child.Contains(b))
                        members.Add(b);
                }
                Build(members, child, item.Items);

                // A loop starts with one of its headers
                if (item.Items.Count < 1 || item.Items[0].Loop is not null ||
                    !IsHeaderOf(child, item.Items[0].Block))
                {
                    throw new InvalidOperationException(
                        $"Loop of '{Method.Name}' does not start with a header");
                }
                items.Add(item);
            }

            foreach (var succ in successors[node])
            {
                if (--inDegree[succ] == 0)
                    ready.Add(succ);
            }
        }
        if (emitted != nodes.Count)
        {
            throw new InvalidOperationException(
                $"Control flow of '{Method.Name}' contains a cycle outside " +
                "of its loops");
        }
    }

    /// <summary>
    /// Returns the ordering key of a schedule node: the reverse post order
    /// index of a block or the lowest index of the headers of a loop.
    /// </summary>
    private int GetOrderIndex(object node)
    {
        if (node is BasicBlock block)
            return _rpoIndex[block];
        int index = int.MaxValue;
        var loop = (Loops<ReversePostOrder<BasicBlock>, Forwards>.Node)node;
        foreach (var header in loop.Headers)
            index = Math.Min(index, _rpoIndex[header]);
        return index;
    }

    /// <summary>
    /// Computes the linear emission order.
    /// </summary>
    private void Flatten(List<Item> items)
    {
        foreach (var item in items)
        {
            if (item.Loop is null)
            {
                _linearIndex[item.Block] = LinearOrder.Count;
                LinearOrder.Add(item.Block);
            }
            else
            {
                Flatten(item.Items);
            }
        }
    }

    /// <summary>
    /// Finds blocks that can reuse the active mask of their predecessor.
    /// </summary>
    private void ComputeElidedMasks()
    {
        foreach (var block in LinearOrder)
        {
            if (block.Predecessors.Length == 0)
            {
                // The entry block runs with the initial active mask
                _elidedMasks.Add(block);
                continue;
            }
            if (block.Predecessors.Length != 1 || IsLoopHeader(block))
                continue;
            var pred = block.Predecessors[0];
            if (pred.TerminationKind != BlockTerminationKind.Unconditional)
                continue;
            int index = _linearIndex[block];
            if (index > 0 && LinearOrder[index - 1] == pred
                && Loops.GetLoop(pred) == Loops.GetLoop(block))
            {
                _elidedMasks.Add(block);
            }
        }
    }

    #endregion
}
