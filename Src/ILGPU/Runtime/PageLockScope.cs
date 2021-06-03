// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PageLockScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the scope/duration of a page lock.
    /// </summary>
    public abstract class PageLockScope<T> : AcceleratorObject
        where T : unmanaged
    {
        /// <summary>
        /// Constructs a page lock scope for the accelerator.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected PageLockScope(Accelerator accelerator)
            : base(accelerator)
        { }

        /// <summary>
        /// Returns the address of page locked object.
        /// </summary>
        public abstract IntPtr AddrOfLockedObject { get; }

        /// <summary>
        /// Returns the number of elements.
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Returns the length of the page locked memory in bytes.
        /// </summary>
        public long LengthInBytes => Length * Interop.SizeOf<T>();
    }
}
