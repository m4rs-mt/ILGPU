using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Cuda.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ILGPU.Runtime
{
    /// <summary>
    /// A buffer that stores a specified amount of elements on the associated accelerator
    /// instance. Furthermore, it keeps a buffer of the same size in pinned CPU memory
    /// to enable asynchronous memory transfers between the CPU and the GPU.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed unsafe class ExchangeBuffer<T> : MemoryBuffer, IMemoryBuffer<T>
        where T : unmanaged
    {
        #region Constants

        /// <summary>
        /// Represents the size of an element in bytes.
        /// </summary>
        public static readonly int ElementSize = MemoryBuffer<T, Index1>.ElementSize;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a view source that allocates native memory in page-locked CPU
        /// memory.
        /// </summary>
        sealed class CudaViewSource : ViewPointerWrapper
        {
            /// <summary>
            /// Creates a new Cuda view source.
            /// </summary>
            /// <param name="sizeInBytes">The size in bytes to allocate.</param>
            /// <returns>An unsafe array view source.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CudaViewSource Create(int sizeInBytes)
            {
                CudaException.ThrowIfFailed(
                    CudaAPI.Current.AllocateHostMemory(
                        out IntPtr hostPtr,
                        new IntPtr(sizeInBytes)));
                return new CudaViewSource(hostPtr);
            }

            /// <summary>
            /// Creates a new unmanaged memory view source.
            /// </summary>
            /// <param name="nativePtr">The native host pointer.</param>
            private CudaViewSource(IntPtr nativePtr)
                : base(nativePtr)
            { }

            /// <summary cref="ArrayViewSource.GetAsRawArray(
            /// AcceleratorStream, Index1, Index1)"/>
            protected internal override ArraySegment<byte> GetAsRawArray(
                AcceleratorStream stream,
                Index1 byteOffset,
                Index1 byteExtent) => throw new InvalidOperationException();

            #region IDispoable

            /// <summary cref="DisposeBase.Dispose(bool)"/>
            protected override void Dispose(bool disposing)
            {
                if (NativePtr != IntPtr.Zero)
                {
                    CudaAPI.Current.FreeHostMemory(NativePtr);
                    NativePtr = IntPtr.Zero;
                }
                base.Dispose(disposing);
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// The internally allocated CPU memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ViewPointerWrapper cpuMemory;

        /// <summary>
        /// A cached version of the CPU memory pointer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly void* cpuMemoryPointer;

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="buffer">The underlying memory buffer.</param>
        /// <param name="mode">The current buffer allocation mode.</param>
        internal ExchangeBuffer(MemoryBuffer<T, Index1> buffer, ExchangeBufferMode mode)
            : base(buffer.Accelerator, buffer.Extent.Size)
        {
            // Allocate CPU memory
            cpuMemory = Accelerator is CudaAccelerator &&
                mode == ExchangeBufferMode.PreferPagedLockedMemory
                ? CudaViewSource.Create(buffer.LengthInBytes)
                : (ViewPointerWrapper)UnmanagedMemoryViewSource.Create(
                    buffer.LengthInBytes);

            cpuMemoryPointer = cpuMemory.NativePtr.ToPointer();
            CPUView = new ArrayView<T>(cpuMemory, 0, buffer.Length);

            // Cache local data
            Buffer = buffer;
            NativePtr = buffer.NativePtr;
            View = buffer.View;
            Extent = buffer.Extent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying generic memory buffer.
        /// </summary>
        public MemoryBuffer<T, Index1> Buffer { get; }

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        public ArrayView<T> View { get; }

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public Index1 LengthInBytes => Buffer.LengthInBytes;

        /// <summary>
        /// Returns the extent of this buffer.
        /// </summary>
        public Index1 Extent { get; }

        /// <summary>
        /// Returns an array view to the CPU part of this buffer.
        /// </summary>
        public ArrayView<T> CPUView { get; }

        /// <summary>
        /// Returns a reference to the i-th element in CPU memory.
        /// </summary>
        /// <param name="index">The element index to access.</param>
        /// <returns>A reference to the i-th element in CPU memory.</returns>
        public unsafe ref T this[Index1 index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.Add(
                ref Unsafe.AsRef<T>(cpuMemoryPointer),
                index);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        public override void MemSetToZero(AcceleratorStream stream) =>
            Buffer.MemSetToZero(stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public override byte[] GetAsRawArray(AcceleratorStream stream) =>
            Buffer.GetAsRawArray(stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="byteOffset">The offset in bytes.</param>
        /// <param name="byteExtent">The extent in bytes (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        protected internal override ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index1 byteOffset,
            Index1 byteExtent) =>
            Buffer.GetAsRawArray(stream, byteOffset, byteExtent);

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public unsafe T[] GetAsArray(AcceleratorStream stream)
        {
            CopyFromAccelerator(stream);
            stream.Synchronize();

            var data = new T[Length];
            fixed (T* ptr = &data[0])
            {
                System.Buffer.MemoryCopy(
                    cpuMemoryPointer,
                    ptr,
                    LengthInBytes,
                    LengthInBytes);
            }
            return data;
        }

        /// <summary>
        /// Copes data from CPU memory to the associated accelerator.
        /// </summary>
        public void CopyToAccelerator() =>
            CopyToAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyToAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Buffer.CopyFromView(stream, CPUView, 0);
        }

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        public void CopyFromAccelerator() =>
            CopyFromAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyFromAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Buffer.CopyToView(stream, CPUView, 0);
        }

        /// <summary>
        /// Returns the underlying generic memory buffer.
        /// </summary>
        /// <returns>The underlying generic memory buffer.</returns>
        public MemoryBuffer<T, Index1> ToMemoryBuffer() => Buffer;

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        /// <returns>An array view that can access this array.</returns>
        public ArrayView<T, Index1> ToArrayView() => View;

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        public static implicit operator ArrayView<T>(ExchangeBuffer<T> buffer)
        {
            Debug.Assert(buffer != null, "Invalid buffer");
            return buffer.View;
        }

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        public static implicit operator MemoryBuffer<T, Index1>(ExchangeBuffer<T> buffer)
        {
            Debug.Assert(buffer != null, "Invalid buffer");
            return buffer.Buffer;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Buffer.Dispose();
                cpuMemory.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// A 2D buffer that stores a specified amount of elements on the associated accelerator
    /// instance. Furthermore, it keeps a buffer of the same size in pinned CPU memory
    /// to enable asynchronous memory transfers between the CPU and the GPU.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed unsafe class ExchangeBuffer2D<T> : MemoryBuffer, IMemoryBuffer<T>
        where T : unmanaged
    {
        #region Constants

        /// <summary>
        /// Represents the size of an element in bytes.
        /// </summary>
        public static readonly int ElementSize = MemoryBuffer<T, Index2>.ElementSize;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a view source that allocates native memory in page-locked CPU
        /// memory.
        /// </summary>
        sealed class CudaViewSource : ViewPointerWrapper
        {
            /// <summary>
            /// Creates a new Cuda view source.
            /// </summary>
            /// <param name="sizeInBytes">The size in bytes to allocate.</param>
            /// <returns>An unsafe array view source.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CudaViewSource Create(int sizeInBytes)
            {
                CudaException.ThrowIfFailed(
                    CudaAPI.Current.AllocateHostMemory(
                        out IntPtr hostPtr,
                        new IntPtr(sizeInBytes)));
                return new CudaViewSource(hostPtr);
            }

            /// <summary>
            /// Creates a new unmanaged memory view source.
            /// </summary>
            /// <param name="nativePtr">The native host pointer.</param>
            private CudaViewSource(IntPtr nativePtr)
                : base(nativePtr)
            { }

            /// <summary cref="ArrayViewSource.GetAsRawArray(
            /// AcceleratorStream, Index1, Index1)"/>
            protected internal override ArraySegment<byte> GetAsRawArray(
                AcceleratorStream stream,
                Index1 byteOffset,
                Index1 byteExtent) => throw new InvalidOperationException();

            #region IDispoable

            /// <summary cref="DisposeBase.Dispose(bool)"/>
            protected override void Dispose(bool disposing)
            {
                if (NativePtr != IntPtr.Zero)
                {
                    CudaAPI.Current.FreeHostMemory(NativePtr);
                    NativePtr = IntPtr.Zero;
                }
                base.Dispose(disposing);
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// The internally allocated CPU memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ViewPointerWrapper cpuMemory;

        /// <summary>
        /// A cached version of the CPU memory pointer.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly void* cpuMemoryPointer;

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="buffer">The underlying memory buffer.</param>
        /// <param name="mode">The current buffer allocation mode.</param>
        internal ExchangeBuffer2D(MemoryBuffer<T, Index2> buffer, ExchangeBufferMode mode)
            : base(buffer.Accelerator, buffer.Extent.Size)
        {
            // Allocate CPU memory
            cpuMemory = Accelerator is CudaAccelerator &&
                mode == ExchangeBufferMode.PreferPagedLockedMemory
                ? CudaViewSource.Create(buffer.LengthInBytes)
                : (ViewPointerWrapper)UnmanagedMemoryViewSource.Create(
                    buffer.LengthInBytes);

            cpuMemoryPointer = cpuMemory.NativePtr.ToPointer();
            ArrayView<T> tempView = new ArrayView<T>(cpuMemory, 0, buffer.Length);
            CPUView = new ArrayView<T, Index2>(tempView, buffer.Extent);

            // Cache local data
            Buffer = buffer;
            NativePtr = buffer.NativePtr;
            View = buffer.View;
            Extent = buffer.Extent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying generic memory buffer.
        /// </summary>
        public MemoryBuffer<T, Index2> Buffer { get; }

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        public ArrayView<T, Index2> View { get; }

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public Index1 LengthInBytes => Buffer.LengthInBytes;

        /// <summary>
        /// Returns the extent of this buffer.
        /// </summary>
        public Index2 Extent { get; }

        /// <summary>
        /// Returns an array view to the CPU part of this buffer.
        /// </summary>
        public ArrayView<T, Index2> CPUView { get; }

        /// <summary>
        /// Gets/sets the element specified by the index
        /// </summary>
        /// <param name="index2">The index to access</param>
        /// <returns>The specified element</returns>
        /// <remarks>
        /// Uses the CPU View (<see cref="CPUView"/>), not the GPU View (<see cref="View"/>). <br></br>
        /// You will need to copy again if you want use the modified version in a kernel.
        /// </remarks>
        public T this[Index2 index2]
        {
            get => CPUView[index2];

            set => CPUView[index2] = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        public override void MemSetToZero(AcceleratorStream stream) =>
            Buffer.MemSetToZero(stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public override byte[] GetAsRawArray(AcceleratorStream stream) =>
            Buffer.GetAsRawArray(stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="byteOffset">The offset in bytes.</param>
        /// <param name="byteExtent">The extent in bytes (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        protected internal override ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index1 byteOffset,
            Index1 byteExtent) =>
            Buffer.GetAsRawArray(stream, byteOffset, byteExtent);

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public unsafe T[] GetAsArray(AcceleratorStream stream)
        {
            CopyFromAccelerator(stream);
            stream.Synchronize();

            var data = new T[Length];
            fixed (T* ptr = &data[0])
            {
                System.Buffer.MemoryCopy(
                    cpuMemoryPointer,
                    ptr,
                    LengthInBytes,
                    LengthInBytes);
            }
            return data;
        }

        /// <summary>
        /// Gets this buffer as a 2D jagged array from the accelerator
        /// </summary>
        /// <param name="stream">The stream to use</param>
        /// <returns>A jagged array containing all the elements in this exchnage buffer</returns>
        public unsafe T[][] GetAs2DArray(AcceleratorStream stream)
        {
            CopyFromAccelerator(stream);
            stream.Synchronize();

            var data = new T[Length];
            fixed (T* ptr = &data[0])
            {
                System.Buffer.MemoryCopy(
                    cpuMemoryPointer,
                    ptr,
                    LengthInBytes,
                    LengthInBytes);
            }

            var data2D = new T[Extent.X][];
            for (int i = 0; i < Extent.X; i++)
            {
                data2D[i] = new T[Extent.Y];
                for (int j = 0; j < Extent.Y; j++)
                {
                    data2D[i][j] = data[Extent.Y * i + j];
                }
            }

            return data2D;
        }

        /// <summary>
        /// Copes data from CPU memory to the associated accelerator.
        /// </summary>
        public void CopyToAccelerator() =>
            CopyToAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from CPU memory to the associated accelerator.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyToAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Buffer.CopyFromView(stream, CPUView.AsLinearView(), 0);
        }

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        public void CopyFromAccelerator() =>
            CopyFromAccelerator(Accelerator.DefaultStream);

        /// <summary>
        /// Copies data from the associated accelerator into CPU memory.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        public void CopyFromAccelerator(AcceleratorStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Buffer.CopyTo(stream, CPUView, Index2.Zero);
        }

        /// <summary>
        /// Returns the underlying generic memory buffer.
        /// </summary>
        /// <returns>The underlying generic memory buffer.</returns>
        public MemoryBuffer<T, Index2> ToMemoryBuffer() => Buffer;

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        /// <returns>An array view that can access this array.</returns>
        public ArrayView2D<T> ToArrayView() => View;

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        public static implicit operator ArrayView<T, Index2>(ExchangeBuffer2D<T> buffer)
        {
            Debug.Assert(buffer != null, "Invalid buffer");
            return buffer.View;
        }

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        public static implicit operator MemoryBuffer<T, Index2>(ExchangeBuffer2D<T> buffer)
        {
            Debug.Assert(buffer != null, "Invalid buffer");
            return buffer.Buffer;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Buffer.Dispose();
                cpuMemory.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
