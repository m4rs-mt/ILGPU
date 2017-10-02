// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CPUMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a managed array that was pinned for unmanaged memory accesses.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public sealed class CPUMemoryBuffer<T, TIndex> : MemoryBuffer<T, TIndex>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Instance

        /// <summary>
        /// Holds the gc handle of the pinned object.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private GCHandle gcHandle;

        /// <summary>
        /// A handle to the managed array.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private T[] array;

        /// <summary>
        /// Constructs a new pinned array.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="extent">The extent.</param>
        internal CPUMemoryBuffer(CPUAccelerator accelerator, TIndex extent)
            : base(accelerator, extent)
        {
            array = new T[extent.Size];
            gcHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
            Pointer = gcHandle.AddrOfPinnedObject();
        }

        #endregion

        #region Methods

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyToViewInternal(ArrayView{T, Index}, AcceleratorType, TIndex, AcceleratorStream)"/>
        protected internal override unsafe void CopyToViewInternal(
            ArrayView<T, Index> target,
            AcceleratorType acceleratorType,
            TIndex sourceOffset,
            AcceleratorStream stream)
        {
            switch (acceleratorType)
            {
                case AcceleratorType.CPU:
                    Buffer.MemoryCopy(
                        GetSubView(sourceOffset).Pointer.ToPointer(),
                        target.Pointer.ToPointer(),
                        target.LengthInBytes,
                        target.LengthInBytes);
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyHostToDevice(
                        target.Pointer,
                        GetSubView(sourceOffset).Pointer,
                        new IntPtr(target.LengthInBytes),
                        stream));
                    break;
                default:
                    throw new NotSupportedException(RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyFromViewInternal(ArrayView{T, Index}, AcceleratorType, TIndex, AcceleratorStream)"/>
        protected internal override unsafe void CopyFromViewInternal(
            ArrayView<T, Index> source,
            AcceleratorType acceleratorType,
            TIndex targetOffset,
            AcceleratorStream stream)
        {
            switch (acceleratorType)
            {
                case AcceleratorType.CPU:
                    Buffer.MemoryCopy(
                        source.Pointer.ToPointer(),
                        GetSubView(targetOffset).Pointer.ToPointer(),
                        source.LengthInBytes,
                        source.LengthInBytes);
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyDeviceToHost(
                        GetSubView(targetOffset).Pointer,
                        source.Pointer,
                        new IntPtr(source.LengthInBytes),
                        stream));
                    break;
                default:
                    throw new NotSupportedException(RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary cref="MemoryBuffer.MemSetToZero(AcceleratorStream)"/>
        public override void MemSetToZero(AcceleratorStream stream)
        {
            Array.Clear(array, 0, array.Length);
        }

        /// <summary>
        /// Returns the managed array.
        /// </summary>
        /// <returns>The managed array.</returns>
        public T[] GetArray()
        {
            return array;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (array == null)
                return;

            gcHandle.Free();
            array = null;
        }

        #endregion
    }
}
