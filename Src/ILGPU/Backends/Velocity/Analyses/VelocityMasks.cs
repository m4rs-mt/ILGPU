// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityMasks.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Uncomment this line to or define a preprocessor symbol to enable detailed Velocity
// accelerator debugging:
// #define DEBUG_VELOCITY

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Loop = ILGPU.IR.Analyses.Loops<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>.Node;

namespace ILGPU.Backends.Velocity.Analyses
{
    /// <summary>
    /// A program analysis to gather information about vector masks to be used.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    sealed class VelocityMasks<TILEmitter>
        where TILEmitter : struct, IILEmitter
    {
        #region Instance

        /// <summary>
        /// The set of all back edge source blocks.
        /// </summary>
        private BasicBlockSet backEdges;

        /// <summary>
        /// The set of all loop blocks.
        /// </summary>
        private BasicBlockSet loopBlocks;

        /// <summary>
        /// The set of all loop headers.
        /// </summary>
        private BasicBlockMap<Loop> loopHeaders;

        /// <summary>
        /// The set of all exit blocks.
        /// </summary>
        private BasicBlockMap<HashSet<Loop>> exitBlocks;

        /// <summary>
        /// The set of all loop masks.
        /// </summary>
        private readonly Dictionary<Loop, ILLocal> loopMasks = new();

        /// <summary>
        /// Maps blocks to their input masks.
        /// </summary>
        private readonly BasicBlockMap<ILLocal> blockMasks;

        /// <summary>
        /// Stores all loops.
        /// </summary>
        private readonly Loops<ReversePostOrder, Forwards> loops;

        public VelocityMasks(
            BasicBlockCollection<ReversePostOrder, Forwards> blocks,
            TILEmitter emitter,
            VelocityTargetSpecializer specializer)
        {
            loopHeaders = blocks.CreateMap<Loop>();
            exitBlocks = blocks.CreateMap<HashSet<Loop>>();
            backEdges = blocks.CreateSet();
            loopBlocks = blocks.CreateSet();
            blockMasks = blocks.CreateMap<ILLocal>();

            // Iterate over all loops and determine all body blocks and all back edges
            var cfg = blocks.CreateCFG();
            loops = cfg.CreateLoops();

            loops.ProcessLoops(loop =>
            {
                // Declare a new loop mask
                loopMasks[loop] = emitter.DeclareLocal(specializer.WarpType32);

                // Register all loop headers
                foreach (var header in loop.Headers)
                    loopHeaders.Add(header, loop);

                // Remember all body blocks
                foreach (var block in loop.AllMembers)
                    loopBlocks.Add(block);

                // Remember all exits
                foreach (var block in loop.Exits)
                {
                    if (!exitBlocks.TryGetValue(block, out var set))
                    {
                        set = new HashSet<Loop>(2)
                        {
                            loop
                        };
                    }
                    else
                    {
                        set.Add(loop);
                    }
                    exitBlocks[block] = set;
                }

                // Register all back edges
                foreach (var backEdge in loop.BackEdges)
                    backEdges.Add(backEdge);
            });

            // Remove all headers again from all loops from the instantly reset set
            loops.ProcessLoops(loop =>
            {
                foreach (var header in loop.Headers)
                    loopBlocks.Remove(header);
            });

            // Allocate local masks and initialize all of them
            foreach (var block in blocks)
            {
                // Create a local variable to store the entry mask for this block
                var blockMask = emitter.DeclareLocal(specializer.WarpType32);
                blockMasks[block] = blockMask;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Disables all internal lanes.
        /// </summary>
        public void DisableAllLanes(
            Method method,
            TILEmitter emitter,
            VelocityTargetSpecializer specializer)
        {
            foreach (var (basicBlock, blockMask) in blockMasks)
            {
                // Ignore the entry block
                if (basicBlock == method.EntryBlock)
                    continue;

                // Ignore blocks that will be reset automatically
                if (NeedsToRefreshMask(basicBlock))
                    continue;

                specializer.PushNoLanesMask32(emitter);
                emitter.Emit(LocalOperation.Store, blockMask);
            }
        }

        /// <summary>
        /// Tries to map the given block to a loop and returns the loop if possible.
        /// </summary>
        /// <param name="block">The block to map to a loop.</param>
        /// <param name="loop">The resolved loop (if any).</param>
        /// <returns>True if the given block could be mapped to a loop.</returns>
        public bool TryGetLoop(BasicBlock block, [NotNullWhen(true)] out Loop? loop) =>
            loops.TryGetLoops(block, out loop);

        /// <summary>
        /// Returns the block mask for the given basic block.
        /// </summary>
        /// <param name="block">The block to lookup.</param>
        /// <returns>The block mask to use.</returns>
        public ILLocal GetBlockMask(BasicBlock block) => blockMasks[block];

        /// <summary>
        /// Returns the loop mask for the given loop.
        /// </summary>
        /// <param name="loop">The loop to lookup.</param>
        /// <returns>The loop mask to use.</returns>
        public ILLocal GetLoopMask(Loop loop) => loopMasks[loop];

        /// <summary>
        /// Returns true if the given block is a header and also returns a set of nested
        /// loop headers that are implicitly controlled by this header.
        /// </summary>
        public bool IsHeader(
            BasicBlock target,
            [NotNullWhen(true)]
            out Loop? loop) =>
            loopHeaders.TryGetValue(target, out loop);

        /// <summary>
        /// Returns true if the given target block is an exit.
        /// </summary>
        public bool IsExit(
            BasicBlock target,
            [NotNullWhen(true)]
            out Predicate<Loop>? containsLoop)
        {
            if (exitBlocks.TryGetValue(target, out var loopsToExit))
            {
                containsLoop = loopsToExit.Contains;
                return true;
            }

            containsLoop = null;
            return false;
        }

        /// <summary>
        /// Returns true if the given block is a target potentially hit from a back edge.
        /// </summary>
        public bool IsBackEdgeBlock(BasicBlock block) => backEdges.Contains(block);

        /// <summary>
        /// Returns true if this block needs to refresh its mask instantly.
        /// </summary>
        public bool NeedsToRefreshMask(BasicBlock block) =>
            loopBlocks.Contains(block);

#if DEBUG_VELOCITY
        public void DumpAllMasks(
            TILEmitter emitter,
            VelocityTargetSpecializer specializer)
        {
            foreach (var (block, mask) in blockMasks)
            {
                emitter.Emit(LocalOperation.Load, mask);
                specializer.DumpWarp32(emitter, $"  {block.ToReferenceString()}");
            }
            foreach (var (loop, mask) in loopMasks)
            {
                emitter.Emit(LocalOperation.Load, mask);
                specializer.DumpWarp32(
                    emitter,
                    $"  LOOP_{loop.Headers[0].ToReferenceString()}");
            }
        }
#endif

        #endregion
    }
}
