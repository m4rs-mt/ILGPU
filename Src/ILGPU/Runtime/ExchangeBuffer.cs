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
        /// Prefer page locked memory for improved transfer speeds.
        /// </summary>
        PreferPageLockedMemory = 0,

        /// <summary>
        /// Prefer paged locked memory for improved transfer speeds.
        /// </summary>
        [Obsolete("Use PreferPageLockedMemory instead")]
        PreferPagedLockedMemory = PreferPageLockedMemory,

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
    public abstract unsafe class ExchangeBufferBase<T, TIndex> :
        MemoryBuffer<T, TIndex>,
        IMemoryBuffer<T>
        where T : unmanaged
        where TIndex : unmanaged, IGenericIndex<TIndex>
    {
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

            #region IDisposable

            /// <summary>
            /// Frees the Cuda host memory.
            /// </summary>
            protected override void DisposeAcceleratorObject(bool disposing)
            {
                if (NativePtr == IntPtr.Zero)
                    return;

                CurrentAPI.FreeHostMemory(NativePtr);
                NativePtr = IntPtr.Zero;
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs the base class for all exchange buffer implementations.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        protected ExchangeBufferBase(
            Accelerator accelerator,
            TIndex extent,
            ExchangeBufferMode mode)
            : base(accelerator, extent)
        {
            CPUMemory = Accelerator is CudaAccelerator &&
                mode == ExchangeBufferMode.PreferPageLockedMemory
                ? CudaViewSource.Create(LengthInBytes)
                : (ViewPointerWrapper)UnmanagedMemoryViewSource.Create(LengthInBytes);

            var baseView = new ArrayView<T>(CPUMemory, 0, Length);
            CPUView = new ArrayView<T, TIndex>(baseView, extent);
        }

        #endregion

        #region Properties

        /// <summary>
        /// The CPU view representing the allocated chunk of memory.
        /// </summary>
        public ArrayView<T, TIndex> CPUView { get; }

        /// <summary>
        /// Returns a span to the part of this buffer in CPU memory.
        /// </summary>
        public Span<T> Span => new Span<T>(CPUMemoryPointer, (int)Length);

        /// <summary>
        /// Returns a reference to the i-th element in CPU memory.
        /// </summary>
        /// <param name="index">The element index to access.</param>
        /// <returns>A reference to the i-th element in CPU memory.</returns>
        public ref T this[TIndex index] => ref CPUView[index];

        /// <summary>
        /// Property for accessing cpuMemory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected ViewPointerWrapper CPUMemory { get; }

        /// <summary>
        /// Property for accessing cpuMemoryPointer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [CLSCompliant(false)]
        protected void* CPUMemoryPointer => CPUMemory.NativePtr.ToPointer();

        #endregion

        #region Methods

        /// <summary>
        /// Copes data from CPU memory to the associated accelerator.
        /// </summary>
        public void CopyToAccelerator() => CopyToAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyToAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            CopyFromView(stream, CPUView.AsLinearView(), 0);
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
                CPUView.Length);

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
                CPUView.Length);

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
                CPUView.Length);

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
                CPUView.Length);

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
            if (!cpuMemoryOffset.InBounds(CPUView.Extent))
                throw new ArgumentOutOfRangeException(nameof(cpuMemoryOffset));
            if (!acceleratorMemoryOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(acceleratorMemoryOffset));
            if (extent < 1 || extent > CPUView.Length)
                throw new ArgumentOutOfRangeException(nameof(extent));
            var linearSourceIndex =
                    cpuMemoryOffset.ComputeLinearIndex(CPUView.Extent);
            var linearTargetIndex =
                    acceleratorMemoryOffset.ComputeLinearIndex(Extent);
            if (linearSourceIndex + extent > CPUView.Length ||
                linearTargetIndex + extent > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            CopyFromView(
                stream,
                CPUView.GetSubView(cpuMemoryOffset, extent),
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
            CopyToView(stream, CPUView.BaseView, 0);
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
                CPUView.Length);

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
            if (!cpuMemoryOffset.InBounds(CPUView.Extent))
                throw new ArgumentOutOfRangeException(nameof(cpuMemoryOffset));
            if (!acceleratorMemoryOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(acceleratorMemoryOffset));
            if (extent < 1 || extent > Length)
                throw new ArgumentOutOfRangeException(nameof(extent));
            var linearSourceIndex =
                    acceleratorMemoryOffset.ComputeLinearIndex(Extent);
            var linearTargetIndex =
                    cpuMemoryOffset.ComputeLinearIndex(CPUView.Extent);
            if (linearSourceIndex + extent > Length ||
                linearTargetIndex + extent > CPUView.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            CopyToView(
                stream,
                CPUView.GetSubView(cpuMemoryOffset, extent),
                linearSourceIndex);
        }

        /// <summary>
        /// Gets the part of this buffer on CPU memory as a 2D View.
        /// </summary>
        /// <param name="extent">The view extent.</param>
        /// <returns>The view.</returns>
        public ArrayView2D<T> As2DView(LongIndex2 extent) =>
            CPUView.BaseView.As2DView(extent);

        /// <summary>
        /// Gets the part of this buffer on CPU memory as a 3D View.
        /// </summary>
        /// <param name="extent">The view extent.</param>
        /// <returns>The view.</returns>
        public ArrayView3D<T> As3DView(LongIndex3 extent) =>
            CPUView.BaseView.As3DView(extent);

        /// <summary>
        /// Gets this exchange buffer as a <see cref="Span{T}"/>, copying from the
        /// accelerator in the process
        /// </summary>
        /// <returns>
        /// The <see cref="Span{T}"/> which accesses the part of this buffer on the CPU.
        /// Uses the default accelerator stream.
        /// </returns>
        public Span<T> GetAsSpan() => GetAsSpan(Accelerator.DefaultStream);

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

        /// <summary>
        /// Frees the underlying CPU memory handles.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
                CPUMemory.Dispose();
        }

        #endregion
    }
}
