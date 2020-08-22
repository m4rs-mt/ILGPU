// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an unmanaged Cuda buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public sealed class CudaMemoryBuffer<T, TIndex> : MemoryBuffer<T, TIndex>
        where T : unmanaged
        where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
    {
        #region Instance

        /// <summary>
        /// Constructs a new Cuda buffer.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="extent">The extent.</param>
        internal CudaMemoryBuffer(CudaAccelerator accelerator, TIndex extent)
            : base(accelerator, extent)
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.AllocateMemory(
                    out IntPtr resultPtr,
                    new IntPtr(extent.Size * ElementSize)));
            NativePtr = resultPtr;
        }

        #endregion

        #region Methods

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyToView(
        /// AcceleratorStream, ArrayView{T}, LongIndex1)"/>
        protected internal unsafe override void CopyToView(
            AcceleratorStream stream,
            ArrayView<T> target,
            LongIndex1 sourceOffset)
        {
            var binding = Accelerator.BindScoped();

            var targetBuffer = target.Source;
            var sourceAddress = new IntPtr(ComputeEffectiveAddress(sourceOffset));
            var targetAddress = new IntPtr(target.LoadEffectiveAddress());
            var lengthInBytes = new IntPtr(target.LengthInBytes);
            switch (targetBuffer.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    CudaException.ThrowIfFailed(
                        CurrentAPI.MemcpyDeviceToHost(
                            targetAddress,
                            sourceAddress,
                            lengthInBytes,
                            stream));
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(
                        CurrentAPI.MemcpyDeviceToDevice(
                            targetAddress,
                            sourceAddress,
                            lengthInBytes,
                            stream));
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            binding.Recover();
        }

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyFromView(
        /// AcceleratorStream, ArrayView{T}, LongIndex1)"/>
        protected internal unsafe override void CopyFromView(
            AcceleratorStream stream,
            ArrayView<T> source,
            LongIndex1 targetOffset)
        {
            var binding = Accelerator.BindScoped();

            var sourceAddress = new IntPtr(source.LoadEffectiveAddress());
            var targetAddress = new IntPtr(ComputeEffectiveAddress(targetOffset));
            var lengthInBytes = new IntPtr(source.LengthInBytes);
            switch (source.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    CudaException.ThrowIfFailed(
                        CurrentAPI.MemcpyHostToDevice(
                            targetAddress,
                            sourceAddress,
                            lengthInBytes,
                            stream));
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(
                        CurrentAPI.MemcpyDeviceToDevice(
                            targetAddress,
                            sourceAddress,
                            lengthInBytes,
                            stream));
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            binding.Recover();
        }

        /// <summary cref="MemoryBuffer.MemSetToZero(AcceleratorStream)"/>
        public override void MemSetToZero(AcceleratorStream stream)
        {
            var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.Memset(
                    NativePtr,
                    0,
                    new IntPtr(LengthInBytes),
                    stream));

            binding.Recover();
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (NativePtr != IntPtr.Zero)
            {
                CudaException.ThrowIfFailed(
                    CurrentAPI.FreeMemory(NativePtr));
                NativePtr = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
