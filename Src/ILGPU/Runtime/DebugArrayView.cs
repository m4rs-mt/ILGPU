// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DebugArrayView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Diagnostics;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a debugger view for generic array views.
    /// </summary>
    sealed class DebugArrayView<T, TIndex>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ArrayView<T, TIndex> view;

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="view">The target array view.</param>
        public DebugArrayView(ArrayView<T, TIndex> view)
        {
            this.view = view;
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Data
        {
            get
            {
                var data = new T[view.Length];
                for (Index i = 0, e = view.Length; i < e; ++i)
                {
                    var idx = view.Extent.ReconstructIndex(i);
                    data[i] = view[idx];
                }
                return data;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a debugger view for generic array views.
    /// </summary>
    sealed class DebugArrayView<T>
        where T : struct
    {
        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly DebugArrayView<T, Index> view;

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="view">The target array view.</param>
        public DebugArrayView(ArrayView<T, Index> view)
        {
            this.view = new DebugArrayView<T, Index>(view);
        }

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="view">The target array view.</param>
        public DebugArrayView(ArrayView<T> view)
        {
            this.view = new DebugArrayView<T, Index>(view);
        }

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="view">The target array view.</param>
        public DebugArrayView(ArrayView2D<T> view)
        {
            this.view = new DebugArrayView<T, Index>(view.AsLinearView());
        }

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="view">The target array view.</param>
        public DebugArrayView(ArrayView3D<T> view)
        {
            this.view = new DebugArrayView<T, Index>(view.AsLinearView());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the encapsulated generic debug view.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public DebugArrayView<T, Index> View => view;

        #endregion
    }
}
