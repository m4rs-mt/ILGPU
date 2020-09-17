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

using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Specifies the allocation mode for a single exchange buffer.
    /// </summary>
    public enum ExchangeBufferMode
    {
        /// <summary>
        /// Prefer paged locked memory for improved transfer speeds.
        /// </summary>
        PreferPagedLockedMemory = 0,

        /// <summary>
        /// Allocate CPU memory in pageable memory to leverage virtual memory.
        /// </summary>
        UsePageablememory = 1,
    }

    /// <summary>
    /// The base class for all exchange buffers.
    /// Contains methods and types that are shared by all implementations.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public unsafe class ExchangeBufferBase<T, TIndex> :
        MemoryBuffer,
        IMemoryBuffer<T>
        where T : unmanaged
        where TIndex : unmanaged, IGenericIndex<TIndex>
    {
        #region Constants

        /// <summary>
        /// Represents the size of an element in bytes.
        /// </summary>
        public static readonly int ElementSize = MemoryBuffer<T>.ElementSize;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a view source that allocates native memory in page-locked CPU.
        /// memory.
        /// </summary>
        protected class CudaViewSource : ViewPointerWrapper
        {
            /// <summary>
            /// Creates a new Cuda view source.
            /// </summary>
            /// <param name="sizeInBytes">The size in bytes to allocate.</param>
            /// <returns>An unsafe array view source.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CudaViewSource Create(long sizeInBytes)
            {
                CudaException.ThrowIfFailed(
                    CurrentAPI.AllocateHostMemory(
                        out IntPtr hostPtr,
                        new IntPtr(sizeInBytes)));
                return new CudaViewSource(hostPtr);
            }

            /// <summary>
            /// Creates a new unmanaged memory view source.
            /// </summary>
            /// <param name="nativePtr">The native host pointer.</param>
            private CudaViewSource(IntPtr nativePtr)
                : base(nativePtr)
            { }

            /// <summary cref="ArrayViewSource.GetAsRawArray(
            /// AcceleratorStream, long, long)"/>
            protected internal override ArraySegment<byte> GetAsRawArray(
                AcceleratorStream stream,
                long byteOffset,
                long byteExtent) => throw new InvalidOperationException();

            #region IDispoable

            /// <summary cref="DisposeBase.Dispose(bool)"/>
            protected override void Dispose(bool disposing)
            {
                if (NativePtr != IntPtr.Zero)
                {
                    CurrentAPI.FreeHostMemory(NativePtr);
                    NativePtr = IntPtr.Zero;
                }
                base.Dispose(disposing);
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// The internally allocated CPU memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ViewPointerWrapper cpuMemory;

        /// <summary>
        /// Property for accessing cpuMemory.
        /// </summary>
        protected ViewPointerWrapper CPUMemory => cpuMemory;

        /// <summary>
        /// A cached version of the CPU memory pointer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly void* cpuMemoryPointer;

        /// <summary>
        /// Property for accessing cpuMemoryPointer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [CLSCompliant(false)]
        protected void* CPUMemoryPointer => cpuMemoryPointer;

        /// <summary>
        /// Constructs the base class for all exchange buffer implementations.
        /// </summary>
        /// <param name="buffer">The memory buffer to use.</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        internal ExchangeBufferBase(MemoryBuffer<T, TIndex> buffer,
            ExchangeBufferMode mode)
            : base(buffer.Accelerator, buffer.Extent.Size)
        {
            cpuMemory = buffer.Accelerator is CudaAccelerator &&
                mode == ExchangeBufferMode.PreferPagedLockedMemory
                ? CudaViewSource.Create(buffer.LengthInBytes)
                : (ViewPointerWrapper)UnmanagedMemoryViewSource.Create(
                    buffer.LengthInBytes);

            cpuMemoryPointer = cpuMemory.NativePtr.ToPointer();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying generic memory buffer.
        /// </summary>
        public MemoryBuffer<T, TIndex> Buffer { get; protected set; }

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        public ArrayView<T, TIndex> View { get; protected set; }

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public long LengthInBytes => Buffer.LengthInBytes;

        /// <summary>
        /// Returns the extent of this buffer.
        /// </summary>
        public TIndex Extent { get; protected set; }

        /// <summary>
        /// Internal array view
        /// </summary>
        protected ArrayView<T, TIndex> CPUArrayView { get; set; }

        /// <summary>
        /// The part of this buffer in CPU memory
        /// </summary>
        /// <remarks>
        /// Is obsolete, sole purpose is to prevent a breaking API change
        /// </remarks>
        [Obsolete("Replaced by Span property")]
        public ArrayView<T, TIndex> CPUView => CPUArrayView;

        /// <summary>
        /// Returns a span to the part of this buffer in CPU memory
        /// </summary>
        public Span<T> Span => new Span<T>(cpuMemoryPointer, (int)Length);

        /// <summary>
        /// Returns a reference to the i-th element in CPU memory.
        /// </summary>
        /// <param name="index">The element index to access.</param>
        /// <returns>A reference to the i-th element in CPU memory.</returns>
        public ref T this[TIndex index] => ref CPUArrayView[index];

        #endregion

        #region Methods

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        public override void MemSetToZero(AcceleratorStream stream) =>
            Buffer.MemSetToZero(stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public override byte[] GetAsRawArray(AcceleratorStream stream) =>
            Buffer.GetAsRawArray(stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="byteOffset">The offset in bytes.</param>
        /// <param name="byteExtent">The extent in bytes (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        protected internal override ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            long byteOffset,
            long byteExtent) =>
            Buffer.GetAsRawArray(stream, byteOffset, byteExtent);

        /// <summary>
        /// Copies the current contents into a new array using the default
        /// accelerator stream.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray() => GetAsArray(Accelerator.DefaultStream);

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public unsafe T[] GetAsArray(AcceleratorStream stream) =>
            Buffer.GetAsArray(stream);

        /// <summary>
        /// Copes data from CPU memory to the associated accelerator.
        /// </summary>
        public void CopyToAccelerator() =>
            CopyToAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyToAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Buffer.CopyFromView(stream, CPUArrayView.AsLinearView(), 0);
        }

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="acceleratorMemoryOffset">The target memory offset.</param>
        public void CopyToAccelerator(TIndex acceleratorMemoryOffset) =>
            CopyToAccelerator(
                Accelerator.DefaultStream,
                default,
                acceleratorMemoryOffset,
                CPUArrayView.Length);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="acceleratorMemoryOffset">The target memory offset.</param>
        public void CopyToAccelerator(
            AcceleratorStream stream,
            TIndex acceleratorMemoryOffset) =>
            CopyToAccelerator(
                stream,
                default,
                acceleratorMemoryOffset,
                CPUArrayView.Length);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="cpuMemoryOffset">the source memory offset.</param>
        /// <param name="acceleratorMemoryOffset">The target memory offset.</param>
        public void CopyToAccelerator(
            TIndex cpuMemoryOffset,
            TIndex acceleratorMemoryOffset) =>
            CopyToAccelerator(
                Accelerator.DefaultStream,
                cpuMemoryOffset,
                acceleratorMemoryOffset,
                CPUArrayView.Length);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="cpuMemoryOffset">the source memory offset.</param>
        /// <param name="acceleratorMemoryOffset">The target memory offset.</param>
        public void CopyToAccelerator(
            AcceleratorStream stream,
            TIndex cpuMemoryOffset,
            TIndex acceleratorMemoryOffset) =>
            CopyToAccelerator(
                stream,
                cpuMemoryOffset,
                acceleratorMemoryOffset,
                CPUArrayView.Length);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="cpuMemoryOffset">the source memory offset.</param>
        /// <param name="acceleratorMemoryOffset">The target memory offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyToAccelerator(
            TIndex cpuMemoryOffset,
            TIndex acceleratorMemoryOffset,
            LongIndex1 extent) =>
            CopyToAccelerator(
                Accelerator.DefaultStream,
                cpuMemoryOffset,
                acceleratorMemoryOffset,
                extent);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="cpuMemoryOffset">the source memory offset.</param>
        /// <param name="acceleratorMemoryOffset">The target memory offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyToAccelerator(
            AcceleratorStream stream,
            TIndex cpuMemoryOffset,
            TIndex acceleratorMemoryOffset,
            LongIndex1 extent)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!cpuMemoryOffset.InBounds(CPUArrayView.Extent))
                throw new ArgumentOutOfRangeException(nameof(cpuMemoryOffset));
            if (!acceleratorMemoryOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(acceleratorMemoryOffset));
            if (extent < 1 || extent > CPUArrayView.Length)
                throw new ArgumentOutOfRangeException(nameof(extent));
            var linearSourceIndex =
                    cpuMemoryOffset.ComputeLinearIndex(CPUArrayView.Extent);
            var linearTargetIndex =
                    acceleratorMemoryOffset.ComputeLinearIndex(Extent);
            if (linearSourceIndex + extent > CPUArrayView.Length ||
                linearTargetIndex + extent > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            Buffer.CopyFromView(
                stream,
                CPUArrayView.GetSubView(cpuMemoryOffset, extent),
                linearTargetIndex);
        }

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        public void CopyFromAccelerator() =>
            CopyFromAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyFromAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            Buffer.CopyToView(stream, CPUArrayView.BaseView, 0);
        }

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="cpuMemoryOffset">The target memory offset.</param>
        public void CopyFromAccelerator(TIndex cpuMemoryOffset) =>
            CopyFromAccelerator(
                Accelerator.DefaultStream,
                default,
                cpuMemoryOffset,
                CPUArrayView.Length);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="cpuMemoryOffset">the target memory offset.</param>
        public void CopyFromAccelerator(
            AcceleratorStream stream,
            TIndex cpuMemoryOffset) =>
            CopyFromAccelerator(
                stream,
                default,
                cpuMemoryOffset,
                Length);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="acceleratorMemoryOffset">The source memory offset.</param>
        /// <param name="cpuMemoryOffset">the target memory offset.</param>
        public void CopyFromAccelerator(
            TIndex cpuMemoryOffset,
            TIndex acceleratorMemoryOffset) =>
            CopyFromAccelerator(
                Accelerator.DefaultStream,
                acceleratorMemoryOffset,
                cpuMemoryOffset,
                Length);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="acceleratorMemoryOffset">The source memory offset.</param>
        /// <param name="cpuMemoryOffset">the target memory offset.</param>
        public void CopyFromAccelerator(
            AcceleratorStream stream,
            TIndex cpuMemoryOffset,
            TIndex acceleratorMemoryOffset) =>
            CopyFromAccelerator(
                stream,
                acceleratorMemoryOffset,
                cpuMemoryOffset,
                Length);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="acceleratorMemoryOffset">The source memory offset.</param>
        /// <param name="cpuMemoryOffset">the target memory offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyFromAccelerator(
            TIndex acceleratorMemoryOffset,
            TIndex cpuMemoryOffset,
            LongIndex1 extent) =>
            CopyFromAccelerator(
                Accelerator.DefaultStream,
                acceleratorMemoryOffset,
                cpuMemoryOffset,
                extent);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="acceleratorMemoryOffset">The source memory offset.</param>
        /// <param name="cpuMemoryOffset">the target memory offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyFromAccelerator(
            AcceleratorStream stream,
            TIndex acceleratorMemoryOffset,
            TIndex cpuMemoryOffset,
            LongIndex1 extent)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!cpuMemoryOffset.InBounds(CPUArrayView.Extent))
                throw new ArgumentOutOfRangeException(nameof(cpuMemoryOffset));
            if (!acceleratorMemoryOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(acceleratorMemoryOffset));
            if (extent < 1 || extent > Length)
                throw new ArgumentOutOfRangeException(nameof(extent));
            var linearSourceIndex =
                    acceleratorMemoryOffset.ComputeLinearIndex(Extent);
            var linearTargetIndex =
                    cpuMemoryOffset.ComputeLinearIndex(CPUArrayView.Extent);
            if (linearSourceIndex + extent > Length ||
                linearTargetIndex + extent > CPUArrayView.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            Buffer.CopyToView(
                stream,
                CPUArrayView.GetSubView(cpuMemoryOffset, extent),
                linearSourceIndex);
        }

        /// <summary>
        /// Gets the part of this buffer on CPU memory as a 2D View.
        /// </summary>
        /// <param name="extent">The view extent.</param>
        /// <returns>The view.</returns>
        public ArrayView2D<T> As2DView(LongIndex2 extent) =>
            CPUArrayView.BaseView.As2DView<T>(extent);

        /// <summary>
        /// Gets the part of this buffer on CPU memory as a 3D View.
        /// </summary>
        /// <param name="extent">The view extent.</param>
        /// <returns>The view.</returns>
        public ArrayView3D<T> As3DView(LongIndex3 extent) =>
            CPUArrayView.BaseView.As3DView<T>(extent);

        /// <summary>
        /// Gets this exchange buffer as a <see cref="Span{T}"/>, copying from the
        /// accelerator in the process
        /// </summary>
        /// <returns>
        /// The <see cref="Span{T}"/> which accesses the part of this buffer on the CPU.
        /// Uses the default accelerator stream.
        /// </returns>
        public Span<T> GetAsSpan() =>
            GetAsSpan(Accelerator.DefaultStream);

        /// <summary>
        /// Gets this exchange buffer as a <see cref="Span{T}"/>, copying from the
        /// accelerator in the process.
        /// </summary>
        /// <param name="stream">The stream to use</param>
        /// <returns>
        /// The <see cref="Span{T}"/> which accesses the part of this buffer on the CPU.
        /// </returns>
        public Span<T> GetAsSpan(AcceleratorStream stream)
        {
            CopyFromAccelerator(stream);
            stream.Synchronize();
            return Span;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Buffer.Dispose();
                cpuMemory.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
