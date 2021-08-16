// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: CudaProgress.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;

namespace MonitorProgress
{
    /// <summary>
    /// Helper class to represent a single value that can be updated by the GPU to
    /// indicate progress of the kernel, and also read by the CPU to determine progress.
    /// </summary>
    class CudaProgress<T> : AcceleratorObject
        where T : unmanaged
    {
        #region Nested Types

        /// <summary>
        /// Constructs a Cuda buffer using host memory.
        /// </summary>
        class CudaProgressMemoryBuffer : MemoryBuffer
        {
            public CudaProgressMemoryBuffer(
                CudaAccelerator accelerator,
                long length,
                int elementSize)
                : base(accelerator, length, elementSize)
            {
                CudaException.ThrowIfFailed(
                   CudaAPI.CurrentAPI.AllocateHostMemory(
                       out IntPtr resultPtr,
                       new IntPtr(LengthInBytes)));
                NativePtr = resultPtr;
            }

            protected override void DisposeAcceleratorObject(bool disposing)
            {
                CudaException.ThrowIfFailed(
                    CudaAPI.CurrentAPI.FreeHostMemory(NativePtr));
                NativePtr = IntPtr.Zero;
            }

            public override void CopyFrom(
                AcceleratorStream stream,
                in ArrayView<byte> sourceView,
                long targetOffsetInBytes) =>
                throw new NotSupportedException();

            public override void CopyTo(
                AcceleratorStream stream,
                long sourceOffsetInBytes,
                in ArrayView<byte> targetView) =>
                throw new NotSupportedException();

            public override void MemSet(
                AcceleratorStream stream,
                byte value,
                long targetOffsetInBytes,
                long length) =>
                throw new NotSupportedException();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns an array view that can be used to update the progress on the GPU.
        /// </summary>
        public readonly ArrayView<T> View;

        /// <summary>
        /// Holds the Cuda memory buffer that contains the progress value.
        /// </summary>
        private readonly CudaProgressMemoryBuffer memoryBuffer;

        /// <summary>
        /// Gets or sets the current progress value from the CPU.
        /// </summary>
        public unsafe T Value
        {
            [NotInsideKernel]
            get
            {
                var ptr = (T*)memoryBuffer.NativePtr.ToPointer();
                return *ptr;
            }
            [NotInsideKernel]
            set
            {
                var ptr = (T*)memoryBuffer.NativePtr.ToPointer();
                *ptr = value;
            }
        }

        #endregion

        #region Instance

        public CudaProgress(CudaAccelerator accelerator)
            : base(accelerator)
        {
            memoryBuffer = new CudaProgressMemoryBuffer(accelerator, 1, Interop.SizeOf<T>());
            View = new ArrayView<T>(memoryBuffer, 0, memoryBuffer.Length);
            Value = default;
        }

        protected override void DisposeAcceleratorObject(bool disposing) =>
            memoryBuffer.Dispose();

        #endregion
    }
}
