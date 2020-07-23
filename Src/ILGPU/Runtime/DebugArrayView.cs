// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: DebugArrayView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// The buffer data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Data
        {
            get
            {
                if (data == null)
                {
                    data = view.IsValid
                        ? view.AcceleratorType == AcceleratorType.CPU
                            ? LoadCPUData()
                            : LoadDeviceData()
                        : Array.Empty<T>();
                }
                return data;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads data from the CPU.
        /// </summary>
        /// <returns></returns>
        private T[] LoadCPUData()
        {
            var result = new T[view.Length];
            for (int i = 0, e = result.Length; i < e; ++i)
                result[i] = view[i];
            return result;
        }

        /// <summary>
        /// Loads raw data from the underlying device.
        /// </summary>
        /// <returns>The loaded data.</returns>
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
                for (long i = 0, e = view.Length; i < e; ++i)
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
    sealed partial class DebugArrayView<T> : BaseDebugArrayView<T>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug view.
        /// </summary>
        /// <param name="source">The target array view.</param>
        public DebugArrayView(ArrayView<T> source)
            : base(source)
        { }

        #endregion
    }

    /// <summary>
    /// Represents a debugger view for generic array views.
    /// </summary>
    sealed class DebugArrayView<T, TIndex> : BaseDebugArrayView<T>
        where T : unmanaged
        where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
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
