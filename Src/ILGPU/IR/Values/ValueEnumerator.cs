// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ValueEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// An enumerator for nodes.
    /// </summary>
    public struct ValueEnumerator : IEnumerator<ValueReference>
    {
        #region Instance

        private readonly ImmutableArray<ValueReference> values;
        private ImmutableArray<ValueReference>.Enumerator enumerator;

        /// <summary>
        /// Constructs a new node enumerator.
        /// </summary>
        /// <param name="valueArray">The nodes to iterate over.</param>
        internal ValueEnumerator(ImmutableArray<ValueReference> valueArray)
        {
            values = valueArray;
            enumerator = valueArray.GetEnumerator();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current node.
        /// </summary>
        public ValueReference Current => enumerator.Current;

        /// <summary cref="IEnumerator.Current"/>
        object IEnumerator.Current => Current;

        #endregion

        #region Methods

        /// <summary cref="IDisposable.Dispose"/>
        public void Dispose() { }

        /// <summary cref="IEnumerator.MoveNext"/>
        public bool MoveNext()
        {
            return enumerator.MoveNext();
        }

        /// <summary cref="IEnumerator.Reset"/>
        void IEnumerator.Reset() => throw new InvalidOperationException();

        #endregion
    }
}
