// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: DebugViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Diagnostics;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Base debug view.
    /// </summary>
    abstract class BaseDebugArrayView<T>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="data">The source data array.</param>
        protected BaseDebugArrayView(T[] data)
        {
            Data = data;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The buffer data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Data { get; }

        #endregion
    }

    /// <summary>
    /// Represents a debugger view for generic array views.
    /// </summary>
    sealed class DebugArrayView<T> : BaseDebugArrayView<T>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The source array view.</param>
        public DebugArrayView(ArrayView<T> source)
            : base(source.GetAsArray())
        { }

        #endregion
    }
}
