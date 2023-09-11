// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityGroupExecutionContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents an execution context for a single thread group.
    /// </summary>
    sealed class VelocityGroupExecutionContext : DisposeBase
    {
        private readonly VelocityMemoryBufferPool sharedMemoryPool;
        private readonly VelocityMemoryBufferPool localMemoryPool;
        private readonly int warpSize;

        /// <summary>
        /// Constructs a new execution context.
        /// </summary>
        /// <param name="accelerator">The parent velocity accelerator.</param>
        public VelocityGroupExecutionContext(VelocityAccelerator accelerator)
        {
            sharedMemoryPool = new VelocityMemoryBufferPool(
                accelerator,
                accelerator.MaxSharedMemoryPerGroup);
            localMemoryPool = new VelocityMemoryBufferPool(
                accelerator,
                accelerator.MaxSharedMemoryPerGroup);
            warpSize = accelerator.WarpSize;
        }

        /// <summary>
        /// Returns the current grid index.
        /// </summary>
        public int GridIdx { get; private set; }

        /// <summary>
        /// Returns the current group dimension.
        /// </summary>
        public int GroupDim { get; private set; }

        /// <summary>
        /// Returns the current grid dimension.
        /// </summary>
        public int GridDim { get; private set; }

        /// <summary>
        /// Returns the user-specific total size.
        /// </summary>
        public int UserSize { get; private set; }

        /// <summary>
        /// Returns a view to dynamic shared memory (if any).
        /// </summary>
        public ArrayView<byte> DynamicSharedMemory { get; private set; }

        /// <summary>
        /// Returns the linear group .
        /// </summary>
        public int GroupOffset => GridIdx * GroupDim;

        /// <summary>
        /// Resets this execution context.
        /// </summary>
        private void Reset()
        {
            sharedMemoryPool.Reset();
            localMemoryPool.Reset();
        }

        /// <summary>
        /// Sets up the current thread grid information for the current thread group.
        /// </summary>
        public void SetupThreadGrid(
            int gridIdx,
            int groupDim,
            int gridDim,
            int userSize,
            int dynamicSharedMemoryLength)
        {
            GridIdx = gridIdx;
            GridDim = gridDim;
            GroupDim = groupDim;
            UserSize = userSize;

            // Reset everything
            Reset();

            // Allocate dynamic shared memory
            if (dynamicSharedMemoryLength > 0)
            {
                DynamicSharedMemory =
                    GetSharedMemoryFromPool<byte>(dynamicSharedMemoryLength);
            }
        }

        /// <summary>
        /// Gets a chunk of shared memory of a certain type.
        /// </summary>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The element type to allocate.</typeparam>
        /// <returns>A view pointing to the right chunk of shared memory.</returns>
        public ArrayView<T> GetSharedMemoryFromPool<T>(int length)
            where T : unmanaged =>
            sharedMemoryPool.Allocate<T>(length);

        /// <summary>
        /// Gets a chunk of local memory of a certain type.
        /// </summary>
        /// <param name="lengthInBytesPerThread">
        /// The number of bytes to allocate per thread.
        /// </param>
        /// <returns>A view pointing to the right chunk of local memory.</returns>
        public ArrayView<byte> GetLocalMemoryFromPool(int lengthInBytesPerThread) =>
            localMemoryPool.Allocate<byte>(lengthInBytesPerThread * warpSize);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                sharedMemoryPool.Dispose();
                localMemoryPool.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
