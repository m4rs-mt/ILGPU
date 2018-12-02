// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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
using System.Runtime.CompilerServices;
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
        /// Constructs a new pinned array.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="extent">The extent.</param>
        internal CPUMemoryBuffer(CPUAccelerator accelerator, TIndex extent)
            : base(accelerator, extent)
        {
            NativePtr = Marshal.AllocHGlobal(extent.Size * Interop.SizeOf<T>());
        }

        #endregion

        #region Methods

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyToView(AcceleratorStream, ArrayView{T}, Index)"/>
        protected internal unsafe override void CopyToView(
            AcceleratorStream stream,
            ArrayView<T> target,
            Index sourceOffset)
        {
            var sourceAddress = ComputeEffectiveAddress(sourceOffset);
            switch (target.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    Unsafe.CopyBlock(
                        target.LoadEffectiveAddress(),
                        sourceAddress,
                        (uint)target.LengthInBytes);
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyHostToDevice(
                        new IntPtr(target.LoadEffectiveAddress()),
                        new IntPtr(sourceAddress),
                        new IntPtr(target.LengthInBytes),
                        stream));
                    break;
                default:
                    throw new NotSupportedException(RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyFromView(AcceleratorStream, ArrayView{T}, Index)"/>
        protected internal unsafe override void CopyFromView(
            AcceleratorStream stream,
            ArrayView<T> source,
            Index targetOffset)
        {
            var targetAddress = ComputeEffectiveAddress(targetOffset);
            switch (source.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    Unsafe.CopyBlock(
                        targetAddress,
                        source.LoadEffectiveAddress(),
                        (uint)source.LengthInBytes);
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyDeviceToHost(
                        new IntPtr(targetAddress),
                        new IntPtr(source.LoadEffectiveAddress()),
                        new IntPtr(source.LengthInBytes),
                        stream));
                    break;
                default:
                    throw new NotSupportedException(RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary cref="MemoryBuffer.MemSetToZero(AcceleratorStream)"/>
        public unsafe override void MemSetToZero(AcceleratorStream stream)
        {
            stream.Synchronize();
            Unsafe.InitBlock(NativePtr.ToPointer(), 0, (uint)LengthInBytes);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Marshal.FreeHGlobal(NativePtr);
            NativePtr = IntPtr.Zero;
        }

        #endregion
    }
}
