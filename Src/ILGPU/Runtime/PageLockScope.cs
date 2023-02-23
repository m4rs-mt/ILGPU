// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PageLockScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
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
        /// <param name="addrOfLockedObject">The address of page locked object.</param>
        /// <param name="numElements">The number of elements.</param>
        protected PageLockScope(
            Accelerator accelerator,
            IntPtr addrOfLockedObject,
            long numElements)
            : base(accelerator)
        {
            AddrOfLockedObject = addrOfLockedObject;
            Length = numElements;

            if (Length > 0)
            {
                MemoryBuffer = CPUMemoryBuffer.Create(
                    accelerator,
                    AddrOfLockedObject,
                    Length,
                    Interop.SizeOf<T>());
                ArrayView = MemoryBuffer.AsArrayView<T>(0L, MemoryBuffer.Length);
            }
            else
            {
                ArrayView = ArrayView<T>.Empty;
            }
        }

        /// <summary>
        /// Returns the address of page locked object.
        /// </summary>
        public IntPtr AddrOfLockedObject { get; }

        /// <summary>
        /// Returns the number of elements.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Returns the length of the page locked memory in bytes.
        /// </summary>
        public long LengthInBytes => Length * Interop.SizeOf<T>();

        /// <summary>
        /// Returns the memory buffer wrapper of the .Net array.
        /// </summary>
        private MemoryBuffer MemoryBuffer { get; set; }

        /// <summary>
        /// Returns the array view of the underlying .Net array.
        /// </summary>
        public ArrayView<T> ArrayView { get; private set; }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
            {
                MemoryBuffer?.Dispose();
                MemoryBuffer = null;
                ArrayView = ArrayView<T>.Empty;
            }
        }
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
            : base(accelerator, hostPtr, numElements)
        { }
    }
}
