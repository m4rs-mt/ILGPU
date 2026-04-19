// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Block.CFGBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues.Construction;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

partial class Block
{
    /// <summary>
    /// Constructs CFGs out of disassembled methods.
    /// </summary>
    internal sealed class CFGBuilder
    {
        #region Nested Types

        /// <summary>
        /// Registers instruction offset mappings.
        /// </summary>
        /// <param name="builder">The parent builder instance.</param>
        /// <param name="instructionIndex">
        /// The current instruction index to map to.
        /// </param>
        private readonly struct RegisterOffsetMapping(
            CFGBuilder builder,
            int instructionIndex) :
            IILInstructionOffsetOperation
        {
            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public CFGBuilder Builder { get; } = builder;

            /// <summary>
            /// Returns the parent instruction index.
            /// </summary>
            public int InstructionIndex { get; } = instructionIndex;

            /// <summary>
            /// Registers the given instruction offset.
            /// </summary>
            public void Apply(ILInstruction instruction, int offset) =>
                Builder._offsetMapping[offset] = InstructionIndex;
        }

        #endregion

        #region Instance

        private readonly Dictionary<int, int> _offsetMapping = [];
        private readonly Dictionary<int, Block> _blockMapping = [];
        private readonly Dictionary<BasicBlock, Block> _basicBlockMapping =
            new(new Value.Comparer());
        private readonly Dictionary<Block, List<Block>> _successorMapping = [];

        /// <summary>
        /// Constructs a new CFG builder.
        /// </summary>
        /// <param name="codeGenerator">The current code generator.</param>
        /// <param name="methodBuilder">The current method builder.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal CFGBuilder(
            CodeGenerator codeGenerator,
            MethodBuilder methodBuilder)
        {
            CodeGenerator = codeGenerator;
            Builder = methodBuilder;

            var mainEntry = methodBuilder.EntryBuilder;
            EntryBlock = new Block(codeGenerator, mainEntry)
            {
                InstructionCount = 0
            };
            _basicBlockMapping.Add(EntryBlock.BasicBlock, EntryBlock);

            // Create a temporary entry block to ensure that we have a single entry
            // block without any predecessors in all cases
            var internalEntryBlock = new Block(
                codeGenerator,
                methodBuilder.CreateBasicBlock(
                    mainEntry.BasicBlock.Location,
                    mainEntry.BasicBlock.Name));
            _blockMapping.Add(0, internalEntryBlock);
            _basicBlockMapping.Add(internalEntryBlock.BasicBlock, internalEntryBlock);
            BuildBasicBlocks();

            var visited = new HashSet<Block>();
            SetupBasicBlocks(visited, internalEntryBlock, 0);
            WireBlocks();

            // Wire the main entry block with the actual entry block
            mainEntry.CreateUnconditionalTermination(
                internalEntryBlock.BasicBlock);

            // Update control-flow structure to refresh all successor/predecessor
            // edge relations
            Blocks = mainEntry.BasicBlock.TraverseToCollection<
                ReversePostOrder<BasicBlock>,
                BasicBlock.SuccessorsProvider<Forwards>,
                Forwards>(visited.Count);

            // Connect predecessors for SSA builder
            foreach (var block in Blocks)
                block.SetupPredecessors();
        }

        /// <summary>
        /// Appends a basic block with the given target.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The block target.</param>
        private Block AppendBasicBlock(Location location, int target)
        {
            if (!_blockMapping.TryGetValue(target, out Block? block))
            {
                var basicBlock = Builder.CreateBasicBlock(location);
                block = new Block(CodeGenerator, basicBlock);
                _blockMapping.Add(target, block);
                _basicBlockMapping.Add(block.BasicBlock, block);
            }
            return block;
        }

        /// <summary>
        /// Build all required basic blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
                        if (_blockMapping.ContainsKey(target))
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
        private void AddSuccessor(Block current, Block successor)
        {
            if (!_successorMapping.TryGetValue(current, out List<Block>? successors))
            {
                successors = new List<Block>();
                _successorMapping.Add(current, successors);
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
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void SetupBasicBlock(
            HashSet<Block> visited,
            Block current,
            int stackCounter,
            int target)
        {
            var targetBlock = _blockMapping[target];
            AddSuccessor(current, targetBlock);
            targetBlock.StackCounter = stackCounter;
            var targetIdx = _offsetMapping[target];
            SetupBasicBlocks(visited, targetBlock, targetIdx);
        }

        /// <summary>
        /// Setups all basic blocks (fills in the required information).
        /// </summary>
        /// <param name="visited">The set of visited blocks.</param>
        /// <param name="current">The current block.</param>
        /// <param name="instructionIdx">The starting instruction index.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
                if (_blockMapping.TryGetValue(instruction.Offset, out Block? other) &&
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
                    var (popCount, pushCount) = ILInstructionTypes.GetPushPopBehavior(
                        instruction);
                    stackCounter += pushCount - popCount;
                    Debug.Assert(stackCounter >= 0, "Invalid stack counter");
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
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void WireBlocks()
        {
            foreach (var entry in _successorMapping)
            {
                var block = entry.Key;
                var targets = new BasicBlock[entry.Value.Count];
                for (int i = 0; i < targets.Length; ++i)
                    targets[i] = entry.Value[i].BasicBlock;
                block.Builder.CreatePendingTermination(targets);
            }

            // Handle blocks without terminator
            foreach (var block in _blockMapping.Values)
            {
                if (!_successorMapping.ContainsKey(block))
                    block.Builder.CreatePendingTermination([]);
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
        public BasicBlockCollection<ReversePostOrder<BasicBlock>, Forwards> Blocks
        {
            get;
        }

        /// <summary>
        /// Returns the internal method builder.
        /// </summary>
        public MethodBuilder Builder { get; }

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public Block EntryBlock { get; }

        /// <summary>
        /// Resolves the block for the given basic block.
        /// </summary>
        /// <param name="basicBlock">The source basic block.</param>
        /// <returns>The resolved frontend block.</returns>
        public Block this[BasicBlock basicBlock] => _basicBlockMapping[basicBlock];

        #endregion
    }
}
