// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Movement.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PreDominators = ILGPU.IR.Analyses.Dominators<
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using PostDominators = ILGPU.IR.Analyses.Dominators<
    ILGPU.IR.Analyses.ControlFlowDirection.Backwards>;
using System;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Analyses.ControlFlowDirection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents a scope in which values can be moved around.
    /// </summary>
    public interface IMovementScope
    {
        /// <summary>
        /// Tries to find the first value of the given type that fulfills the given
        /// predicate in the given block.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="basicBlock">The basic block to look into.</param>
        /// <param name="predicate">The predicate.</param>
        /// <param name="entry">
        /// The result pair consisting of a value index and the matched value itself.
        /// </param>
        /// <returns>True, if a value could be matched.</returns>
        bool TryFindFirstValueOf<T>(
            BasicBlock basicBlock,
            Predicate<T> predicate,
            out (int Index, T Value) entry)
            where T : Value;
    }

    /// <summary>
    /// Tracks and validates potential movement of values with side effects to different
    /// blocks. Reasoning about movement of side effect values makes several program
    /// analyses significantly smarter and more aggressive.
    /// </summary>
    /// <typeparam name="TScope">
    /// The movement scope in which values can be moved around.
    /// </typeparam>
    public class Movement<TScope>
        where TScope : IMovementScope
    {
        #region Static

        /// <summary>
        /// Returns true if a value operating on the current address space can skip a
        /// different value operating on the different address space.
        /// </summary>
        /// <param name="currentAddressSpace">
        /// The address space the current value operates on.
        /// </param>
        /// <param name="addressSpaceToSkip">
        /// The address space the other value operates on.
        /// </param>
        /// <returns>
        /// True, if the other value can be skipped based on the address space.
        /// </returns>
        private static bool CanSkipAddressSpace(
            MemoryAddressSpace currentAddressSpace,
            MemoryAddressSpace addressSpaceToSkip) =>
            currentAddressSpace != MemoryAddressSpace.Generic &&
            currentAddressSpace != addressSpaceToSkip;

        /// <summary>
        /// Returns true if an operation working on the current address space can skip
        /// the given memory value.
        /// </summary>
        /// <param name="currentAddressSpace">
        /// The address space the current value operates on.
        /// </param>
        /// <param name="toSkip">The memory value to skip.</param>
        /// <returns>
        /// True, if the value to skip can be skipped without breaking the semantics of
        /// the program.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanSkip(
            MemoryAddressSpace currentAddressSpace,
            MemoryValue toSkip) =>
            toSkip switch
            {
                Alloca _ => true,
                Load load => CanSkipAddressSpace(
                    currentAddressSpace,
                    load.SourceAddressSpace),
                Store store => CanSkipAddressSpace(
                    currentAddressSpace,
                    store.TargetAddressSpace),
                AtomicValue atomic => CanSkipAddressSpace(
                    currentAddressSpace,
                    atomic.TargetAddressSpace),
                // Barriers, Calls etc.
                _ => false,
            };

        /// <summary>
        /// Returns true if the current memory value can skip the value passed in
        /// <paramref name="toSkip"/>.
        /// </summary>
        /// <param name="memoryValue">The memory value to move.</param>
        /// <param name="toSkip">The value to skip.</param>
        /// <returns>
        /// True, if the current value can skip the other memory value without breaking
        /// the semantics of the program.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanSkip(MemoryValue memoryValue, MemoryValue toSkip) =>
            // Check whether one of the values operates on a different address space or
            // whether they are just loads or allocas that do not modify the contents
            // of memory regions
            memoryValue switch
            {
                Load load => toSkip is Load || CanSkip(load.SourceAddressSpace, toSkip),
                Store store => CanSkip(store.TargetAddressSpace, toSkip),
                AtomicValue atomic => CanSkip(atomic.TargetAddressSpace, toSkip),
                Alloca _ => true,
                // Barriers, Calls etc.
                _ => false,
            };

        #endregion

        #region Instance

        /// <summary>
        /// Maps side effect values to their original source blocks in which they
        /// have been defined
        /// </summary>
        private readonly Dictionary<SideEffectValue, BasicBlock> valueBlocks;

        /// <summary>
        /// Stores all memory values according to the reverse post order.
        /// </summary>
        private readonly List<MemoryValue> values;

        /// <summary>
        /// Maps all memory values to their global indices in the <see cref="values"/>
        /// list determined by a reverse-post-order search.
        /// </summary>
        private readonly Dictionary<MemoryValue, int> valueIndices;

        /// <summary>
        /// Stores all end indices of all blocks pointing to offsets in the
        /// <see cref="values"/> list.
        /// </summary>
        private readonly BasicBlockMap<int> blockRanges;

        /// <summary>
        /// Constructs a new movement analysis.
        /// </summary>
        /// <param name="blocks">The source blocks.</param>
        /// <param name="scope">The parent scope.</param>
        [SuppressMessage(
            "Maintainability",
            "CA1508:Avoid dead conditional code",
            Justification = "There is no dead code in the method below")]
        public Movement(
            in BasicBlockCollection<ReversePostOrder, Forwards> blocks,
            TScope scope)
        {
            // Setup internal mappings to store value indices and block offsets
            values = new List<MemoryValue>(blocks.Count << 1);
            valueBlocks = new Dictionary<SideEffectValue, BasicBlock>(
                values.Capacity);
            valueIndices = new Dictionary<MemoryValue, int>(values.Capacity);
            blockRanges = blocks.CreateMap<int>();

            // Gather all side effect values in the order of their appearance
            foreach (var block in blocks)
            {
                foreach (Value value in block)
                {
                    if (!(value is SideEffectValue sev))
                        continue;

                    valueBlocks[sev] = value.BasicBlock;
                    if (value is MemoryValue memoryValue)
                    {
                        int index = values.Count;
                        values.Add(memoryValue);
                        valueIndices.Add(memoryValue, index);
                    }
                }
                blockRanges[block] = values.Count - 1;
            }

            // Setup all properties and compute dominators and post dominators
            Scope = scope;
            Dominators = blocks.CreateDominators();
            PostDominators = blocks.CreatePostDominators();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying movement scope.
        /// </summary>
        public TScope Scope { get; }

        /// <summary>
        /// Returns the current method.
        /// </summary>
        public Method Method => Dominators.Root.Method;

        /// <summary>
        /// Returns the dominators of the current method.
        /// </summary>
        public PreDominators Dominators { get; }

        /// <summary>
        /// Returns the post dominators of the current method.
        /// </summary>
        public PostDominators PostDominators { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given generic value can be moved within the method.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>
        /// True, if the given value can be moved to a different block.
        /// </returns>
        private bool CanMoveGenericValue(Value value)
        {
            value.Assert(!(value is SideEffectValue));
            return Method == value.Method;
        }

        /// <summary>
        /// Tests whether the given value (with side effects) can be moved to the
        /// specified target block.
        /// </summary>
        /// <param name="value">The value to move to the target block.</param>
        /// <param name="targetBlock">The target block to move the value to.</param>
        /// <returns>True, if we can move the value to the target block.</returns>
        private bool CanMoveSideEffectValue(SideEffectValue value, BasicBlock targetBlock)
        {
            // Check the dominance relation between both blocks
            var sourceBlock = valueBlocks[value];
            bool lower = Dominators.Dominates(sourceBlock, targetBlock);
            // Do not move side effect values into divergent regions
            if (!lower || !PostDominators.Dominates(sourceBlock, targetBlock))
                return false;

            // MemoryValues need specific handling as there might be other operations
            // in between changing the behavior/results of the value
            if (!(value is MemoryValue memoryValue))
                return true;

            // Test whether might miss other memory operations in between
            var valueIndex = valueIndices[memoryValue];

            // Find the first memory value in the target block (if any) to begin with
            int startIndex = blockRanges[targetBlock];
            if (Scope.TryFindFirstValueOf<MemoryValue>(
                targetBlock,
                memValue => !(memValue is Load),
                out var entry))
            {
                startIndex = valueIndices[entry.Value];
            }

            // Check for trivial cases
            if (startIndex == valueIndex)
                return true;

            // Sweep upwards or downwards and check whether we might break the program
            // semantics by violating the order of load/store operations
            int increment = startIndex > valueIndex ? -1 : 1;
            for (int i = startIndex + increment;
                lower && i > valueIndex || i < valueIndex;
                i += increment)
            {
                if (!CanSkip(memoryValue, values[i]))
                    return false;
            }

            // We can safely move the current memory value to the target block
            return true;
        }

        /// <summary>
        /// Returns true if the given value can be moved to a different block. This is
        /// the case if we reach a node without side effects or phi values that
        /// belongs to the same method. Alternatively, this might be a value with
        /// side effects. In this case, we validate if this can change the current
        /// program semantics.
        /// </summary>
        /// <param name="value">The value to be moved to the target block.</param>
        /// <param name="targetBlock">The target movement block.</param>
        /// <returns>
        /// True, if the given value can be moved to the target block given by the
        /// placement entry.
        /// </returns>
        public bool CanMoveTo(Value value, BasicBlock targetBlock) =>
            value switch
            {
                Parameter _ => false,
                SideEffectValue sev => CanMoveSideEffectValue(sev, targetBlock),
                PhiValue _ => false,
                TerminatorValue _ => false,
                _ => CanMoveGenericValue(value),
            };

        #endregion
    }
}
