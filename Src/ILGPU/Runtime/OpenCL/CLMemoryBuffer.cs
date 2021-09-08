// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an unmanaged OpenCL buffer.
    /// </summary>
    public sealed class CLMemoryBuffer : MemoryBuffer
    {
        #region Static

        /// <summary>
        /// Performs an OpenCL memset operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="stream">The CL stream to use (must not be null)</param>
        /// <param name="value">The value to write into the buffer.</param>
        /// <param name="targetView">The target view to write to.</param>
        public static void CLMemSet<T>(
            CLStream stream,
            byte value,
            in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (targetView.GetAcceleratorType() != AcceleratorType.OpenCL)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            var binding = stream.Accelerator.BindScoped();

            var target = targetView.Buffer;
            CLException.ThrowIfFailed(
                CurrentAPI.FillBuffer(
                    stream,
                    target.NativePtr,
                    value,
                    new IntPtr(targetView.Index * target.ElementSize),
                    new IntPtr(targetView.LengthInBytes)));

            binding.Recover();
        }

        /// <summary>
        /// Performs an OpenCL copy operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="stream">The CL stream to use (must not be null)</param>
        /// <param name="sourceView">The source view to copy from.</param>
        /// <param name="targetView">The target view to copy to.</param>
        public static void CLCopy<T>(
            CLStream stream,
            in ArrayView<T> sourceView,
            in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using var binding = stream.Accelerator.BindScoped();

            var sourceType = sourceView.GetAcceleratorType();
            var targetType = targetView.GetAcceleratorType();

            var source = sourceView.Buffer;
            var target = targetView.Buffer;
            var length = new IntPtr(targetView.LengthInBytes);

            if (sourceType == AcceleratorType.CPU &&
                targetType == AcceleratorType.OpenCL)
            {
                // Copy from CPU to GPU
                CLException.ThrowIfFailed(
                    CurrentAPI.WriteBuffer(
                        stream,
                        target.NativePtr,
                        false,
                        new IntPtr(targetView.Index * ArrayView<T>.ElementSize),
                        new IntPtr(target.LengthInBytes),
                        sourceView.LoadEffectiveAddressAsPtr()));
                return;
            }
            else if (sourceType == AcceleratorType.OpenCL)
            {
                switch (targetType)
                {
                    case AcceleratorType.CPU:
                        // Copy from GPU to CPU
                        CLException.ThrowIfFailed(
                            CurrentAPI.ReadBuffer(
                                stream,
                                source.NativePtr,
                                false,
                                new IntPtr(sourceView.Index * ArrayView<T>.ElementSize),
                                new IntPtr(target.LengthInBytes),
                                targetView.LoadEffectiveAddressAsPtr()));
                        return;
                    case AcceleratorType.OpenCL:
                        // Copy from GPU to GPU
                        CLException.ThrowIfFailed(
                            CurrentAPI.CopyBuffer(
                                stream,
                                source.NativePtr,
                                target.NativePtr,
                                new IntPtr(sourceView.Index * ArrayView<T>.ElementSize),
                                new IntPtr(targetView.Index * ArrayView<T>.ElementSize),
                                new IntPtr(targetView.LengthInBytes)));
                        return;
                }
            }
            throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new CL buffer.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        public CLMemoryBuffer(
            CLAccelerator accelerator,
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
                CLException.ThrowIfFailed(
                    CurrentAPI.CreateBuffer(
                        accelerator.NativePtr,
                        CLBufferFlags.CL_MEM_READ_WRITE,
                        new IntPtr(LengthInBytes),
                        IntPtr.Zero,
                        out IntPtr resultPtr));
                NativePtr = resultPtr;
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected internal override void MemSet(
            AcceleratorStream stream,
            byte value,
            in ArrayView<byte> targetView) =>
            CLMemSet(stream as CLStream, value, targetView);

        /// <inheritdoc/>
        protected internal override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CLCopy(stream as CLStream, sourceView, targetView);

        /// <inheritdoc/>
        protected internal override void CopyTo(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CLCopy(stream as CLStream, sourceView, targetView);

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this OpenCL buffer.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            // Skip buffers with Length = 0
            if (NativePtr == IntPtr.Zero)
                return;

            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.ReleaseBuffer(NativePtr));
            NativePtr = IntPtr.Zero;
        }

        #endregion
    }
}
