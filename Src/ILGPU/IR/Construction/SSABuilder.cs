﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SSABuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using CFG = ILGPU.IR.Analyses.CFG<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using CFGNode = ILGPU.IR.Analyses.CFG.Node<
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using BlockCollection = ILGPU.IR.BasicBlockCollection<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder>;
using ILGPU.IR.Analyses.Duplicates;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Constructs IR nodes that are in SSA form.
    /// </summary>
    /// <typeparam name="TVariable">The variable type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class SSABuilder<TVariable>
        where TVariable : IEquatable<TVariable>
    {
        #region Nested Types

        /// <summary>
        /// A successor or predecessor enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<CFGNode>
        {
            private readonly HashSet<ValueContainer> set;
            private HashSet<ValueContainer>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="values">The values to enumerate.</param>
            internal Enumerator(HashSet<ValueContainer> values)
            {
                set = values;
                enumerator = default;
                Reset();
            }

            /// <summary>
            /// Returns the current value.
            /// </summary>
            public CFGNode Current => enumerator.Current.Node;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => enumerator.Dispose();

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            public void Reset() => enumerator = set.GetEnumerator();
        }

        /// <summary>
        /// Provides marker values.
        /// </summary>
        internal ref struct MarkerProvider
        {
            /// <summary>
            /// Constructs a new marker provider.
            /// </summary>
            /// <param name="markerValue">The current marker value.</param>
            public MarkerProvider(int markerValue)
            {
                MarkerValue = markerValue;
            }

            /// <summary>
            /// Returns the current marker value.
            /// </summary>
            public int MarkerValue { get; private set; }

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
            public void Apply(ref int targetMarkerValue) =>
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
            private readonly struct IncompletePhi
            {
                /// <summary>
                /// Constructs an incomplete phi.
                /// </summary>
                /// <param name="variableRef">The referenced variable.</param>
                /// <param name="phiBuilder">The phi builder.</param>
                public IncompletePhi(
                    TVariable variableRef,
                    PhiValue.Builder phiBuilder)
                {
                    VariableRef = variableRef;
                    PhiBuilder = phiBuilder;
                }

                /// <summary>
                /// Returns the associated variable ref.
                /// </summary>
                public TVariable VariableRef { get; }

                /// <summary>
                /// Returns the associated phi builder.
                /// </summary>
                public PhiValue.Builder PhiBuilder { get; }

                /// <summary>
                /// Returns the type of the underlying phi node.
                /// </summary>
                public TypeNode PhiType => PhiBuilder.Type;

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
            private int markerValue = 0;

            /// <summary>
            /// Represents the current block builder.
            /// </summary>
            private BasicBlock.Builder blockBuilder;

            /// <summary>
            /// Value cache for SSA GetValue and SetValue functionality.
            /// </summary>
            private readonly Dictionary<TVariable, Value> values =
                new Dictionary<TVariable, Value>();

            /// <summary>
            /// Container for incomplete "phis" that have to be wired during block
            /// sealing.
            /// </summary>
            private readonly Dictionary<TVariable, IncompletePhi> incompletePhis =
                new Dictionary<TVariable, IncompletePhi>();

            /// <summary>
            /// Constructs a new SSA block.
            /// </summary>
            /// <param name="parent">The associated parent builder.</param>
            /// <param name="node">The current node.</param>
            internal ValueContainer(SSABuilder<TVariable> parent, CFGNode node)
            {
                Debug.Assert(parent != null, "Invalid parent");
                Parent = parent;
                Node = node;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent SSA builder.
            /// </summary>
            public SSABuilder<TVariable> Parent { get; }

            /// <summary>
            /// Returns the associated node.
            /// </summary>
            public CFGNode Node { get; }

            /// <summary>
            /// Returns the associated block builder.
            /// </summary>
            public BasicBlock.Builder Builder
            {
                get
                {
                    if (blockBuilder == null)
                        blockBuilder = Parent.MethodBuilder[Node.Block];
                    return blockBuilder;
                }
            }

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
                    foreach (var predecessor in Node.Predecessors)
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
                Interlocked.Exchange(ref markerValue, newMarker) != newMarker;

            /// <summary>
            /// Sets the given variable to the given value.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="value">The value to set.</param>
            public void SetValue(TVariable var, Value value) =>
                values[var] = value;

            /// <summary>
            /// Returns the value of the given variable.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="markerProvider">A provider of new marker values.</param>
            /// <returns>The value of the given variable.</returns>
            public Value GetValue(TVariable var, ref MarkerProvider markerProvider) =>
                values.TryGetValue(var, out Value value)
                ? value
                : GetValueRecursive(var, ref markerProvider);

            /// <summary>
            /// Removes the value of the given variable.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            public void RemoveValue(TVariable var) => values.Remove(var);

            /// <summary>
            /// Peeks a value recursively. This method only retrieves a value
            /// from a predecessor but does not build any phi nodes.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="marker">The current marker to break cycles.</param>
            /// <returns></returns>
            private Value PeekValue(TVariable var, int marker)
            {
                if (!IsProcessed || !Mark(marker))
                    return null;
                if (values.TryGetValue(var, out Value value))
                    return value;
                foreach (var predecessor in Node.Predecessors)
                {
                    var valueContainer = Parent[predecessor];
                    Value result;
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
            private Value GetValueRecursive(
                TVariable var,
                ref MarkerProvider markerProvider)
            {
                Debug.Assert(Node.NumPredecessors > 0);
                Value value;
                if (Node.NumPredecessors == 1 && IsSealed)
                {
                    var valueContainer = Parent[Node.Predecessors[0]];
                    value = valueContainer.GetValue(var, ref markerProvider);
                }
                else
                {
                    // Insert the actual phi value
                    var peekedValue = PeekValue(var, markerProvider.CreateMarker());
                    // Let the phi point to the beginning of the current block
                    var phiBuilder = Builder.CreatePhi(
                        blockBuilder.BasicBlock.Location,
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
                        incompletePhis[var] = incompletePhi;
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
            private Value SetupPhiArguments(
                in IncompletePhi incompletePhi,
                ref MarkerProvider markerProvider)
            {
                var phiBuilder = incompletePhi.PhiBuilder;
                foreach (var predecessor in Node.Predecessors)
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
                            primitiveType);
                    }

                    // Set argument value
                    phiBuilder.AddArgument(predecessor.Block, value);
                }
                incompletePhi.Location.Assert(
                    phiBuilder.Count == Node.Predecessors.Count);
                var phiValue = phiBuilder.Seal();
                return phiValue.TryRemoveTrivialPhi(Parent.MethodBuilder);
            }

            /// <summary>
            /// Seals this block (called when all predecessors have been seen) and
            /// wires all (previously unwired) phi nodes.
            /// </summary>
            /// <param name="markerProvider">A provider of new marker values.</param>
            public void Seal(ref MarkerProvider markerProvider)
            {
                Debug.Assert(!IsSealed, "Cannot seal a sealed block");
                foreach (var var in incompletePhis.Values)
                    SetupPhiArguments(var, ref markerProvider);
                IsSealed = true;
                incompletePhis.Clear();
            }

            #endregion

            #region Objects

            /// <summary>
            /// Returns the string representation of this block.
            /// </summary>
            /// <returns>The string representation of this block.</returns>
            public override string ToString() => Node.ToString();

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new SSA builder.
        /// </summary>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <returns>The created SSA builder.</returns>
        public static SSABuilder<TVariable> Create(
            Method.Builder methodBuilder) =>
            Create(methodBuilder, methodBuilder.SourceBlocks.CreateCFG());

        /// <summary>
        /// Creates a new SSA builder.
        /// </summary>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <param name="cfg">The parent CFG.</param>
        /// <returns>The created SSA builder.</returns>
        public static SSABuilder<TVariable> Create(
            Method.Builder methodBuilder,
            CFG cfg) =>
            new SSABuilder<TVariable>(
                methodBuilder ?? throw new ArgumentNullException(nameof(methodBuilder)),
                cfg ?? throw new ArgumentNullException(nameof(cfg)));

        #endregion

        #region Instance

        private int markerValue = 0;
        private readonly BasicBlockMap<ValueContainer> mapping;

        /// <summary>
        /// Constructs a new SSA builder.
        /// </summary>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <param name="cfg">The CFG.</param>
        private SSABuilder(Method.Builder methodBuilder, CFG cfg)
        {
            MethodBuilder = methodBuilder;
            CFG = cfg;
            mapping = cfg.CreateMapping(
                node => new ValueContainer(this, node));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated method builder.
        /// </summary>
        public Method.Builder MethodBuilder { get; }

        /// <summary>
        /// Returns the underlying list of blocks.
        /// </summary>
        public BasicBlockCollection<ReversePostOrder> Blocks =>
            MethodBuilder.SourceBlocks;

        /// <summary>
        /// Returns the associated graph.
        /// </summary>
        public CFG CFG { get; }

        /// <summary>
        /// Returns the internal value container for the given node.
        /// </summary>
        /// <param name="node">The CFG node.</param>
        /// <returns>The resolved value container.</returns>
        private ValueContainer this[CFGNode node] => mapping[node.Block];

        #endregion

        #region Methods

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(CFGNode node, TVariable var, Value value)
        {
            var valueContainer = this[node];
            valueContainer.SetValue(var, value);
        }

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value GetValue(CFGNode node, TVariable var)
        {
            var markerProvider = new MarkerProvider(markerValue);
            var valueContainer = this[node];
            var result = valueContainer.GetValue(var, ref markerProvider);
            markerProvider.Apply(ref markerValue);
            return result;
        }

        /// <summary>
        /// Removes the value of the given variable.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveValue(CFGNode node, TVariable var)
        {
            var valueContainer = this[node];
            valueContainer.RemoveValue(var);
        }

        /// <summary>
        /// Sets the given variable to the given block.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(BasicBlock basicBlock, TVariable var, Value value)
        {
            var cfgNode = CFG[basicBlock];
            SetValue(cfgNode, var, value);
        }

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value GetValue(BasicBlock basicBlock, TVariable var)
        {
            var cfgNode = CFG[basicBlock];
            return GetValue(cfgNode, var);
        }

        /// <summary>
        /// Removes the value of the given variable.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        /// <param name="var">The variable reference.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveValue(BasicBlock basicBlock, TVariable var)
        {
            var cfgNode = CFG[basicBlock];
            RemoveValue(cfgNode, var);
        }

        /// <summary>
        /// Tries to process the associated block.
        /// </summary>
        /// <param name="node">The target node.</param>
        public bool Process(CFGNode node)
        {
            var block = this[node];
            return block.IsProcessed
                ? false
                : block.IsProcessed = true;
        }

        /// <summary>
        /// Tries to process the associated block.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        public bool Process(BasicBlock basicBlock)
        {
            var cfgNode = CFG[basicBlock];
            return Process(cfgNode);
        }

        /// <summary>
        /// Tries to seals the associated block.
        /// </summary>
        /// <param name="block">The block to seal.</param>
        private void Seal(ValueContainer block)
        {
            Debug.Assert(block.CanSeal, "Invalid sealing operation");
            var markerProvider = new MarkerProvider(markerValue);
            block.Seal(ref markerProvider);
            markerProvider.Apply(ref markerValue);
        }

        /// <summary>
        /// Tries to seals the associated node.
        /// </summary>
        /// <param name="node">The target node.</param>
        public bool Seal(CFGNode node)
        {
            var valueContainer = this[node];
            if (valueContainer.CanSeal)
            {
                Seal(valueContainer);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to seals the associated block.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        public bool Seal(BasicBlock basicBlock)
        {
            var cfgNode = CFG[basicBlock];
            return Seal(cfgNode);
        }

        /// <summary>
        /// Tries to process the given node while always trying
        /// to seal the given node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>True, if the node has not been processed.</returns>
        public bool ProcessAndSeal(CFGNode node)
        {
            var valueContainer = this[node];
            if (valueContainer.CanSeal)
                Seal(valueContainer);
            return valueContainer.IsProcessed
                ? false
                : valueContainer.IsProcessed = true;
        }

        /// <summary>
        /// Tries to process the given node while always trying
        /// to seal the given block.
        /// </summary>
        /// <param name="basicBlock">The basic block.</param>
        /// <returns>True, if the node has not been processed.</returns>
        public bool ProcessAndSeal(BasicBlock basicBlock)
        {
            var cfgNode = CFG[basicBlock];
            return ProcessAndSeal(cfgNode);
        }

        #endregion
    }

    public static class SSABuilder
    {
        public static BlockCollection ComputeSSABlockOrder<TOrder>(
            this BasicBlockCollection<TOrder> blockCollection)
            where TOrder : struct, ITraversalOrder =>
            blockCollection.ComputeBlockOrder<
                ReversePostOrder,
                PostOrder,
                CanHaveDuplicates<BasicBlock>>();

        public static BlockCollection ComputeSSABlockOrder(
            this Method.Builder methodBuilder) =>
            methodBuilder.ComputeBlockOrder<
                ReversePostOrder,
                PostOrder,
                CanHaveDuplicates<BasicBlock>>();
    }
}
