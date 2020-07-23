// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        where T : unmanaged
        where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
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
            NativePtr = Marshal.AllocHGlobal(
                new IntPtr(extent.Size * Interop.SizeOf<T>()));
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
            var binding = stream.BindScoped();

            var sourceAddress = ComputeEffectiveAddress(sourceOffset);
            var targetAddress = target.LoadEffectiveAddress();
            switch (target.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    Buffer.MemoryCopy(
                        sourceAddress,
                        targetAddress,
                        target.LengthInBytes,
                        target.LengthInBytes);
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyHostToDevice(
                        new IntPtr(targetAddress),
                        new IntPtr(sourceAddress),
                        new IntPtr(target.LengthInBytes),
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
            var binding = stream.BindScoped();

            var sourceAddress = source.LoadEffectiveAddress();
            var targetAddress = ComputeEffectiveAddress(targetOffset);
            switch (source.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    Buffer.MemoryCopy(
                        sourceAddress,
                        targetAddress,
                        LengthInBytes,
                        source.LengthInBytes);
                    break;
                case AcceleratorType.Cuda:
                    CudaException.ThrowIfFailed(CudaAPI.Current.MemcpyDeviceToHost(
                        new IntPtr(targetAddress),
                        new IntPtr(sourceAddress),
                        new IntPtr(source.LengthInBytes),
                        stream));
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            binding.Recover();
        }

        /// <summary cref="MemoryBuffer.MemSetToZero(AcceleratorStream)"/>
        public unsafe override void MemSetToZero(AcceleratorStream stream)
        {
            stream.Synchronize();
            ref byte targetAddress = ref Unsafe.AsRef<byte>(NativePtr.ToPointer());
            for (long offset = 0, e = LengthInBytes; offset < e; offset += uint.MaxValue)
            {
                Unsafe.InitBlock(
                    ref Unsafe.AddByteOffset(
                        ref targetAddress,
                        new IntPtr(offset)),
                    0,
                    (uint)Math.Min(uint.MaxValue, e - offset));
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (NativePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(NativePtr);
                NativePtr = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
