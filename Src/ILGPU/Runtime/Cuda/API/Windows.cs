// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Windows.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

#pragma warning disable CA1060
#pragma warning disable IDE1006 // Naming

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// Represents the Cuda-driver API for Windows.
    /// </summary>
    sealed class CudaAPIWindows : CudaAPI
    {
        #region Constants

        /// <summary>
        /// Represents the driver library name.
        /// </summary>
        public const string LibName = "nvcuda";

        #endregion

        #region Imports

        [DllImport(LibName)]
        private static extern CudaError cuInit([In] int flags);

        [DllImport(LibName)]
        private static extern CudaError cuDriverGetVersion([Out] out int driverVersion);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceGet(
            [Out] out int device,
            [In] int ordinal);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceGetCount([Out] out int count);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceGetName(
            [In, Out] byte[] name,
            [In] int length,
            [In] int device);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceTotalMem_v2(
            [Out] out IntPtr bytes,
            [In] int device);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceGetAttribute(
            [Out] out int value,
            [In] DeviceAttribute attribute,
            [In] int device);

        [DllImport(LibName)]
        private static extern CudaError cuCtxCreate_v2(
            [Out] out IntPtr context,
            [In] CudaAcceleratorFlags flags,
            [In] int device);

        [DllImport(LibName)]
        private static extern CudaError cuD3D11CtxCreate_v2(
            [Out] out IntPtr context,
            [Out] out int device,
            [In] CudaAcceleratorFlags flags,
            [In] IntPtr d3dDevice);

        [DllImport(LibName)]
        private static extern CudaError cuCtxDestroy_v2(
            [In] IntPtr context);

        [DllImport(LibName)]
        private static extern CudaError cuCtxSetCurrent(
            [In] IntPtr context);

        [DllImport(LibName)]
        private static extern CudaError cuCtxSynchronize();

        [DllImport(LibName)]
        private static extern CudaError cuCtxGetCacheConfig(
            [Out] out CudaCacheConfiguration pconfig);

        [DllImport(LibName)]
        private static extern CudaError cuCtxSetCacheConfig(
            [In] CudaCacheConfiguration config);

        [DllImport(LibName)]
        private static extern CudaError cuCtxGetSharedMemConfig(
            [Out] out CudaSharedMemoryConfiguration pConfig);

        [DllImport(LibName)]
        private static extern CudaError cuCtxSetSharedMemConfig(
            [In] CudaSharedMemoryConfiguration config);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceCanAccessPeer(
            [Out] out int canAccess,
            [In] int device,
            [In] int peerDev);

        [DllImport(LibName)]
        private static extern CudaError cuCtxEnablePeerAccess(
            [In] IntPtr peerContext,
            [In] int flags);

        [DllImport(LibName)]
        private static extern CudaError cuCtxDisablePeerAccess(
            IntPtr peerContext);

        [DllImport(LibName)]
        private static extern CudaError cuDeviceGetP2PAttribute(
            [Out] out int value,
            [In] Peer2PeerAttribute attrib,
            [In] int sourceDevice,
            [In] int destinationDevice);

        [DllImport(LibName)]
        private static extern CudaError cuPointerGetAttribute(
            [In] IntPtr targetPtr,
            [In] PointerAttribute attribute,
            [In] IntPtr devicePtr);

        [DllImport(LibName)]
        private static extern CudaError cuMemGetInfo_v2(
            [Out] out IntPtr free,
            [Out] out IntPtr total);

        [DllImport(LibName)]
        private static extern CudaError cuMemAlloc_v2(
            [Out] out IntPtr devicePtr,
            [In] IntPtr bytesize);

        [DllImport(LibName)]
        private static extern CudaError cuMemFree_v2(
            [In] IntPtr devicePtr);

        [DllImport(LibName)]
        private static extern CudaError cuMemcpy(
            [In] IntPtr destination,
            [In] IntPtr source,
            [In] IntPtr length);

        [DllImport(LibName)]
        private static extern CudaError cuMemcpyHtoDAsync_v2(
            [In] IntPtr destinationDevice,
            [In] IntPtr sourceHost,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DllImport(LibName)]
        private static extern CudaError cuMemcpyDtoHAsync_v2(
            [In] IntPtr destinationHost,
            [In] IntPtr sourceDevice,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DllImport(LibName)]
        private static extern CudaError cuMemcpyDtoDAsync_v2(
            [In] IntPtr destinationDevice,
            [In] IntPtr sourceDevice,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DllImport(LibName)]
        private static extern CudaError cuMemsetD8Async(
            [In] IntPtr destinationDevice,
            [In] byte value,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DllImport(LibName)]
        private static extern CudaError cuStreamCreate(
            [Out] out IntPtr stream,
            [In] StreamFlags flags);

        [DllImport(LibName)]
        private static extern CudaError cuStreamCreateWithPriority(
            [Out] out IntPtr stream,
            [In] StreamFlags flags,
            [In] int priority);

        [DllImport(LibName)]
        private static extern CudaError cuStreamDestroy_v2(
            [In] IntPtr stream);

        [DllImport(LibName)]
        private static extern CudaError cuStreamSynchronize(
            [In] IntPtr stream);

        [DllImport(LibName)]
        private static extern CudaError cuGetErrorString(
            [In] CudaError error,
            [Out] out IntPtr pStr);

        [DllImport(LibName, BestFitMapping = false, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true)]
        private static extern CudaError cuModuleLoadData(
            [Out] out IntPtr module,
            [In, MarshalAs(UnmanagedType.LPStr)] string moduleData);

        [DllImport(LibName, BestFitMapping = false, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true)]
        private static extern CudaError cuModuleLoadDataEx(
            [Out] out IntPtr module,
            [In, MarshalAs(UnmanagedType.LPStr)] string moduleData,
            [In] int numOptions,
            [In] IntPtr jitOptions,
            [In] IntPtr jitOptionValues);

        [DllImport(LibName)]
        private static extern CudaError cuModuleUnload(IntPtr module);

        [DllImport(LibName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern CudaError cuModuleGetFunction(
            [Out] out IntPtr function,
            [In] IntPtr module,
            [In] string functionName);

        [DllImport(LibName)]
        private static extern CudaError cuLaunchKernel(
            [In] IntPtr function,
            [In] int gridDimX,
            [In] int gridDimY,
            [In] int gridDimZ,
            [In] int blockDimX,
            [In] int blockDimY,
            [In] int blockDimZ,
            [In] int sharedMemSizeInBytes,
            [In] IntPtr stream,
            [In] IntPtr args,
            [In] IntPtr kernelArgs);

        [DllImport(LibName)]
        private static extern CudaError cuOccupancyMaxActiveBlocksPerMultiprocessor(
            [Out] out int numBlocks,
            [In] IntPtr func,
            [In] int blockSize,
            [In] IntPtr dynamicSMemSize);

        [DllImport(LibName)]
        public static extern CudaError cuOccupancyMaxPotentialBlockSize(
            [Out] out int minGridSize,
            [Out] out int blockSize,
            [In] IntPtr func,
            [In] [MarshalAs(UnmanagedType.FunctionPtr)] ComputeDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            [In] IntPtr dynamicSMemSize,
            [In] int blockSizeLimit);

        #endregion

        #region Instance

        /// <summary>
        /// Intializes a new driver API for Windows.
        /// </summary>
        public CudaAPIWindows() { }

        #endregion

        #region General Methods

        /// <summary cref="CudaAPI.InitAPI"/>
        protected override CudaError InitAPI()
        {
            return cuInit(0);
        }

        /// <summary cref="CudaAPI.GetDriverVersion(out CudaDriverVersion)"/>
        public override CudaError GetDriverVersion(out CudaDriverVersion driverVersion)
        {
            var error = cuDriverGetVersion(out var driverVersionValue);
            if (error != CudaError.CUDA_SUCCESS)
            {
                driverVersion = default;
                return error;
            }
            driverVersion = CudaDriverVersion.FromValue(driverVersionValue);
            return CudaError.CUDA_SUCCESS;
        }

        /// <summary cref="CudaAPI.GetErrorString(CudaError, out IntPtr)"/>
        internal override CudaError GetErrorString(CudaError error, out IntPtr pStr)
        {
            return cuGetErrorString(error, out pStr);
        }

        #endregion

        #region Device Methods

        /// <summary cref="CudaAPI.GetDevice(out int, int)"/>
        public override CudaError GetDevice(out int device, int ordinal)
        {
            return cuDeviceGet(out device, ordinal);
        }

        /// <summary cref="CudaAPI.GetDevice(out int, int)"/>
        public override CudaError GetDeviceCount(out int count)
        {
            return cuDeviceGetCount(out count);
        }

        /// <summary cref="CudaAPI.GetDeviceName(byte[], int, int)"/>
        protected override CudaError GetDeviceName(byte[] bytes, int length, int device)
        {
            return cuDeviceGetName(bytes, length, device);
        }

        /// <summary cref="CudaAPI.GetTotalDeviceMemory(out IntPtr, int)"/>
        public override CudaError GetTotalDeviceMemory(out IntPtr bytes, int device)
        {
            return cuDeviceTotalMem_v2(out bytes, device);
        }

        /// <summary cref="CudaAPI.GetDeviceAttribute(out int, DeviceAttribute, int)"/>
        internal override CudaError GetDeviceAttribute(
            out int value,
            DeviceAttribute attribute,
            int device)
        {
            return cuDeviceGetAttribute(out value, attribute, device);
        }

        /// <summary cref="CudaAPI.GetDeviceComputeCapability(out int, out int, int)"/>
        public override CudaError GetDeviceComputeCapability(
            out int major,
            out int minor,
            int device)
        {
            var error = cuDeviceGetAttribute(out major, DeviceAttribute.CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR, device);
            if (error != CudaError.CUDA_SUCCESS)
            {
                minor = default;
                return error;
            }
            return cuDeviceGetAttribute(out minor, DeviceAttribute.CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR, device);
        }

        #endregion

        #region Context Methods

        /// <summary cref="CudaAPI.CreateContext(out IntPtr, CudaAcceleratorFlags, int)"/>
        public override CudaError CreateContext(
            out IntPtr context,
            CudaAcceleratorFlags flags,
            int device)
        {
            return cuCtxCreate_v2(out context, flags, device);
        }

        /// <summary cref="CudaAPI.CreateContextD3D11(out IntPtr, out int, CudaAcceleratorFlags, IntPtr)"/>
        public override CudaError CreateContextD3D11(
            out IntPtr context,
            out int device,
            CudaAcceleratorFlags flags,
            IntPtr d3dDevice)
        {
            return cuD3D11CtxCreate_v2(
                out context,
                out device,
                flags,
                d3dDevice);
        }

        /// <summary cref="CudaAPI.DestroyContext(IntPtr)"/>
        public override CudaError DestroyContext(IntPtr context)
        {
            return cuCtxDestroy_v2(context);
        }

        /// <summary cref="CudaAPI.SetCurrentContext(IntPtr)"/>
        public override CudaError SetCurrentContext(IntPtr context)
        {
            return cuCtxSetCurrent(context);
        }

        /// <summary cref="CudaAPI.SynchronizeContext"/>
        public override CudaError SynchronizeContext()
        {
            return cuCtxSynchronize();
        }

        /// <summary cref="CudaAPI.GetCacheConfig(out CudaCacheConfiguration)"/>
        public override CudaError GetCacheConfig(out CudaCacheConfiguration config)
        {
            return cuCtxGetCacheConfig(out config);
        }

        /// <summary cref="CudaAPI.SetCacheConfig(CudaCacheConfiguration)"/>
        public override CudaError SetCacheConfig(CudaCacheConfiguration config)
        {
            return cuCtxSetCacheConfig(config);
        }

        /// <summary cref="CudaAPI.GetSharedMemoryConfig(out CudaSharedMemoryConfiguration)"/>
        public override CudaError GetSharedMemoryConfig(out CudaSharedMemoryConfiguration config)
        {
            return cuCtxGetSharedMemConfig(out config);
        }

        /// <summary cref="CudaAPI.SetSharedMemoryConfig(CudaSharedMemoryConfiguration)"/>
        public override CudaError SetSharedMemoryConfig(CudaSharedMemoryConfiguration config)
        {
            return cuCtxSetSharedMemConfig(config);
        }

        /// <summary cref="CudaAPI.CanAccessPeer(out int, int, int)"/>
        public override CudaError CanAccessPeer(
            out int canAccess,
            int device,
            int peerDevice)
        {
            return cuDeviceCanAccessPeer(out canAccess, device, peerDevice);
        }

        /// <summary cref="CudaAPI.EnablePeerAccess(IntPtr, int)"/>
        public override CudaError EnablePeerAccess(IntPtr peerContext, int flags)
        {
            return cuCtxEnablePeerAccess(peerContext, flags);
        }

        /// <summary cref="CudaAPI.DisablePeerAccess(IntPtr)"/>
        public override CudaError DisablePeerAccess(IntPtr peerContext)
        {
            return cuCtxDisablePeerAccess(peerContext);
        }

        /// <summary cref="CudaAPI.GetPeerAttribute(out int, Peer2PeerAttribute, int, int)"/>
        internal override CudaError GetPeerAttribute(
            out int value,
            Peer2PeerAttribute attribute,
            int sourceDevice,
            int destinationDevice)
        {
            return cuDeviceGetP2PAttribute(
                out value,
                attribute,
                sourceDevice,
                destinationDevice);
        }

        #endregion

        #region Memory Methods

        /// <summary cref="CudaAPI.GetMemoryInfo(out IntPtr, out IntPtr)"/>
        public override CudaError GetMemoryInfo(out IntPtr free, out IntPtr total)
        {
            return cuMemGetInfo_v2(out free, out total);
        }

        /// <summary cref="CudaAPI.AllocateMemory(out IntPtr, IntPtr)"/>
        public override CudaError AllocateMemory(out IntPtr devicePtr, IntPtr bytesize)
        {
            return cuMemAlloc_v2(out devicePtr, bytesize);
        }

        /// <summary cref="CudaAPI.FreeMemory(IntPtr)"/>
        public override CudaError FreeMemory(IntPtr devicePtr)
        {
            return cuMemFree_v2(devicePtr);
        }

        /// <summary cref="CudaAPI.Memset(IntPtr, byte, IntPtr, IntPtr)"/>
        public override CudaError Memcpy(
            IntPtr destination,
            IntPtr source,
            IntPtr length)
        {
            return cuMemcpy(destination, source, length);
        }

        /// <summary cref="CudaAPI.MemcpyHostToDevice(IntPtr, IntPtr, IntPtr, AcceleratorStream)"/>
        public override CudaError MemcpyHostToDevice(
            IntPtr destinationDevice,
            IntPtr sourceHost,
            IntPtr length,
            IntPtr stream)
        {
            return cuMemcpyHtoDAsync_v2(
                destinationDevice,
                sourceHost,
                length,
                stream);
        }

        /// <summary cref="CudaAPI.MemcpyDeviceToHost(IntPtr, IntPtr, IntPtr, AcceleratorStream)"/>
        public override CudaError MemcpyDeviceToHost(
            IntPtr destinationHost,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream)
        {
            return cuMemcpyDtoHAsync_v2(
                destinationHost,
                sourceDevice,
                length,
                stream);
        }

        /// <summary cref="CudaAPI.MemcpyDeviceToDevice(IntPtr, IntPtr, IntPtr, AcceleratorStream)"/>
        public override CudaError MemcpyDeviceToDevice(
            IntPtr destinationDevice,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream)
        {
            return cuMemcpyDtoDAsync_v2(
                destinationDevice,
                sourceDevice,
                length,
                stream);
        }

        /// <summary cref="CudaAPI.Memset(IntPtr, byte, IntPtr, IntPtr)"/>
        public override CudaError Memset(
            IntPtr destinationDevice,
            byte value,
            IntPtr length,
            IntPtr stream)
        {
            return cuMemsetD8Async(destinationDevice, value, length, stream);
        }

        /// <summary cref="CudaAPI.GetPointerAttribute(IntPtr, PointerAttribute, IntPtr)"/>
        internal override CudaError GetPointerAttribute(
            IntPtr targetPtr,
            PointerAttribute attribute,
            IntPtr devicePtr)
        {
            return cuPointerGetAttribute(
                targetPtr,
                attribute,
                devicePtr);
        }

        #endregion

        #region Stream Methods

        /// <summary cref="CudaAPI.CreateStream(out IntPtr, StreamFlags)"/>
        internal override CudaError CreateStream(out IntPtr stream, StreamFlags flags)
        {
            return cuStreamCreate(out stream, flags);
        }

        /// <summary cref="CudaAPI.CreateStreamWithPriority(out IntPtr, StreamFlags, int)"/>
        internal override CudaError CreateStreamWithPriority(
            out IntPtr stream,
            StreamFlags flags,
            int priority)
        {
            return cuStreamCreateWithPriority(out stream, flags, priority);
        }

        /// <summary cref="CudaAPI.DestroyStream(IntPtr)"/>
        public override CudaError DestroyStream(IntPtr stream)
        {
            return cuStreamDestroy_v2(stream);
        }

        public override CudaError SynchronizeStream(IntPtr stream)
        {
            return cuStreamSynchronize(stream);
        }

        #endregion

        #region Kernel Methods

        /// <summary cref="CudaAPI.LoadModule(out IntPtr, string)"/>
        public override CudaError LoadModule(out IntPtr module, string moduleData)
        {
            return cuModuleLoadData(out module, moduleData);
        }

        /// <summary cref="CudaAPI.LoadModule(out IntPtr, string, int, IntPtr, IntPtr)"/>
        public override CudaError LoadModule(
            out IntPtr kernelModule,
            string moduleData,
            int numOptions,
            IntPtr jitOptions,
            IntPtr jitOptionValues)
        {
            return cuModuleLoadDataEx(
                out kernelModule,
                moduleData,
                numOptions,
                jitOptions,
                jitOptionValues);
        }

        /// <summary cref="CudaAPI.DestroyModule(IntPtr)"/>
        public override CudaError DestroyModule(IntPtr module)
        {
            return cuModuleUnload(module);
        }

        /// <summary cref="CudaAPI.GetModuleFunction(out IntPtr, IntPtr, string)"/>
        public override CudaError GetModuleFunction(
            out IntPtr function,
            IntPtr module,
            string functionName)
        {
            return cuModuleGetFunction(out function, module, functionName);
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
            return cuLaunchKernel(
                function,
                gridDimX,
                gridDimY,
                gridDimZ,
                blockDimX,
                blockDimY,
                blockDimZ,
                sharedMemSizeInBytes,
                stream,
                args,
                kernelArgs);
        }

        /// <summary cref="CudaAPI.ComputeOccupancyMaxActiveBlocksPerMultiprocessor(out int, IntPtr, int, IntPtr)"/>
        public override CudaError ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            IntPtr dynamicSMemSize)
        {
            return cuOccupancyMaxActiveBlocksPerMultiprocessor(
                out numBlocks,
                func,
                blockSize,
                dynamicSMemSize);
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
            return cuOccupancyMaxPotentialBlockSize(
                out minGridSize,
                out blockSize,
                func,
                blockSizeToDynamicSMemSize,
                dynamicSMemSize,
                blockSizeLimit);
        }

        #endregion
    }
}

#pragma warning restore IDE1006 // Naming
#pragma warning restore CA1060
