// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an unmanaged Cuda buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public sealed class CudaMemoryBuffer<T, TIndex> : MemoryBuffer<T, TIndex>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Instance

        /// <summary>
        /// Constructs a new cuda buffer.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="extent">The extent.</param>
        internal CudaMemoryBuffer(CudaAccelerator accelerator, TIndex extent)
            : base(accelerator, extent)
        {
            CudaException.ThrowIfFailed(
                CudaAPI.Current.AllocateMemory(
                    out IntPtr resultPtr,
                    new IntPtr(extent.Size * ElementSize)));
            NativePtr = resultPtr;
        }

        #endregion

        #region Methods

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyToView(AcceleratorStream, ArrayView{T}, Index)"/>
        protected internal unsafe override void CopyToView(
            AcceleratorStream stream,
            ArrayView<T> target,
            Index sourceOffset)
        {
            using (var binding = Accelerator.BindScoped())
            {
                var targetBuffer = target.Source;
                var sourceAddress = new IntPtr(ComputeEffectiveAddress(sourceOffset));
                var targetAddress = new IntPtr(target.LoadEffectiveAddress());
                switch (targetBuffer.AcceleratorType)
                {
                    case AcceleratorType.CPU:
                        CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyDeviceToHost(
                            targetAddress,
                            sourceAddress,
                            new IntPtr(target.LengthInBytes),
                            stream));
                        break;
                    case AcceleratorType.Cuda:
                        CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyDeviceToDevice(
                            targetAddress,
                            sourceAddress,
                            new IntPtr(target.LengthInBytes),
                            stream));
                        break;
                    default:
                        throw new NotSupportedException(RuntimeErrorMessages.NotSupportedTargetAccelerator);
                }
            }
        }

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyFromView(AcceleratorStream, ArrayView{T}, Index)"/>
        protected internal unsafe override void CopyFromView(
            AcceleratorStream stream,
            ArrayView<T> source,
            Index targetOffset)
        {
            using (var binding = Accelerator.BindScoped())
            {
                var sourceAddress = new IntPtr(source.LoadEffectiveAddress());
                var targetAddress = new IntPtr(ComputeEffectiveAddress(targetOffset));
                switch (source.AcceleratorType)
                {
                    case AcceleratorType.CPU:
                        CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyHostToDevice(
                            targetAddress,
                            sourceAddress,
                            new IntPtr(source.LengthInBytes),
                            stream));
                        break;
                    case AcceleratorType.Cuda:
                        CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyDeviceToDevice(
                            targetAddress,
                            sourceAddress,
                            new IntPtr(source.LengthInBytes),
                            stream));
                        break;
                    default:
                        throw new NotSupportedException(RuntimeErrorMessages.NotSupportedTargetAccelerator);
                }
            }
        }

        /// <summary cref="MemoryBuffer.MemSetToZero(AcceleratorStream)"/>
        public override void MemSetToZero(AcceleratorStream stream)
        {
            using (var binding = Accelerator.BindScoped())
                CudaAPI.Current.Memset(NativePtr, 0, new IntPtr(LengthInBytes));
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            CudaException.ThrowIfFailed(CudaAPI.Current.FreeMemory(NativePtr));
            NativePtr = IntPtr.Zero;
        }

        #endregion
    }
}
