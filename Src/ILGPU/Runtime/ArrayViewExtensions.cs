// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArrayViewExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime.CPU;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Array view extension methods
    /// </summary>
    public static partial class ArrayViewExtensions
    {
        #region ArrayView

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <returns>The effective address.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [NotInsideKernel]
        public static ref byte LoadEffectiveAddress<T>(this ArrayView<T> view)
            where T : unmanaged =>
            ref view.LoadEffectiveAddress();

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <returns>The effective address.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [NotInsideKernel]
        public static IntPtr LoadEffectiveAddressAsPtr<T>(this ArrayView<T> view)
            where T : unmanaged =>
            view.LoadEffectiveAddressAsPtr();

        /// <summary>
        /// Aligns the given array view to the specified alignment in bytes and returns a
        /// view spanning the initial unaligned parts of the given view and another
        /// view (main) spanning the remaining aligned elements of the given view.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="alignmentInBytes">The basic alignment in bytes.</param>
        /// <returns>
        /// The prefix and main views pointing to non-aligned and aligned sub-views of
        /// the given view.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ArrayView<T> prefix, ArrayView<T> main) AlignTo<T>(
            this ArrayView<T> view,
            int alignmentInBytes)
            where T : unmanaged
        {
            Trace.Assert(
                alignmentInBytes > 0 &
                (alignmentInBytes % Interop.SizeOf<T>() == 0),
                "Invalid alignment in bytes");

            return view.AlignToInternal(alignmentInBytes);
        }

        /// <summary>
        /// Aligns the given array view to the specified alignment in bytes and returns a
        /// view spanning the initial unaligned parts of the given view and another
        /// view (main) spanning the remaining aligned elements of the given view.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="alignmentInBytes">The basic alignment in bytes.</param>
        /// <returns>
        /// The prefix and main views pointing to non-aligned and aligned sub-views of
        /// the given view.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ArrayView<T> prefix, ArrayView<T> main) AlignTo<T>(
            this ArrayView1D<T, Stride1D.Dense> view,
            int alignmentInBytes)
            where T : unmanaged =>
            view.BaseView.AlignTo(alignmentInBytes);

        /// <summary>
        /// Returns a variable view to the given element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="element">The element index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableView<T> VariableView<T>(
            this ArrayView<T> view,
            Index1D element)
            where T : unmanaged =>
            new VariableView<T>(view.SubView(element, 1));

        /// <summary>
        /// Returns a variable view to the given element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="element">The element index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableView<T> VariableView<T>(
            this ArrayView<T> view,
            LongIndex1D element)
            where T : unmanaged =>
            new VariableView<T>(view.SubView(element, 1L));

        #endregion

        #region Base Methods

        /// <summary>
        /// Returns true if the current view is not valid or does not span over a single
        /// element (length &lt; 1).
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>True, this view has no data.</returns>
        public static bool HasNoData<TView>(this TView view)
            where TView : IArrayView =>
            !view.IsValid || view.Length < 1;

        /// <summary>
        /// Returns true if the current view is valid and includes at least a single
        /// element (length &gt; 0).
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>True, this view has a least one valid data element.</returns>
        public static bool HasData<TView>(this TView view)
            where TView : IArrayView =>
            view.IsValid & view.Length > 0;

        /// <summary>
        /// Returns the associated accelerator of the current view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The associated parent accelerator.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Accelerator GetAccelerator<TView>(this TView view)
            where TView : IArrayView
        {
            var parentBuffer = view.Buffer;
            return parentBuffer is null
                ? throw new InvalidOperationException(
                    RuntimeErrorMessages.UnknownParentAccelerator)
                : parentBuffer.Accelerator;
        }

        /// <summary>
        /// Returns the associated parent context of the current view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The associated parent context.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Context GetContext<TView>(this TView view)
            where TView : IArrayView =>
            view.GetAccelerator().Context;

        /// <summary>
        /// Returns the associated accelerator of the current view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The associated parent accelerator.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ContextProperties GetContextProperties<TView>(this TView view)
            where TView : IArrayView =>
            view.GetContext().Properties;

        /// <summary>
        /// Returns the associated accelerator type of the current view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The associated parent accelerator type.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AcceleratorType GetAcceleratorType<TView>(this TView view)
            where TView : IArrayView =>
            view.Buffer.AcceleratorType;

        /// <summary>
        /// Returns the associated default stream of the parent accelerator.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The default stream of the parent accelerator.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AcceleratorStream GetDefaultStream<TView>(this TView view)
            where TView : IArrayView =>
            view.GetAccelerator().DefaultStream;

        /// <summary>
        /// Returns the current page locking mode.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The current page locking mode.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PageLockingMode GetPageLockingMode<TView>(this TView view)
            where TView : IArrayView =>
            view.GetContextProperties().PageLockingMode;

        /// <summary>
        /// Returns true if the view is attached to a context using
        /// <see cref="PageLockingMode.Auto"/>.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>True, if the parent context uses automatic page locking.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UsesAutoPageLocking<TView>(this TView view)
            where TView : IArrayView =>
            view.GetPageLockingMode() >= PageLockingMode.Auto;

        #endregion

        #region MemSet

        /// <summary>
        /// Sets the contents of the given buffer to zero using the default accelerator
        /// stream.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static void MemSetToZero<TView>(this TView view)
            where TView : IContiguousArrayView =>
            view.MemSetToZero(view.GetDefaultStream());

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static void MemSetToZero<TView>(
            this TView view,
            AcceleratorStream stream)
            where TView : IContiguousArrayView =>
            view.MemSet(stream, 0);

        /// <summary>
        /// Sets the contents of the given buffer to the given byte value using the
        /// default accelerator stream.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <param name="value">The value to write into the memory buffer.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static void MemSet<TView>(
            this TView view,
            byte value)
            where TView : IContiguousArrayView =>
            view.MemSet(view.GetDefaultStream(), value);

        /// <summary>
        /// Sets the contents of the current buffer to the given byte value.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="value">The value to write into the memory buffer.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static void MemSet<TView>(
            this TView view,
            AcceleratorStream stream,
            byte value)
            where TView : IContiguousArrayView
        {
            if (!view.IsValid)
                throw new ArgumentNullException(nameof(view));
            if (view.HasNoData())
                return;

            var rawView = view.AsRawArrayView();
            view.Buffer.MemSet(
                stream,
                value,
                rawView.Index,
                rawView.Length);
        }

        #endregion

        #region Copy from/to Views

        /// <summary>
        /// Copies from the source view into the target view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="target">The target view instance.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<TView>(
            this TView source,
            in TView target)
            where TView : IContiguousArrayView =>
            source.CopyTo(source.GetDefaultStream(), target);

        /// <summary>
        /// Copies from the source view into the target view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target view instance.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<TView>(
            this TView source,
            AcceleratorStream stream,
            in TView target)
            where TView : IContiguousArrayView =>
            source.Buffer.CopyTo(
                stream,
                source.IndexInBytes,
                target.AsRawArrayView());

        /// <summary>
        /// Copies from the source view into the target view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="target">The target view instance.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<TView>(
            this TView target,
            in TView source)
            where TView : IContiguousArrayView =>
            target.CopyFrom(target.GetDefaultStream(), source);

        /// <summary>
        /// Copies from the source view into the target view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source view instance.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<TView>(
            this TView target,
            AcceleratorStream stream,
            in TView source)
            where TView : IContiguousArrayView =>
            target.Buffer.CopyFrom(
                stream,
                source.AsRawArrayView(),
                target.IndexInBytes);

        #endregion

        #region Copy elements to/from CPU async

        /// <summary>
        /// Copies from the source view into the given CPU target address without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="cpuData">The base address of the pinned CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToCPUUnsafeAsync<T, TView>(
            this TView source,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            source.CopyToCPUUnsafeAsync(
                source.GetDefaultStream(),
                ref cpuData,
                length);

        /// <summary>
        /// Copies from the source view into the given CPU target address without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="cpuData">The base address of the pinned CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static unsafe void CopyToCPUUnsafeAsync<T, TView>(
            this TView source,
            AcceleratorStream stream,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length < 1)
                return;

            // Check for an aggressive page-locking mode.
            if (source.GetPageLockingMode() == PageLockingMode.Aggressive)
            {
                var accelerator = source.GetAccelerator();
                using var pageLockScope = accelerator.CreatePageLockFromPinned<T>(
                    new IntPtr(Unsafe.AsPointer(ref cpuData)),
                    length);
                source.CopyToPageLockedAsync(stream, pageLockScope);
            }
            else
            {
                using var buffer = CPUMemoryBuffer.Create(
                    source.GetAccelerator(),
                    ref cpuData,
                    length);
                source.Buffer.CopyTo(
                    stream,
                    source.IndexInBytes,
                    buffer.AsRawArrayView());
            }
        }

        /// <summary>
        /// Copies from the CPU source address into the given target view without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="cpuData">The base address of the pinned CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFromCPUUnsafeAsync<T, TView>(
            this TView target,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            target.CopyFromCPUUnsafeAsync(
                target.GetDefaultStream(),
                ref cpuData,
                length);

        /// <summary>
        /// Copies from the CPU source address into the given target view without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target view instance.</param>
        /// <param name="cpuData">The base address of the pinned CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static unsafe void CopyFromCPUUnsafeAsync<T, TView>(
            this TView target,
            AcceleratorStream stream,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length < 1)
                return;

            // Check for an aggressive page-locking mode.
            if (target.GetPageLockingMode() == PageLockingMode.Aggressive)
            {
                var accelerator = target.GetAccelerator();
                using var pageLockScope = accelerator.CreatePageLockFromPinned<T>(
                    new IntPtr(Unsafe.AsPointer(ref cpuData)),
                    length);
                target.CopyFromPageLockedAsync(pageLockScope);
            }
            else
            {
                using var buffer = CPUMemoryBuffer.Create(
                    target.GetAccelerator(),
                    ref cpuData,
                    length);
                target.Buffer.CopyFrom(
                    stream,
                    buffer.AsRawArrayView(),
                    target.IndexInBytes);
            }
        }

        #endregion

        #region Copy elements to/from CPU

        /// <summary>
        /// Copies from the source view into the given CPU target address while
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="cpuData">The base address of the CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToCPU<T, TView>(
            this TView source,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            source.CopyToCPU(
                source.GetDefaultStream(),
                ref cpuData,
                length);

        /// <summary>
        /// Copies from the source view into the given CPU target address while
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="cpuData">The base address of the CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToCPU<T, TView>(
            this TView source,
            AcceleratorStream stream,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            // Copy async into memory
            source.CopyToCPUUnsafeAsync(stream, ref cpuData, length);
            stream.Synchronize();
        }

        /// <summary>
        /// Copies from the CPU source address into the given target view while
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="cpuData">The base address of the CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFromCPU<T, TView>(
            this TView target,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            target.CopyFromCPU(
                target.GetDefaultStream(),
                ref cpuData,
                length);

        /// <summary>
        /// Copies from the CPU source address into the given target view while
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="cpuData">The base address of the CPU buffer.</param>
        /// <param name="length">The number of elements to copy.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFromCPU<T, TView>(
            this TView target,
            AcceleratorStream stream,
            ref T cpuData,
            long length)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            // Copy async into memory
            target.CopyFromCPUUnsafeAsync(stream, ref cpuData, length);
            stream.Synchronize();
        }

        #endregion

        #region Copy from/to Spans

        /// <summary>
        /// Copies from the source view into the given CPU data array while
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="span">The CPU data target.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static unsafe void CopyToCPU<T, TView>(
            this TView source,
            AcceleratorStream stream,
            in Span<T> span)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            if (span.IsEmpty || span.Length < 1)
                return;

            fixed (T* ptr = span)
            {
                source.CopyToCPUUnsafeAsync(
                    stream,
                    ref Unsafe.AsRef<T>(ptr),
                    span.Length);
                stream.Synchronize();
            }
        }

        /// <summary>
        /// Copies from the CPU source span into the given target view while
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="span">The CPU data source.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static unsafe void CopyFromCPU<T, TView>(
            this TView target,
            AcceleratorStream stream,
            in ReadOnlySpan<T> span)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            if (span.IsEmpty || span.Length < 1)
                return;

            fixed (T* ptr = span)
            {
                target.CopyFromCPUUnsafeAsync(
                    stream,
                    ref Unsafe.AsRef<T>(ptr),
                    span.Length);
                stream.Synchronize();
            }
        }

        #endregion

        #region Copy to/from arrays

        /// <summary>
        /// Copies the contents of the 1D view into the given 1D array using the default
        /// accelerator stream.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="data">The target data array.</param>
        /// <remarks>
        /// This method is not supported on accelerators.
        /// </remarks>
        [NotInsideKernel]
        public static void CopyToCPU<T>(
            this ArrayView<T> view,
            T[] data)
            where T : unmanaged =>
            ((ArrayView1D<T, Stride1D.Dense>)view).CopyToCPU(data);

        /// <summary>
        /// Copies the contents of the 1D view into the given 1D array using the given
        /// accelerator stream.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="data">The target data array.</param>
        /// <remarks>
        /// This method is not supported on accelerators.
        /// </remarks>
        [NotInsideKernel]
        public static void CopyToCPU<T>(
            this ArrayView<T> view,
            AcceleratorStream stream,
            T[] data)
            where T : unmanaged =>
            ((ArrayView1D<T, Stride1D.Dense>)view).CopyToCPU(stream, data);

        /// <summary>
        /// Copies the contents of the 1D array into the given 1D view using the default
        /// accelerator stream.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="data">The target data array.</param>
        /// <remarks>
        /// This method is not supported on accelerators.
        /// </remarks>
        [NotInsideKernel]
        public static void CopyFromCPU<T>(
            this ArrayView<T> view,
            T[] data)
            where T : unmanaged =>
            ((ArrayView1D<T, Stride1D.Dense>)view).CopyFromCPU(data);

        /// <summary>
        /// Copies the contents of the 1D array into the given 1D view using the
        /// given stream.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="data">The target data array.</param>
        /// <remarks>
        /// This method is not supported on accelerators.
        /// </remarks>
        [NotInsideKernel]
        public static void CopyFromCPU<T>(
            this ArrayView<T> view,
            AcceleratorStream stream,
            T[] data)
            where T : unmanaged =>
            ((ArrayView1D<T, Stride1D.Dense>)view).CopyFromCPU(stream, data);

        #endregion

        #region Copy to/from Page Lock async

        /// <summary>
        /// Copies from the source view into the given page locked memory without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="pageLockScope">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static void CopyToPageLockedAsync<T, TView>(
            this TView source,
            AcceleratorStream stream,
            PageLockScope<T> pageLockScope)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            if (pageLockScope == null)
                throw new ArgumentNullException(nameof(pageLockScope));
            if (pageLockScope.LengthInBytes < 1)
                return;

            using var buffer = CPUMemoryBuffer.Create(
                source.GetAccelerator(),
                pageLockScope.AddrOfLockedObject,
                pageLockScope.LengthInBytes,
                Interop.SizeOf<byte>());
            source.Buffer.CopyTo(
                stream,
                source.IndexInBytes,
                buffer.AsRawArrayView());
            if (pageLockScope is NullPageLockScope<T>)
                stream.Synchronize();
        }

        /// <summary>
        /// Copies from the page locked memory into the given target view without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="pageLockScope">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static void CopyFromPageLockedAsync<T, TView>(
            this TView target,
            AcceleratorStream stream,
            PageLockScope<T> pageLockScope)
            where TView : IContiguousArrayView<T>
            where T : unmanaged
        {
            if (pageLockScope == null)
                throw new ArgumentNullException(nameof(pageLockScope));
            if (pageLockScope.LengthInBytes < 1)
                return;

            using var buffer = CPUMemoryBuffer.Create(
                target.GetAccelerator(),
                pageLockScope.AddrOfLockedObject,
                pageLockScope.LengthInBytes,
                Interop.SizeOf<byte>());
            target.Buffer.CopyFrom(
                stream,
                buffer.AsRawArrayView(),
                target.IndexInBytes);
            if (pageLockScope is NullPageLockScope<T>)
                stream.Synchronize();
        }

        /// <summary>
        /// Copies from the source view into the given page locked memory without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="pageLockScope">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToPageLockedAsync<T, TView>(
            this TView source,
            PageLockScope<T> pageLockScope)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            CopyToPageLockedAsync(
                source,
                source.GetDefaultStream(),
                pageLockScope);

        /// <summary>
        /// Copies from the page locked memory into the given target view without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="pageLockScope">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFromPageLockedAsync<T, TView>(
            this TView target,
            PageLockScope<T> pageLockScope)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            CopyFromPageLockedAsync(
                target,
                target.GetDefaultStream(),
                pageLockScope);

        /// <summary>
        /// Copies from the source view into the given page locked memory without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="pageLockedArray">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToPageLockedAsync<T, TView>(
            this TView source,
            AcceleratorStream stream,
            PageLockedArray<T> pageLockedArray)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            source.CopyToPageLockedAsync(
                stream,
                pageLockedArray.Scope);

        /// <summary>
        /// Copies from the page locked memory into the given target view without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="pageLockedArray">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFromPageLockedAsync<T, TView>(
            this TView target,
            AcceleratorStream stream,
            PageLockedArray<T> pageLockedArray)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            target.CopyFromPageLockedAsync(
                stream,
                pageLockedArray.Scope);

        /// <summary>
        /// Copies from the source view into the given page locked memory without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="source">The source view instance.</param>
        /// <param name="pageLockedArray">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToPageLockedAsync<T, TView>(
            this TView source,
            PageLockedArray<T> pageLockedArray)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            source.CopyToPageLockedAsync(pageLockedArray.Scope);

        /// <summary>
        /// Copies from the page locked memory into the given target view without
        /// synchronizing the current accelerator stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="target">The target view instance.</param>
        /// <param name="pageLockedArray">The page locked memory.</param>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFromPageLockedAsync<T, TView>(
            this TView target,
            PageLockedArray<T> pageLockedArray)
            where TView : IContiguousArrayView<T>
            where T : unmanaged =>
            target.CopyFromPageLockedAsync(pageLockedArray.Scope);

        #endregion

        #region Array Methods

        /// <summary>
        /// Copies the current contents into a new array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] GetAsArray<T>(this ArrayView<T> view)
            where T : unmanaged =>
            view.GetAsArray(view.GetDefaultStream());

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static T[] GetAsArray<T>(
            this ArrayView<T> view,
            AcceleratorStream stream)
            where T : unmanaged
        {
            if (view.HasNoData())
                return Array.Empty<T>();

            if (view.UsesAutoPageLocking())
            {
                // Extract the managed .Net array from the locked array, as this instance
                // will not be disposed by the using statement.
                using var lockedArray = view.GetAsPageLockedArray(stream);
                return lockedArray.GetArray();
            }

            var result = new T[view.Length];
            view.CopyToCPU(stream, new Span<T>(result, 0, result.Length));
            return result;
        }

        /// <summary>
        /// Copies the current contents into a new array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PageLockedArray1D<T> GetAsPageLockedArray<T>(this ArrayView<T> view)
            where T : unmanaged =>
            view.GetAsPageLockedArray(view.GetDefaultStream());

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static PageLockedArray1D<T> GetAsPageLockedArray<T>(
            this ArrayView<T> view,
            AcceleratorStream stream)
            where T : unmanaged
        {
            if (view.HasNoData())
                return PageLockedArray1D<T>.Empty;
            var accelerator = view.GetAccelerator();
#if NET5_0
            var result = accelerator.AllocatePageLockedArray1D<T>(
                view.Length,
                uninitialized: true);
#else
            var result = accelerator.AllocatePageLockedArray1D<T>(view.Length);
#endif
            view.CopyToPageLockedAsync(stream, result);
            stream.Synchronize();
            return result;
        }

        #endregion

        #region Raw Array Methods

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static ArraySegment<byte> GetRawData<TView>(this TView view)
            where TView : IContiguousArrayView =>
            view.GetRawData(view.GetDefaultStream());

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static ArraySegment<byte> GetRawData<TView>(
            this TView view,
            AcceleratorStream stream)
            where TView : IContiguousArrayView =>
            view.GetRawData(stream, 0, view.LengthInBytes);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <param name="byteOffset">The offset within the view in bytes.</param>
        /// <param name="byteExtent">The number of bytes to load.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static ArraySegment<byte> GetRawData<TView>(
            this TView view,
            long byteOffset,
            long byteExtent)
            where TView : IContiguousArrayView =>
            view.GetRawData(view.GetDefaultStream(), byteOffset, byteExtent);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="view">The source view instance.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="byteOffset">The offset within the view in bytes.</param>
        /// <param name="byteExtent">The number of bytes to load.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static unsafe ArraySegment<byte> GetRawData<TView>(
            this TView view,
            AcceleratorStream stream,
            long byteOffset,
            long byteExtent)
            where TView : IContiguousArrayView
        {
            var rawOffset = TypeNode.Align(byteOffset, view.ElementSize);
            var rawExtent = TypeNode.Align(byteExtent, view.ElementSize);

            IndexTypeExtensions.AssertIntIndexRange(rawOffset);
            IndexTypeExtensions.AssertIntIndexRange(rawExtent);

            var result = new byte[rawExtent];
            var rawView = view.AsRawArrayView();
            rawView.CopyToCPU(
                stream,
                new Span<byte>(
                    result,
                    (int)(rawOffset / view.ElementSize),
                    (int)rawExtent));

            return new ArraySegment<byte>(
                result,
                0,
                (int)(byteExtent + (rawExtent - byteExtent)));
        }

        #endregion

        #region Data Allocations

        /// <summary>
        /// Allocates a buffer with the specified content on the given accelerator
        /// using the default stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="data">The source CPU data.</param>
        /// <returns>An allocated buffer on this accelerator.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static MemoryBuffer1D<T, Stride1D.Dense> Allocate1D<T>(
            this Accelerator accelerator,
            T[] data)
            where T : unmanaged =>
            Allocate1D<T>(accelerator, accelerator.DefaultStream, data);

        /// <summary>
        /// Allocates a buffer with the specified content on the given accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="data">The source CPU data.</param>
        /// <returns>An allocated buffer on this accelerator.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public static MemoryBuffer1D<T, Stride1D.Dense> Allocate1D<T>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            T[] data)
            where T : unmanaged
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length < 1)
            {
                return new MemoryBuffer1D<T, Stride1D.Dense>(
                    accelerator,
                    ArrayView1D<T, Stride1D.Dense>.Empty);
            }

            // Allocate the raw buffer
            var buffer = accelerator.Allocate1D<T>(data.Length);

            // Copy the data
            buffer.View.CopyFromCPU(stream, data);

            return buffer;
        }

        #endregion
    }

    partial struct ArrayView1D<T, TStride>
    {
        /// <summary>
        /// Converts this array view into a general 1D view.
        /// </summary>
        /// <returns>The converted general 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView1D<T, Stride1D.General> To1DView() =>
            new ArrayView1D<T, Stride1D.General>(
                BaseView,
                Extent,
                Stride.To1DStride());

        /// <summary>
        /// Converts this array view into a 2D view.
        /// </summary>
        /// <typeparam name="TOtherStride">The stride type.</typeparam>
        /// <param name="extent">The target extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 2D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView2D<T, TOtherStride> As2DView<TOtherStride>(
            LongIndex2D extent,
            TOtherStride stride)
            where TOtherStride : struct, IStride2D
        {
            long size = stride.ComputeBufferLength(extent);
            Trace.Assert(size <= Length, "Extent out of range");
            var baseView = BaseView.SubView(0, size);
            return new ArrayView2D<T, TOtherStride>(
                baseView,
                extent,
                stride);
        }

        /// <summary>
        /// Converts the given view into a 3D view.
        /// </summary>
        /// <typeparam name="TOtherStride">The stride type.</typeparam>
        /// <param name="extent">The target extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView3D<T, TOtherStride> As3DView<TOtherStride>(
            LongIndex3D extent,
            TOtherStride stride)
            where TOtherStride : struct, IStride3D
        {
            long size = stride.ComputeBufferLength(extent);
            Trace.Assert(size <= Length, "Extent out of range");
            var baseView = BaseView.SubView(0, size);
            return new ArrayView3D<T, TOtherStride>(
                baseView,
                extent,
                stride);
        }
    }

    partial struct ArrayView2D<T, TStride>
    {
        #region Casts

        /// <summary>
        /// Converts this array view into a general 1D view.
        /// </summary>
        /// <returns>The converted general 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView1D<T, Stride1D.General> To1DView() =>
            new ArrayView1D<T, Stride1D.General>(
                BaseView,
                Extent.Size,
                Stride.To1DStride());

        /// <summary>
        /// Converts this array view into a dense version with leading dimension X.
        /// </summary>
        /// <returns>The updated array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView2D<T, Stride2D.DenseX> AsDenseX()
        {
            Trace.Assert(Stride.XStride == 1, "Incompatible dense stride");
            return new ArrayView2D<T, Stride2D.DenseX>(
                BaseView,
                Extent,
                new Stride2D.DenseX(Stride.YStride));
        }

        /// <summary>
        /// Converts this array view into a dense version with leading dimension Y.
        /// </summary>
        /// <returns>The updated array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView2D<T, Stride2D.DenseY> AsDenseY()
        {
            Trace.Assert(Stride.YStride == 1, "Incompatible dense stride");
            return new ArrayView2D<T, Stride2D.DenseY>(
                BaseView,
                Extent,
                new Stride2D.DenseY(Stride.XStride));
        }

        /// <summary>
        /// Converts the given view into a 1D view.
        /// </summary>
        /// <typeparam name="TOtherStride">The stride type.</typeparam>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView1D<T, TOtherStride> As1DView<TOtherStride>(
            TOtherStride stride)
            where TOtherStride : struct, IStride1D
        {
            long bufferLength = stride.ComputeBufferLength(Length);
            var baseView = BaseView.SubView(0, bufferLength);
            return new ArrayView1D<T, TOtherStride>(
                baseView,
                bufferLength,
                stride);
        }

        /// <summary>
        /// Converts the given view into a 3D view.
        /// </summary>
        /// <typeparam name="TOtherStride">The stride type.</typeparam>
        /// <param name="extent">The extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView3D<T, TOtherStride> As3DView<TOtherStride>(
            LongIndex3D extent,
            TOtherStride stride)
            where TOtherStride : struct, IStride3D
        {
            long bufferLength = stride.ComputeBufferLength(extent);
            var baseView = BaseView.SubView(0, bufferLength);
            return new ArrayView3D<T, TOtherStride>(
                baseView,
                extent,
                stride);
        }

        #endregion
    }

    partial struct ArrayView3D<T, TStride>
    {
        #region Casts

        /// <summary>
        /// Converts this array view into a general 1D view.
        /// </summary>
        /// <returns>The converted general 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView1D<T, Stride1D.General> To1DView() =>
            new ArrayView1D<T, Stride1D.General>(
                BaseView,
                Extent.Size,
                Stride.To1DStride());

        /// <summary>
        /// Converts this array view into a dense version with leading dimensions XY.
        /// </summary>
        /// <returns>The updated array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView3D<T, Stride3D.DenseXY> AsDenseXY()
        {
            Trace.Assert(Stride.XStride == 1, "Incompatible dense stride");
            return new ArrayView3D<T, Stride3D.DenseXY>(
                BaseView,
                Extent,
                new Stride3D.DenseXY(Stride.YStride, Stride.YStride * Stride.ZStride));
        }

        /// <summary>
        /// Converts this array view into a dense version with leading dimensions ZY.
        /// </summary>
        /// <returns>The updated array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView3D<T, Stride3D.DenseZY> AsDenseZY()
        {
            Trace.Assert(Stride.ZStride == 1, "Incompatible dense stride");
            return new ArrayView3D<T, Stride3D.DenseZY>(
                BaseView,
                Extent,
                new Stride3D.DenseZY(Stride.XStride * Stride.YStride, Stride.YStride));
        }

        /// <summary>
        /// Converts the given view into a 1D view.
        /// </summary>
        /// <typeparam name="TOtherStride">The stride type.</typeparam>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView1D<T, TOtherStride> As1DView<TOtherStride>(
            TOtherStride stride)
            where TOtherStride : struct, IStride1D
        {
            long bufferLength = stride.ComputeBufferLength(Length);
            var baseView = BaseView.SubView(0, bufferLength);
            return new ArrayView1D<T, TOtherStride>(
                baseView,
                bufferLength,
                stride);
        }

        /// <summary>
        /// Converts the given view into a 3D view.
        /// </summary>
        /// <typeparam name="TOtherStride">The stride type.</typeparam>
        /// <param name="extent">The extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView3D<T, TOtherStride> As3DView<TOtherStride>(
            LongIndex3D extent,
            TOtherStride stride)
            where TOtherStride : struct, IStride3D
        {
            long bufferLength = stride.ComputeBufferLength(extent);
            var baseView = BaseView.SubView(0, bufferLength);
            return new ArrayView3D<T, TOtherStride>(
                baseView,
                extent,
                stride);
        }

        #endregion
    }
}
