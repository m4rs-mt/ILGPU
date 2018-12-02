// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Block.CFGBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
            internal CFGBuilder(CodeGenerator codeGenerator, Method.Builder methodBuilder)
            {
                Debug.Assert(codeGenerator != null, "Invalid code generator");
                Debug.Assert(methodBuilder != null, "Invalid method builder");

                CodeGenerator = codeGenerator;
                Builder = methodBuilder;

                EntryBlock = AppendBasicBlock(0);
                BuildBasicBlocks();
                methodBuilder.EntryBlock = EntryBlock.BasicBlock;

                var nodeMarker = methodBuilder.Context.NewNodeMarker();
                SetupBasicBlocks(nodeMarker, EntryBlock, 0);

                WireBlocks();

                Scope = Builder.Method.CreateScope(ScopeFlags.AddAlreadyVisitedNodes);
                CFG = Scope.CreateCFG();

                // Update internal block mapping
                foreach (var cfgNode in CFG)
                {
                    var block = basicBlockMapping[cfgNode.Block];
                    block.CFGNode = cfgNode;
                }
            }

            /// <summary>
            /// Appends a basic block with the given target.
            /// </summary>
            /// <param name="target">The block target.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Block AppendBasicBlock(int target)
            {
                if (!blockMapping.TryGetValue(target, out Block block))
                {
                    var basicBlock = Builder.CreateBasicBlock();
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
                    offsetMapping[instruction.Offset] = i;
                    if (!instruction.IsTerminator)
                        continue;
                    if (instruction.Argument is ILInstructionBranchTargets targets)
                    {
                        foreach (var target in targets.GetTargetOffsets())
                        {
                            if (blockMapping.ContainsKey(target))
                                continue;
                            AppendBasicBlock(target);
                        }
                    }
                }
            }

            /// <summary>
            /// Adds a new successor to the current block.
            /// </summary>
            /// <param name="current">The current block.</param>
            /// <param name="successor">The successor to add to the current block.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AddSuccessor(Block current, Block successor)
            {
                if (!successorMapping.TryGetValue(current, out List<Block> successors))
                {
                    successors = new List<Block>();
                    successorMapping.Add(current, successors);
                }
                Debug.Assert(!successors.Contains(successor), "Invalid successor setup");
                successors.Add(successor);
            }

            /// <summary>
            /// Setups a single basic block.
            /// </summary>
            /// <param name="nodeMarker">The current node marker.</param>
            /// <param name="current">The current block.</param>
            /// <param name="stackCounter">The current stack counter.</param>
            /// <param name="target">The target block.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupBasicBlock(
                NodeMarker nodeMarker,
                Block current,
                int stackCounter,
                int target)
            {
                var targetBlock = blockMapping[target];
                AddSuccessor(current, targetBlock);
                targetBlock.StackCounter = stackCounter;
                var targetIdx = offsetMapping[target];
                SetupBasicBlocks(nodeMarker, targetBlock, targetIdx);
            }

            /// <summary>
            /// Setups all basic blocks (fills in the required information).
            /// </summary>
            /// <param name="nodeMarker">The current node marker.</param>
            /// <param name="current">The current block.</param>
            /// <param name="instructionIdx">The starting instruction index.</param>
            private void SetupBasicBlocks(
                NodeMarker nodeMarker,
                Block current,
                int instructionIdx)
            {
                if (!current.BasicBlock.Mark(nodeMarker))
                    return;

                var disassembledMethod = CodeGenerator.DisassembledMethod;
                current.InstructionOffset = instructionIdx;
                var stackCounter = current.StackCounter;
                for (int e = disassembledMethod.Count; instructionIdx < e; ++instructionIdx)
                {
                    var instruction = disassembledMethod[instructionIdx];
                    // Handle implicit cases: jumps to blocks without a jump instruction
                    if (blockMapping.TryGetValue(instruction.Offset, out Block other) &&
                        current != other)
                    {
                        // Wire current and new block
                        AddSuccessor(current, other);
                        other.StackCounter = stackCounter;
                        SetupBasicBlocks(nodeMarker, other, instructionIdx);
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
                                // Create appropriate temp targets
                                var targetOffsets = targets.GetTargetOffsets();
                                if (targetOffsets.Length > 1)
                                {
                                    foreach (var target in targetOffsets)
                                        SetupBasicBlock(nodeMarker, current, stackCounter, target);
                                }
                                else
                                {
                                    SetupBasicBlock(nodeMarker, current, stackCounter, targetOffsets[0]);
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
                    var successorBuilder = ImmutableArray.CreateBuilder<BasicBlock>(
                        entry.Value.Count);
                    foreach (var target in entry.Value)
                        successorBuilder.Add(target.BasicBlock);

                    var successors = successorBuilder.MoveToImmutable();
                    var block = entry.Key;
                    block.Builder.CreateBuilderTerminator(successors);
                }

                // Handle blocks without terminator
                foreach (var block in blockMapping.Values)
                {
                    if (!successorMapping.ContainsKey(block))
                        block.Builder.CreateBuilderTerminator(ImmutableArray<BasicBlock>.Empty);
                }
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated code generator.
            /// </summary>
            public CodeGenerator CodeGenerator { get; }

            /// <summary>
            /// Returns the associated scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the associated CFG.
            /// </summary>
            public CFG CFG { get; }

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
            public Block this[BasicBlock basicBlock] =>
                basicBlockMapping[basicBlock];

            #endregion
        }
    }
}
