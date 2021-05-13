﻿// ---------------------------------------------------------------------------------------
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
using System;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an unmanaged Cuda buffer.
    /// </summary>
    public sealed class CudaMemoryBuffer : MemoryBuffer
    {
        #region Static

        /// <summary>
        /// Performs a Cuda memset operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="stream">The Cuda stream to use (must not be null)</param>
        /// <param name="value">The value to write into the buffer.</param>
        /// <param name="targetView">The target view to write to.</param>
        public static void CudaMemSet<T>(
            CudaStream stream,
            byte value,
            in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (targetView.GetAcceleratorType() != AcceleratorType.Cuda)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            var binding = stream.Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.Memset(
                    targetView.LoadEffectiveAddressAsPtr(),
                    value,
                    new IntPtr(targetView.LengthInBytes),
                    stream));

            binding.Recover();
        }

        /// <summary>
        /// Performs a Cuda copy operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="stream">The Cuda stream to use (must not be null)</param>
        /// <param name="sourceView">The source view to copy from.</param>
        /// <param name="targetView">The target view to copy to.</param>
        public static void CudaCopy<T>(
            CudaStream stream,
            in ArrayView<T> sourceView,
            in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using var binding = stream.Accelerator.BindScoped();

            var sourceType = sourceView.GetAcceleratorType();
            var targetType = targetView.GetAcceleratorType();

            if (sourceType == AcceleratorType.OpenCL ||
                targetType == AcceleratorType.OpenCL)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            var sourceAddress = sourceView.LoadEffectiveAddressAsPtr();
            var targetAddress = targetView.LoadEffectiveAddressAsPtr();

            var length = new IntPtr(targetView.LengthInBytes);

            // a) Copy from CPU to GPU
            // b) Copy from GPU to CPU
            // c) Copy from GPU to GPU
            CudaException.ThrowIfFailed(
                CurrentAPI.MemcpyAsync(
                    targetAddress,
                    sourceAddress,
                    length,
                    stream));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Cuda buffer.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        public CudaMemoryBuffer(
            CudaAccelerator accelerator,
            long length,
            int elementSize)
            : base(accelerator, length, elementSize)
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.AllocateMemory(
                    out IntPtr resultPtr,
                    new IntPtr(LengthInBytes)));
            NativePtr = resultPtr;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override unsafe void MemSet(
            AcceleratorStream stream,
            byte value,
            long targetOffsetInBytes,
            long length)
        {
            var targetView = AsRawArrayView(targetOffsetInBytes, length);
            CudaMemSet(stream as CudaStream, value, targetView);
        }

        /// <inheritdoc/>
        public override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            long targetOffsetInBytes)
        {
            var targetView = AsRawArrayView(
                targetOffsetInBytes,
                sourceView.LengthInBytes);
            CudaCopy(stream as CudaStream, sourceView, targetView);
        }

        /// <inheritdoc/>
        public override unsafe void CopyTo(
            AcceleratorStream stream,
            long sourceOffsetInBytes,
            in ArrayView<byte> targetView)
        {
            var sourceView = AsRawArrayView(
                sourceOffsetInBytes,
                targetView.LengthInBytes);
            CudaCopy(stream as CudaStream, sourceView, targetView);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this Cuda buffer.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.FreeMemory(NativePtr));
            NativePtr = IntPtr.Zero;
        }

        #endregion
    }
}
