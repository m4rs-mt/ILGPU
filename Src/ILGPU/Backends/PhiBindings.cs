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
        /// Represents a readonly list of phi entries.
        /// </summary>
        public readonly struct PhiBindingCollection : IReadOnlyList<(Value, PhiValue)>
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to enumerate all entries in this collection.
            /// </summary>
            public struct Enumerator : IEnumerator<(Value, PhiValue)>
            {
                private List<(Value, PhiValue)>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new entry enumerator.
                /// </summary>
                /// <param name="collection">The parent collection.</param>
                internal Enumerator(in PhiBindingCollection collection)
                {
                    enumerator = collection.values.GetEnumerator();
                }

                /// <summary cref="IEnumerator{T}.Current"/>
                public (Value, PhiValue) Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            private readonly List<(Value, PhiValue)> values;

            /// <summary>
            /// Constructs a new parameter collection.
            /// </summary>
            /// <param name="valueList">The list of all values.</param>
            internal PhiBindingCollection(List<(Value, PhiValue)> valueList)
            {
                values = valueList;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the number of entries.
            /// </summary>
            public int Count => values.Count;

            /// <summary>
            /// Returns the i-th entry.
            /// </summary>
            /// <param name="index">The index of the entry to get.</param>
            /// <returns>The desired entry.</returns>
            public (Value, PhiValue) this[int index] => values[index];

            #endregion

            #region Methods

            /// <summary>
            /// Returns an enumerator to enumerate all entries in this collection.
            /// </summary>
            /// <returns>
            /// An enumerator to enumerate all entries in this collection.
            /// </returns>
            public Enumerator GetEnumerator() => new Enumerator(this);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<(Value, PhiValue)>
                IEnumerable<(Value, PhiValue)>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Provides new intermediate list instances.
        /// </summary>
        private readonly struct ListProvider :
            IBasicBlockMapValueProvider<List<(Value, PhiValue)>>
        {
            /// <summary>
            /// Creates a new <see cref="List{T}"/> instance.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly List<(Value, PhiValue)> GetValue(
                BasicBlock block,
                int traversalIndex) =>
                new List<(Value, PhiValue)>();
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
            var mapping = collection.CreateMap(new ListProvider());

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
                        mapping[argumentBlock].Add((phi[i], phi));
                    }
                }
            }

            return new PhiBindings(mapping);
        }

        #endregion

        #region Instance

        private readonly BasicBlockMap<List<(Value, PhiValue)>> phiMapping;

        /// <summary>
        /// Constructs new phi bindings.
        /// </summary>
        /// <param name="mapping">The phi mapping.</param>
        private PhiBindings(in BasicBlockMap<List<(Value, PhiValue)>> mapping)
        {
            phiMapping = mapping;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve phi bindings for the given block.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="bindings">The resolved bindings (if any)</param>
        /// <returns>True, if phi bindings could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetBindings(BasicBlock block, out PhiBindingCollection bindings)
        {
            bindings = default;
            if (!phiMapping.TryGetValue(block, out var values))
                return false;
            bindings = new PhiBindingCollection(values);
            return true;
        }

        #endregion
    }
}
