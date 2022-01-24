// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CodePlacement.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Dominators = ILGPU.IR.Analyses.Dominators<
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using PostDominators = ILGPU.IR.Analyses.Dominators<
    ILGPU.IR.Analyses.ControlFlowDirection.Backwards>;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Specifies the placement mode for a single <see cref="CodePlacement"/>
    /// instance.
    /// </summary>
    public enum CodePlacementMode
    {
        /// <summary>
        /// The default placement mode does not touch any values with side effects.
        /// </summary>
        Default,

        /// <summary>
        /// The aggressive mode allows the movement of values with side effects.
        /// </summary>
        Aggressive,
    }

    /// <summary>
    /// Represents a global code placement phase that moves values as close as possible
    /// to their uses. This minimizes live ranges of all values in the program.
    /// </summary>
    /// <remarks>
    /// This placement transformation should be used in combination with the
    /// <see cref="LoopInvariantCodeMotion"/> transformation to use values out of loops.
    /// </remarks>
    public abstract class CodePlacement : SequentialUnorderedTransformation
    {
        #region Basic Types

        /// <summary>
        /// A single entry during the placement process.
        /// </summary>
        public readonly struct PlacementEntry
        {
            /// <summary>
            /// Constructs a new placement entry.
            /// </summary>
            /// <param name="value">The current value to place.</param>
            /// <param name="block">The target block.</param>
            public PlacementEntry(Value value, BasicBlock block)
            {
                value.AssertNotNull(value);
                value.AssertNotNull(block);

                Value = value;
                Block = block;
            }

            /// <summary>
            /// The value to be placed.
            /// </summary>
            public Value Value { get; }

            /// <summary>
            /// The intended initial basic block.
            /// </summary>
            public BasicBlock Block { get; }

            /// <summary>
            /// Returns the string representation of this entry for debugging purposes.
            /// </summary>
            public override string ToString() => $"{Value} @ {Block}";
        }

        /// <summary>
        /// An abstract mover that validates the movement of placement entries.
        /// </summary>
        public interface IMover
        {
            /// <summary>
            /// Returns true whether the given value can be moved to the desired target
            /// block.
            /// </summary>
            /// <param name="entry">
            /// The input entry consisting of the value to move and its target block to
            /// move the value to.
            /// </param>
            /// <returns>
            /// True, if the given entry is valid, and thus, the value can be moved.
            /// </returns>
            bool CanMove(in PlacementEntry entry);
        }

        /// <summary>
        /// An abstract strategy that describes how operands of values are scheduled.
        /// </summary>
        public interface IPlacementStrategy
        {
            /// <summary>
            /// Initializes this placement strategy.
            /// </summary>
            /// <param name="capacity">The internal stack/queue capacity.</param>
            void Init(int capacity);

            /// <summary>
            /// Returns the number of placement entries to process.
            /// </summary>
            int Count { get; }

            /// <summary>
            /// Pushes the next placement entry.
            /// </summary>
            void Push(in PlacementEntry entry);

            /// <summary>
            /// Pops the next placement entry to process.
            /// </summary>
            PlacementEntry Pop();

            /// <summary>
            /// Enqueues all child values of the given placement entry.
            /// </summary>
            /// <param name="mover">The current mover instance.</param>
            /// <param name="entry">The entry to enqueue all children for.</param>
            void EnqueueChildren<TMover>(in TMover mover, in PlacementEntry entry)
                where TMover : struct, IMover;
        }

        #endregion

        #region Strategies

        /// <summary>
        /// Groups operands into "logical computation/processing groups" to keep them in
        /// an ascending order with respect to their parent target operations.
        /// </summary>
        public struct GroupOperands : IPlacementStrategy
        {
            /// <summary>
            /// The stack of all remaining entries to be placed.
            /// </summary>
            private Stack<PlacementEntry> toPlace;

            /// <summary>
            /// Initializes the internal stack with the given capacity.
            /// </summary>
            /// <param name="capacity">The initial stack capacity.</param>
            public void Init(int capacity) =>
                toPlace = new Stack<PlacementEntry>(capacity);

            /// <summary>
            /// Returns the number of elements on the stack.
            /// </summary>
            public readonly int Count => toPlace.Count;

            /// <summary>
            /// Pushes the given placement entry to the stack.
            /// </summary>
            public readonly void Push(in PlacementEntry entry) => toPlace.Push(entry);

            /// <summary>
            /// Pops the next placement entry from the stack.
            /// </summary>
            public readonly PlacementEntry Pop() => toPlace.Pop();

            /// <summary>
            /// Pushes all operands from right to left onto the internal processing stack.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void EnqueueChildren<TMover>(
                in TMover mover,
                in PlacementEntry entry)
                where TMover : struct, IMover
            {
                // Push all child values
                var value = entry.Value;
                for (int i = 0, e = value.Count; i < e; ++i)
                {
                    Value node = value[i];
                    // Skip values that cannot be moved here
                    var nodeEntry = new PlacementEntry(node, entry.Block);
                    if (!mover.CanMove(nodeEntry))
                        continue;

                    // Add the node for processing but use the current block
                    Push(nodeEntry);
                }
            }
        }

        /// <summary>
        /// Minimizes the distances between all operands.
        /// </summary>
        public struct MinimizeOperandDistances : IPlacementStrategy
        {
            /// <summary>
            /// The queue of all remaining entries to be placed.
            /// </summary>
            private Queue<PlacementEntry> toPlace;

            /// <summary>
            /// Initializes the internal queue with the given capacity.
            /// </summary>
            /// <param name="capacity">The initial queue capacity.</param>
            public void Init(int capacity) =>
                toPlace = new Queue<PlacementEntry>(capacity);

            /// <summary>
            /// Returns the number of elements in the queue.
            /// </summary>
            public readonly int Count => toPlace.Count;

            /// <summary>
            /// Enqueues the given placement entry into the queue.
            /// </summary>
            public readonly void Push(in PlacementEntry entry) => toPlace.Enqueue(entry);

            /// <summary>
            /// Dequeues the next placement entry from the queue.
            /// </summary>
            public readonly PlacementEntry Pop() => toPlace.Dequeue();

            /// <summary>
            /// Enqueues all operands from right to left into the internal queue.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void EnqueueChildren<TMover>(
                in TMover mover,
                in PlacementEntry entry)
                where TMover : struct, IMover
            {
                // Push all child values
                var value = entry.Value;
                for (int i = 0, e = value.Count; i < e; ++i)
                {
                    Value node = value[i];
                    // Skip values that cannot be moved here
                    var nodeEntry = new PlacementEntry(node, entry.Block);
                    if (!mover.CanMove(nodeEntry))
                        continue;

                    // Add the node for processing but use the current block
                    Push(nodeEntry);
                }
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new instance while specifying the placement mode.
        /// </summary>
        /// <param name="mode">The mode to use.</param>
        protected CodePlacement(CodePlacementMode mode)
        {
            Mode = mode;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current placement mode.
        /// </summary>
        public CodePlacementMode Mode { get; }

        #endregion
    }

    /// <summary>
    /// Represents a global code placement phase that moves values a close as possible
    /// to their uses. This minimizes live ranges of all values in the program.
    /// </summary>
    /// <remarks>
    /// This placement transformation should be used in combination with the
    /// <see cref="LoopInvariantCodeMotion"/> transformation to use values out of loops.
    /// </remarks>
    public class CodePlacement<TStrategy> : CodePlacement
        where TStrategy : struct, CodePlacement.IPlacementStrategy
    {
        #region Placer Modes

        /// <summary>
        /// An abstract placer mode that setups insert position for given blocks.
        /// </summary>
        private interface IPlacerMode
        {
            /// <summary>
            /// Gets the insert position for the given builder.
            /// </summary>
            /// <param name="builder">The current block builder.</param>
            int GetInsertPosition(BasicBlock.Builder builder);

            /// <summary>
            /// Setups the value insert position for the given block builder.
            /// </summary>
            /// <param name="builder">The current block builder.</param>
            void SetupInsertPosition(BasicBlock.Builder builder);
        }

        /// <summary>
        /// Appends values by inserting them behind all phi values.
        /// </summary>
        private readonly struct AppendMode : IPlacerMode
        {
            public AppendMode(in BasicBlockMap<(Value[], int)> blocks)
            {
                Blocks = blocks;
            }

            /// <summary>
            /// Returns the current basic block map.
            /// </summary>
            private BasicBlockMap<(Value[] Values, int NumPhis)> Blocks { get; }

            /// <summary>
            /// Determines the insert position according to the number of detected phi
            /// values in each block.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            public int GetInsertPosition(BasicBlock.Builder builder) =>
                Blocks[builder.BasicBlock].NumPhis;

            /// <summary>
            /// Setups the insert position according to the number of detected phi
            /// values in each block.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            public void SetupInsertPosition(BasicBlock.Builder builder) =>
                builder.InsertPosition = GetInsertPosition(builder);
        }

        /// <summary>
        /// Inserts all values at the beginning of each block.
        /// </summary>
        private readonly struct InsertMode : IPlacerMode
        {
            /// <summary>
            /// Setups the insert position to point to the start of the block.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            public int GetInsertPosition(BasicBlock.Builder builder) => 0;

            /// <summary>
            /// Setups the insert position to point to the start of the block.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            public void SetupInsertPosition(BasicBlock.Builder builder) =>
                builder.SetupInsertPositionToStart();
        }

        #endregion

        #region Movement and Placement

        /// <summary>
        /// Tracks and validates the movement of values with side effects to different
        /// blocks. Enabling movement of side effect values makes the whole placement
        /// algorithm significantly more aggressive.
        /// </summary>
        private readonly struct Mover : IMover
        {
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
            /// Constructs a new mover.
            /// </summary>
            /// <param name="builder">The parent builder.</param>
            /// <param name="mode">The current placement mode.</param>
            [SuppressMessage(
                "Maintainability",
                "CA1508:Avoid dead conditional code",
                Justification = "There is no dead code in the method below")]
            public Mover(Method.Builder builder, CodePlacementMode mode)
            {
                // Setup internal mappings to store value indices and block offsets
                var blocks = builder.SourceBlocks;
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
                Builder = builder;
                Dominators = builder.SourceBlocks.CreateDominators();
                PostDominators = builder.SourceBlocks.CreatePostDominators();
                Mode = mode;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the underlying method builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns the dominators of the current method.
            /// </summary>
            public Dominators Dominators { get; }

            /// <summary>
            /// Returns the post dominators of the current method.
            /// </summary>
            private PostDominators PostDominators { get; }

            /// <summary>
            /// Returns the current placement mode.
            /// </summary>
            private CodePlacementMode Mode { get; }

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
                return Builder.Method == value.Method;
            }

            /// <summary>
            /// Tests whether the given value (with side effects) can be moved to the
            /// specified target block.
            /// </summary>
            /// <param name="value">The value to move to the target block.</param>
            /// <param name="targetBlock">The target block to move the value to.</param>
            /// <returns>True, if we can move the value to the target block.</returns>
            private bool CanMoveSideEffectValue(
                SideEffectValue value,
                BasicBlock targetBlock)
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
                var targetBuilder = Builder[targetBlock];

                // Find the first memory value in the target block (if any) to begin with
                int startIndex = blockRanges[targetBlock];
                if (targetBuilder.TryFindFirstValueOf<MemoryValue>(
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
                bool skipLoads = memoryValue is Load;
                int increment = startIndex > valueIndex ? -1 : 1;
                for (int i = startIndex + increment;
                    lower && i > valueIndex || i < valueIndex;
                    i += increment)
                {
                    // Ignore loads
                    if (skipLoads && values[i] is Load)
                        continue;

                    // Reject other modifications in between
                    // TODO: make this smarter (e.g. based on alias information)
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
            /// <param name="entry">The current placement entry.</param>
            /// <returns>
            /// True, if the given value can be moved to the target block given by the
            /// placement entry.
            /// </returns>
            public bool CanMove(in PlacementEntry entry) =>
                entry.Value switch
                {
                    Parameter _ => false,
                    SideEffectValue sev =>
                        Mode == CodePlacementMode.Aggressive &&
                        CanMoveSideEffectValue(sev, entry.Block),
                    PhiValue _ => false,
                    TerminatorValue _ => false,
                    _ => CanMoveGenericValue(entry.Value),
                };

            #endregion
        }

        /// <summary>
        /// An internal placement helper structure that manages all values to be placed.
        /// </summary>
        private struct Placer
        {
            /// <summary>
            /// The strategy instance to manage the placement order.
            /// </summary>
            private TStrategy strategy;

            /// <summary>
            /// The set of all values that have been placed.
            /// </summary>
            private readonly HashSet<Value> placed;

            /// <summary>
            /// Constructs a new placer instance.
            /// </summary>
            /// <param name="mover">The parent mover.</param>
            public Placer(Mover mover)
            {
                Mover = mover;
                strategy = new TStrategy();
                strategy.Init(mover.Dominators.CFG.Count);
                placed = new HashSet<Value>();
            }

            /// <summary>
            /// Returns the parent mover.
            /// </summary>
            private Mover Mover { get; }

            /// <summary>
            /// Returns the parent method builder.
            /// </summary>
            private Method.Builder Builder => Mover.Builder;

            /// <summary>
            /// Returns the parent dominators.
            /// </summary>
            private Dominators Dominators => Mover.Dominators;

            /// <summary>
            /// Returns true if the given value has been placed.
            /// </summary>
            public bool IsPlaced(Value value) => placed.Contains(value);

            /// <summary>
            /// Places this value and all of its dependencies recursively.
            /// </summary>
            /// <param name="entry">The placement entry to place.</param>
            /// <param name="mode">The current placing mode.</param>
            public void PlaceRecursive<TMode>(in PlacementEntry entry, in TMode mode)
                where TMode : struct, IPlacerMode
            {
                // Check whether we have to skip the current value
                if (!IsPlaced(entry.Value))
                    PlaceDirect(entry, mode);

                // Place all children
                strategy.EnqueueChildren(Mover, entry);
                while (strategy.Count > 0)
                {
                    // Get the next value to be placed
                    var current = strategy.Pop();

                    // Check whether we have to skip this value
                    if (placed.Contains(current.Value) || !TryPlace(ref current))
                    {
                        // This value cannot be placed since all of its uses are not
                        // placed yet or this value needs to be skipped anyway.
                        continue;
                    }

                    // Place this value
                    PlaceDirect(current, mode);
                }
            }

            /// <summary>
            /// Tries to place the given entry while determining a proper placement block.
            /// </summary>
            /// <param name="entry">The placement entry.</param>
            /// <returns>
            /// True, if the value could be placed given its operand conditions.
            /// </returns>
            private bool TryPlace(ref PlacementEntry entry)
            {
                // Test whether can actually place the current value
                if (!CanPlace(entry.Value) || !Mover.CanMove(entry))
                    return false;

                // Determine the actual placement block
                var placementBlock = Dominators.GetImmediateCommonDominatorOfUses(
                    entry.Block,
                    entry.Value.Uses);

                // Push all child values
                entry = new PlacementEntry(entry.Value, placementBlock);
                strategy.EnqueueChildren(Mover, entry);

                return true;
            }

            /// <summary>
            /// Places a value directly without placing its operands.
            /// </summary>
            /// <param name="entry">The placement entry to be placed.</param>
            /// <param name="mode">The current placing mode.</param>
            public void PlaceDirect<TMode>(in PlacementEntry entry, TMode mode)
                where TMode : struct, IPlacerMode
            {
                // Mark the current value as placed
                var value = entry.Value;
                bool hasNotBeenPlaced = placed.Add(value);
                value.Assert(hasNotBeenPlaced);

                // Skip terminator values
                if (value is TerminatorValue)
                    return;

                // Move the value to the determined placement block
                value.BasicBlock = entry.Block;
                var blockBuilder = Builder[entry.Block];
                mode.SetupInsertPosition(blockBuilder);
                blockBuilder.Add(value);
            }

            /// <summary>
            /// Returns true if the current value can be placed now by checking all of
            /// its uses.
            /// </summary>
            /// <param name="value">The value to be placed.</param>
            /// <returns>True, if the value could be placed.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CanPlace(Value value)
            {
                // Check of all of its uses
                foreach (Value use in value.Uses)
                {
                    if (!placed.Contains(use) && !(use is PhiValue))
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Gathers phi values in all blocks and clears all block-internal lists.
        /// </summary>
        private readonly struct GatherValuesInBlock :
            IBasicBlockMapValueProvider<(Value[] Values, int NumPhis)>
        {
            public GatherValuesInBlock(Method.Builder builder, List<PhiValue> phiValues)
            {
                Builder = builder;
                PhiValues = phiValues;
            }

            /// <summary>
            /// Returns the parent method builder.
            /// </summary>
            private Method.Builder Builder { get; }

            /// <summary>
            /// Returns the list of all phi values.
            /// </summary>
            private List<PhiValue> PhiValues { get; }

            /// <summary>
            /// Determines an array of all values of the given block in post order.
            /// </summary>
            /// <param name="block">The current block.</param>
            /// <param name="traversalIndex">The current traversal index.</param>
            public (Value[], int) GetValue(BasicBlock block, int traversalIndex)
            {
                // Track the number of phi values in this block
                int numPhis = 0;

                // Build an array of values to process
                var values = new Value[block.Count];

                // "Append" all values in reversed order
                for (int i = 0, e = block.Count; i < e; ++i)
                {
                    Value value = block[e - 1 - i];
                    if (value is PhiValue phiValue)
                    {
                        PhiValues.Add(phiValue);
                        ++numPhis;
                    }
                    values[i] = value;
                }

                // Clear the lists of this block
                Builder[block].ClearLists();

                return (values, numPhis);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new instance while specifying the placement mode.
        /// </summary>
        /// <param name="mode">The mode to use.</param>
        public CodePlacement(CodePlacementMode mode)
            : base(mode)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Applies an accelerator-specialization transformation.
        /// </summary>
        protected override bool PerformTransformation(
            IRContext context,
            Method.Builder builder)
        {
            // Initialize new mover and placer instances
            var mover = new Mover(builder, Mode);
            var placer = new Placer(mover);

            // Iterate over all values and determine their actual position in post order
            var blocks = builder.SourceBlocks.AsOrder<PostOrder>();

            // Gather all values in the whole function to be placed
            var phiValues = new List<PhiValue>(blocks.Count);
            var blockMapping = blocks.CreateMap(
                new GatherValuesInBlock(
                    builder,
                    phiValues));

            // Do not move phi values to different blocks
            foreach (var phiValue in phiValues)
            {
                var phiEntry = new PlacementEntry(phiValue, phiValue.BasicBlock);
                placer.PlaceDirect(phiEntry, new InsertMode());
            }

            // Place all terminators first
            var appendMode = new AppendMode(blockMapping);
            foreach (var block in blocks)
            {
                var terminatorEntry = new PlacementEntry(block.Terminator, block);
                placer.PlaceRecursive(terminatorEntry, appendMode);
            }

            // Place all phi values recursively
            foreach (var phiValue in phiValues)
            {
                var phiEntry = new PlacementEntry(phiValue, phiValue.BasicBlock);
                placer.PlaceRecursive(phiEntry, appendMode);
            }

            // Place all values that require explicit placement operations
            foreach (var block in blocks)
            {
                var blockEntry = blockMapping[block];

                // Place all values that have to be placed recursively
                foreach (var value in blockEntry.Values)
                {
                    // Check whether we have to place the current value at this time
                    if (!(value is SideEffectValue))
                        continue;

                    // Force a placement of these values as they will have either
                    // side effects or should be placed here to minimize live spans of
                    // values.
                    var valueEntry = new PlacementEntry(value, block);
                    placer.PlaceRecursive(valueEntry, appendMode);
                }
            }

#if DEBUG
            // Once we have placed all live values, all remaining values which have not
            // been placed yet are dead. However, these values can lead to invalid
            // results of this transformation.
            foreach (var block in blocks)
            {
                var (values, _) = blockMapping[block];
                foreach (var value in values)
                    value.Assert(placer.IsPlaced(value));
            }
#endif

            return true;
        }

        #endregion
    }
}
