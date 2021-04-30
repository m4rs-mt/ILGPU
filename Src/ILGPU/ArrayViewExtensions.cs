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
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    partial struct ArrayView2D<T, TStride>
    {
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
    }

    partial struct ArrayView3D<T, TStride>
    {
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
        /// Converts this array view into a dense version with leading dimensions YZ.
        /// </summary>
        /// <returns>The updated array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ArrayView3D<T, Stride3D.DenseYZ> AsDenseYZ()
        {
            Trace.Assert(Stride.ZStride == 1, "Incompatible dense stride");
            return new ArrayView3D<T, Stride3D.DenseYZ>(
                BaseView,
                Extent,
                new Stride3D.DenseYZ(Stride.XStride, Stride.XStride * Stride.YStride));
        }
    }

    /// <summary>
    /// Array view extension methods
    /// </summary>
    public static partial class ArrayViewExtensions
    {
        #region ArrayView

        /// <summary>
        /// Loads the effective address of the current view.
        /// This operation is not supported on accelerators.
        /// </summary>
        /// <remarks>
        /// Use with caution since this operation does not make sense with respect to all
        /// target platforms.
        /// </remarks>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte LoadEffectiveAddress<T>(this ArrayView<T> view)
            where T : unmanaged =>
            ref view.LoadEffectiveAddress();

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
        /// Returns a variable view to the given element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="element">The element index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableView<T> VariableView<T>(
            this ArrayView<T> view,
            Index1 element)
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
            LongIndex1 element)
            where T : unmanaged =>
            new VariableView<T>(view.SubView(element, 1L));

        #endregion

        #region ArrayView1D

        /// <summary>
        /// Converts the given view into a 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="extent">The target extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 2D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView2D<T, TStride> As2DView<T, TStride>(
            this ArrayView1D<T, Stride1D.Dense> view,
            LongIndex2 extent,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            Trace.Assert(extent.Size <= view.Length, "Extent out of range");
            var baseView = view.BaseView.SubView(0, extent.Size);
            return new ArrayView2D<T, TStride>(
                baseView,
                extent,
                stride);
        }

        /// <summary>
        /// Converts the given view into a 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="extent">The target extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T, TStride> As3DView<T, TStride>(
            this ArrayView1D<T, Stride1D.Dense> view,
            LongIndex3 extent,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride3D
        {
            Trace.Assert(extent.Size <= view.Length, "Extent out of range");
            var baseView = view.BaseView.SubView(0, extent.Size);
            return new ArrayView3D<T, TStride>(
                baseView,
                extent,
                stride);
        }

        #endregion

        #region ArrayView2D

        /// <summary>
        /// Converts the given view into a 1D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView1D<T, TStride> As1DView<T, TStride>(
            this ArrayView2D<T, Stride2D.DenseX> view,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride1D =>
            new ArrayView1D<T, TStride>(
                view.BaseView,
                view.Extent.Size,
                stride);

        /// <summary>
        /// Converts the given view into a 1D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView1D<T, TStride> As1DView<T, TStride>(
            this ArrayView2D<T, Stride2D.DenseY> view,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride1D =>
            new ArrayView1D<T, TStride>(
                view.BaseView,
                view.Extent.Size,
                stride);

        /// <summary>
        /// Converts the given view into a 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="extent">The target extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T, TStride> As3DView<T, TStride>(
            this ArrayView2D<T, Stride2D.Dense> view,
            LongIndex3 extent,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride3D
        {
            Trace.Assert(extent.Size <= view.Length, "Extent out of range");
            var baseView = view.BaseView.SubView(0, extent.Size);
            return new ArrayView3D<T, TStride>(
                baseView,
                extent,
                stride);
        }

        /// <summary>
        /// Internal helper function to perform a slice on the X dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The current view instance to slice.</param>
        /// <param name="y">The y index.</param>
        /// <returns>The slices sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<T> SliceXInternal<T, TStride>(
            ArrayView2D<T, TStride> view,
            long y)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            Trace.Assert(y >= 0 & y < view.Extent.Y, "y out of range");
            long offset = y * view.Stride.YStride;
            return view.BaseView.SubView(offset, view.Extent.X * view.Stride.XStride);
        }

        /// <summary>
        /// Slices a 1D chunk out of a 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to slice.</param>
        /// <param name="y">The y index.</param>
        /// <returns>The sliced 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView1D<T, Stride1D.General> SliceX<T, TStride>(
            this ArrayView2D<T, TStride> view,
            long y)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            var baseView = SliceXInternal(view, y);
            return new ArrayView1D<T, Stride1D.General>(
                baseView,
                view.Extent.X,
                new Stride1D.General(view.Stride.XStride));
        }

        /// <summary>
        /// Slices a 1D chunk out of a 2D dense view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view to slice.</param>
        /// <param name="y">The y index.</param>
        /// <returns>The sliced 1D view.</returns>
        public static ArrayView1D<T, Stride1D.Dense> SliceX<T>(
            this ArrayView2D<T, Stride2D.DenseX> view,
            long y)
            where T : unmanaged
        {
            var baseView = SliceXInternal(view, y);
            return new ArrayView1D<T, Stride1D.Dense>(
                baseView,
                view.Extent.X,
                default);
        }

        /// <summary>
        /// Internal helper function to perform a slice on the Y dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The current view instance to slice.</param>
        /// <param name="x">The x index.</param>
        /// <returns>The slices sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<T> SliceYInternal<T, TStride>(
            ArrayView2D<T, TStride> view,
            long x)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            Trace.Assert(x >= 0 & x < view.Extent.X, "x out of range");
            long offset = x * view.Stride.XStride;
            return view.BaseView.SubView(offset, view.Extent.Y);
        }

        /// <summary>
        /// Slices a 1D chunk out of a 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to slice.</param>
        /// <param name="x">The x index.</param>
        /// <returns>The sliced 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView1D<T, Stride1D.General> SliceY<T, TStride>(
            this ArrayView2D<T, TStride> view,
            long x)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            var baseView = SliceYInternal(view, x);
            return new ArrayView1D<T, Stride1D.General>(
                baseView,
                new Stride1D.General(view.Stride.YStride));
        }

        /// <summary>
        /// Slices a 1D chunk out of a 2D dense view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view to slice.</param>
        /// <param name="x">The x index.</param>
        /// <returns>The sliced 1D view.</returns>
        public static ArrayView1D<T, Stride1D.Dense> SliceY<T>(
            this ArrayView2D<T, Stride2D.Dense> view,
            long x)
            where T : unmanaged
        {
            var baseView = SliceYInternal(view, x);
            return new ArrayView1D<T, Stride1D.Dense>(baseView, default);
        }

        #endregion

        #region ArrayView3D

        /// <summary>
        /// Converts the given view into a 1D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 1D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView1D<T, TStride> As1DView<T, TStride>(
            this ArrayView3D<T, Stride3D.Dense> view,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride1D =>
            new ArrayView1D<T, TStride>(view.BaseView, stride);

        /// <summary>
        /// Converts the given view into a 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride type.</typeparam>
        /// <param name="view">The view to convert.</param>
        /// <param name="extent">The target extent to use.</param>
        /// <param name="stride">The target stride to use.</param>
        /// <returns>The converted 2D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView2D<T, TStride> As2DView<T, TStride>(
            this ArrayView3D<T, Stride3D.Dense> view,
            LongIndex2 extent,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            Trace.Assert(extent.Size <= view.Length, "Extent out of range");
            return new ArrayView2D<T, TStride>(
                view.BaseView,
                extent,
                stride);
        }

        #endregion
    }

    public static class ArrayViewExtensions
    {
        #region Base Methods

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns></returns>
        public static bool HasNoData<TView>(this TView view)
            where TView : IArrayView =>
            !view.IsValid || view.Length < 1;

        public static bool HasNonZeroLength<TView>(this TView view)
            where TView : IArrayView =>
            view.IsValid & view.Length > 0;

        /// <summary>
        /// Returns the associated accelerator of the current view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The associated parent accelerator.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static Accelerator GetAccelerator<TView>(this TView view)
            where TView : IArrayView =>
            view.Buffer.Accelerator;

        /// <summary>
        /// Returns the associated accelerator type of the current view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The associated parent accelerator type.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
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
        /// <remarks>This method is not supported on accelerators.</remarks>
        internal static AcceleratorStream GetDefaultStream<TView>(this TView view)
            where TView : IArrayView =>
            view.GetAccelerator().DefaultStream;

        /// <summary>
        /// Returns the index in bytes of the given view.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The index in bytes of the given view.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        internal static long GetIndexInBytes<TView>(this TView view)
            where TView : IArrayView =>
            view.Index * view.ElementSize;

        /// <summary>
        /// Converts the given generic array view into a raw view of bytes.
        /// </summary>
        /// <typeparam name="TView">The view type.</typeparam>
        /// <param name="view">The view instance.</param>
        /// <returns>The raw array view.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        internal static ArrayView<byte> AsRawArrayView<TView>(this TView view)
            where TView : IArrayView =>
            new ArrayView<byte>(
                view.Buffer,
                view.GetIndexInBytes(),
                view.LengthInBytes);

        #endregion

        #region View Methods

        /// <summary>
        /// Copies elements from the current buffer to the target view using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(TView target, TIndex sourceOffset) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        public void CopyTo(
            AcceleratorStream stream,
            TView target,
            TIndex sourceOffset)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!target.IsValid)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!sourceOffset.Add(target.Extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(target));

            CopyToView(
                stream,
                target.AsLinearView(),
                sourceOffset.ComputeLongLinearIndex(Extent));
        }

        /// <summary>
        /// Copies elements to the current buffer from the source view.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(TView source, TIndex targetOffset) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                targetOffset);

        /// <summary>
        /// Copies elements to the current buffer from the source view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFrom(
            AcceleratorStream stream,
            TView source,
            TIndex targetOffset)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!source.IsValid)
                throw new ArgumentNullException(nameof(source));
            if (!targetOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (!targetOffset.Add(source.Extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(source));

            CopyFromView(
                stream,
                source.AsLinearView(),
                targetOffset.ComputeLongLinearIndex(Extent));
        }

        #endregion

        #region Copy Methods

        /// <summary>
        /// Copies elements from the current buffer to the target buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(TView target, TIndex sourceOffset) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            AcceleratorStream stream,
            TView target,
            TIndex sourceOffset) =>
            CopyTo(
                stream,
                target,
                sourceOffset,
                default,
                Length);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use CopyTo(MemoryBuffer<T, TIndex>, TIndex, TIndex, Index1) instead")]
        public void CopyTo(
            TView target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [Obsolete("Use CopyTo(AcceleratorStream, MemoryBuffer<T, TIndex>, TIndex, " +
            "TIndex, Index1) instead")]
        public void CopyTo(
            AcceleratorStream stream,
            MemoryBuffer23<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyTo(
                stream,
                target,
                sourceOffset,
                targetOffset,
                extent.Size);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            MemoryBuffer23<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            LongIndex1 extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyTo(
            AcceleratorStream stream,
            MemoryBuffer23<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            LongIndex1 extent)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!targetOffset.InBounds(target.Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            var linearSourceIndex = sourceOffset.ComputeLongLinearIndex(Extent);
            var linearTargetIndex = targetOffset.ComputeLongLinearIndex(target.Extent);
            if (linearSourceIndex + extent > Length ||
                linearTargetIndex + extent > target.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            CopyToView(
                stream,
                target.View.GetSubView(targetOffset, extent),
                linearSourceIndex);
        }

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory using the default accelerator stream.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(out T target, TIndex targetIndex) =>
            CopyTo(Accelerator.DefaultStream, out target, targetIndex);

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        public void CopyTo(
            AcceleratorStream stream,
            out T target,
            TIndex targetIndex)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            target = default;
            using (var wrapper = ViewPointerWrapper.Create(ref target))
            {
                CopyToView(
                    stream,
                    new ArrayView<T>(wrapper, 0, 1),
                    targetIndex.ComputeLongLinearIndex(Extent));
            }
            stream.Synchronize();
        }

        /// <summary>
        /// Copies the contents of this buffer into the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            Span<T> target,
            TIndex sourceOffset,
            long targetOffset,
            TIndex extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents of this buffer into the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public unsafe void CopyTo(
            AcceleratorStream stream,
            Span<T> target,
            TIndex sourceOffset,
            long targetOffset,
            TIndex extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            var length = target.Length;
            if (targetOffset < 0 || targetOffset >= length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (extent.Size < 1 ||
                targetOffset + extent.Size > length ||
                !sourceOffset.Add(extent).InBoundsInclusive(Extent))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            fixed (T* ptr = &target[0])
            {
                using (var wrapper = ViewPointerWrapper.Create(ptr))
                {
                    CopyToView(
                        stream,
                        new ArrayView<T>(wrapper, 0, length).GetSubView(
                            targetOffset, extent.Size),
                        sourceOffset.ComputeLongLinearIndex(Extent));
                }
                stream.Synchronize();
            }
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFrom(MemoryBuffer23<T, TIndex> source, TIndex targetOffset) =>
            CopyFrom(Accelerator.DefaultStream, source, targetOffset);

        /// <summary>
        /// Copies elements to the current buffer from the source buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use CopyFrom(MemoryBuffer<T, TIndex>, TIndex, TIndex, Index) " +
            "instead")]
        public void CopyFrom(
            MemoryBuffer23<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use CopyFrom(AcceleratorStream, MemoryBuffer<T, TIndex>, TIndex, " +
            "TIndex, Index) instead")]
        public void CopyFrom(
            AcceleratorStream stream,
            MemoryBuffer23<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyFrom(
                stream,
                source,
                sourceOffset,
                targetOffset,
                extent.Size);

        /// <summary>
        /// Copies elements to the current buffer from the source buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            MemoryBuffer23<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            LongIndex1 extent) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            AcceleratorStream stream,
            MemoryBuffer23<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            LongIndex1 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            source.CopyTo(
                stream,
                this,
                targetOffset,
                sourceOffset,
                extent);
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="targetOffset">The target offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            AcceleratorStream stream,
            MemoryBuffer23<T, TIndex> source,
            TIndex targetOffset) =>
            CopyFrom(
                stream,
                source,
                default,
                targetOffset,
                source.Length);

        /// <summary>
        /// Copies a single element from CPU memory to this buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="sourceIndex">The source index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(T source, TIndex sourceIndex) =>
            CopyFrom(Accelerator.DefaultStream, source, sourceIndex);

        /// <summary>
        /// Copies a single element from CPU memory to this buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source value.</param>
        /// <param name="sourceIndex">The source index.</param>
        public void CopyFrom(
            AcceleratorStream stream,
            T source,
            TIndex sourceIndex)
        {
            using var wrapper = ViewPointerWrapper.Create(ref source);
            CopyFromView(
                stream,
                new ArrayView<T>(wrapper, 0, 1),
                sourceIndex.ComputeLongLinearIndex(Extent));
            stream.Synchronize();
        }

        /// <summary>
        /// Copies the contents to this buffer from the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            ReadOnlySpan<T> source,
            long sourceOffset,
            TIndex targetOffset,
            long extent) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents to this buffer from the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public unsafe void CopyFrom(
            AcceleratorStream stream,
            ReadOnlySpan<T> source,
            long sourceOffset,
            TIndex targetOffset,
            long extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var length = source.Length;
            if (sourceOffset < 0 || sourceOffset >= length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            var linearIndex = targetOffset.ComputeLongLinearIndex(Extent);
            if (!targetOffset.InBounds(Extent) || linearIndex >= Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (extent < 1 || extent > source.Length ||
                extent + sourceOffset > source.Length ||
                linearIndex + extent > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            fixed (T* ptr = &source[0])
            {
                using var wrapper = ViewPointerWrapper.Create(ptr);
                CopyFromView(
                    stream,
                    new ArrayView<T>(wrapper, 0, source.Length).GetSubView(
                        sourceOffset,
                        extent),
                    linearIndex);
                stream.Synchronize();
            }
        }

        #endregion

        #region Array Methods

        /// <summary>
        /// Copies the current contents into a new array using
        /// the default accelerator stream.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        public static T[] GetAsArray<T>(this ArrayView<T> view)
            where T : unmanaged =>
            view.GetAsArray(view.GetDefaultStream());

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public static T[] GetAsArray<T>(this ArrayView<T> view, AcceleratorStream stream)
            where T : unmanaged =>
            GetAsArray(stream, default, Extent);

        /// <summary>
        /// Copies the current contents into a new array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray(long offset, long extent) =>
            GetAsArray(Accelerator.DefaultStream, offset, extent);

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray(
            AcceleratorStream stream,
            long offset,
            long extent)
        {
            var length = extent.Size;
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[length];
            CopyTo(stream, result, offset, 0, extent);
            return result;
        }

        #endregion

        #region Raw Array Methods

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static ArraySegment<byte> GetRawData<TView>(this TView view)
            where TView : IArrayView =>
            view.GetRawData(view.GetDefaultStream());

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        public static ArraySegment<byte> GetRawData<TView>(
            this TView view,
            AcceleratorStream stream)
            where TView : IArrayView =>
            view.GetRawData(0, view.LengthInBytes, stream);

        public static ArraySegment<byte> GetRawData<TView>(
            this TView view,
            long byteOffset,
            long byteExtent)
            where TView : IArrayView =>
            view.GetRawData(byteOffset, byteExtent, view.GetDefaultStream());

        public static unsafe ArraySegment<byte> GetRawData<TView>(
            this TView view,
            long byteOffset,
            long byteExtent,
            AcceleratorStream stream)
            where TView : IArrayView
        {
            var rawOffset = TypeNode.Align(byteOffset, view.ElementSize);
            var rawExtent = TypeNode.Align(byteExtent, view.ElementSize);

            var result = new byte[rawExtent];
            fixed (byte* ptr = &result[0])
            {
                using var wrapper = CPUMemoryBuffer.Create(ptr, 1);
                CopyToView(
                    stream,
                    new ArrayView<T>(wrapper, 0, rawExtent / ElementSize),
                    rawOffset / ElementSize);
            }

            IndexTypeExtensions.AssertIntIndexRange(rawOffset);
            IndexTypeExtensions.AssertIntIndexRange(rawExtent);
            return new ArraySegment<byte>(
                result,
                0,
                (int)(byteExtent + (rawExtent - byteExtent)));
        }

        #endregion
    }
}
