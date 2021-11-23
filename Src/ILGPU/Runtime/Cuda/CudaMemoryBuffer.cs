﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
            if (targetView.GetAcceleratorType() != AcceleratorType.Cuda)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            CudaException.ThrowIfFailed(
                CurrentAPI.Memset(
                    targetView.LoadEffectiveAddressAsPtr(),
                    value,
                    new IntPtr(targetView.LengthInBytes),
                    stream));
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
            if (LengthInBytes == 0)
            {
                NativePtr = IntPtr.Zero;
            }
            else
            {
                CudaException.ThrowIfFailed(
                    CurrentAPI.AllocateMemory(
                        out IntPtr resultPtr,
                        new IntPtr(LengthInBytes)));
                NativePtr = resultPtr;
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected internal override unsafe void MemSet(
            AcceleratorStream stream,
            byte value,
            in ArrayView<byte> targetView) =>
            CudaMemSet(stream as CudaStream, value, targetView);

        /// <inheritdoc/>
        protected internal override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CudaCopy(stream as CudaStream, sourceView, targetView);

        /// <inheritdoc/>
        protected internal override unsafe void CopyTo(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CudaCopy(stream as CudaStream, sourceView, targetView);

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
