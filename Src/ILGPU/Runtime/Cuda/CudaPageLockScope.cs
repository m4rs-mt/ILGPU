// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaPageLockScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Cuda;
using System;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a CUDA page lock scope.
    /// </summary>
    public sealed class CudaPageLockScope<T> : PageLockScope<T>
        where T : unmanaged
    {
        /// <summary>
        /// Constructs a page lock scope for the accelerator.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="hostPtr">The host buffer pointer to page lock.</param>
        /// <param name="numElements">The number of elements in the buffer.</param>
        internal CudaPageLockScope(
            CudaAccelerator accelerator,
            IntPtr hostPtr,
            long numElements)
            : base(accelerator)
        {
            var flags = MemHostRegisterFlags.CU_MEMHOSTREGISTER_PORTABLE;
            if (!accelerator.Device.SupportsMappingHostMemory)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedPageLock);
            }

            AddrOfLockedObject = hostPtr;
            Length = numElements;

            CudaException.ThrowIfFailed(
                CurrentAPI.MemHostRegister(
                    hostPtr,
                    new IntPtr(LengthInBytes),
                    flags));
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing) =>
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.MemHostUnregister(AddrOfLockedObject));

        /// <inheritdoc/>
        public override IntPtr AddrOfLockedObject { get; }

        /// <inheritdoc/>
        public override long Length { get; }
    }
}
