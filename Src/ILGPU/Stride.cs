// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Stride.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// A generic stride description based on the given <typeparamref name="TIndex"/>
    /// type.
    /// </summary>
    /// <typeparam name="TIndex">The underlying n-D index type.</typeparam>
    public interface IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Returns the associated stride extent.
        /// </summary>
        TIndex StrideExtent { get; }

        /// <summary>
        /// Converts this stride instance into a general 1D stride.
        /// </summary>
        /// <returns>The general 1D stride.</returns>
        Stride1D.General To1DStride();
    }

    /// <summary>
    /// A generic stride based on 32-bit and 64-bit index information.
    /// </summary>
    /// <typeparam name="TIndex">The actual 32-bit stride index.</typeparam>
    /// <typeparam name="TLongIndex">The 64-bit stride index.</typeparam>
    public interface IStride<TIndex, TLongIndex> : IStride<TIndex>
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
    {
        /// <summary>
        /// Computes the linear 32-bit element address using the given index.
        /// </summary>
        /// <param name="index">The dimension for index computation.</param>
        /// <returns>The computed linear element address.</returns>
        int ComputeElementIndex(TIndex index);

        /// <summary>
        /// Computes the linear 64-bit element address using the given index.
        /// </summary>
        /// <param name="index">The dimension for index computation.</param>
        /// <returns>The computed linear element address.</returns>
        long ComputeElementIndex(TLongIndex index);

        /// <summary>
        /// Computes the linear 32-bit element address using the given index while
        /// verifying that the given index is within the bounds of the specified extent.
        /// </summary>
        /// <param name="index">The dimension for the index computation.</param>
        /// <param name="extent">The extent dimension to check.</param>
        /// <returns>The computed linear element address.</returns>
        int ComputeElementIndexChecked(TIndex index, TIndex extent);

        /// <summary>
        /// Computes the linear 64-bit element address using the given index while
        /// verifying that the given index is within the bounds of the specified extent.
        /// </summary>
        /// <param name="index">The dimension for index computation.</param>
        /// <param name="extent">The extent dimension to check.</param>
        /// <returns>The computed linear element address.</returns>
        long ComputeElementIndexChecked(TLongIndex index, TLongIndex extent);

        /// <summary>
        /// Reconstructs a 32-bit index from a linear element index.
        /// </summary>
        /// <param name="elementIndex">The linear element index.</param>
        /// <returns>The reconstructed index.</returns>
        TIndex ReconstructFromElementIndex(int elementIndex);

        /// <summary>
        /// Reconstructs a 64-bit index from a linear element index.
        /// </summary>
        /// <param name="elementIndex">The linear element index.</param>
        /// <returns>The reconstructed index.</returns>
        TLongIndex ReconstructFromElementIndex(long elementIndex);

        /// <summary>
        /// Computes the 32-bit length of a required allocation.
        /// </summary>
        /// <param name="extent">The extent to allocate.</param>
        /// <returns>The 32-bit length of a required allocation.</returns>
        int ComputeBufferLength(TIndex extent);

        /// <summary>
        /// Computes the 64-bit length of a required allocation.
        /// </summary>
        /// <param name="extent">The extent to allocate.</param>
        /// <returns>The 64-bit length of a required allocation.</returns>
        long ComputeBufferLength(TLongIndex extent);
    }

    /// <summary>
    /// A stride that can be cast using a <see cref="IStrideCastContext"/>.
    /// </summary>
    /// <typeparam name="TIndex">The actual 32-bit stride index.</typeparam>
    /// <typeparam name="TLongIndex">The 64-bit stride index.</typeparam>
    /// <typeparam name="TStride">
    /// The stride type implementing this interface.
    /// </typeparam>
    public interface ICastableStride<TIndex, TLongIndex, TStride>
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
        where TStride :
            struct,
            IStride<TIndex, TLongIndex>,
            ICastableStride<TIndex, TLongIndex, TStride>
    {
        /// <summary>
        /// Computes a new extent and stride based on the given cast context. The context
        /// thereby adjusts extent information which can in turn be based on the element
        /// size of the source view and the element size of the target element type.
        /// </summary>
        /// <typeparam name="TContext">The cast context type.</typeparam>
        /// <param name="context">
        /// The cast context to adjust element information.
        /// </param>
        /// <param name="extent">The source extent.</param>
        /// <returns>The adjusted extent and stride information.</returns>
        (TLongIndex Extent, TStride Stride) Cast<TContext>(
            in TContext context,
            in TLongIndex extent)
            where TContext : struct, IStrideCastContext;
    }

    /// <summary>
    /// A generic cast context for <see cref="ICastableStride{TIndex, TLongIndex,
    /// TStride}.Cast{TContext}(in TContext, in TLongIndex)"/> operations.
    /// </summary>
    public interface IStrideCastContext
    {
        /// <summary>
        /// Computes an adjusted extent taking source and target element size information
        /// into account (based on 32bit).
        /// </summary>
        /// <param name="sourceExtent">The source extent.</param>
        /// <returns>The adjusted extent.</returns>
        int ComputeNewExtent(int sourceExtent);

        /// <summary>
        /// Computes an adjusted extent taking source and target element size information
        /// into account (based on 64bit).
        /// </summary>
        /// <param name="sourceExtent">The source extent.</param>
        /// <returns>The adjusted extent.</returns>
        long ComputeNewExtent(long sourceExtent);
    }

    /// <summary>
    /// Contains helper functions for generic <see cref="IStride{TIndex}"/> types.
    /// </summary>
    public static class StrideExtensions
    {
        /// <summary>
        /// Determines a pitched leading dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="leadingDimension">The size of the leading dimension.</param>
        /// <param name="alignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>The pitched leading dimension.</returns>
        public static long GetPitchedLeadingDimension<T>(
            long leadingDimension,
            int alignmentInBytes)
            where T : unmanaged
        {
            // Validate the byte pitch and the element size
            if (alignmentInBytes < 1 || !Utilities.IsPowerOf2(alignmentInBytes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alignmentInBytes));
            }
            int elementSize = ArrayView<T>.ElementSize;
            if (elementSize > alignmentInBytes || alignmentInBytes % elementSize != 0)
            {
                throw new ArgumentException(
                    string.Format(
                        RuntimeErrorMessages.NotSupportedPitchedAllocation,
                        nameof(T),
                        alignmentInBytes));
            }

            // Ensure a proper alignment of the leading dimension
            long unpichtedBytes = leadingDimension * elementSize;
            long pitchedBytes = TypeNode.Align(unpichtedBytes, alignmentInBytes);

            // Return the pitched dimension
            return pitchedBytes / elementSize;
        }

        /// <summary>
        /// An element-size based cast context.
        /// </summary>
        public readonly struct ElementSizeCastContext : IStrideCastContext
        {
            /// <summary>
            /// Initializes a new element-size context.
            /// </summary>
            /// <param name="elementSize">The source element size.</param>
            /// <param name="newElementSize">The target element size.</param>
            public ElementSizeCastContext(int elementSize, int newElementSize)
            {
                ElementSize = elementSize;
                NewElementSize = newElementSize;
            }

            /// <summary>
            /// Returns the source element size.
            /// </summary>
            public int ElementSize { get; }

            /// <summary>
            /// Returns the target element size.
            /// </summary>
            public int NewElementSize { get; }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeNewExtent(int sourceExtent) =>
                sourceExtent * ElementSize / NewElementSize;

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeNewExtent(long sourceExtent) =>
                sourceExtent * ElementSize / NewElementSize;
        }

        /// <summary>
        /// Creates a new <see cref="ElementSizeCastContext"/>.
        /// </summary>
        /// <param name="elementSize">The source element size.</param>
        /// <param name="newElementSize">The target element size.</param>
        /// <returns>The created cast context.</returns>
        public static ElementSizeCastContext CreateCastContext(
            int elementSize,
            int newElementSize) =>
            new ElementSizeCastContext(elementSize, newElementSize);
    }

    partial class Stride1D
    {
        partial struct Dense : ICastableStride<Index1D, LongIndex1D, Dense>
        {
            /// <summary>
            /// Computes a new extent and stride based on the given cast context.
            /// </summary>
            /// <typeparam name="TContext">The cast context type.</typeparam>
            /// <param name="context">
            /// The cast context to adjust element information.
            /// </param>
            /// <param name="extent">The source extent.</param>
            /// <returns>The adjusted extent and stride information.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (LongIndex1D Extent, Dense Stride) Cast<TContext>(
                in TContext context,
                in LongIndex1D extent)
                where TContext : struct, IStrideCastContext =>
                (
                    new LongIndex1D(context.ComputeNewExtent(extent)),
                    new Dense()
                );
        }
    }

    partial class Stride2D
    {
        /// <summary>
        /// A 2D dense X stride.
        /// </summary>
        public readonly struct DenseX :
            IStride2D,
            ICastableStride<Index2D, LongIndex2D, DenseX>
        {
            #region Static

            /// <summary>
            /// Constructs a 2D stride from the given 2D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseX FromExtent(Index2D extent) =>
                new DenseX(extent.X);

            /// <summary>
            /// Constructs a 2D stride from the given 2D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseX FromExtent(LongIndex2D extent)
            {
                IndexTypeExtensions.AssertIntIndex(extent.X);
                return new DenseX((int)extent.X);
            }

            /// <summary>
            /// Computes the linear 32-bit element address using the given index and
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ComputeElementIndex(Index2D index, Index2D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long ComputeElementIndex(
                LongIndex2D index,
                LongIndex2D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Index2D ReconstructFromElementIndex(
                int elementIndex,
                Index2D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static LongIndex2D ReconstructFromElementIndex(
                long elementIndex,
                LongIndex2D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new dense X stride.
            /// </summary>
            /// <param name="yStride">The stride of the Y dimension.</param>
            public DenseX(int yStride)
            {
                Trace.Assert(yStride >= 1, "yStride out of range");

                YStride = yStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int XStride => 1;

            /// <summary>
            /// Returns the Y-dimension stride.
            /// </summary>
            public int YStride { get; }

            /// <summary>
            /// Returns the associated stride extent of the form (1, YStride).
            /// </summary>
            public readonly Index2D StrideExtent => new Index2D(XStride, YStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 32-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for the index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndexChecked(
                Index2D index,
                Index2D extent) =>
                Stride2D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndexChecked(
                LongIndex2D index,
                LongIndex2D extent) =>
                Stride2D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Index2D ReconstructFromElementIndex(int elementIndex)
            {
                int x = elementIndex % YStride;
                int y = elementIndex / YStride;
                return new Index2D(x, y);
            }

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly LongIndex2D ReconstructFromElementIndex(long elementIndex)
            {
                long x = elementIndex % YStride;
                long y = elementIndex / YStride;
                return new LongIndex2D(x, y);
            }

            /// <summary>
            /// Computes the 32-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 32-bit length of a required allocation.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeBufferLength(Index2D extent) =>
                Stride2D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex2D extent) =>
                Stride2D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes a new extent and stride based on the given cast context.
            /// </summary>
            /// <typeparam name="TContext">The cast context type.</typeparam>
            /// <param name="context">
            /// The cast context to adjust element information.
            /// </param>
            /// <param name="extent">The source extent.</param>
            /// <returns>The adjusted extent and stride information.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (LongIndex2D Extent, DenseX Stride) Cast<TContext>(
                in TContext context,
                in LongIndex2D extent)
                where TContext : struct, IStrideCastContext =>
                (
                    new LongIndex2D(
                        context.ComputeNewExtent(extent.X),
                        extent.Y),
                    new DenseX(context.ComputeNewExtent(YStride))
                );

            /// <summary>
            /// Returns this stride as general 2D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            /// <summary>
            /// Converts this stride instance into a general 1D stride.
            /// </summary>
            public readonly Stride1D.General To1DStride() =>
                new Stride1D.General(XStride);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }

        /// <summary>
        /// A 2D dense Y stride.
        /// </summary>
        public readonly struct DenseY :
            IStride2D,
            ICastableStride<Index2D, LongIndex2D, DenseY>
        {
            #region Static

            /// <summary>
            /// Constructs a 2D stride from the given 2D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseY FromExtent(Index2D extent) =>
                new DenseY(extent.Y);

            /// <summary>
            /// Constructs a 2D stride from the given 2D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseY FromExtent(LongIndex2D extent)
            {
                IndexTypeExtensions.AssertIntIndex(extent.Y);
                return new DenseY((int)extent.Y);
            }

            /// <summary>
            /// Computes the linear 32-bit element address using the given index and
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ComputeElementIndex(Index2D index, Index2D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long ComputeElementIndex(
                LongIndex2D index,
                LongIndex2D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Index2D ReconstructFromElementIndex(
                int elementIndex,
                Index2D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static LongIndex2D ReconstructFromElementIndex(
                long elementIndex,
                LongIndex2D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new dense Y stride.
            /// </summary>
            /// <param name="xStride">The stride of the X dimension.</param>
            public DenseY(int xStride)
            {
                Trace.Assert(xStride >= 1, "xStride out of range");

                XStride = xStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the X-dimension stride.
            /// </summary>
            public int XStride { get; }

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int YStride => 1;

            /// <summary>
            /// Returns the associated stride extent of the form (XStride, 1).
            /// </summary>
            public readonly Index2D StrideExtent => new Index2D(XStride, YStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 32-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for the index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndexChecked(
                Index2D index,
                Index2D extent) =>
                Stride2D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndexChecked(
                LongIndex2D index,
                LongIndex2D extent) =>
                Stride2D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Index2D ReconstructFromElementIndex(int elementIndex)
            {
                int x = elementIndex / XStride;
                int y = elementIndex % XStride;
                return new Index2D(x, y);
            }

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly LongIndex2D ReconstructFromElementIndex(long elementIndex)
            {
                long x = elementIndex / XStride;
                long y = elementIndex % XStride;
                return new LongIndex2D(x, y);
            }

            /// <summary>
            /// Computes the 32-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 32-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeBufferLength(Index2D extent) =>
                Stride2D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex2D extent) =>
                Stride2D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes a new extent and stride based on the given cast context.
            /// </summary>
            /// <typeparam name="TContext">The cast context type.</typeparam>
            /// <param name="context">
            /// The cast context to adjust element information.
            /// </param>
            /// <param name="extent">The source extent.</param>
            /// <returns>The adjusted extent and stride information.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (LongIndex2D Extent, DenseY Stride) Cast<TContext>(
                in TContext context,
                in LongIndex2D extent)
                where TContext : struct, IStrideCastContext =>
                (
                    new LongIndex2D(
                        extent.X,
                        context.ComputeNewExtent(extent.Y)),
                    new DenseY(context.ComputeNewExtent(XStride))
                );

            /// <summary>
            /// Returns this stride as general 2D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            /// <summary>
            /// Converts this stride instance into a general 1D stride.
            /// </summary>
            public readonly Stride1D.General To1DStride() =>
                new Stride1D.General(YStride);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }
    }

    partial class Stride3D
    {
        /// <summary>
        /// A 3D dense XY stride.
        /// </summary>
        public readonly struct DenseXY :
            IStride3D,
            ICastableStride<Index3D, LongIndex3D, DenseXY>
        {
            #region Static

            /// <summary>
            /// Constructs a 3D stride from the given 3D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseXY FromExtent(Index3D extent)
            {
                IndexTypeExtensions.AssertIntIndex((long)extent.X * extent.Y);
                return new DenseXY(extent.X, extent.X * extent.Y);
            }

            /// <summary>
            /// Constructs a 3D stride from the given 3D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseXY FromExtent(LongIndex3D extent)
            {
                IndexTypeExtensions.AssertIntIndex(extent.X);
                IndexTypeExtensions.AssertIntIndex(extent.X * extent.Y);
                return new DenseXY((int)extent.X, (int)(extent.X * extent.Y));
            }

            /// <summary>
            /// Computes the linear 32-bit element address using the given index and
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ComputeElementIndex(Index3D index, Index3D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long ComputeElementIndex(
                LongIndex3D index,
                LongIndex3D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Index3D ReconstructFromElementIndex(
                int elementIndex,
                Index3D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static LongIndex3D ReconstructFromElementIndex(
                long elementIndex,
                LongIndex3D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new dense XY stride.
            /// </summary>
            /// <param name="yStride">The stride of the Y dimension.</param>
            /// <param name="zStride">The stride of the Z dimension.</param>
            public DenseXY(int yStride, int zStride)
            {
                Trace.Assert(yStride >= 1, "yStride out of range");
                Trace.Assert(zStride >= 1, "zStride out of range");
                Trace.Assert(zStride >= yStride, "zStride out of range");

                YStride = yStride;
                ZStride = zStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int XStride => 1;

            /// <summary>
            /// Returns the Y-dimension stride.
            /// </summary>
            public int YStride { get; }

            /// <summary>
            /// Returns the Z-dimension stride.
            /// </summary>
            public int ZStride { get; }

            /// <summary>
            /// Returns the associated stride extent of the form (1, YStride, ZStride).
            /// </summary>
            public readonly Index3D StrideExtent =>
                new Index3D(XStride, YStride, ZStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 32-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for the index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndexChecked(
                Index3D index,
                Index3D extent) =>
                Stride3D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndexChecked(
                LongIndex3D index,
                LongIndex3D extent) =>
                Stride3D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Index3D ReconstructFromElementIndex(int elementIndex)
            {
                int x = elementIndex % YStride;
                int yz = elementIndex / YStride;
                int zDimDiff = ZStride / YStride;
                int y = yz % zDimDiff;
                int z = yz / zDimDiff;
                return new Index3D(x, y, z);
            }

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly LongIndex3D ReconstructFromElementIndex(long elementIndex)
            {
                long x = elementIndex % YStride;
                long yz = elementIndex / YStride;
                int zDimDiff = ZStride / YStride;
                long y = yz % zDimDiff;
                long z = yz / zDimDiff;
                return new LongIndex3D(x, y, z);
            }

            /// <summary>
            /// Computes the 32-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 3264-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeBufferLength(Index3D extent) =>
                Stride3D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex3D extent) =>
                Stride3D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes a new extent and stride based on the given cast context.
            /// </summary>
            /// <typeparam name="TContext">The cast context type.</typeparam>
            /// <param name="context">
            /// The cast context to adjust element information.
            /// </param>
            /// <param name="extent">The source extent.</param>
            /// <returns>The adjusted extent and stride information.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (LongIndex3D Extent, DenseXY Stride) Cast<TContext>(
                in TContext context,
                in LongIndex3D extent)
                where TContext : struct, IStrideCastContext =>
                (
                    new LongIndex3D(
                        context.ComputeNewExtent(extent.X),
                        extent.Y,
                        extent.Z),
                    new DenseXY(context.ComputeNewExtent(YStride), ZStride)
                );

            /// <summary>
            /// Returns this stride as general 3D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            /// <summary>
            /// Converts this stride instance into a general 1D stride.
            /// </summary>
            public readonly Stride1D.General To1DStride() =>
                new Stride1D.General(XStride);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }

        /// <summary>
        /// A 3D dense ZY stride.
        /// </summary>
        public readonly struct DenseZY :
            IStride3D,
            ICastableStride<Index3D, LongIndex3D, DenseZY>
        {
            #region Static

            /// <summary>
            /// Constructs a 3D stride from the given 3D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseZY FromExtent(Index3D extent)
            {
                IndexTypeExtensions.AssertIntIndex((long)extent.Z * extent.Y);
                return new DenseZY(extent.Z * extent.Y, extent.Z);
            }

            /// <summary>
            /// Constructs a 3D stride from the given 3D extent.
            /// </summary>
            /// <param name="extent">The index to use.</param>
            /// <returns>The constructed stride.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DenseZY FromExtent(LongIndex3D extent)
            {
                IndexTypeExtensions.AssertIntIndex(extent.Z * extent.Y);
                IndexTypeExtensions.AssertIntIndex(extent.Z);
                return new DenseZY((int)(extent.Z * extent.Y), (int)extent.Z);
            }

            /// <summary>
            /// Computes the linear 32-bit element address using the given index and
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ComputeElementIndex(Index3D index, Index3D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index
            /// extent information.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long ComputeElementIndex(
                LongIndex3D index,
                LongIndex3D extent) =>
                FromExtent(extent).ComputeElementIndex(index);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Index3D ReconstructFromElementIndex(
                int elementIndex,
                Index3D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <param name="extent">The extent to be used.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static LongIndex3D ReconstructFromElementIndex(
                long elementIndex,
                LongIndex3D extent) =>
                FromExtent(extent).ReconstructFromElementIndex(elementIndex);

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new dense ZY stride.
            /// </summary>
            /// <param name="xStride">The stride of the X dimension.</param>
            /// <param name="yStride">The stride of the Y dimension.</param>
            public DenseZY(int xStride, int yStride)
            {
                Trace.Assert(xStride >= 1, "xStride out of range");
                Trace.Assert(yStride >= 1, "yStride out of range");
                Trace.Assert(xStride >= yStride, "xStride out of range");

                XStride = xStride;
                YStride = yStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the X-dimension stride.
            /// </summary>
            public int XStride { get; }

            /// <summary>
            /// Returns the Y-dimension stride.
            /// </summary>
            public int YStride { get; }

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int ZStride => 1;

            /// <summary>
            /// Returns the associated stride extent of the form (XStride, YStride, 1).
            /// </summary>
            public readonly Index3D StrideExtent =>
                new Index3D(XStride, YStride, ZStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 32-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for the index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndexChecked(
                Index3D index,
                Index3D extent) =>
                Stride3D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index while
            /// verifying that the given index is within the bounds of the specified
            /// extent.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <param name="extent">The extent dimension to check.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndexChecked(
                LongIndex3D index,
                LongIndex3D extent) =>
                Stride3D.ComputeElementIndexChecked(this, index, extent);

            /// <summary>
            /// Reconstructs a 32-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Index3D ReconstructFromElementIndex(int elementIndex)
            {
                int z = elementIndex % YStride;
                int xy = elementIndex / YStride;
                int xDimDiff = XStride / YStride;
                int y = xy % xDimDiff;
                int x = xy / xDimDiff;
                return new Index3D(x, y, z);
            }

            /// <summary>
            /// Reconstructs a 64-bit index from a linear element index.
            /// </summary>
            /// <param name="elementIndex">The linear element index.</param>
            /// <returns>The reconstructed index.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly LongIndex3D ReconstructFromElementIndex(long elementIndex)
            {
                long z = elementIndex % YStride;
                long xy = elementIndex / YStride;
                int xDimDiff = XStride / YStride;
                long y = xy % xDimDiff;
                long x = xy / xDimDiff;
                return new LongIndex3D(x, y, z);
            }

            /// <summary>
            /// Computes the 32-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 32-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeBufferLength(Index3D extent) =>
                Stride3D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex3D extent) =>
                Stride3D.ComputeBufferLength(this, extent);

            /// <summary>
            /// Computes a new extent and stride based on the given cast context.
            /// </summary>
            /// <typeparam name="TContext">The cast context type.</typeparam>
            /// <param name="context">
            /// The cast context to adjust element information.
            /// </param>
            /// <param name="extent">The source extent.</param>
            /// <returns>The adjusted extent and stride information.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (LongIndex3D Extent, DenseZY Stride) Cast<TContext>(
                in TContext context,
                in LongIndex3D extent)
                where TContext : struct, IStrideCastContext =>
                (
                    new LongIndex3D(
                        extent.X,
                        extent.Y,
                        context.ComputeNewExtent(extent.Z)),
                    new DenseZY(XStride, context.ComputeNewExtent(YStride))
                );

            /// <summary>
            /// Returns this stride as general 3D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            /// <summary>
            /// Converts this stride instance into a general 1D stride.
            /// </summary>
            public readonly Stride1D.General To1DStride() =>
                new Stride1D.General(ZStride);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }
    }
}
