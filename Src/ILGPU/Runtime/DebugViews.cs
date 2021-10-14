// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Base debug view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    abstract class BaseDebugArrayView<T>
        where T : unmanaged
    {
        #region Static

        /// <summary>
        /// Synchronizes all running streams to ensure a consistent debugging state.
        /// </summary>
        /// <param name="source">The source debugger state.</param>
        protected static void SyncDebuggerState(ArrayView<T> source) =>
            source.GetAccelerator().Synchronize();

        /// <summary>
        /// Returns the underlying data of the given view for debugging purposes.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <returns>The raw view data for debugging purposes.</returns>
        protected static T[] GetDebuggerData(ArrayView<T> source)
        {
            SyncDebuggerState(source);
            return source.GetAsArray();
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        protected BaseDebugArrayView() { }

        #endregion
    }

    /// <summary>
    /// Represents a debugger view for generic array views.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    sealed class DebugArrayView<T> : BaseDebugArrayView<T>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The source array view.</param>
        public DebugArrayView(ArrayView<T> source)
        {
            Data = GetDebuggerData(source);
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
