// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
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
    /// Creates a new view pointer wrapper that wraps a pointer reference
    /// inside an array view.
    /// </summary>
    public class ViewPointerWrapper : ArrayViewSource
    {
        /// <summary>
        /// Creates a new pointer wrapper.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="value">The value reference to the variable.</param>
        /// <returns>An unsafe array view source.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ViewPointerWrapper Create<T>(ref T value)
            where T : struct =>
            new ViewPointerWrapper(
                new IntPtr(Unsafe.AsPointer(ref value)));

        /// <summary>
        /// Creates a new pointer wrapper.
        /// </summary>
        /// <param name="value">The native value pointer.</param>
        /// <returns>An unsafe array view source.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ViewPointerWrapper Create(IntPtr value) =>
            new ViewPointerWrapper(value);

        /// <summary>
        /// Creates a new pointer wrapper.
        /// </summary>
        /// <param name="ptr">The native value pointer.</param>
        protected ViewPointerWrapper(IntPtr ptr)
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
    /// Represents a view source that allocates native memory in the CPU address space.
    /// </summary>
    internal sealed class UnmanagedMemoryViewSource : ViewPointerWrapper
    {
        /// <summary>
        /// Creates a new unmanaged memory view source.
        /// </summary>
        /// <param name="sizeInBytes">The size in bytes to allocate..</param>
        /// <returns>An unsafe array view source.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UnmanagedMemoryViewSource Create(int sizeInBytes) =>
            new UnmanagedMemoryViewSource(sizeInBytes);

        private UnmanagedMemoryViewSource(int sizeInBytes)
            : base(Marshal.AllocHGlobal(sizeInBytes))
        { }

        /// <summary cref="ArrayViewSource.GetAsRawArray(AcceleratorStream, Index, Index)"/>
        protected internal override ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index byteOffset,
            Index byteExtent) => throw new InvalidOperationException();

        #region IDispoable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Marshal.FreeHGlobal(NativePtr);
        }

        #endregion
    }

    /// <summary>
    /// Represents a view array wrapper that wraps a <see cref="GCHandle"/>
    /// instance. Note that this instance will not be freed automatically during
    /// the <see cref="IDisposable.Dispose"/> operation.
    /// </summary>
    public sealed class ViewArrayWrapper : ArrayViewSource
    {
        /// <summary>
        /// Creates a new array wrapper.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="handle">The gc handle of the fixed array.</param>
        /// <returns>An unsafe array view source.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ViewArrayWrapper Create<T>(GCHandle handle)
            where T : struct =>
            new ViewArrayWrapper(handle, Unsafe.SizeOf<T>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
