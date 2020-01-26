// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ICache.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU
{
    /// <summary>
    /// Specifies which resources should be removed from the cache.
    /// </summary>
    public enum ClearCacheMode : int
    {
        /// <summary>
        /// Removes all non-ILGPU objects form the caches.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Removes everything from the caches.
        /// </summary>
        Everything = 1,
    }

    /// <summary>
    /// Represents an object that contains internal caches.
    /// </summary>
    public interface ICache
    {
        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>
        /// Implementations of this method are not guaranteed to be thread-safe.
        /// </remarks>
        void ClearCache(ClearCacheMode mode);
    }
}
