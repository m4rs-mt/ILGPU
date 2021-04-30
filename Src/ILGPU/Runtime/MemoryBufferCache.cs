// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MemoryBufferCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using System.Diagnostics;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a cached memory buffer with a specific capacity.  It minimizes
    /// reallocations in cases of requests that can also be handled with the currently
    /// allocated amount of memory.  If the requested amount of memory is not
    /// sufficient, the current buffer will be freed and a new buffer will be allocated.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class MemoryBufferCache : AcceleratorObject, ICache
    {
        #region Instance

        /// <summary>
        /// This represents the actual memory cache.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MemoryBuffer<ArrayView1D<byte, Stride1D.Dense>> cache;

        /// <summary>
        /// Constructs a new memory-buffer cache.
        /// </summary>
        /// <param name="accelerator">
        /// The associated accelerator to allocate memory on.
        /// </param>
        public MemoryBufferCache(Accelerator accelerator)
            : this(accelerator, 0)
        { }

        /// <summary>
        /// Constructs a new memory-buffer cache.
        /// </summary>
        /// <param name="accelerator">
        /// The associated accelerator to allocate memory on.
        /// </param>
        /// <param name="initialLength">The initial length of the buffer.</param>
        public MemoryBufferCache(Accelerator accelerator, long initialLength)
            : base(accelerator)
        {
            if (initialLength > 0)
                cache = accelerator.Allocate1D<byte>(initialLength);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current cached size in bytes.
        /// </summary>
        public long CacheSizeInBytes => cache?.LengthInBytes ?? 0;

        /// <summary>
        /// Returns the underlying memory buffer view.
        /// </summary>
        public ArrayView<byte> Cache => cache.View;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the available number of elements of type T.
        /// </summary>
        /// <typeparam name="T">The desired element type.</typeparam>
        /// <returns>The available number of elements of type T.</returns>
        public long GetCacheSize<T>()
            where T : unmanaged =>
            (cache?.Length ?? 0L) / Interop.SizeOf<T>();

        /// <summary>
        /// Allocates the given number of elements and returns an array view
        /// to the requested amount of elements. Note that the array view
        /// points to not-initialized memory.
        /// </summary>
        /// <param name="numElements">The number of elements to allocate.</param>
        /// <returns>
        /// An array view that can access the requested number of elements.
        /// </returns>
        public ArrayView<T> Allocate<T>(long numElements)
            where T : unmanaged
        {
            if (numElements < Index1D.One)
                return default;
            if (numElements > GetCacheSize<T>())
            {
                cache?.Dispose();
                cache = Accelerator.Allocate1D<byte>(numElements * Interop.SizeOf<T>());
            }
            Debug.Assert(numElements <= GetCacheSize<T>());
            return Cache.Cast<T>().SubView(0, numElements);
        }

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target location.</param>
        /// <param name="sourceIndex">
        /// The source index from which to copy to the output.
        /// </param>
        public unsafe void CopyTo<T>(
            AcceleratorStream stream,
            out T target,
            long sourceIndex)
            where T : unmanaged
        {
            target = default;

            using var wrapper = CPUMemoryBuffer.Create(ref target, 1);
            cache.CopyTo(stream, sourceIndex, wrapper.AsRawArrayView());
        }

        /// <summary>
        /// Copies a single element from CPU memory to this buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source value.</param>
        /// <param name="targetIndex">
        /// The target index to which to copy the input.
        /// </param>
        public unsafe void CopyFrom<T>(
            AcceleratorStream stream,
            T source,
            long targetIndex)
            where T : unmanaged
        {
            using var wrapper = CPUMemoryBuffer.Create(ref source, 1);
            cache.CopyFrom(stream, wrapper.AsRawArrayView(), targetIndex);
        }

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public void ClearCache(ClearCacheMode mode)
        {
            cache?.Dispose();
            cache = null;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this cache by disposing the associated cache buffer.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing && cache != null)
                cache.Dispose();
            cache = null;
        }

        #endregion
    }
}
