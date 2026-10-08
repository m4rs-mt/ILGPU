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
    /// A successor or predecessor enumerator.
    /// </summary>
    internal struct Enumerator(HashSet<ValueContainer> set)
    {
        private HashSet<ValueContainer>.Enumerator _enumerator = set.GetEnumerator();

        /// <summary>
        /// Returns the current value.
        /// </summary>
        public BasicBlock Current => _enumerator.Current.Block;

        /// <summary cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => _enumerator.MoveNext();
    }

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
            _values.TryGetValue(var, out Value? value)
            ? value
            : GetValueRecursive(var, ref markerProvider);

        /// <summary>
        /// Removes the value of the given variable.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        public void RemoveValue(TVariable var) => _values.Remove(var);

        /// <summary>
        /// Peeks a value recursively. This method only retrieves a value
        /// from a predecessor but does not build any phi nodes.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="marker">The current marker to break cycles.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private Value? PeekValue(TVariable var, int marker)
        {
            if (!IsProcessed || !Mark(marker))
                return null;
            if (_values.TryGetValue(var, out Value? value))
                return value;
            foreach (var predecessor in Block.Predecessors)
            {
                var valueContainer = Parent[predecessor];
                Value? result;
                if ((result = valueContainer.PeekValue(var, marker)) != null)
                    return result;
            }
            return null;
        }

        //
        // Implements an adapted version of the SSA-construction algorithm from the
        // paper: Simple and Efficient Construction of Static Single Assignment Form
        //

        /// <summary>
        /// Returns the value of the given variable by asking the predecessors.
        /// This method recursively constructs required phi nodes to break cycles.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        /// <returns>The value of the given variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private Value GetValueRecursive(TVariable var, ref MarkerProvider markerProvider)
        {
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
            Value value;
            if (Block.Predecessors.Length == 1 && IsSealed)
            {
                var valueContainer = Parent[Block.Predecessors[0]];
                value = valueContainer.GetValue(var, ref markerProvider);
            }
            else
            {
                // Insert the actual phi value
                var peekedValue =
                    PeekValue(var, markerProvider.CreateMarker()).AsNotNull();
                // Let the phi point to the beginning of the current block
                var phiBuilder = Builder.CreatePhi(
                    Builder.BasicBlock.Location,
                    peekedValue.Type);
                value = phiBuilder.PhiValue;

                var incompletePhi = new IncompletePhi(var, phiBuilder);
                if (IsSealed)
                {
                    SetValue(var, value);
                    value = SetupPhiArguments(incompletePhi, ref markerProvider);
                }
                else
                {
                    _incompletePhis[var] = incompletePhi;
                }
            }
            SetValue(var, value);
            return value;
        }

        /// <summary>
        /// Setups phi arguments for the given variable reference and the given
        /// phi parameter. This method is invoked for sealed blocks during CPS
        /// construction or during the sealing process in the last step.
        /// </summary>
        /// <param name="incompletePhi">An incomplete phi node to complete.</param>
        /// <param name="markerProvider">A provider of new marker values.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private PhiValue SetupPhiArguments(
            IncompletePhi incompletePhi,
            ref MarkerProvider markerProvider)
        {
            var phiBuilder = incompletePhi.PhiBuilder;
            foreach (var predecessor in Block.Predecessors)
            {
                var valueContainer = Parent[predecessor];

                // Get the related predecessor value
                var value = valueContainer.GetValue(
                    incompletePhi.VariableRef,
                    ref markerProvider);

                // Convert the value into the target type
                if (incompletePhi.PhiType is PrimitiveType primitiveType)
                {
                    // Use the predecessor block to convert the value
                    value = valueContainer.Builder.CreateConvert(
                        incompletePhi.Location,
                        value,
                        primitiveType).AsNotNull();
                }

                // Set argument value — transform the predecessor to the
                // current generation. Block.Predecessors are old-gen blocks
                // but the phi lives in a new-gen block.
                phiBuilder.AddArgument(
                    Parent._blockTransformer(predecessor), value);
            }
            incompletePhi.Location.Assert(
                phiBuilder.Count == Block.Predecessors.Length);
            return phiBuilder.Seal();
        }

        /// <summary>
        /// Seals this block (called when all predecessors have been seen) and
        /// wires all (previously un-wired) phi nodes.
        /// </summary>
        /// <param name="markerProvider">A provider of new marker values.</param>
        public void Seal(ref MarkerProvider markerProvider)
        {
            Block.Assert(!IsSealed);
            foreach (var var in _incompletePhis.Values)
                SetupPhiArguments(var, ref markerProvider);
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
    /// Tries to seals the associated node.
    /// </summary>
    /// <param name="block">The target block.</param>
    public bool Seal(BasicBlock block)
    {
        var valueContainer = this[block];
        if (valueContainer.CanSeal)
        {
            Seal(valueContainer);
            return true;
        }
        return false;
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
