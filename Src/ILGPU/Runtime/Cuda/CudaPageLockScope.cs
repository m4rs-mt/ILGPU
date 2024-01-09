// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaPageLockScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
            : base(
                  accelerator,
                  GetAddrOfLockedObject(accelerator, hostPtr, numElements),
                  numElements)
        {
            HostPtr = hostPtr;
        }

        /// <summary>
        /// Registers the page lock.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="hostPtr">The host buffer pointer to page lock.</param>
        /// <param name="numElements">The number of elements in the buffer.</param>
        /// <returns>The page locked address.</returns>
        private static IntPtr GetAddrOfLockedObject(
            CudaAccelerator accelerator,
            IntPtr hostPtr,
            long numElements)
        {
            if (!accelerator.Device.SupportsMappingHostMemory)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedPageLock);
            }

            bool supportsHostPointer = accelerator
                .Device
                .SupportsUsingHostPointerForRegisteredMemory;

            // Setup internal memory registration flags.
            var flags = MemHostRegisterFlags.CU_MEMHOSTREGISTER_PORTABLE;
            if (!supportsHostPointer)
                flags |= MemHostRegisterFlags.CU_MEMHOSTREGISTER_DEVICEMAP;

            // Perform the memory registration.
            var lengthInBytes = numElements * Interop.SizeOf<T>();
            CudaException.ThrowIfFailed(
                CurrentAPI.MemHostRegister(
                    hostPtr,
                    new IntPtr(lengthInBytes),
                    flags));

            // Check whether we have to determine the actual device pointer or are able
            // to reuse the host pointer for all operations.
            if (supportsHostPointer)
            {
                return hostPtr;
            }
            else
            {
                CudaException.ThrowIfFailed(
                    CurrentAPI.MemHostGetDevicePointer(
                        out IntPtr devicePtr,
                        hostPtr,
                        0));
                return devicePtr;
            }
        }

        /// <summary>
        /// The host pointer used for registration.
        /// </summary>
        private IntPtr HostPtr { get; }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.MemHostUnregister(HostPtr));
            base.DisposeAcceleratorObject(disposing);
        }
    }
}
