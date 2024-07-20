// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// A memory buffer that lives in CPU space.
    /// </summary>
    public class VelocityMemoryBuffer : MemoryBuffer
    {
        #region Instance

        /// <summary>
        /// Initializes this array view source on the CPU.
        /// </summary>
        /// <param name="accelerator">The parent accelerator (if any).</param>
        /// <param name="length">The length of this source.</param>
        /// <param name="elementSize">The element size.</param>
        internal unsafe VelocityMemoryBuffer(
            Accelerator accelerator,
            long length,
            int elementSize)
            : base(accelerator, length, elementSize)
        {
            // Ensure that all element accesses will be properly aligned
            var nativeLength = (UIntPtr)(length * elementSize);
            var alignment = (UIntPtr)(accelerator as VelocityAccelerator)!.DataAlignment;

            // Allocate resources and assign pointers
            NativePtr = new IntPtr(NativeMemory.AlignedAlloc(nativeLength, alignment));
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected internal override void MemSet(
            AcceleratorStream stream,
            byte value,
            in ArrayView<byte> targetView) =>
            CPUMemoryBuffer.CPUMemSet(
                targetView.LoadEffectiveAddressAsPtr(),
                value,
                0L,
                targetView.LengthInBytes);

        /// <inheritdoc/>
        protected internal override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CPUMemoryBuffer.CPUCopyFrom(stream, sourceView, targetView);

        /// <inheritdoc/>
        protected internal override void CopyTo(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CPUMemoryBuffer.CPUCopyTo(stream, sourceView, targetView);

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the underlying memory buffer.
        /// </summary>
        protected override unsafe void DisposeAcceleratorObject(bool disposing)
        {
            NativeMemory.AlignedFree(NativePtr.ToPointer());
            NativePtr = IntPtr.Zero;
        }

        #endregion

    }

    sealed class VelocityMemoryBufferPool : VelocityMemoryBuffer
    {
        #region Instance

        private int sharedMemoryOffset;

        public VelocityMemoryBufferPool(
            VelocityAccelerator accelerator,
            int size)
            : base(accelerator, size, 1)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Resets the internal shared memory offset.
        /// </summary>
        public void Reset() => sharedMemoryOffset = 0;

        /// <summary>
        /// Gets a chunk of memory of a certain type.
        /// </summary>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The element type to allocate.</typeparam>
        /// <returns>A view pointing to the right chunk of shared memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> Allocate<T>(int length)
            where T : unmanaged
        {
            int totalElementSize = length * Interop.SizeOf<T>();
            int alignment = Interop.ComputeAlignmentOffset(
                sharedMemoryOffset,
                totalElementSize);
            int newOffset = sharedMemoryOffset + alignment;
            sharedMemoryOffset += alignment + totalElementSize;
            return new ArrayView<T>(this, newOffset, length);
        }

        #endregion
    }
}
