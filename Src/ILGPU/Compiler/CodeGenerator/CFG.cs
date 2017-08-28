// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CFG.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        #region Instance

        /// <summary>
        /// The block mapping from il offsets to basic blocks.
        /// </summary>
        private readonly Dictionary<int, BasicBlock> bbMapping = new Dictionary<int, BasicBlock>();
        private readonly List<BasicBlock> postOrder = new List<BasicBlock>();

        private void InitCFG()
        {
            // Build basic blocks
            EntryBlock = new BasicBlock(this, AppendBasicBlock(Function, "Entry"));
            bbMapping.Add(0, EntryBlock);
            var offsetMapping = BuildBasicBlocks();

            // Setup basic blocks
            SetupBasicBlocks(offsetMapping, new HashSet<BasicBlock>(), EntryBlock, 0);

            // Determine post ordering
            processedBasicBlocks.Clear();
            DeterminePostOrder(EntryBlock);
        }

        /// <summary>
        /// Determines the post order of all blocks.
        /// </summary>
        /// <param name="block">The current block.</param>
        private void DeterminePostOrder(BasicBlock block)
        {
            if (!processedBasicBlocks.Contains(block))
            {
                processedBasicBlocks.Add(block);
                foreach (var successor in block.Successors)
                    DeterminePostOrder(successor);
            }
            postOrder.Add(block);
        }

        /// <summary>
        /// Build all required basic blocks.
        /// </summary>
        private Dictionary<int, int> BuildBasicBlocks()
        {
            int blockIdx = 0;
            var result = new Dictionary<int, int>();
            for (int i = 0, e = disassembledMethod.Count; i < e; ++i)
            {
                var instruction = disassembledMethod[i];
                result[instruction.Offset] = i;
                var targets = instruction.Argument as ILInstructionBranchTargets;
                if (targets == null)
                    continue;
                foreach (var target in targets.GetTargetOffsets())
                {
                    if (bbMapping.ContainsKey(target))
                        continue;
                    bbMapping.Add(target, new BasicBlock(this, AppendBasicBlock(Function, $"BB_{blockIdx}")));
                    ++blockIdx;
                }
            }
            return result;
        }

        /// <summary>
        /// Setups all basic blocks (fills in the required information).
        /// </summary>
        /// <param name="offsetMapping">The offset mapping that maps il-byte offsets to indices.</param>
        /// <param name="handledBlocks">A collection of handled blocks.</param>
        /// <param name="current">The current block.</param>
        /// <param name="instructionIdx">The starting instruction index.</param>
        private void SetupBasicBlocks(
            Dictionary<int, int> offsetMapping,
            HashSet<BasicBlock> handledBlocks,
            BasicBlock current,
            int instructionIdx)
        {
            if (handledBlocks.Contains(current))
                return;
            handledBlocks.Add(current);
            current.InstructionOffset = instructionIdx;
            var stackCounter = current.StackCounter;
            for (int e = disassembledMethod.Count; instructionIdx < e; ++instructionIdx)
            {
                var instruction = disassembledMethod[instructionIdx];
                // Handle implicit cases: jumps to blocks without a jump instruction
                if (bbMapping.TryGetValue(instruction.Offset, out BasicBlock other) && current != other)
                {
                    // Wire current and new block
                    current.AddSuccessor(other);
                    other.StackCounter = stackCounter;
                    SetupBasicBlocks(offsetMapping, handledBlocks, other, instructionIdx);
                    break;
                }
                else
                {
                    // Update the current block
                    stackCounter += (instruction.PushCount - instruction.PopCount);
                    current.InstructionCount += 1;

                    if (instruction.IsTerminator)
                    {
                        if (instruction.Argument is ILInstructionBranchTargets targets)
                        {
                            foreach (var target in targets.GetTargetOffsets())
                            {
                                var targetBlock = bbMapping[target];
                                current.AddSuccessor(targetBlock);
                                targetBlock.StackCounter = stackCounter;
                                var targetIdx = offsetMapping[target];
                                SetupBasicBlocks(offsetMapping, handledBlocks, targetBlock, targetIdx);
                            }
                        }
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
