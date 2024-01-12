// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: StringCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ILGPU.Util
{
    /// <summary>
    /// Maintains a cache of strings that have been marshalled to native strings
    /// and need to be kept in memory.
    /// </summary>
    internal class StringCache : DisposeBase
    {
        #region Nested Types

        public readonly struct StringCacheEntry
        {
            public IntPtr NativePtr { get; }
            public int Length { get; }

            public StringCacheEntry(IntPtr ptr, int length)
            {
                NativePtr = ptr;
                Length = length;
            }
        }

        #endregion

        #region Instance

        private readonly InlineList<StringCacheEntry> _cache =
            InlineList<StringCacheEntry>.Create(1);

        #endregion

        #region Methods

        /// <summary>
        /// Adds a string to the cache, and returns the native pointer and length.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <returns>The native pointer and length.</returns>
        public unsafe StringCacheEntry AddString(string value)
        {
            // Create null-terminated native string
            var len = Encoding.ASCII.GetMaxByteCount(value.Length);
            var ptr = Marshal.AllocHGlobal(len + 1);
            var ptrSpan = new Span<byte>((void*)ptr, len);

            len = Encoding.ASCII.GetBytes(value, ptrSpan);
            ptrSpan[len] = 0;

            // Add to cache, so that memory is valid until cache is disposed.
            var entry = new StringCacheEntry(ptr, len);
            _cache.Add(entry);
            return entry;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            foreach (var entry in _cache)
                Marshal.FreeHGlobal(entry.NativePtr);
            base.Dispose(disposing);
        }

        #endregion
    }
}
