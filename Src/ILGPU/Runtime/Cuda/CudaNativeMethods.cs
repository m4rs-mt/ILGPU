// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaNativeMethods.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable IDE1006 // Naming Styles

namespace ILGPU.Runtime.Cuda
{
    #region Enums

    enum StreamFlags
    {
        CU_STREAM_DEFAULT = 0,
        CU_STREAM_NON_BLOCKING = 1
    }

    enum DeviceAttribute
    {
        CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK = 1,
        CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_X = 2,
        CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Y = 3,
        CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Z = 4,
        CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_X = 5,
        CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Y = 6,
        CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Z = 7,
        CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK = 8,
        CU_DEVICE_ATTRIBUTE_SHARED_MEMORY_PER_BLOCK = 8,
        CU_DEVICE_ATTRIBUTE_TOTAL_CONSTANT_MEMORY = 9,
        CU_DEVICE_ATTRIBUTE_WARP_SIZE = 10,
        CU_DEVICE_ATTRIBUTE_MAX_PITCH = 11,
        CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_BLOCK = 12,
        CU_DEVICE_ATTRIBUTE_REGISTERS_PER_BLOCK = 12,
        CU_DEVICE_ATTRIBUTE_CLOCK_RATE = 13,
        CU_DEVICE_ATTRIBUTE_TEXTURE_ALIGNMENT = 14,
        CU_DEVICE_ATTRIBUTE_GPU_OVERLAP = 15,
        CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT = 16,
        CU_DEVICE_ATTRIBUTE_KERNEL_EXEC_TIMEOUT = 17,
        CU_DEVICE_ATTRIBUTE_INTEGRATED = 18,
        CU_DEVICE_ATTRIBUTE_CAN_MAP_HOST_MEMORY = 19,
        CU_DEVICE_ATTRIBUTE_COMPUTE_MODE = 20,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE1D_WIDTH = 21,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_WIDTH = 22,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_HEIGHT = 23,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE3D_WIDTH = 24,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE3D_HEIGHT = 25,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE3D_DEPTH = 26,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_LAYERED_WIDTH = 27,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_LAYERED_HEIGHT = 28,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_LAYERED_LAYERS = 29,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_ARRAY_WIDTH = 27,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_ARRAY_HEIGHT = 28,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_ARRAY_NUMSLICES = 29,
        CU_DEVICE_ATTRIBUTE_SURFACE_ALIGNMENT = 30,
        CU_DEVICE_ATTRIBUTE_CONCURRENT_KERNELS = 31,
        CU_DEVICE_ATTRIBUTE_ECC_ENABLED = 32,
        CU_DEVICE_ATTRIBUTE_PCI_BUS_ID = 33,
        CU_DEVICE_ATTRIBUTE_PCI_DEVICE_ID = 34,
        CU_DEVICE_ATTRIBUTE_TCC_DRIVER = 35,
        CU_DEVICE_ATTRIBUTE_MEMORY_CLOCK_RATE = 36,
        CU_DEVICE_ATTRIBUTE_GLOBAL_MEMORY_BUS_WIDTH = 37,
        CU_DEVICE_ATTRIBUTE_L2_CACHE_SIZE = 38,
        CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR = 39,
        CU_DEVICE_ATTRIBUTE_ASYNC_ENGINE_COUNT = 40,
        CU_DEVICE_ATTRIBUTE_UNIFIED_ADDRESSING = 41,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE1D_LAYERED_WIDTH = 42,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE1D_LAYERED_LAYERS = 43,
        CU_DEVICE_ATTRIBUTE_CAN_TEX2D_GATHER = 44,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_GATHER_WIDTH = 45,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_GATHER_HEIGHT = 46,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE3D_WIDTH_ALTERNATE = 47,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE3D_HEIGHT_ALTERNATE = 48,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE3D_DEPTH_ALTERNATE = 49,
        CU_DEVICE_ATTRIBUTE_PCI_DOMAIN_ID = 50,
        CU_DEVICE_ATTRIBUTE_TEXTURE_PITCH_ALIGNMENT = 51,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURECUBEMAP_WIDTH = 52,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURECUBEMAP_LAYERED_WIDTH = 53,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURECUBEMAP_LAYERED_LAYERS = 54,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE1D_WIDTH = 55,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE2D_WIDTH = 56,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE2D_HEIGHT = 57,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE3D_WIDTH = 58,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE3D_HEIGHT = 59,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE3D_DEPTH = 60,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE1D_LAYERED_WIDTH = 61,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE1D_LAYERED_LAYERS = 62,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE2D_LAYERED_WIDTH = 63,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE2D_LAYERED_HEIGHT = 64,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACE2D_LAYERED_LAYERS = 65,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACECUBEMAP_WIDTH = 66,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACECUBEMAP_LAYERED_WIDTH = 67,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_SURFACECUBEMAP_LAYERED_LAYERS = 68,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE1D_LINEAR_WIDTH = 69,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_LINEAR_WIDTH = 70,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_LINEAR_HEIGHT = 71,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_LINEAR_PITCH = 72,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_MIPMAPPED_WIDTH = 73,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE2D_MIPMAPPED_HEIGHT = 74,
        CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR = 75,
        CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR = 76,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE1D_MIPMAPPED_WIDTH = 77,
        CU_DEVICE_ATTRIBUTE_STREAM_PRIORITIES_SUPPORTED = 78,
        CU_DEVICE_ATTRIBUTE_GLOBAL_L1_CACHE_SUPPORTED = 79,
        CU_DEVICE_ATTRIBUTE_LOCAL_L1_CACHE_SUPPORTED = 80,
        CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_MULTIPROCESSOR = 81,
        CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_MULTIPROCESSOR = 82,
        CU_DEVICE_ATTRIBUTE_MANAGED_MEMORY = 83,
        CU_DEVICE_ATTRIBUTE_MULTI_GPU_BOARD = 84,
        CU_DEVICE_ATTRIBUTE_MULTI_GPU_BOARD_GROUP_ID = 85,
        CU_DEVICE_ATTRIBUTE_HOST_NATIVE_ATOMIC_SUPPORTED = 86,
        CU_DEVICE_ATTRIBUTE_SINGLE_TO_DOUBLE_PRECISION_PERF_RATIO = 87,
        CU_DEVICE_ATTRIBUTE_PAGEABLE_MEMORY_ACCESS = 88,
        CU_DEVICE_ATTRIBUTE_CONCURRENT_MANAGED_ACCESS = 89,
        CU_DEVICE_ATTRIBUTE_COMPUTE_PREEMPTION_SUPPORTED = 90,
        CU_DEVICE_ATTRIBUTE_CAN_USE_HOST_POINTER_FOR_REGISTERED_MEM = 91,
        CU_DEVICE_ATTRIBUTE_MAX = 92
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public enum CudaError
    {
        CUDA_SUCCESS = 0,
        CUDA_ERROR_INVALID_VALUE = 1,
        CUDA_ERROR_OUT_OF_MEMORY = 2,
        CUDA_ERROR_NOT_INITIALIZED = 3,
        CUDA_ERROR_DEINITIALIZED = 4,
        CUDA_ERROR_PROFILER_DISABLED = 5,
        CUDA_ERROR_PROFILER_NOT_INITIALIZED = 6,
        CUDA_ERROR_PROFILER_ALREADY_STARTED = 7,
        CUDA_ERROR_PROFILER_ALREADY_STOPPED = 8,
        CUDA_ERROR_NO_DEVICE = 100,
        CUDA_ERROR_INVALID_DEVICE = 101,
        CUDA_ERROR_INVALID_IMAGE = 200,
        CUDA_ERROR_INVALID_CONTEXT = 201,
        CUDA_ERROR_CONTEXT_ALREADY_CURRENT = 202,
        CUDA_ERROR_MAP_FAILED = 205,
        CUDA_ERROR_UNMAP_FAILED = 206,
        CUDA_ERROR_ARRAY_IS_MAPPED = 207,
        CUDA_ERROR_ALREADY_MAPPED = 208,
        CUDA_ERROR_NO_BINARY_FOR_GPU = 209,
        CUDA_ERROR_ALREADY_ACQUIRED = 210,
        CUDA_ERROR_NOT_MAPPED = 211,
        CUDA_ERROR_NOT_MAPPED_AS_ARRAY = 212,
        CUDA_ERROR_NOT_MAPPED_AS_POINTER = 213,
        CUDA_ERROR_ECC_UNCORRECTABLE = 214,
        CUDA_ERROR_UNSUPPORTED_LIMIT = 215,
        CUDA_ERROR_CONTEXT_ALREADY_IN_USE = 216,
        CUDA_ERROR_PEER_ACCESS_UNSUPPORTED = 217,
        CUDA_ERROR_INVALID_PTX = 218,
        CUDA_ERROR_INVALID_GRAPHICS_CONTEXT = 219,
        CUDA_ERROR_NVLINK_UNCORRECTABLE = 220,
        CUDA_ERROR_INVALID_SOURCE = 300,
        CUDA_ERROR_FILE_NOT_FOUND = 301,
        CUDA_ERROR_SHARED_OBJECT_SYMBOL_NOT_FOUND = 302,
        CUDA_ERROR_SHARED_OBJECT_INIT_FAILED = 303,
        CUDA_ERROR_OPERATING_SYSTEM = 304,
        CUDA_ERROR_INVALID_HANDLE = 400,
        CUDA_ERROR_NOT_FOUND = 500,
        CUDA_ERROR_NOT_READY = 600,
        CUDA_ERROR_ILLEGAL_ADDRESS = 700,
        CUDA_ERROR_LAUNCH_OUT_OF_RESOURCES = 701,
        CUDA_ERROR_LAUNCH_TIMEOUT = 702,
        CUDA_ERROR_LAUNCH_INCOMPATIBLE_TEXTURING = 703,
        CUDA_ERROR_PEER_ACCESS_ALREADY_ENABLED = 704,
        CUDA_ERROR_PEER_ACCESS_NOT_ENABLED = 705,
        CUDA_ERROR_PRIMARY_CONTEXT_ACTIVE = 708,
        CUDA_ERROR_CONTEXT_IS_DESTROYED = 709,
        CUDA_ERROR_ASSERT = 710,
        CUDA_ERROR_TOO_MANY_PEERS = 711,
        CUDA_ERROR_HOST_MEMORY_ALREADY_REGISTERED = 712,
        CUDA_ERROR_HOST_MEMORY_NOT_REGISTERED = 713,
        CUDA_ERROR_HARDWARE_STACK_ERROR = 714,
        CUDA_ERROR_ILLEGAL_INSTRUCTION = 715,
        CUDA_ERROR_MISALIGNED_ADDRESS = 716,
        CUDA_ERROR_INVALID_ADDRESS_SPACE = 717,
        CUDA_ERROR_INVALID_PC = 718,
        CUDA_ERROR_LAUNCH_FAILED = 719,
        CUDA_ERROR_NOT_PERMITTED = 800,
        CUDA_ERROR_NOT_SUPPORTED = 801,
        CUDA_ERROR_UNKNOWN = 999
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    
    enum PointerAttribute
    {
        CU_POINTER_ATTRIBUTE_CONTEXT = 1,
        CU_POINTER_ATTRIBUTE_MEMORY_TYPE = 2,
        CU_POINTER_ATTRIBUTE_DEVICE_POINTER = 3,
        CU_POINTER_ATTRIBUTE_HOST_POINTER = 4,
        CU_POINTER_ATTRIBUTE_P2P_TOKENS = 5,
        CU_POINTER_ATTRIBUTE_SYNC_MEMOPS = 6,
        CU_POINTER_ATTRIBUTE_BUFFER_ID = 7,
        CU_POINTER_ATTRIBUTE_IS_MANAGED = 8
    }

    enum Peer2PeerAttribute
    {
        CU_DEVICE_P2P_ATTRIBUTE_PERFORMANCE_RANK = 1,
        CU_DEVICE_P2P_ATTRIBUTE_ACCESS_SUPPORTED = 2,
        CU_DEVICE_P2P_ATTRIBUTE_NATIVE_ATOMIC_SUPPORTED = 3
    }

    #endregion

    /// <summary>
    /// Wraps native Cuda methods from the driver API.
    /// </summary>
    static unsafe class CudaNativeMethods
    {
        internal const string CudaDriverLibName = "nvcuda";

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDriverGetVersion([Out] out int driverVersion);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceGet(
            [Out] out int device,
            [In] int ordinal);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceGetCount([Out] out int count);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceGetName(
            [In, Out] byte[] name,
            [In] int length,
            [In] int device);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceTotalMem_v2(
            [In,
            Out] ref IntPtr bytes, [In] int device);

        [DllImport(CudaDriverLibName)]
        private static extern CudaError cuDeviceGetAttribute(
            [Out] out int value,
            [In] DeviceAttribute attribute,
            [In] int device);

        public static int cuDeviceGetAttribute(DeviceAttribute attribute, int device)
        {
            CudaException.ThrowIfFailed(
                cuDeviceGetAttribute(out int value, attribute, device));
            return value;
        }

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceComputeCapability(
            [Out] out uint major,
            [Out] out uint minor,
            [In] int device);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxCreate_v2(
            [Out] out IntPtr ctx,
            [In] CudaAcceleratorFlags flags,
            [In] int device);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuD3D11CtxCreate_v2(
            [Out] out IntPtr ctx,
            [Out] out int deviceId,
            [In] CudaAcceleratorFlags flags,
            [In] IntPtr d3dDevice);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxDestroy_v2(
            [In] IntPtr ctx);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxSetCurrent(
            [In] IntPtr ctx);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxSynchronize();

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxGetCacheConfig(
            [Out] out CudaCacheConfiguration pconfig);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxSetCacheConfig(
            [In] CudaCacheConfiguration config);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxGetSharedMemConfig(
            [Out] out CudaSharedMemoryConfiguration pConfig);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxSetSharedMemConfig(
            [In] CudaSharedMemoryConfiguration config);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceCanAccessPeer(
            [Out] out int canAccessPeer,
            [In] int device,
            [In] int peerDev);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxEnablePeerAccess(
            [In] IntPtr peerContext,
            [In] uint flags);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuCtxDisablePeerAccess(
            IntPtr peerContext);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuDeviceGetP2PAttribute(
            [Out] out int value,
            [In] Peer2PeerAttribute attrib,
            [In] int srcDevice,
            [In] int dstDevice);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuPointerGetAttribute(
            [In] IntPtr targetPtr,
            [In] PointerAttribute attribute,
            [In] IntPtr devicePtr);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuMemGetInfo_v2(
            [Out] out IntPtr free,
            [Out] out IntPtr total);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuMemAlloc_v2(
            [Out] out IntPtr dptr,
            [In] IntPtr bytesize);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuMemFree_v2(
            [In] IntPtr dptr);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuMemcpy(
            [In] IntPtr dst,
            [In] IntPtr src,
            [In] IntPtr ByteCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CudaError cuMemcpyHtoD(
            IntPtr dstDevice,
            IntPtr srcHost,
            IntPtr byteCount,
            AcceleratorStream stream)
        {
            CudaStream cudaStream = stream as CudaStream;
            return cuMemcpyHtoDAsync_v2(
                dstDevice,
                srcHost,
                byteCount,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        [DllImport(CudaDriverLibName)]
        private static extern CudaError cuMemcpyHtoDAsync_v2(
            [In] IntPtr dstDevice,
            [In] IntPtr srcHost,
            [In] IntPtr ByteCount,
            [In] IntPtr stream);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CudaError cuMemcpyDtoH(
            IntPtr dstHost,
            IntPtr srcDevice,
            IntPtr byteCount,
            AcceleratorStream stream)
        {
            CudaStream cudaStream = stream as CudaStream;
            return cuMemcpyDtoHAsync_v2(
                dstHost,
                srcDevice,
                byteCount,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        [DllImport(CudaDriverLibName)]
        private static extern CudaError cuMemcpyDtoHAsync_v2(
            [In] IntPtr dstHost,
            [In] IntPtr srcDevice,
            [In] IntPtr ByteCount,
            [In] IntPtr stream);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CudaError cuMemcpyDtoD(
            IntPtr dstDevice,
            IntPtr srcDevice,
            IntPtr byteCount,
            AcceleratorStream stream)
        {
            CudaStream cudaStream = stream as CudaStream;
            return cuMemcpyDtoDAsync_v2(
                dstDevice,
                srcDevice,
                byteCount,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        [DllImport(CudaDriverLibName)]
        private static extern CudaError cuMemcpyDtoDAsync_v2(
            [In] IntPtr dstDevice,
            [In] IntPtr srcDevice,
            [In] IntPtr ByteCount,
            [In] IntPtr stream);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuMemsetD8_v2(
            [In] IntPtr dstDevice,
            [In] byte uc,
            [In] IntPtr N);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuStreamCreate(
            [Out] out IntPtr stream,
            [In] StreamFlags flags);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuStreamCreateWithPriority(
            [Out] out IntPtr stream,
            [In] StreamFlags flags,
            [In] int priority);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuStreamDestroy_v2(
            [In] IntPtr stream);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuStreamSynchronize(
            [In] IntPtr stream);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuInit(
            [In] uint Flags);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuGetErrorString(
            [In] CudaError error,
            [Out] out IntPtr pStr);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuModuleLoadData(
            [Out] out IntPtr module,
            [In] byte[] moduleData);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuModuleUnload(IntPtr module);

        [DllImport(CudaDriverLibName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern CudaError cuModuleGetFunction(
            [Out] out IntPtr function,
            [In] IntPtr module,
            [In] string functionName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CudaError cuLaunchKernel(
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
            Debug.Assert(gridDimX > 0, "Invalid X grid dimension");
            Debug.Assert(gridDimY > 0, "Invalid Y grid dimension");
            Debug.Assert(gridDimZ > 0, "Invalid Z grid dimension");

            Debug.Assert(blockDimX > 0, "Invalid X block dimension");
            Debug.Assert(blockDimY > 0, "Invalid Y block dimension");
            Debug.Assert(blockDimZ > 0, "Invalid Z block dimension");

            Debug.Assert(sharedMemSizeInBytes >= 0, "Invalid shared-memory size in bytes");
            Debug.Assert(function != IntPtr.Zero, "Invalid kernel function");
            Debug.Assert(args != IntPtr.Zero, "Internal launcher error");

            return cuLaunchKernelEx(
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

        [DllImport(CudaDriverLibName, EntryPoint = "cuLaunchKernel")]
        private static extern CudaError cuLaunchKernelEx(
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

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuOccupancyMaxActiveBlocksPerMultiprocessor(
            [Out] out int numBlocks,
            [In] IntPtr func,
            [In] int blockSize,
            [In] IntPtr dynamicSMemSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr CUoccupancyB2DSize(int blockSize);

        [DllImport(CudaDriverLibName)]
        public static extern CudaError cuOccupancyMaxPotentialBlockSize(
            [Out] out int minGridSize,
            [Out] out int blockSize,
            [In] IntPtr func,
            [In] [MarshalAs(UnmanagedType.FunctionPtr)] CUoccupancyB2DSize blockSizeToDynamicSMemSize,
            [In] IntPtr dynamicSMemSize,
            [In] int blockSizeLimit);

    }
}

#pragma warning restore IDE1006 // Naming Styles
