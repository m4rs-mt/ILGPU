// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: NotSupported.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// Represents the not-supported Cuda-driver API.
    /// </summary>
    sealed class NotSupportedCudaAPI : CudaAPI
    {
        #region General Methods

        /// <summary cref="CudaAPI.InitAPI"/>
        protected override CudaError InitAPI()
        {
            return CudaError.CUDA_ERROR_NOT_INITIALIZED;
        }

        /// <summary cref="CudaAPI.GetDriverVersion(out int)"/>
        public override CudaError GetDriverVersion(out int driverVersion)
        {
            driverVersion = 0;
            return CudaError.CUDA_ERROR_NOT_INITIALIZED;
        }

        /// <summary cref="CudaAPI.GetErrorString(CudaError, out IntPtr)"/>
        internal override CudaError GetErrorString(CudaError error, out IntPtr pStr)
        {
            pStr = IntPtr.Zero;
            return CudaError.CUDA_ERROR_NOT_INITIALIZED;
        }

        #endregion

        #region Device Methods

        /// <summary cref="CudaAPI.GetDevice(out int, int)"/>
        public override CudaError GetDevice(out int device, int ordinal)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetDevice(out int, int)"/>
        public override CudaError GetDeviceCount(out int count)
        {
            count = 0;
            return CudaError.CUDA_SUCCESS;
        }

        /// <summary cref="CudaAPI.GetDeviceName(byte[], int, int)"/>
        protected override CudaError GetDeviceName(byte[] bytes, int length, int device)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetTotalDeviceMemory(out IntPtr, int)"/>
        public override CudaError GetTotalDeviceMemory(out IntPtr bytes, int device)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetDeviceAttribute(out int, DeviceAttribute, int)"/>
        internal override CudaError GetDeviceAttribute(
            out int value,
            DeviceAttribute attribute,
            int device)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetDeviceComputeCapability(out int, out int, int)"/>
        public override CudaError GetDeviceComputeCapability(
            out int major,
            out int minor,
            int device)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        #endregion

        #region Context Methods

        /// <summary cref="CudaAPI.CreateContext(out IntPtr, CudaAcceleratorFlags, int)"/>
        public override CudaError CreateContext(
            out IntPtr context,
            CudaAcceleratorFlags flags,
            int device)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.CreateContextD3D11(out IntPtr, out int, CudaAcceleratorFlags, IntPtr)"/>
        public override CudaError CreateContextD3D11(
            out IntPtr context,
            out int device,
            CudaAcceleratorFlags flags,
            IntPtr d3dDevice)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.DestroyContext(IntPtr)"/>
        public override CudaError DestroyContext(IntPtr context)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.SetCurrentContext(IntPtr)"/>
        public override CudaError SetCurrentContext(IntPtr context)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.SynchronizeContext"/>
        public override CudaError SynchronizeContext()
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetCacheConfig(out CudaCacheConfiguration)"/>
        public override CudaError GetCacheConfig(out CudaCacheConfiguration config)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.SetCacheConfig(CudaCacheConfiguration)"/>
        public override CudaError SetCacheConfig(CudaCacheConfiguration config)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetSharedMemoryConfig(out CudaSharedMemoryConfiguration)"/>
        public override CudaError GetSharedMemoryConfig(out CudaSharedMemoryConfiguration config)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.SetSharedMemoryConfig(CudaSharedMemoryConfiguration)"/>
        public override CudaError SetSharedMemoryConfig(CudaSharedMemoryConfiguration config)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.CanAccessPeer(out int, int, int)"/>
        public override CudaError CanAccessPeer(
            out int canAccess,
            int device,
            int peerDevice)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.EnablePeerAccess(IntPtr, int)"/>
        public override CudaError EnablePeerAccess(IntPtr peerContext, int flags)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.DisablePeerAccess(IntPtr)"/>
        public override CudaError DisablePeerAccess(IntPtr peerContext)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetPeerAttribute(out int, Peer2PeerAttribute, int, int)"/>
        internal override CudaError GetPeerAttribute(
            out int value,
            Peer2PeerAttribute attribute,
            int sourceDevice,
            int destinationDevice)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        #endregion

        #region Memory Methods

        /// <summary cref="CudaAPI.GetMemoryInfo(out IntPtr, out IntPtr)"/>
        public override CudaError GetMemoryInfo(out IntPtr free, out IntPtr total)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.AllocateMemory(out IntPtr, IntPtr)"/>
        public override CudaError AllocateMemory(out IntPtr devicePtr, IntPtr bytesize)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.FreeMemory(IntPtr)"/>
        public override CudaError FreeMemory(IntPtr devicePtr)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.Memset(IntPtr, byte, IntPtr)"/>
        public override CudaError Memcpy(
            IntPtr destination,
            IntPtr source,
            IntPtr length)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.MemcpyHostToDevice(IntPtr, IntPtr, IntPtr, AcceleratorStream)"/>
        public override CudaError MemcpyHostToDevice(
            IntPtr destinationDevice,
            IntPtr sourceHost,
            IntPtr length,
            IntPtr stream)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.MemcpyDeviceToHost(IntPtr, IntPtr, IntPtr, AcceleratorStream)"/>
        public override CudaError MemcpyDeviceToHost(
            IntPtr destinationHost,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.MemcpyDeviceToDevice(IntPtr, IntPtr, IntPtr, AcceleratorStream)"/>
        public override CudaError MemcpyDeviceToDevice(
            IntPtr destinationDevice,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.Memset(IntPtr, byte, IntPtr)"/>
        public override CudaError Memset(
            IntPtr destinationDevice,
            byte value,
            IntPtr length)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetPointerAttribute(IntPtr, PointerAttribute, IntPtr)"/>
        internal override CudaError GetPointerAttribute(
            IntPtr targetPtr,
            PointerAttribute attribute,
            IntPtr devicePtr)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        #endregion

        #region Stream Methods

        /// <summary cref="CudaAPI.CreateStream(out IntPtr, StreamFlags)"/>
        internal override CudaError CreateStream(out IntPtr stream, StreamFlags flags)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.CreateStreamWithPriority(out IntPtr, StreamFlags, int)"/>
        internal override CudaError CreateStreamWithPriority(
            out IntPtr stream,
            StreamFlags flags,
            int priority)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.DestroyStream(IntPtr)"/>
        public override CudaError DestroyStream(IntPtr stream)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        public override CudaError SynchronizeStream(IntPtr stream)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        #endregion

        #region Kernel Methods

        /// <summary cref="CudaAPI.LoadModule(out IntPtr, string)"/>
        public override CudaError LoadModule(out IntPtr module, string moduleData)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.LoadModule(out IntPtr, string, int, IntPtr, IntPtr)"/>
        public override CudaError LoadModule(
            out IntPtr kernelModule,
            string moduleData,
            int numOptions,
            IntPtr jitOptions,
            IntPtr jitOptionValues)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.DestroyModule(IntPtr)"/>
        public override CudaError DestroyModule(IntPtr module)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.GetModuleFunction(out IntPtr, IntPtr, string)"/>
        public override CudaError GetModuleFunction(
            out IntPtr function,
            IntPtr module,
            string functionName)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.LaunchKernel(IntPtr, int, int, int, int, int, int, int, IntPtr, IntPtr, IntPtr)"/>
        public override CudaError LaunchKernel(
            IntPtr function,
            int gridDimX,
            int gridDimY,
            int gridDimZ,
            int blockDimX,
            int blockDimY,
            int blockDimZ,
            int sharedMemSizeInBytes,
            IntPtr stream,
            IntPtr args,
            IntPtr kernelArgs)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.ComputeOccupancyMaxActiveBlocksPerMultiprocessor(out int, IntPtr, int, IntPtr)"/>
        public override CudaError ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            IntPtr dynamicSMemSize)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        /// <summary cref="CudaAPI.ComputeOccupancyMaxPotentialBlockSize(out int, out int, IntPtr, ComputeDynamicMemorySizeForBlockSize, IntPtr, int)"/>
        public override CudaError ComputeOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            ComputeDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            IntPtr dynamicSMemSize,
            int blockSizeLimit)
        {
            throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
        }

        #endregion
    }
}
