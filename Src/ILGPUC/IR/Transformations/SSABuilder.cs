// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SSABuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using BlockCollection = ILGPUC.IR.MethodValues.BasicBlockCollection<
    ILGPUC.IR.Analyses.ReversePostOrder<ILGPUC.IR.MethodValues.BasicBlock>,
    ILGPUC.IR.MethodValues.Forwards>;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Constructs IR nodes that are in SSA form.
/// </summary>
/// <typeparam name="TVariable">The variable type.</typeparam>
/// <remarks>Members of this class are not thread safe.</remarks>
sealed class SSABuilder<TVariable> where TVariable : notnull
{
    #region Nested Types

    /// <summary>
    /// Provides marker values.
    /// </summary>
    /// <param name="markerValue">The current marker value.</param>
    internal ref struct MarkerProvider(int markerValue)
    {
        /// <summary>
        /// Returns the current marker value.
        /// </summary>
        public int MarkerValue { get; private set; } = markerValue;

        /// <summary>
        /// Creates a new marker value.
        /// </summary>
        /// <returns>The created marker value.</returns>
        public int CreateMarker() => ++MarkerValue;

        /// <summary>
        /// Applies the internal marker value to the given target.
        /// </summary>
        /// <param name="targetMarkerValue">
        /// The target marker value reference.
        /// </param>
        public readonly void Apply(ref int targetMarkerValue) =>
            targetMarkerValue = MarkerValue;
    }

    /// <summary>
    /// Represents a basic block during cps construction.
    /// </summary>
    internal sealed class ValueContainer
    {
        #region Nested Types

        /// <summary>
        /// Represents an incomplete phi parameter that has to be
        /// completed by adding its required operands later on.
        /// </summary>
        /// <param name="variableRef">The referenced variable.</param>
        /// <param name="phiBuilder">The phi builder.</param>
        private readonly struct IncompletePhi(
            TVariable variableRef,
            PhiValue.Builder phiBuilder)
        {
            /// <summary>
            /// Returns the associated variable ref.
            /// </summary>
            public TVariable VariableRef { get; } = variableRef;

            /// <summary>
            /// Returns the associated phi builder.
            /// </summary>
            public PhiValue.Builder PhiBuilder { get; } = phiBuilder;

            /// <summary>
            /// Returns the type of the underlying phi node.
            /// </summary>
            public TypeValue PhiType => PhiBuilder.Type;

            /// <summary>
            /// Returns the location of the phi node.
            /// </summary>
            public Location Location => PhiBuilder.PhiValue.Location;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Represents the internal marker value.
        /// </summary>
        private int _markerValue;

        /// <summary>
        /// Value cache for SSA GetValue and SetValue functionality.
        /// </summary>
        private readonly Dictionary<TVariable, Value> _values = new(8);

        /// <summary>
        /// Container for incomplete "phis" that have to be wired during block
        /// sealing.
        /// </summary>
        private readonly Dictionary<TVariable, IncompletePhi> _incompletePhis = new(2);

        /// <summary>
        /// Constructs a new SSA block.
        /// </summary>
        /// <param name="parent">The associated parent builder.</param>
        /// <param name="block">The current block.</param>
        /// <param name="blockTransformer">
        /// The block transformer to map existing blocks to different blocks.
        /// </param>
        internal ValueContainer(
            SSABuilder<TVariable> parent,
            BasicBlock block,
            Func<BasicBlock, BasicBlock> blockTransformer)
        {
            Parent = parent;
            Block = block;
            Builder = parent.MethodBuilder[blockTransformer(block)];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent SSA builder.
        /// </summary>
        public SSABuilder<TVariable> Parent { get; }

        /// <summary>
        /// Returns the associated basic block.
        /// </summary>
        public BasicBlock Block { get; }

        /// <summary>
        /// Returns the associated block builder.
        /// </summary>
        public BasicBlockBuilder Builder { get; }

        /// <summary>
        /// Returns True if this block is sealed.
        /// </summary>
        public bool IsSealed { get; private set; }

        /// <summary>
        /// Returns true if this block can be sealed.
        /// </summary>
        public bool CanSeal
        {
            get
            {
                if (IsSealed)
                    return false;
                foreach (var predecessor in Block.Predecessors)
                {
                    var valueContainer = Parent[predecessor];
                    if (!valueContainer.IsProcessed &&
                        !valueContainer.IsSealed)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Returns true if this block has been processed.
        /// </summary>
        public bool IsProcessed { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Marks the current block with the new marker value.
        /// </summary>
        /// <param name="newMarker">The new value to apply.</param>
        /// <returns>
        /// True, if the old marker was not equal to the new marker
        /// (the block was not marked with the new marker value).
        /// </returns>
        public bool Mark(int newMarker) =>
            Interlocked.Exchange(ref _markerValue, newMarker) != newMarker;

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(TVariable var, Value value) =>
            _values[var] = value;

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        /// <returns>The value of the given variable.</returns>
        public Value GetValue(TVariable var, ref MarkerProvider markerProvider) =>
            BeginGetValue(var, ref markerProvider) ??
            CompleteLookups(var, ref markerProvider);

        /// <summary>
        /// Removes the value of the given variable.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        public void RemoveValue(TVariable var) => _values.Remove(var);

        /// <summary>
        /// Peeks a value in this block and its predecessors. This method only
        /// retrieves a value from a predecessor but does not build any phi nodes.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="marker">The current marker to break cycles.</param>
        /// <returns>
        /// The first value found in a depth-first search through the predecessors.
        /// </returns>
        /// <remarks>
        /// The search keeps its pending blocks on the heap instead of the stack,
        /// as chains of predecessors can be thousands of blocks long.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private Value? PeekValue(TVariable var, int marker)
        {
            var frames = Parent._peekFrames;
            Block.Assert(frames.Count == 0);
            try
            {
                ValueContainer? container = this;
                while (container is not null)
                {
                    // Visit the block once per marker
                    if (container.IsProcessed && container.Mark(marker))
                    {
                        if (container._values.TryGetValue(var, out Value? value))
                            return value;
                        frames.Add((container, 0));
                    }

                    // Continue with the next predecessor of the innermost block
                    // that has not been searched yet
                    container = null;
                    while (frames.Count > 0 && container is null)
                    {
                        var (current, index) = frames[^1];
                        var predecessors = current.Block.Predecessors;
                        if (index < predecessors.Length)
                        {
                            frames[^1] = (current, index + 1);
                            container = Parent[predecessors[index]];
                        }
                        else
                        {
                            frames.RemoveAt(frames.Count - 1);
                        }
                    }
                }
                return null;
            }
            finally
            {
                frames.Clear();
            }
        }

        /// <summary>
        /// Returns the type of a new phi for the given variable: the type of the
        /// first value found in a predecessor. If that first value is a bool, the
        /// type is widened to the widest integer type of the other predecessor
        /// values that are not the constants 0 or 1. IL stack slots hold
        /// comparison results as bools, and joining them with integers must not
        /// narrow the integers to bools.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        /// <returns>The phi type.</returns>
        private TypeValue GetPhiType(TVariable var, ref MarkerProvider markerProvider)
        {
            var type = PeekValue(var, markerProvider.CreateMarker()).AsNotNull().Type;
            if (type is PointerType)
                return GetPointerPhiType(var, type, ref markerProvider);
            if (type.BasicValueType != BasicValueType.Int1)
                return type;
            foreach (var predecessor in Block.Predecessors)
            {
                var value = Parent[predecessor].PeekValue(
                    var,
                    markerProvider.CreateMarker());

                // Constants 0 and 1 convert to bools without loss, which keeps
                // short-circuit joins on bools
                if (value is PrimitiveValue { Int64Value: 0 or 1 })
                    continue;
                if (value?.Type is PrimitiveType primitiveType &&
                    primitiveType.BasicValueType.IsInt() &&
                    primitiveType.BasicValueType > type.BasicValueType)
                {
                    type = primitiveType;
                }
            }
            return type;
        }

        /// <summary>
        /// Returns the type of a new phi joining pointers. IL stack slots join
        /// typed pointers with untyped ones, such as null pointers and pointers
        /// converted to native integers: the phi points to the element type of
        /// the first typed pointer, into the address space of all pointers that
        /// are not null, or into the generic address space if these differ.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="type">The type of the first value found.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        /// <returns>The phi type.</returns>
        private TypeValue GetPointerPhiType(
            TVariable var,
            TypeValue type,
            ref MarkerProvider markerProvider)
        {
            TypeValue? elementType = null;
            MemoryAddressSpace? addressSpace = null;
            foreach (var predecessor in Block.Predecessors)
            {
                var value = Parent[predecessor].PeekValue(
                    var,
                    markerProvider.CreateMarker());
                if (value is NullValue || value?.Type is not PointerType pointer)
                    continue;
                if (elementType is null or VoidType)
                    elementType = pointer.ElementType;
                addressSpace = addressSpace is null ||
                    addressSpace == pointer.AddressSpace
                    ? pointer.AddressSpace
                    : MemoryAddressSpace.Generic;
            }
            return elementType is null || addressSpace is null
                ? type
                : Builder.ModuleBuilder.CreatePointerType(
                    elementType,
                    addressSpace.Value);
        }

        //
        // Implements an adapted version of the SSA-construction algorithm from the
        // paper: Simple and Efficient Construction of Static Single Assignment Form
        //
        // The paper reads variables recursively, one level per predecessor. Chains
        // of predecessors can be thousands of blocks long, so the lookups that wait
        // for the value of a predecessor are kept as frames on the heap
        // (LookupFrame), and the values, phis and conversions are created in the
        // same order as by the recursive formulation.
        //

        /// <summary>
        /// Starts to look up the value of the given variable in this block.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        /// <returns>
        /// The value of the given variable, or null if its lookup waits for the value
        /// of a predecessor and has been pushed as a frame.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private Value? BeginGetValue(TVariable var, ref MarkerProvider markerProvider)
        {
            if (_values.TryGetValue(var, out Value? value))
                return value;

            // Handle unreachable blocks gracefully: if all predecessor
            // edges were removed by branch folding (e.g. the dead target
            // of a folded delegate-caching brtrue), the variable is
            // undefined. Return a placeholder — the block won't appear
            // in the final module.
            if (Block.Predecessors.Length == 0)
            {
                var undef = Builder.ModuleBuilder.UndefinedValue;
                SetValue(var, undef);
                return undef;
            }

            // The value of a sealed block with a single predecessor is the value
            // of the predecessor
            if (Block.Predecessors.Length == 1 && IsSealed)
            {
                Parent._lookupFrames.Add(new(this, null, setValue: true));
                return null;
            }

            // Insert the actual phi value
            var phiType = GetPhiType(var, ref markerProvider);
            // Let the phi point to the beginning of the current block
            var phiBuilder = Builder.CreatePhi(
                Builder.BasicBlock.Location,
                phiType);
            var phiValue = phiBuilder.PhiValue;
            SetValue(var, phiValue);
            if (IsSealed)
            {
                Parent._lookupFrames.Add(new(this, phiBuilder, setValue: true));
                return null;
            }
            _incompletePhis[var] = new IncompletePhi(var, phiBuilder);
            return phiValue;
        }

        /// <summary>
        /// Completes all pending lookups of the given variable, innermost first.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        /// <returns>The value of the outermost lookup.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private Value CompleteLookups(TVariable var, ref MarkerProvider markerProvider)
        {
            var frames = Parent._lookupFrames;
            try
            {
                // The value of the last completed lookup, which the innermost
                // frame waits for
                Value? value = null;
                while (true)
                {
                    var frame = frames[^1];
                    var container = frame.Container;
                    var predecessors = container.Block.Predecessors;
                    if (frame.PhiBuilder is not { } phiBuilder)
                    {
                        // Wait for the value of the single predecessor
                        value ??= Parent[predecessors[0]].BeginGetValue(
                            var,
                            ref markerProvider);
                        if (value is null)
                            continue;
                    }
                    else
                    {
                        // Wire the value of the current predecessor
                        if (value is not null)
                        {
                            container.AddPhiArgument(
                                phiBuilder,
                                predecessors[frame.ArgumentIndex],
                                value);
                            frames[^1] = frame.NextArgument();
                            value = null;
                            continue;
                        }

                        // Wait for the value of the next predecessor
                        if (frame.ArgumentIndex < predecessors.Length)
                        {
                            value = Parent[predecessors[frame.ArgumentIndex]]
                                .BeginGetValue(var, ref markerProvider);
                            continue;
                        }

                        phiBuilder.PhiValue.Location.Assert(
                            phiBuilder.Count == predecessors.Length);
                        value = phiBuilder.Seal();
                    }

                    // The lookup of this frame is complete
                    if (frame.SetValue)
                        container.SetValue(var, value);
                    frames.RemoveAt(frames.Count - 1);
                    if (frames.Count < 1)
                        return value;
                }
            }
            finally
            {
                frames.Clear();
            }
        }

        /// <summary>
        /// Adds the value of a variable in a predecessor as an argument to the given
        /// phi of this block.
        /// </summary>
        /// <param name="phiBuilder">The phi builder.</param>
        /// <param name="predecessor">The predecessor.</param>
        /// <param name="value">The value in the predecessor.</param>
        private void AddPhiArgument(
            PhiValue.Builder phiBuilder,
            BasicBlock predecessor,
            Value value)
        {
            // Convert the value into the target type
            if (phiBuilder.Type is PrimitiveType primitiveType)
            {
                // Use the predecessor block to convert the value
                value = Parent[predecessor].Builder.CreateConvert(
                    phiBuilder.PhiValue.Location,
                    value,
                    primitiveType).AsNotNull();
            }
            else if (
                phiBuilder.Type is PointerType pointerType &&
                value.Type is PointerType valueType &&
                !valueType.Equals(pointerType) &&
                (value is NullValue ||
                valueType.AddressSpace == pointerType.AddressSpace ||
                pointerType.AddressSpace == MemoryAddressSpace.Generic))
            {
                // Use the predecessor block to cast the pointer
                var builder = Parent[predecessor].Builder;
                value = builder.CreatePointerCast(
                    phiBuilder.PhiValue.Location,
                    builder.CreateAddressSpaceCast(
                        phiBuilder.PhiValue.Location,
                        value,
                        pointerType.AddressSpace),
                    pointerType.ElementType).AsNotNull();
            }

            // Set argument value — transform the predecessor to the
            // current generation. Block.Predecessors are old-gen blocks
            // but the phi lives in a new-gen block.
            phiBuilder.AddArgument(Parent._blockTransformer(predecessor), value);
        }

        /// <summary>
        /// Seals this block (called when all predecessors have been seen) and
        /// wires all (previously un-wired) phi nodes.
        /// </summary>
        /// <param name="markerProvider">A provider of new marker values.</param>
        public void Seal(ref MarkerProvider markerProvider)
        {
            Block.Assert(!IsSealed);
            foreach (var incompletePhi in _incompletePhis.Values)
            {
                Parent._lookupFrames.Add(
                    new(this, incompletePhi.PhiBuilder, setValue: false));
                CompleteLookups(incompletePhi.VariableRef, ref markerProvider);
            }
            IsSealed = true;
            _incompletePhis.Clear();
        }

        /// <summary>
        /// Tries to seal all successor blocks of this one.
        /// </summary>
        /// <param name="markerProvider">A provider of new marker values.</param>
        public void TrySealSuccessors(ref MarkerProvider markerProvider)
        {
            Block.Assert(IsProcessed);
            foreach (var successor in Block.Successors)
            {
                var valueContainer = Parent[successor];
                if (valueContainer.IsProcessed && valueContainer.CanSeal)
                    valueContainer.Seal(ref markerProvider);
            }
        }

        #endregion

        #region Objects

        /// <summary>
        /// Returns the string representation of this block.
        /// </summary>
        /// <returns>The string representation of this block.</returns>
        public override string ToString() => Block.ToString();

        #endregion
    }

    /// <summary>
    /// A lookup of a variable in a block that waits for the value of the variable in
    /// a predecessor.
    /// </summary>
    /// <param name="container">The block of the lookup.</param>
    /// <param name="phiBuilder">
    /// The phi whose arguments are looked up, or null if the block is sealed and has a
    /// single predecessor whose value is the value of the block.
    /// </param>
    /// <param name="setValue">
    /// True if the value of the block is set to the result of the lookup.
    /// </param>
    /// <param name="argumentIndex">
    /// The index of the predecessor whose value is looked up next.
    /// </param>
    private readonly struct LookupFrame(
        ValueContainer container,
        PhiValue.Builder? phiBuilder,
        bool setValue,
        int argumentIndex = 0)
    {
        /// <summary>
        /// Returns the block of the lookup.
        /// </summary>
        public ValueContainer Container { get; } = container;

        /// <summary>
        /// Returns the phi whose arguments are looked up, if any.
        /// </summary>
        public PhiValue.Builder? PhiBuilder { get; } = phiBuilder;

        /// <summary>
        /// Returns true if the value of the block is set to the result.
        /// </summary>
        public bool SetValue { get; } = setValue;

        /// <summary>
        /// Returns the index of the predecessor whose value is looked up next.
        /// </summary>
        public int ArgumentIndex { get; } = argumentIndex;

        /// <summary>
        /// Returns this frame advanced to the next predecessor.
        /// </summary>
        public LookupFrame NextArgument() =>
            new(Container, PhiBuilder, SetValue, ArgumentIndex + 1);
    }

    #endregion

    #region Static

    /// <summary>
    /// Maps blocks to different blocks in case generations do not match or blocks will
    /// be mapped to different target blocks during code generation.
    /// </summary>
    private readonly Func<BasicBlock, BasicBlock> _blockTransformer;

    /// <summary>
    /// Creates a new SSA builder.
    /// </summary>
    /// <param name="methodBuilder">The current method builder.</param>
    /// <param name="blocks">The blocks to use.</param>
    /// <param name="blockTransformer">
    /// The block transformer to map existing blocks to different blocks. Note that this
    /// has to be done during mapping of old blocks to new blocks.
    /// </param>
    /// <returns>The created SSA builder.</returns>
    public static SSABuilder<TVariable> Create(
        MethodBuilder methodBuilder,
        BlockCollection? blocks = null,
        Func<BasicBlock, BasicBlock>? blockTransformer = null)
    {
        var blockCollection = blocks ?? methodBuilder.TraverseToCollection();
        Debug.Assert(blocks.HasValue || blockTransformer is not null);

        blockTransformer ??= static block => block;
        return new(methodBuilder, blockCollection, blockTransformer);
    }

    #endregion

    #region Instance

    private int _markerValue;
    private readonly ValueMap<Method, BasicBlock, ValueContainer> _mapping;

    /// <summary>
    /// The pending lookups of a variable, innermost last.
    /// </summary>
    private readonly List<LookupFrame> _lookupFrames = [];

    /// <summary>
    /// The blocks of a search for a value, innermost last, with the index of the
    /// next predecessor to search.
    /// </summary>
    private readonly List<(ValueContainer Container, int Index)> _peekFrames = [];

    /// <summary>
    /// Constructs a new SSA builder.
    /// </summary>
    /// <param name="methodBuilder">The current method builder.</param>
    /// <param name="blockCollection">The block collection.</param>
    /// <param name="blockTransformer">
    /// The block transformer to map existing blocks to different blocks.
    /// </param>
    private SSABuilder(
        MethodBuilder methodBuilder,
        BlockCollection blockCollection,
        Func<BasicBlock, BasicBlock> blockTransformer)
    {
        _blockTransformer = blockTransformer;

        MethodBuilder = methodBuilder;
        Blocks = blockCollection;

        _mapping = blockCollection.CreateMap(
            (block, _) => new ValueContainer(this, block, blockTransformer));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated method builder.
    /// </summary>
    public MethodBuilder MethodBuilder { get; }

    /// <summary>
    /// Returns the underlying list of blocks.
    /// </summary>
    public BlockCollection Blocks { get; }

    /// <summary>
    /// Returns the internal value container for the given block.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <returns>The resolved value container.</returns>
    // private ValueContainer this[BasicBlock block] => _mapping[_blockTransformer(block)];
    private ValueContainer this[BasicBlock block] => _mapping[block];

    #endregion

    #region Methods

    /// <summary>
    /// Sets the given variable to the given value.
    /// </summary>
    /// <param name="block">The target block.</param>
    /// <param name="var">The variable reference.</param>
    /// <param name="value">The value to set.</param>
    public void SetValue(BasicBlock block, TVariable var, Value value)
    {
        var valueContainer = this[block];
        valueContainer.SetValue(var, value);
    }

    /// <summary>
    /// Returns the value of the given variable.
    /// </summary>
    /// <param name="block">The target block.</param>
    /// <param name="var">The variable reference.</param>
    /// <returns>The value of the given variable.</returns>
    public Value GetValue(BasicBlock block, TVariable var)
    {
        var markerProvider = new MarkerProvider(_markerValue);
        var valueContainer = this[block];
        var result = valueContainer.GetValue(var, ref markerProvider);
        markerProvider.Apply(ref _markerValue);
        return result;
    }

    /// <summary>
    /// Removes the value of the given variable.
    /// </summary>
    /// <param name="block">The target block.</param>
    /// <param name="var">The variable reference.</param>
    public void RemoveValue(BasicBlock block, TVariable var)
    {
        var valueContainer = this[block];
        valueContainer.RemoveValue(var);
    }

    /// <summary>
    /// Tries to seals the associated block.
    /// </summary>
    /// <param name="container">The container to seal.</param>
    private void Seal(ValueContainer container)
    {
        container.Block.Assert(container.CanSeal);
        var markerProvider = new MarkerProvider(_markerValue);
        container.Seal(ref markerProvider);
        markerProvider.Apply(ref _markerValue);
    }

    /// <summary>
    /// Tries to process the given node while always trying to seal the given node.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <returns>True, if the node has not been processed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool ProcessAndSeal(BasicBlock block)
    {
        var valueContainer = this[block];
        if (valueContainer.CanSeal)
            Seal(valueContainer);
        return !valueContainer.IsProcessed &&
            (valueContainer.IsProcessed = true);
    }

    /// <summary>
    /// Tries to seal all successors of the given block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void TrySealSuccessors(BasicBlock block)
    {
        var valueContainer = this[block];
        block.Assert(valueContainer.IsProcessed);
        var markerProvider = new MarkerProvider(_markerValue);
        valueContainer.TrySealSuccessors(ref markerProvider);
        markerProvider.Apply(ref _markerValue);
    }

    /// <summary>
    /// Seals all remaining blocks in the appropriate order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SealRemainingBlocks()
    {
        // Seal all unsealed blocks to close back edges
        var markerProvider = new MarkerProvider(_markerValue);
        foreach (var block in Blocks)
        {
            var container = _mapping[block];
            if (container.CanSeal)
                container.Seal(ref markerProvider);
            else
                block.Assert(container.IsSealed);
        }
        markerProvider.Apply(ref _markerValue);
    }

    /// <summary>
    /// Asserts that all blocks have been sealed.
    /// </summary>
    /// <remarks>
    /// This operation is only available in debug mode.
    /// </remarks>
    [Conditional("DEBUG")]
    public void AssertAllSealed()
    {
        foreach (var block in Blocks)
        {
            var container = _mapping[block];
            block.Assert(container.IsSealed);
        }
    }

    #endregion
}
