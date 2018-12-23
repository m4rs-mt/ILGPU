// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryBufferCache.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a cached memory buffer with a specific capacity.
    /// It minimizes reallocations in cases of requests that can also
    /// be handled with the currently allocated amount of memory.
    /// If the requested amount of memory is not sufficient, the current
    /// buffer will be freed and a new buffer will be allocated.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class MemoryBufferCache : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// This represents the actual memory cache.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MemoryBuffer<byte, Index> cache;

        /// <summary>
        /// Constructs a new memory-buffer cache.
        /// </summary>
        /// <param name="accelerator">The associated accelerator to allocate memory on.</param>
        public MemoryBufferCache(Accelerator accelerator)
            : this(accelerator, 0)
        { }

        /// <summary>
        /// Constructs a new memory-buffer cache.
        /// </summary>
        /// <param name="accelerator">The associated accelerator to allocate memory on.</param>
        /// <param name="initialLength">The initial length of the buffer.</param>
        public MemoryBufferCache(Accelerator accelerator, Index initialLength)
            : base(accelerator)
        {
            if (initialLength > 0)
                cache = accelerator.Allocate<byte, Index>(initialLength);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current cached size in bytes.
        /// </summary>
        public Index CacheSizeInBytes => cache?.LengthInBytes ?? 0;

        /// <summary>
        /// Returns the underlying memory buffer.
        /// </summary>
        public MemoryBuffer<byte, Index> Cache => cache;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the available number of elements of type T.
        /// </summary>
        /// <typeparam name="T">The desired element type.</typeparam>
        /// <returns>The available number of elements of type T.</returns>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter",
            Justification = "The generic parameter is required to compute the number of elements of the given type that can be stored")]
        public Index GetCacheSize<T>()
            where T : struct => (cache?.Length ?? 0) * ArrayView<T>.ElementSize;

        /// <summary>
        /// Allocates the given number of elements and returns an array view
        /// to the requested amount of elements. Note that the array view
        /// points to not-initialized memory.
        /// </summary>
        /// <param name="numElements">The number of elements to allocate.</param>
        /// <returns>An array view that can access the requested number of elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> Allocate<T>(Index numElements)
            where T : struct
        {
            Debug.Assert(numElements > 0, "Invalid allocation of 0 bytes");
            if (numElements > GetCacheSize<T>())
            {
                cache?.Dispose();
                cache = Accelerator.Allocate<byte, Index>(numElements * ArrayView<T>.ElementSize);
            }
            Debug.Assert(numElements <= GetCacheSize<T>());
            return cache.View.Cast<T>().GetSubView(0, numElements).AsLinearView();
        }

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo<T>(AcceleratorStream stream, out T target, Index targetIndex)
            where T : struct
        {
            target = default;
            using (var wrapper = ViewPointerWrapper.Create(ref target))
            {
                var view = new ArrayView<T>(wrapper, 0, 1);
                cache.CopyToView(stream, view.Cast<byte>(), targetIndex);
            }
        }

        /// <summary>
        /// Copies a single element from CPU memory to this buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source value.</param>
        /// <param name="sourceIndex">The target index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyFrom<T>(AcceleratorStream stream, T source, Index sourceIndex)
            where T : struct
        {
            using (var wrapper = ViewPointerWrapper.Create(ref source))
            {
                var view = new ArrayView<T>(wrapper, 0, 1);
                cache.CopyFromView(stream, view.Cast<byte>(), sourceIndex);
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Dispose(ref cache);
        }

        #endregion
    }
}
