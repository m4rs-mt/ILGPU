// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Phis.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Gathers all phis in a basic block.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of the current entity")]
    public readonly struct Phis : IEnumerable<PhiValue>
    {
        #region Nested Types

        /// <summary>
        /// Represents a phi-value enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<PhiValue>
        {
            private List<PhiValue>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="phiValues">All phi values.</param>
            internal Enumerator(List<PhiValue> phiValues)
            {
                enumerator = phiValues.GetEnumerator();
            }

            /// <summary>
            /// Returns the current basic block.
            /// </summary>
            public PhiValue Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => enumerator.Dispose();

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        #region Static

        /// <summary>
        /// Resolves all phi values in the given block.
        /// </summary>
        /// <param name="block">The source block.</param>
        /// <returns>The resolved phis.</returns>
        public static Phis Create(BasicBlock block)
        {
            Debug.Assert(block != null, "Invalid block");

            var phiValues = new List<PhiValue>();
            foreach (Value value in block)
            {
                if (value is PhiValue phiValue)
                    phiValues.Add(phiValue);
            }
            return new Phis(phiValues);
        }

        /// <summary>
        /// Resolves all phi values using the given enumerator.
        /// </summary>
        /// <param name="enumerator">The enumerator.</param>
        /// <returns>The resolved phis.</returns>
        public static Phis Create<TEnumerator>(TEnumerator enumerator)
            where TEnumerator : IEnumerator<Value>
        {
            var result = new List<PhiValue>();
            var collected = new HashSet<PhiValue>();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current is PhiValue phiValue &&
                    collected.Add(phiValue))
                    result.Add(phiValue);
            }

            return new Phis(result);
        }

        #endregion

        #region Instance

        private readonly List<PhiValue> phiValues;

        /// <summary>
        /// Constructs a new Phis instance.
        /// </summary>
        /// <param name="phis">All detected phi values.</param>
        private Phis(List<PhiValue> phis)
        {
            phiValues = phis;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of phi values.
        /// </summary>
        public int Count => phiValues.Count;

        /// <summary>
        /// Returns the i-th phi value.
        /// </summary>
        /// <param name="index">The phi value index.</param>
        /// <returns>The resolved phi value.</returns>
        public PhiValue this[int index] => phiValues[index];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a phi-value enumerator.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(phiValues);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<PhiValue> IEnumerable<PhiValue>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
