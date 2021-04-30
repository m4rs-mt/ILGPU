// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ExchangeBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using System;
using System.Diagnostics;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Specifies the allocation mode for a single exchange buffer.
    /// </summary>
    public enum ExchangeBufferMode
    {
        /// <summary>
        /// Prefer page locked memory for improved transfer speeds.
        /// </summary>
        PreferPageLockedMemory = 0,

        /// <summary>
        /// Allocate CPU memory in pageable memory to leverage virtual memory.
        /// </summary>
        UsePageablememory = 1,
    }

    /// <summary>
    /// Represents an opaque memory buffer that contains a GPU and a CPU back buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public class ExchangeBuffer<T> : AcceleratorObject
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Initializes a new basic exchange buffer.
        /// </summary>
        /// <param name="gpuBuffer">The parent GPU buffer to use.</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        /// <remarks>
        /// CAUTION: The ownership of the <paramref name="gpuBuffer"/> is transferred to
        /// this instance.
        /// </remarks>
        protected internal ExchangeBuffer(
            MemoryBuffer<ArrayView1D<T, Stride1D.Dense>> gpuBuffer,
            ExchangeBufferMode mode)
            : base(gpuBuffer.Accelerator)
        {
            GPUBuffer = gpuBuffer;
            CPUBuffer = Accelerator is CudaAccelerator &&
                mode == ExchangeBufferMode.PreferPageLockedMemory
                ? CPUMemoryBuffer.CreatePinned(
                    Accelerator,
                    gpuBuffer.LengthInBytes,
                    gpuBuffer.ElementSize)
                : CPUMemoryBuffer.Create(gpuBuffer.LengthInBytes, gpuBuffer.ElementSize);
            CPUView = new ArrayView<T>(CPUBuffer, 0, gpuBuffer.Length);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the owned and underlying CPU buffer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected CPUMemoryBuffer CPUBuffer { get; }

        /// <summary>
        /// Returns the owned and underlying GPU buffer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected MemoryBuffer<ArrayView1D<T, Stride1D.Dense>> GPUBuffer { get; }

        /// <summary>
        /// The CPU view representing the allocated chunk of memory.
        /// </summary>
        public ArrayView1D<T, Stride1D.Dense> CPUView { get; }

        /// <summary>
        /// The GPU view representing the allocated chunk of memory.
        /// </summary>
        public ArrayView1D<T, Stride1D.Dense> GPUView => GPUBuffer.View;

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public long Length => GPUBuffer.Length;

        /// <summary>
        /// Returns the element size.
        /// </summary>
        public int ElementSize => GPUBuffer.ElementSize;

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public long LengthInBytes => GPUBuffer.LengthInBytes;

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public LongIndex1D Extent => GPUView.Extent;

        /// <summary>
        /// Returns the 32-bit extent of this view.
        /// </summary>
        public Index1D IntExtent => GPUView.IntExtent;

        /// <summary>
        /// Returns a reference to the i-th element on the CPU.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>A reference to the i-th element on the CPU.</returns>
        public ref T this[Index1D index] => ref CPUView[index];

        /// <summary>
        /// Returns a reference to the i-th element on the CPU.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>A reference to the i-th element on the CPU.</returns>
        public ref T this[LongIndex1D index] => ref CPUView[index];

        #endregion

        #region Methods

        /// <summary>
        /// Sets the contents of both buffers to zero using the default accelerator
        /// stream.
        /// </summary>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public void MemSetToZero() => MemSetToZero(GPUView.GetDefaultStream());

        /// <summary>
        /// Sets the contents of both buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public void MemSetToZero(AcceleratorStream stream) => MemSet(stream, 0);

        /// <summary>
        /// Sets the contents of both buffer to the given byte value using the default
        /// accelerator stream.
        /// </summary>
        /// <param name="value">The value to write into the memory buffer.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public void MemSet(byte value) => MemSet(GPUView.GetDefaultStream(), value);

        /// <summary>
        /// Sets the contents of both buffers to the given byte value.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="value">The value to write into the memory buffer.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public void MemSet(AcceleratorStream stream, byte value)
        {
            GPUView.MemSet(stream, value);
            CPUView.MemSet(stream, value);
        }

        /// <summary>
        /// Copes data from CPU memory to the associated accelerator.
        /// </summary>
        public void CopyToAcceleratorAsync() =>
            CopyToAcceleratorAsync(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyToAcceleratorAsync(AcceleratorStream stream) =>
            CopyToAcceleratorAsync(stream, 0L, Length);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="offset">The target memory offset.</param>
        /// <param name="length">The length (number of elements).</param>
        public void CopyToAcceleratorAsync(long offset, long length) =>
            CopyToAcceleratorAsync(Accelerator.DefaultStream, offset, length);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="offset">The element offset.</param>
        /// <param name="length">The length (number of elements).</param>
        public void CopyToAcceleratorAsync(
            AcceleratorStream stream,
            long offset,
            long length)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var sourceView = CPUView.SubView(offset, length);
            var targetView = GPUView.SubView(offset, length);

            sourceView.CopyTo(stream, targetView);
        }

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        public void CopyFromAcceleratorAsync() =>
            CopyFromAcceleratorAsync(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyFromAcceleratorAsync(AcceleratorStream stream) =>
            CopyFromAcceleratorAsync(stream, 0L, Length);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="offset">The element offset.</param>
        /// <param name="length">The length (number of elements).</param>
        public void CopyFromAcceleratorAsync(long offset, long length) =>
            CopyFromAcceleratorAsync(Accelerator.DefaultStream, offset, length);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="offset">The element offset.</param>
        /// <param name="length">The length (number of elements).</param>
        public void CopyFromAcceleratorAsync(
            AcceleratorStream stream,
            long offset,
            long length)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var sourceView = GPUView.SubView(offset, length);
            var targetView = CPUView.SubView(offset, length);
            sourceView.CopyTo(stream, targetView);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Frees the underlying CPU and GPU memory handles.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (!disposing)
                return;

            CPUBuffer.Dispose();
            GPUBuffer.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Exchange buffer utility and extension methods.
    /// </summary>
    public static class ExchangeBufferExtensions
    {
        /// <summary>
        /// Allocates an exchange buffer using the mode
        /// <see cref="ExchangeBufferMode.PreferPageLockedMemory"/>
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <param name="length">The number of elements to allocate.</param>
        /// <returns>The allocated exchange mode.</returns>
        public static ExchangeBuffer<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            long length)
            where T : unmanaged =>
            accelerator.AllocateExchangeBuffer<T>(
                length,
                ExchangeBufferMode.PreferPageLockedMemory);

        /// <summary>
        /// Allocates an exchange buffer using the given mode.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <param name="length">The number of elements to allocate.</param>
        /// <param name="mode">The buffer mode to use.</param>
        /// <returns>The allocated exchange mode.</returns>
        public static ExchangeBuffer<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            long length,
            ExchangeBufferMode mode)
            where T : unmanaged =>
            new ExchangeBuffer<T>(accelerator.Allocate1D<T>(length), mode);
    }
}
