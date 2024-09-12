// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Phis.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PhiValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.PhiValue>;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Gathers all phis in a basic block.
    /// </summary>
    public readonly ref struct Phis
    {
        #region Nested Types

        /// <summary>
        /// The builder class for the <see cref="Phis"/> analysis.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private PhiValueList phiValues;
            private readonly HashSet<PhiValue> phiValueSet;

            /// <summary>
            /// Constructs a new internal builder.
            /// </summary>
            /// <param name="method">The parent method.</param>
            internal Builder(Method method)
            {
                method.AssertNotNull(method);

                phiValues = PhiValueList.Create(
                    Math.Max(method.Blocks.Count >> 2, 4));
                phiValueSet = new HashSet<PhiValue>();

                Method = method;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent method.
            /// </summary>
            public Method Method { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given phi value to the list.
            /// </summary>
            /// <param name="phiValue">The phi value to add.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(PhiValue phiValue)
            {
                Method.AssertNotNull(phiValue);
                Method.Assert(Method == phiValue.BasicBlock.Method);

                if (phiValueSet.Add(phiValue))
                    phiValues.Add(phiValue);
            }

            /// <summary>
            /// Adds all phi values in the given block.
            /// </summary>
            /// <param name="block">The block to analyze.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(BasicBlock block)
            {
                Method.AssertNotNull(block);
                Method.Assert(Method == block.Method);

                foreach (Value value in block)
                {
                    if (value is PhiValue phiValue)
                        Add(phiValue);
                }
            }

            /// <summary>
            /// Adds all phi values in the given block collection.
            /// </summary>
            /// <param name="blocks">The blocks to analyze.</param>
            public void Add<TOrder, TDirection>(
                in BasicBlockCollection<TOrder, TDirection> blocks)
                where TOrder : struct, ITraversalOrder
                where TDirection : struct, IControlFlowDirection
            {
                foreach (var block in blocks)
                    Add(block);
            }

            /// <summary>
            /// Seals the current builder and creates a <see cref="Phis"/> instance.
            /// </summary>
            /// <returns>The created <see cref="Phis"/> instance.</returns>
            public Phis Seal() => new Phis(Method, phiValues.AsReadOnlySpan());

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new builder.
        /// </summary>
        /// <param name="method">The parent method to use.</param>
        /// <returns>The created analysis builder.</returns>
        public static Builder CreateBuilder(Method method) => new Builder(method);

        /// <summary>
        /// Resolves all phi values in the given block collection.
        /// </summary>
        /// <param name="blocks">The blocks to analyze.</param>
        /// <returns>The resolved phis.</returns>
        public static Phis Create<TOrder, TDirection>(
            in BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
        {
            var builder = CreateBuilder(blocks.Method);
            builder.Add(blocks);
            return builder.Seal();
        }

        /// <summary>
        /// Resolves all phi values in the given block.
        /// </summary>
        /// <param name="block">The source block.</param>
        /// <returns>The resolved phis.</returns>
        public static Phis Create(BasicBlock block)
        {
            block.AssertNotNull(block);

            var builder = CreateBuilder(block.Method);
            builder.Add(block);
            return builder.Seal();
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Phis instance.
        /// </summary>
        /// <param name="method">The parent method.</param>
        /// <param name="phiValues">All detected phi values.</param>
        private Phis(Method method, ReadOnlySpan<PhiValue> phiValues)
        {
            Method = method;
            Values = phiValues;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns all phi values.
        /// </summary>
        public ReadOnlySpan<PhiValue> Values { get; }

        /// <summary>
        /// Returns the number of phi values.
        /// </summary>
        public readonly int Count => Values.Length;

        /// <summary>
        /// Returns the i-th phi value.
        /// </summary>
        /// <param name="index">The phi value index.</param>
        /// <returns>The resolved phi value.</returns>
        public readonly PhiValue this[int index] => Values[index];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a phi-value enumerator.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public readonly ReadOnlySpan<PhiValue>.Enumerator GetEnumerator() =>
            Values.GetEnumerator();

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts the given instance into a span.
        /// </summary>
        /// <param name="phis">The phi anlysis instance to convert.</param>
        public static implicit operator ReadOnlySpan<PhiValue>(Phis phis) => phis.Values;

        #endregion
    }

    /// <summary>
    /// Analysis extensions for <see cref="PhiValue"/> instances.
    /// </summary>
    public static class PhiValueExtensions
    {
        /// <summary>
        /// Gathers all phi source blocks.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The blocks to use.</param>
        /// <returns>All phi value source blocks.</returns>
        public static BasicBlockSet ComputePhiSources<TOrder, TDirection>(
            this BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
        {
            var result = blocks.CreateSet();
            foreach (var block in blocks)
            {
                foreach (Value value in block)
                {
                    if (value is PhiValue phiValue)
                    {
                        foreach (var source in phiValue.Sources)
                            result.Add(source);
                    }
                }
            }
            return result;
        }
    }
}
