// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Block.CFGBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.Frontend
{
    partial class Block
    {
        /// <summary>
        /// Constructs CFGs out of disassembled methods.
        /// </summary>
        public sealed class CFGBuilder
        {
            #region Nested Types

            /// <summary>
            /// Registers instruction offset mappings.
            /// </summary>
            private readonly struct RegisterOffsetMapping :
                IILInstructionOffsetOperation
            {
                /// <summary>
                /// Constructs a new offset registration mapping.
                /// </summary>
                /// <param name="builder">The parent builder instance.</param>
                /// <param name="instructionIndex">
                /// The current instruction index to map to.
                /// </param>
                public RegisterOffsetMapping(CFGBuilder builder, int instructionIndex)
                {
                    Builder = builder;
                    InstructionIndex = instructionIndex;
                }

                /// <summary>
                /// Returns the parent builder.
                /// </summary>
                public readonly CFGBuilder Builder { get; }

                /// <summary>
                /// Returns the parent instruction index.
                /// </summary>
                public readonly int InstructionIndex { get; }

                /// <summary>
                /// Registers the given instruction offset.
                /// </summary>
                public readonly void Apply(ILInstruction instruction, int offset) =>
                    Builder.offsetMapping[offset] = InstructionIndex;
            }

            #endregion

            #region Instance

            private readonly Dictionary<int, int> offsetMapping =
                new Dictionary<int, int>();
            private readonly Dictionary<int, Block> blockMapping =
                new Dictionary<int, Block>();
            private readonly Dictionary<BasicBlock, Block> basicBlockMapping =
                new Dictionary<BasicBlock, Block>();
            private readonly Dictionary<Block, List<Block>> successorMapping =
                new Dictionary<Block, List<Block>>();

            /// <summary>
            /// Constructs a new CFG builder.
            /// </summary>
            /// <param name="codeGenerator">The current code generator.</param>
            /// <param name="methodBuilder">The current method builder.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal CFGBuilder(
                CodeGenerator codeGenerator,
                Method.Builder methodBuilder)
            {
                CodeGenerator = codeGenerator;
                Builder = methodBuilder;

                var mainEntry = methodBuilder.EntryBlockBuilder;
                EntryBlock = new Block(
                    codeGenerator,
                    mainEntry)
                {
                    InstructionCount = 0
                };
                basicBlockMapping.Add(EntryBlock.BasicBlock, EntryBlock);

                // Create a temporary entry block to ensure that we have a single entry
                // block without any predecessors in all cases
                var internalEntryBlock = new Block(
                    codeGenerator,
                    methodBuilder.CreateBasicBlock(
                        mainEntry.BasicBlock.Location,
                        mainEntry.BasicBlock.Name));
                blockMapping.Add(0, internalEntryBlock);
                basicBlockMapping.Add(internalEntryBlock.BasicBlock, internalEntryBlock);
                BuildBasicBlocks();

                var visited = new HashSet<Block>();
                SetupBasicBlocks(visited, internalEntryBlock, 0);
                WireBlocks();

                // Wire the main entry block with the actual entry block
                mainEntry.CreateBranch(
                    mainEntry.BasicBlock.Location,
                    internalEntryBlock.BasicBlock);

                // Update control-flow structure to refresh all successor/predecessor
                // edge relations
                Blocks = methodBuilder.UpdateControlFlow();
            }

            /// <summary>
            /// Appends a basic block with the given target.
            /// </summary>
            /// <param name="location">The current location.</param>
            /// <param name="target">The block target.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Block AppendBasicBlock(Location location, int target)
            {
                if (!blockMapping.TryGetValue(target, out Block block))
                {
                    var basicBlock = Builder.CreateBasicBlock(location);
                    block = new Block(CodeGenerator, basicBlock);
                    blockMapping.Add(target, block);
                    basicBlockMapping.Add(block.BasicBlock, block);
                }
                return block;
            }

            /// <summary>
            /// Build all required basic blocks.
            /// </summary>
            private void BuildBasicBlocks()
            {
                var disassembledMethod = CodeGenerator.DisassembledMethod;
                for (int i = 0, e = disassembledMethod.Count; i < e; ++i)
                {
                    var instruction = disassembledMethod[i];
                    instruction.ForEachOffset(
                        new RegisterOffsetMapping(
                            this,
                            i));
                    if (!instruction.IsTerminator)
                        continue;
                    if (instruction.Argument is ILInstructionBranchTargets targets)
                    {
                        foreach (var target in targets.GetTargetOffsets())
                        {
                            if (blockMapping.ContainsKey(target))
                                continue;
                            AppendBasicBlock(instruction.Location, target);
                        }
                    }
                }
            }

            /// <summary>
            /// Adds a new successor to the current block.
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="successor">
            /// The successor to add to the current block.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AddSuccessor(Block current, Block successor)
            {
                if (!successorMapping.TryGetValue(current, out List<Block> successors))
                {
                    successors = new List<Block>();
                    successorMapping.Add(current, successors);
                }
                successors.Add(successor);
            }

            /// <summary>
            /// Setups a single basic block.
            /// </summary>
            /// <param name="visited">The set of visited blocks.</param>
            /// <param name="current">The current block.</param>
            /// <param name="stackCounter">The current stack counter.</param>
            /// <param name="target">The target block.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupBasicBlock(
                HashSet<Block> visited,
                Block current,
                int stackCounter,
                int target)
            {
                var targetBlock = blockMapping[target];
                AddSuccessor(current, targetBlock);
                targetBlock.StackCounter = stackCounter;
                var targetIdx = offsetMapping[target];
                SetupBasicBlocks(visited, targetBlock, targetIdx);
            }

            /// <summary>
            /// Setups all basic blocks (fills in the required information).
            /// </summary>
            /// <param name="visited">The set of visited blocks.</param>
            /// <param name="current">The current block.</param>
            /// <param name="instructionIdx">The starting instruction index.</param>
            private void SetupBasicBlocks(
                HashSet<Block> visited,
                Block current,
                int instructionIdx)
            {
                if (!visited.Add(current))
                    return;

                var disassembledMethod = CodeGenerator.DisassembledMethod;
                current.InstructionOffset = instructionIdx;
                var stackCounter = current.StackCounter;
                for (
                    int e = disassembledMethod.Count;
                    instructionIdx < e;
                    ++instructionIdx)
                {
                    var instruction = disassembledMethod[instructionIdx];
                    // Handle implicit cases: jumps to blocks without a jump instruction
                    if (blockMapping.TryGetValue(instruction.Offset, out Block other) &&
                        current != other)
                    {
                        // Wire current and new block
                        AddSuccessor(current, other);
                        other.StackCounter = stackCounter;
                        SetupBasicBlocks(visited, other, instructionIdx);
                        break;
                    }
                    else
                    {
                        // Update the current block
                        stackCounter += instruction.PushCount - instruction.PopCount;
                        current.InstructionCount += 1;

                        if (instruction.IsTerminator)
                        {
                            if (instruction.Argument is
                                ILInstructionBranchTargets targets)
                            {
                                // Create appropriate temp targets
                                var targetOffsets = targets.GetTargetOffsets();
                                if (targetOffsets.Length > 1)
                                {
                                    foreach (var target in targetOffsets)
                                    {
                                        SetupBasicBlock(
                                            visited,
                                            current,
                                            stackCounter,
                                            target);
                                    }
                                }
                                else
                                {
                                    SetupBasicBlock(
                                        visited,
                                        current,
                                        stackCounter,
                                        targetOffsets[0]);
                                }
                            }
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Wires all terminators and connects all basic blocks.
            /// </summary>
            private void WireBlocks()
            {
                foreach (var entry in successorMapping)
                {
                    var block = entry.Key;
                    var terminatorBuilder = block.Builder.CreateBuilderTerminator(
                        entry.Value.Count);
                    foreach (var target in entry.Value)
                        terminatorBuilder.Add(target.BasicBlock);
                    terminatorBuilder.Seal();
                }

                // Handle blocks without terminator
                foreach (var block in blockMapping.Values)
                {
                    if (!successorMapping.ContainsKey(block))
                        block.Builder.CreateBuilderTerminator(0).Seal();
                }
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated code generator.
            /// </summary>
            public CodeGenerator CodeGenerator { get; }

            /// <summary>
            /// Returns the associated SSA block collection.
            /// </summary>
            public BasicBlockCollection<ReversePostOrder, Forwards> Blocks { get; }

            /// <summary>
            /// Returns the internal method builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns the entry block.
            /// </summary>
            public Block EntryBlock { get; }

            /// <summary>
            /// Resolves the block for the given basic block.
            /// </summary>
            /// <param name="basicBlock">The source basic block.</param>
            /// <returns>The resolved frontend block.</returns>
            public Block this[BasicBlock basicBlock] => basicBlockMapping[basicBlock];

            #endregion
        }
    }
}
