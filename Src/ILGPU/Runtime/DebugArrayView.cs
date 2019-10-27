// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: DebugArrayView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Base debug view.
    /// </summary>
    abstract class BaseDebugArrayView<T>
        where T : struct
    {
        #region Instance

        /// <summary>
        /// Stores the associated view.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected readonly ArrayView<T> view;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private T[] data;

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The source array view.</param>
        protected BaseDebugArrayView(ArrayView<T> source)
        {
            view = source;
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Data
        {
            get
            {
                if (data == null)
                {
                    if (view.IsValid)
                    {
                        if (view.AcceleratorType == AcceleratorType.CPU)
                            data = LoadCPUData();
                        else
                            data = LoadDeviceData();
                    }
                    else
                        data = Array.Empty<T>();
                }
                return data;
            }
        }

        #endregion

        #region Methods

        private T[] LoadCPUData()
        {
            var result = new T[view.Length];
            for (int i = 0, e = result.Length; i < e; ++i)
                result[i] = view[i];
            return result;
        }

        private unsafe T[] LoadDeviceData()
        {
            var elementSize = ArrayView<T>.ElementSize;
            var rawData = view.Source.GetAsDebugRawArray(
                view.Index * elementSize,
                view.Extent * elementSize);

            var result = new T[view.Length];
            fixed (byte* ptr = &rawData.Array[rawData.Offset])
            {
                ref var castedPtr = ref Unsafe.AsRef<byte>(ptr);
                for (Index i = 0, e = view.Length; i < e; ++i)
                {
                    ref var elementPtr = ref Interop.ComputeEffectiveAddress(
                        ref castedPtr,
                        i,
                        elementSize);
                    result[i] = Unsafe.As<byte, T>(ref elementPtr);
                }
            }
            return result;
        }

        #endregion
    }

    /// <summary>public
    /// Represents a debugger array view.
    /// </summary>
    sealed class DebugArrayView<T> : BaseDebugArrayView<T>
        where T : struct
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The target array view.</param>
        public DebugArrayView(ArrayView<T> source)
            : base(source)
        { }

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The target array view.</param>
        public DebugArrayView(ArrayView2D<T> source)
            : this(source.AsLinearView())
        { }

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The target array view.</param>
        public DebugArrayView(ArrayView3D<T> source)
            : this(source.AsLinearView())
        { }

        #endregion
    }

    /// <summary>
    /// Represents a debugger view for generic array views.
    /// </summary>
    sealed class DebugArrayView<T, TIndex> : BaseDebugArrayView<T>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The target array view.</param>
        public DebugArrayView(ArrayView<T, TIndex> source)
            : base(source.AsLinearView())
        { }

        #endregion
    }
}
