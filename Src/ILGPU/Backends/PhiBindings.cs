// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PhiBindings.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends
{
    /// <summary>
    /// An abstract binding allocator for the class <see cref="PhiBindings"/>.
    /// </summary>
    public interface IPhiBindingAllocator
    {
        /// <summary>
        /// Processes all phis that are declared in the given node.
        /// </summary>
        /// <param name="block">The current block.</param>
        /// <param name="phis">The phi nodes to process.</param>
        void Process(BasicBlock block, Phis phis);

        /// <summary>
        /// Allocates the given phi node.
        /// </summary>
        /// <param name="block">The current block.</param>
        /// <param name="phiValue">The phi node to allocate.</param>
        void Allocate(BasicBlock block, PhiValue phiValue);
    }

    /// <summary>
    /// Maps phi nodes to basic blocks in order to emit move command during
    /// the final code generation phase.
    /// </summary>
    public readonly struct PhiBindings
    {
        #region Nested Types

        /// <summary>
        /// A collection of intermediate phi values that need to be stored to temporary
        /// intermediate registers.
        /// </summary>
        public readonly struct IntermediatePhiCollection
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to enumerate all entries in this collection.
            /// </summary>
            public struct Enumerator : IEnumerator<PhiValue>
            {
                private HashSet<PhiValue>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new entry enumerator.
                /// </summary>
                /// <param name="blockInfo">The parent collection.</param>
                internal Enumerator(in BlockInfo blockInfo)
                {
                    enumerator = blockInfo.IntermediatePhis.GetEnumerator();
                }

                /// <summary cref="IEnumerator{T}.Current"/>
                public PhiValue Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                void IDisposable.Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            private readonly BlockInfo blockInfo;

            /// <summary>
            /// Constructs a new temp assignment collection.
            /// </summary>
            /// <param name="info">The phi information per block.</param>
            internal IntermediatePhiCollection(in BlockInfo info)
            {
                blockInfo = info;
            }

            #endregion

            #region Methods

            /// <summary>
            /// Returns an enumerator to enumerate all entries in this collection.
            /// </summary>
            /// <returns>
            /// An enumerator to enumerate all entries in this collection.
            /// </returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(blockInfo);

            #endregion
        }

        /// <summary>
        /// Represents a readonly list of phi entries.
        /// </summary>
        public readonly struct PhiBindingCollection
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to enumerate all entries in this collection.
            /// </summary>
            public struct Enumerator : IEnumerator<(PhiValue Phi, Value Value)>
            {
                private List<(PhiValue, Value)>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new entry enumerator.
                /// </summary>
                /// <param name="collection">The parent collection.</param>
                internal Enumerator(in PhiBindingCollection collection)
                {
                    enumerator = collection.blockInfo.Bindings.GetEnumerator();
                }

                /// <summary cref="IEnumerator{T}.Current"/>
                public (PhiValue Phi, Value Value) Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                void IDisposable.Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            private readonly BlockInfo blockInfo;

            /// <summary>
            /// Constructs a new binding collection.
            /// </summary>
            /// <param name="info">The phi information per block.</param>
            internal PhiBindingCollection(in BlockInfo info)
            {
                blockInfo = info;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns all intermediate phi values that must be assigned to temporaries.
            /// </summary>
            public readonly IntermediatePhiCollection Intermediates =>
                new IntermediatePhiCollection(blockInfo);

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given phi is an intermediate phi value that requires
            /// a temporary intermediate variable to be assigned to.
            /// </summary>
            /// <param name="phi">The phi value to test.</param>
            public readonly bool IsIntermediate(PhiValue phi) =>
                blockInfo.IntermediatePhis.Contains(phi);

            /// <summary>
            /// Returns an enumerator to enumerate all entries in this collection.
            /// </summary>
            /// <returns>
            /// An enumerator to enumerate all entries in this collection.
            /// </returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(this);

            #endregion
        }

        /// <summary>
        /// Stores <see cref="PhiValue"/> information per block.
        /// </summary>
        internal readonly struct BlockInfo
        {
            /// <summary>
            /// Constructs a new information object.
            /// </summary>
            /// <param name="capacity">
            /// The initial capacity of the internal data structures.
            /// </param>
            public BlockInfo(int capacity)
            {
#if NET5_0 || NETSTANDARD2_1_OR_GREATER
                LHSPhis = new HashSet<PhiValue>(capacity);
                IntermediatePhis = new HashSet<PhiValue>(capacity);
#else
                LHSPhis = new HashSet<PhiValue>();
                IntermediatePhis = new HashSet<PhiValue>();
#endif
                Bindings = new List<(PhiValue, Value)>(capacity);
            }

            /// <summary>
            /// The set of all phi values in this block on the left-hand side.
            /// </summary>
            public HashSet<PhiValue> LHSPhis { get; }

            /// <summary>
            /// The set of all phi values in this block that need to be stored into a
            /// temporary location in order to recover their original value.
            /// </summary>
            public HashSet<PhiValue> IntermediatePhis { get; }

            /// <summary>
            /// The list of value phi bindings.
            /// </summary>
            public List<(PhiValue Phi, Value Value)> Bindings { get; }

            /// <summary>
            /// Registers a new phi binding.
            /// </summary>
            /// <param name="phi">The phi value it has to be bound to.</param>
            /// <param name="value">The source value to read from.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Add(PhiValue phi, Value value)
            {
                LHSPhis.Add(phi);
                // Check whether we are assigning the value of a phi value
                if (value is PhiValue phiValue && LHSPhis.Contains(phiValue))
                    IntermediatePhis.Add(phiValue);
                Bindings.Add((phi, value));
            }
        }

        /// <summary>
        /// Provides new intermediate list instances.
        /// </summary>
        private readonly struct InfoProvider : IBasicBlockMapValueProvider<BlockInfo>
        {
            /// <summary>
            /// Creates a new <see cref="List{T}"/> instance.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly BlockInfo GetValue(BasicBlock block, int traversalIndex) =>
                new BlockInfo(block.Count);
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new phi bindings mapping.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TAllocator">The custom allocator type.</typeparam>
        /// <param name="collection">The source collection.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>The created phi bindings.</returns>
        public static PhiBindings Create<TOrder, TDirection, TAllocator>(
            in BasicBlockCollection<TOrder, TDirection> collection,
            TAllocator allocator)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
            where TAllocator : IPhiBindingAllocator
        {
            var mapping = collection.CreateMap(new InfoProvider());

            foreach (var block in collection)
            {
                // Resolve phis
                var phis = Phis.Create(block);
                allocator.Process(block, phis);

                // Map all phi arguments
                foreach (var phi in phis)
                {
                    // Allocate phi for further processing
                    allocator.Allocate(block, phi);

                    // Determine predecessor mapping
                    phi.Assert(block.Predecessors.Length == phi.Nodes.Length);

                    // Assign values to their appropriate blocks
                    for (int i = 0, e = phi.Count; i < e; ++i)
                    {
                        var argumentBlock = phi.Sources[i];
                        mapping[argumentBlock].Add(phi, phi[i]);
                    }
                }
            }

            // Determine the maximum number of intermediate phi values
            int maxNumIntermediatePhis = 0;
            foreach (var (_, info) in mapping)
            {
                maxNumIntermediatePhis = Math.Max(
                    maxNumIntermediatePhis,
                    info.IntermediatePhis.Count);
            }

            return new PhiBindings(mapping, maxNumIntermediatePhis);
        }

        #endregion

        #region Instance

        private readonly BasicBlockMap<BlockInfo> phiMapping;

        /// <summary>
        /// Constructs new phi bindings.
        /// </summary>
        /// <param name="mapping">The phi mapping.</param>
        /// <param name="maxNumIntermediatePhis">
        /// The maximum number of intermediate phi values.
        /// </param>
        private PhiBindings(
            in BasicBlockMap<BlockInfo> mapping,
            int maxNumIntermediatePhis)
        {
            phiMapping = mapping;
            MaxNumIntermediatePhis = maxNumIntermediatePhis;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the maximum number of intermediate phi values to store temporarily.
        /// </summary>
        public int MaxNumIntermediatePhis { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve phi bindings for the given block.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="bindings">The resolved bindings (if any)</param>
        /// <returns>True, if phi bindings could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetBindings(
            BasicBlock block,
            out PhiBindingCollection bindings)
        {
            bindings = default;
            if (!phiMapping.TryGetValue(block, out var info))
                return false;
            bindings = new PhiBindingCollection(info);
            return true;
        }

        #endregion
    }
}
