using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime
{
    /// <summary>
    /// A static helper class for all exchange buffer implementations.
    /// </summary>
    public static class ExchangeBuffer
    {                            
        /// <summary>
        /// Allocates a new 1D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>The allocated 1D exchange buffer.</returns>
        /// <remarks>
        /// This function uses the default buffer allocation mode.
        /// <see cref="ExchangeBufferMode.PreferPagedLockedMemory"/>
        /// </remarks>
        public static ExchangeBuffer<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            Index1 extent)
            where T : unmanaged => accelerator.AllocateExchangeBuffer<T>(
                extent,
                ExchangeBufferMode.PreferPagedLockedMemory);

        /// <summary>
        /// Allocates a new 1D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        /// <returns>The allocated 1D exchange buffer.</returns>
        public static ExchangeBuffer<T> AllocateExchangeBuffer<T> (
            this Accelerator accelerator,
            Index1 extent,
            ExchangeBufferMode mode)
            where T : unmanaged
        {
            var gpuBuffer = accelerator.Allocate<T>(extent);
            return new ExchangeBuffer<T>(gpuBuffer, mode);
        }
                                
        /// <summary>
        /// Allocates a new 2D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>The allocated 2D exchange buffer.</returns>
        /// <remarks>
        /// This function uses the default buffer allocation mode.
        /// <see cref="ExchangeBufferMode.PreferPagedLockedMemory"/>
        /// </remarks>
        public static ExchangeBuffer2D<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            Index2 extent)
            where T : unmanaged => accelerator.AllocateExchangeBuffer<T>(
                extent,
                ExchangeBufferMode.PreferPagedLockedMemory);

        /// <summary>
        /// Allocates a new 2D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        /// <returns>The allocated 2D exchange buffer.</returns>
        public static ExchangeBuffer2D<T> AllocateExchangeBuffer<T> (
            this Accelerator accelerator,
            Index2 extent,
            ExchangeBufferMode mode)
            where T : unmanaged
        {
            var gpuBuffer = accelerator.Allocate<T>(extent);
            return new ExchangeBuffer2D<T>(gpuBuffer, mode);
        }
                                
        /// <summary>
        /// Allocates a new 3D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>The allocated 3D exchange buffer.</returns>
        /// <remarks>
        /// This function uses the default buffer allocation mode.
        /// <see cref="ExchangeBufferMode.PreferPagedLockedMemory"/>
        /// </remarks>
        public static ExchangeBuffer3D<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            Index3 extent)
            where T : unmanaged => accelerator.AllocateExchangeBuffer<T>(
                extent,
                ExchangeBufferMode.PreferPagedLockedMemory);

        /// <summary>
        /// Allocates a new 3D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        /// <returns>The allocated 3D exchange buffer.</returns>
        public static ExchangeBuffer3D<T> AllocateExchangeBuffer<T> (
            this Accelerator accelerator,
            Index3 extent,
            ExchangeBufferMode mode)
            where T : unmanaged
        {
            var gpuBuffer = accelerator.Allocate<T>(extent);
            return new ExchangeBuffer3D<T>(gpuBuffer, mode);
        }
    }
                                
    /// <summary>
    /// 1D implementation of <see cref="ExchangeBufferBase{T, TIndex}"/>
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed unsafe class ExchangeBuffer<T> : ExchangeBufferBase<T, Index1>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="buffer">The underlying memory buffer.</param>
        /// <param name="mode">The current buffer allocation mode.</param>
        internal ExchangeBuffer(MemoryBuffer<T, Index1> buffer,
            ExchangeBufferMode mode)
            : base(buffer, mode)
        {
            CPUView = new ArrayView<T>(CPUMemory, 0, buffer.Length);
            
            // Cache local data
            Buffer = buffer;
            NativePtr = buffer.NativePtr;
            View = buffer.View;
            Extent = buffer.Extent;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets this buffer as a higher level memory buffer.
        /// </summary>
        /// <returns> A <see cref="MemoryBuffer{T}"/> containing the data in this
        /// exchanage buffer.</returns>
        public MemoryBuffer<T> AsMemoryBuffer() => new MemoryBuffer<T>(Buffer);

        #endregion
    }
                            
    /// <summary>
    /// 2D implementation of <see cref="ExchangeBufferBase{T, TIndex}"/>
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed unsafe class ExchangeBuffer2D<T> : ExchangeBufferBase<T, Index2>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="buffer">The underlying memory buffer.</param>
        /// <param name="mode">The current buffer allocation mode.</param>
        internal ExchangeBuffer2D(MemoryBuffer<T, Index2> buffer,
            ExchangeBufferMode mode)
            : base(buffer, mode)
        {
            var baseView = new ArrayView<T>(CPUMemory, 0, buffer.Length);
            CPUView = new ArrayView<T, Index2>(baseView, buffer.Extent);
            
            // Cache local data
            Buffer = buffer;
            NativePtr = buffer.NativePtr;
            View = buffer.View;
            Extent = buffer.Extent;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets this buffer as a higher level memory buffer.
        /// </summary>
        /// <returns> A <see cref="MemoryBuffer2D{T}"/> containing the data in this
        /// exchanage buffer.</returns>
        public MemoryBuffer2D<T> AsMemoryBuffer2D() => new MemoryBuffer2D<T>(Buffer);

        /// <summary>
        /// Gets the part of this buffer on CPU memory as a 2D View
        /// using the current extent.
        /// </summary>
        /// <returns></returns>
        public ArrayView2D<T> As2DView() => As2DView(Extent);

        /// <summary>
        /// Gets this buffer as a 2D array from the accelerator using the
        /// default stream.
        /// </summary>
        /// <returns>The array containing all the elements in the buffer.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public T[,] GetAs2DArray() => GetAs2DArray(Accelerator.DefaultStream);

        /// <summary>
        /// Gets this buffer as a 2D array from the accelerator.
        /// </summary>
        /// <returns>The array containing all the elements in the buffer.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public T[,] GetAs2DArray(AcceleratorStream stream)
        {
            var buffer = new MemoryBuffer2D<T>(Buffer);
            var array = buffer.GetAs2DArray(stream);
            buffer.Dispose();
            return array;
        }

        #endregion
    }
                            
    /// <summary>
    /// 3D implementation of <see cref="ExchangeBufferBase{T, TIndex}"/>
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed unsafe class ExchangeBuffer3D<T> : ExchangeBufferBase<T, Index3>
        where T : unmanaged
    {
        #region Instance

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="buffer">The underlying memory buffer.</param>
        /// <param name="mode">The current buffer allocation mode.</param>
        internal ExchangeBuffer3D(MemoryBuffer<T, Index3> buffer,
            ExchangeBufferMode mode)
            : base(buffer, mode)
        {
            var baseView = new ArrayView<T>(CPUMemory, 0, buffer.Length);
            CPUView = new ArrayView<T, Index3>(baseView, buffer.Extent);
            
            // Cache local data
            Buffer = buffer;
            NativePtr = buffer.NativePtr;
            View = buffer.View;
            Extent = buffer.Extent;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets this buffer as a higher level memory buffer.
        /// </summary>
        /// <returns> A <see cref="MemoryBuffer3D{T}"/> containing the data in this
        /// exchanage buffer.</returns>
        public MemoryBuffer3D<T> AsMemoryBuffer3D() => new MemoryBuffer3D<T>(Buffer);

        /// <summary>
        /// Gets the part of this buffer on CPU memory as a 3D View
        /// using the current extent.
        /// </summary>
        /// <returns></returns>
        public ArrayView3D<T> As3DView() => As3DView(Extent);

        /// <summary>
        /// Gets this buffer as a 3D array from the accelerator using the
        /// default stream.
        /// </summary>
        /// <returns>The array containing all the elements in the buffer.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public T[,,] GetAs3DArray() => GetAs3DArray(Accelerator.DefaultStream);

        /// <summary>
        /// Gets this buffer as a 3D array from the accelerator.
        /// </summary>
        /// <returns>The array containing all the elements in the buffer.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public T[,,] GetAs3DArray(AcceleratorStream stream)
        {
            var buffer = new MemoryBuffer3D<T>(Buffer);
            var array = buffer.GetAs3DArray(stream);
            buffer.Dispose();
            return array;
        }

        #endregion
    }
}