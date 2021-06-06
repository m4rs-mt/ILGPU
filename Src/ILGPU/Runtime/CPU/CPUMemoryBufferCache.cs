// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUMemoryBufferCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using System.Diagnostics;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a cached memory CPU buffer with a specific capacity.
    /// </summary>
    sealed class CPUMemoryBufferCache : AcceleratorObject
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
        public CPUMemoryBufferCache(CPUAccelerator accelerator)
            : this(accelerator, 0)
        { }

        /// <summary>
        /// Constructs a new memory-buffer cache.
        /// </summary>
        /// <param name="accelerator">
        /// The associated accelerator to allocate memory on.
        /// </param>
        /// <param name="initialLength">The initial length of the buffer.</param>
        public CPUMemoryBufferCache(CPUAccelerator accelerator, long initialLength)
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

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this cache by disposing the associated cache buffer.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
                cache?.Dispose();
            cache = null;
        }

        #endregion
    }
}
