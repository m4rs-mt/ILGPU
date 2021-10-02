// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: PageLockScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
        /// <param name="numElements">The number of elements.</param>
        protected PageLockScope(Accelerator accelerator, long numElements)
            : base(accelerator)
        {
            Length = numElements;
        }

        /// <summary>
        /// Returns the address of page locked object.
        /// </summary>
        public IntPtr AddrOfLockedObject { get; protected set; }

        /// <summary>
        /// Returns the number of elements.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Returns the length of the page locked memory in bytes.
        /// </summary>
        public long LengthInBytes => Length * Interop.SizeOf<T>();
    }

    /// <summary>
    /// A null/no-op page lock scope.
    /// </summary>
    internal sealed class NullPageLockScope<T> : PageLockScope<T>
        where T : unmanaged
    {
        /// <summary>
        /// Constructs a page lock scope for the accelerator.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="hostPtr">The host buffer pointer to page lock.</param>
        /// <param name="numElements">The number of elements in the buffer.</param>
        public NullPageLockScope(
            Accelerator accelerator,
            IntPtr hostPtr,
            long numElements)
            : base(accelerator, numElements)
        {
            AddrOfLockedObject = hostPtr;
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing) { }
    }
}
