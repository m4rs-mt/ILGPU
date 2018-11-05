// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ArrayViewSource.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Internal abstract interface for generic array-view sources.
    /// </summary>
    public abstract class ArrayViewSource : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Initializes this array view source on the CPU.
        /// </summary>
        protected ArrayViewSource() { }

        /// <summary>
        /// Initializes this array view source.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected ArrayViewSource(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native pointer of this buffer.
        /// </summary>
        public IntPtr NativePtr { get; protected set; }

        #endregion

        #region Methods

        /// <summary>
        /// Loads the effective address of the first memory element.
        /// </summary>
        /// <param name="index">The base index.</param>
        /// <param name="elementSize">The element size.</param>
        /// <returns>The loaded effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal unsafe ref byte LoadEffectiveAddress(
            Index index,
            int elementSize)
        {
            return ref Unsafe.AddByteOffset(
                ref Unsafe.AsRef<byte>(NativePtr.ToPointer()),
                new IntPtr(index * elementSize));
        }

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="byteOffset">The offset in bytes.</param>
        /// <param name="byteExtent">The extent in bytes (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        internal ArraySegment<byte> GetAsDebugRawArray(Index byteOffset, Index byteExtent) =>
            GetAsRawArray(Accelerator.DefaultStream, byteOffset, byteExtent);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="byteOffset">The offset in bytes.</param>
        /// <param name="byteExtent">The extent in bytes (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        protected internal abstract ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index byteOffset,
            Index byteExtent);

        #endregion
    }

    /// <summary>
    /// Creates a new view pointer wrapper.
    /// </summary>
    sealed class ViewPointerWrapper : ArrayViewSource
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ViewPointerWrapper Create<T>(ref T value)
            where T : struct
        {
            return new ViewPointerWrapper(
                new IntPtr(Unsafe.AsPointer(ref value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ViewPointerWrapper Create(IntPtr value)
        {
            return new ViewPointerWrapper(value);
        }

        private ViewPointerWrapper(IntPtr ptr)
        {
            NativePtr = ptr;
        }

        /// <summary cref="ArrayViewSource.GetAsRawArray(AcceleratorStream, Index, Index)"/>
        protected internal override ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index byteOffset,
            Index byteExtent) => throw new InvalidOperationException();

        #region IDispoable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing) { }

        #endregion
    }

    /// <summary>
    /// Creates a new view array wrapper.
    /// </summary>
    sealed class ViewArrayWrapper : ArrayViewSource
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ViewArrayWrapper Create<T>(GCHandle handle)
            where T : struct
        {
            return new ViewArrayWrapper(handle, Unsafe.SizeOf<T>());
        }

        private ViewArrayWrapper(GCHandle handle, int elementSize)
        {
            NativePtr = handle.AddrOfPinnedObject();
            ElementSize = elementSize;
        }

        /// <summary>
        /// Returns the associated element size.
        /// </summary>
        public int ElementSize { get; }

        /// <summary cref="ArrayViewSource.GetAsRawArray(AcceleratorStream, Index, Index)"/>
        protected internal override ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index byteOffset,
            Index byteExtent) => throw new InvalidOperationException();

        #region IDispoable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing) { }

        #endregion
    }

}
