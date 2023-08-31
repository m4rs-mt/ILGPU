// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PageLockedArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.CPU;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a .Net array that is pinned with respect to the .Net GC and page
    /// locked in physical memory.
    /// </summary>
    /// <typeparam name="T">The array element type.</typeparam>
    public abstract class PageLockedArray<T> : DisposeBase
        where T : unmanaged
    {
        #region Properties

        /// <summary>
        /// Returns the span including all elements of the underlying arrays.
        /// </summary>
        public abstract Span<T> Span { get; }

        /// <summary>
        /// Returns the memory buffer wrapper of the .Net array.
        /// </summary>
        protected internal MemoryBuffer MemoryBuffer { get; private set; } =
            Utilities.InitNotNullable<MemoryBuffer>();

        /// <summary>
        /// Returns the page locking scope that includes the underlying array.
        /// </summary>
        protected internal PageLockScope<T> Scope { get; private set; } =
            Utilities.InitNotNullable<PageLockScope<T>>();

        /// <summary>
        /// Returns the array view of the underlying .Net array.
        /// </summary>
        public ArrayView<T> ArrayView { get; private set; } = ArrayView<T>.Empty;

        /// <summary>
        /// Returns the length of this array.
        /// </summary>
        public long Length => Span.Length;

        #endregion

        #region Methods

        /// <summary>
        /// Initializes this page locked array.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="ptr">The pinned host pointer.</param>
        /// <param name="length">The total number of elements.</param>
        protected unsafe void Initialize(
            Accelerator? accelerator,
            IntPtr ptr,
            long length)
        {
            if (length < 0L)
                throw new ArgumentNullException(nameof(length));

            if (accelerator != null && length > 0L)
            {
                MemoryBuffer = CPUMemoryBuffer.Create(
                    accelerator,
                    ptr,
                    length,
                    Interop.SizeOf<T>());
                ArrayView = MemoryBuffer.AsArrayView<T>(0L, MemoryBuffer.Length);
                Scope = accelerator.CreatePageLockFromPinned<T>(ptr, length);
            }
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns the internal underlying <see cref="Span{T}.Enumerator"/> enumerator.
        /// </summary>
        /// <returns>An enumerator to iterate over all elements in the array.</returns>
        public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MemoryBuffer?.Dispose();
                Scope?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Represents a page locked 1D array in memory.
    /// </summary>
    /// <typeparam name="T">The array element type.</typeparam>
    public sealed class PageLockedArray1D<T> : PageLockedArray<T>
        where T : unmanaged
    {
        #region Static

        /// <summary>
        /// Represents an empty 1D array.
        /// </summary>
        public static readonly PageLockedArray1D<T> Empty =
            new PageLockedArray1D<T>(null, LongIndex1D.Zero);

        #endregion

        #region Instance

        private readonly T[] array;

        /// <summary>
        /// Creates a new page-locked 1D array.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="extent">The number of elements to allocate.</param>
        internal PageLockedArray1D(Accelerator? accelerator, LongIndex1D extent)
            : this(accelerator, extent, false)
        { }

        /// <summary>
        /// Creates a new page-locked 1D array.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <param name="uninitialized">True, to allocate an uninitialized array.</param>
        internal unsafe PageLockedArray1D(
            Accelerator? accelerator,
            LongIndex1D extent,
            bool uninitialized)
        {
            if (extent < 0L)
                throw new ArgumentOutOfRangeException(nameof(extent));
            array = uninitialized
                ? GC.AllocateUninitializedArray<T>(extent.ToIntIndex(), pinned: true)
                : GC.AllocateArray<T>(extent.ToIntIndex(), pinned: true);
            fixed (T* ptr = array)
                Initialize(accelerator, new IntPtr(ptr), extent);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the extent of this array.
        /// </summary>
        public LongIndex1D Extent => array.LongLength;

        /// <inheritdoc/>
        public override Span<T> Span => array.AsSpan();

        /// <summary>
        /// Returns a reference to the i-th array element.
        /// </summary>
        /// <param name="index">The index of the array element.</param>
        /// <returns>The determined value reference.</returns>
        public ref T this[int index] => ref array[index];

        /// <summary>
        /// Returns a reference to the i-th array element.
        /// </summary>
        /// <param name="index">The index of the array element.</param>
        /// <returns>The determined value reference.</returns>
        public ref T this[long index] => ref array[index];

        #endregion

        #region Methods

        /// <summary>
        /// Returns the underlying array.
        /// </summary>
        public T[] GetArray() => array;

        #endregion
    }

    /// <summary>
    /// Extension methods for page locked array types.
    /// </summary>
    public static partial class PageLockedArrayExtensions
    {
        /// <summary>
        /// Creates a page locked array in CPU memory optimized for GPU data exchange.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="extent">The number of elements.</param>
        /// <param name="uninitialized">True, to skip data initialization.</param>
        /// <returns>The allocated array.</returns>
        public static PageLockedArray1D<T> AllocatePageLocked1D<T>(
            this Accelerator accelerator,
            LongIndex1D extent,
            bool uninitialized)
            where T : unmanaged =>
            new PageLockedArray1D<T>(accelerator, extent, uninitialized);
    }
}
