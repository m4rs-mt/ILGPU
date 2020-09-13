﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable IDE1006 // Naming

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Computes the amount of shared memory for the given block size.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The amount of required shared memory.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate IntPtr ComputeDynamicMemorySizeForBlockSize(int blockSize);

    /// <summary>
    /// Computes the amount of shared memory for the given block size.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The amount of required shared memory.</returns>
    public delegate int ComputeManagedDynamicMemorySizeForBlockSize(int blockSize);

    /// <summary>
    /// Wraps the Cuda-driver API.
    /// </summary>
    /// <remarks>
    /// Since the current implementation of .Net Core does not support
    /// platform-dependent DLL imports with different entry point and libs,
    /// we have to wrap the direct low-level calls with (slow) virtual dispatchers.
    /// This will be removed as soon as .Net Core adds additional support.
    /// </remarks>
    public abstract class CudaAPI : RuntimeAPI
    {
        #region Constants

        /// <summary>
        /// Represents the driver library name on Windows.
        /// </summary>
        public const string LibNameWindows = "nvcuda";

        /// <summary>
        /// Represents the driver library name on Linux.
        /// </summary>
        public const string LibNameLinux = "cuda";

        /// <summary>
        /// Represents the driver library name on MacOS.
        /// </summary>
        public const string LibNameMacOS = "cuda";

        #endregion

        #region Native Methods

        [DynamicImport]
        internal abstract CudaError cuInit([In] int flags);

        [DynamicImport]
        internal abstract CudaError cuDriverGetVersion([Out] out int driverVersion);

        [DynamicImport]
        internal abstract CudaError cuDeviceGet(
            [Out] out int device,
            [In] int ordinal);

        [DynamicImport]
        internal abstract CudaError cuDeviceGetCount([Out] out int count);

        [DynamicImport]
        internal abstract CudaError cuDeviceGetName(
            [In, Out] byte[] name,
            [In] int length,
            [In] int device);

        [DynamicImport]
        internal abstract CudaError cuDeviceTotalMem_v2(
            [Out] out IntPtr bytes,
            [In] int device);

        [DynamicImport]
        internal abstract CudaError cuDeviceGetAttribute(
            [Out] out int value,
            [In] DeviceAttribute attribute,
            [In] int device);

        [DynamicImport]
        internal abstract CudaError cuCtxCreate_v2(
            [Out] out IntPtr context,
            [In] CudaAcceleratorFlags flags,
            [In] int device);

        [DynamicImport]
        internal abstract CudaError cuD3D11CtxCreate_v2(
            [Out] out IntPtr context,
            [Out] out int device,
            [In] CudaAcceleratorFlags flags,
            [In] IntPtr d3dDevice);

        [DynamicImport]
        internal abstract CudaError cuCtxDestroy_v2(
            [In] IntPtr context);

        [DynamicImport]
        internal abstract CudaError cuCtxSetCurrent(
            [In] IntPtr context);

        [DynamicImport]
        internal abstract CudaError cuCtxSynchronize();

        [DynamicImport]
        internal abstract CudaError cuCtxGetCacheConfig(
            [Out] out CudaCacheConfiguration pconfig);

        [DynamicImport]
        internal abstract CudaError cuCtxSetCacheConfig(
            [In] CudaCacheConfiguration config);

        [DynamicImport]
        internal abstract CudaError cuCtxGetSharedMemConfig(
            [Out] out CudaSharedMemoryConfiguration pConfig);

        [DynamicImport]
        internal abstract CudaError cuCtxSetSharedMemConfig(
            [In] CudaSharedMemoryConfiguration config);

        [DynamicImport]
        internal abstract CudaError cuDeviceCanAccessPeer(
            [Out] out int canAccess,
            [In] int device,
            [In] int peerDev);

        [DynamicImport]
        internal abstract CudaError cuCtxEnablePeerAccess(
            [In] IntPtr peerContext,
            [In] int flags);

        [DynamicImport]
        internal abstract CudaError cuCtxDisablePeerAccess(
            IntPtr peerContext);

        [DynamicImport]
        internal abstract CudaError cuDeviceGetP2PAttribute(
            [Out] out int value,
            [In] Peer2PeerAttribute attrib,
            [In] int sourceDevice,
            [In] int destinationDevice);

        [DynamicImport]
        internal abstract CudaError cuPointerGetAttribute(
            [In] IntPtr targetPtr,
            [In] PointerAttribute attribute,
            [In] IntPtr devicePtr);

        [DynamicImport]
        internal abstract CudaError cuMemGetInfo_v2(
            [Out] out IntPtr free,
            [Out] out IntPtr total);

        [DynamicImport]
        internal abstract CudaError cuMemAlloc_v2(
            [Out] out IntPtr devicePtr,
            [In] IntPtr bytesize);

        [DynamicImport]
        internal abstract CudaError cuMemFree_v2(
            [In] IntPtr devicePtr);

        [DynamicImport]
        internal abstract CudaError cuMemAllocHost_v2(
            [Out] out IntPtr devicePtr,
            [In] IntPtr bytesize);

        [DynamicImport]
        internal abstract CudaError cuMemFreeHost(
            [In] IntPtr devicePtr);

        [DynamicImport]
        internal abstract CudaError cuMemcpy(
            [In] IntPtr destination,
            [In] IntPtr source,
            [In] IntPtr length);

        [DynamicImport]
        internal abstract CudaError cuMemcpyHtoDAsync_v2(
            [In] IntPtr destinationDevice,
            [In] IntPtr sourceHost,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DynamicImport]
        internal abstract CudaError cuMemcpyDtoHAsync_v2(
            [In] IntPtr destinationHost,
            [In] IntPtr sourceDevice,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DynamicImport]
        internal abstract CudaError cuMemcpyDtoDAsync_v2(
            [In] IntPtr destinationDevice,
            [In] IntPtr sourceDevice,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DynamicImport]
        internal abstract CudaError cuMemsetD8Async(
            [In] IntPtr destinationDevice,
            [In] byte value,
            [In] IntPtr length,
            [In] IntPtr stream);

        [DynamicImport]
        internal abstract CudaError cuStreamCreate(
            [Out] out IntPtr stream,
            [In] StreamFlags flags);

        [DynamicImport]
        internal abstract CudaError cuStreamCreateWithPriority(
            [Out] out IntPtr stream,
            [In] StreamFlags flags,
            [In] int priority);

        [DynamicImport]
        internal abstract CudaError cuStreamDestroy_v2(
            [In] IntPtr stream);

        [DynamicImport]
        internal abstract CudaError cuStreamSynchronize(
            [In] IntPtr stream);

        [DynamicImport]
        internal abstract CudaError cuGetErrorString(
            [In] CudaError error,
            [Out] out IntPtr pStr);

        [DynamicImport(
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        internal abstract CudaError cuModuleLoadData(
            [Out] out IntPtr module,
            [In] string moduleData);

        [DynamicImport(
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        internal abstract CudaError cuModuleLoadDataEx(
            [Out] out IntPtr module,
            [In] string moduleData,
            [In] int numOptions,
            [In] IntPtr jitOptions,
            [In] IntPtr jitOptionValues);

        [DynamicImport]
        internal abstract CudaError cuModuleUnload(IntPtr module);

        [DynamicImport(
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        internal abstract CudaError cuModuleGetFunction(
            [Out] out IntPtr function,
            [In] IntPtr module,
            [In] string functionName);

        [DynamicImport]
        internal abstract CudaError cuLaunchKernel(
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

        [DynamicImport]
        internal abstract CudaError cuOccupancyMaxActiveBlocksPerMultiprocessor(
            [Out] out int numBlocks,
            [In] IntPtr func,
            [In] int blockSize,
            [In] IntPtr dynamicSMemSize);

        [DynamicImport]
        internal abstract CudaError cuOccupancyMaxPotentialBlockSize(
            [Out] out int minGridSize,
            [Out] out int blockSize,
            [In] IntPtr func,
            [In] ComputeDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            [In] IntPtr dynamicSMemSize,
            [In] int blockSizeLimit);

        #endregion

        #region Static

        /// <summary>
        /// Returns the driver API for the current platform.
        /// </summary>
        public static CudaAPI CurrentAPI { get; } =
            RuntimeSystem.Instance.CreateDllWrapper<CudaAPI>(
                windows: LibNameWindows,
                linux: LibNameLinux,
                macos: LibNameMacOS,
                RuntimeErrorMessages.CudaNotSupported);

        #endregion

        #region General Methods

        /// <summary>
        /// Initializes the driver API.
        /// </summary>
        /// <returns>The error status.</returns>
        public override bool Init() => cuInit(0) == CudaError.CUDA_SUCCESS;

        /// <summary>
        /// Resolves the current driver version.
        /// </summary>
        /// <param name="driverVersion">The resolved driver version.</param>
        /// <returns>The error status.</returns>
        public CudaError GetDriverVersion(out CudaDriverVersion driverVersion)
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

        /// <summary>
        /// Resolves the error string for the given error status.
        /// </summary>
        /// <param name="error">The error to resolve.</param>
        /// <returns>The resolved error string.</returns>
        public string GetErrorString(CudaError error) =>
            cuGetErrorString(error, out IntPtr ptr) != CudaError.CUDA_SUCCESS
            ? RuntimeErrorMessages.CannotResolveErrorString
            : Marshal.PtrToStringAnsi(ptr);

        #endregion

        #region Device Methods

        /// <summary>
        /// Resolves the device id for the given ordinal.
        /// </summary>
        /// <param name="device">The device id.</param>
        /// <param name="ordinal">The device ordinal.</param>
        /// <returns>The error status.</returns>
        public CudaError GetDevice(out int device, int ordinal) =>
            cuDeviceGet(out device, ordinal);

        /// <summary>
        /// Resolves the number of available devices.
        /// </summary>
        /// <param name="count">The number of devices</param>
        /// <returns>The error status.</returns>
        public CudaError GetDeviceCount(out int count)
        {
            count = 0;
            return IsSupported
                ? cuDeviceGetCount(out count)
                : CudaError.CUDA_ERROR_NOT_INITIALIZED;
        }

        /// <summary>
        /// Resolves the name of a device.
        /// </summary>
        /// <param name="name">The resolved name.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        public unsafe CudaError GetDeviceName(out string name, int device)
        {
            const int MaxAcceleratorNameLength = 2048;
            name = null;
            var nameBuffer = new byte[MaxAcceleratorNameLength];
            var error = cuDeviceGetName(nameBuffer, nameBuffer.Length, device);
            if (error != CudaError.CUDA_SUCCESS)
                return error;
            var endIdx = Array.FindIndex(nameBuffer, 0, c => c == 0);
            name = Encoding.ASCII.GetString(nameBuffer, 0, endIdx);
            return CudaError.CUDA_SUCCESS;
        }

        /// <summary>
        /// Resolves total device memory.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        public CudaError GetTotalDeviceMemory(out IntPtr bytes, int device) =>
            cuDeviceTotalMem_v2(out bytes, device);

        /// <summary>
        /// Resolves total device memory.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        public CudaError GetTotalDeviceMemory(out long bytes, int device)
        {
            var error = GetTotalDeviceMemory(out IntPtr memory, device);
            bytes = memory.ToInt64();
            return error;
        }

        /// <summary>
        /// Resolves the value of the given device attribute.
        /// </summary>
        /// <param name="attribute">The device attribute.</param>
        /// <param name="device">The device.</param>
        /// <returns>The resolved value.</returns>
        internal int GetDeviceAttribute(DeviceAttribute attribute, int device)
        {
            CudaException.ThrowIfFailed(
                cuDeviceGetAttribute(out int value, attribute, device));
            return value;
        }

        /// <summary>
        /// Resolves the compute capability of the given device.
        /// </summary>
        /// <param name="major">The major capability.</param>
        /// <param name="minor">The minor capability.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        public CudaError GetDeviceComputeCapability(
            out int major,
            out int minor,
            int device)
        {
            var error = cuDeviceGetAttribute(
                out major,
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR,
                device);
            if (error != CudaError.CUDA_SUCCESS)
            {
                minor = default;
                return error;
            }
            return cuDeviceGetAttribute(
                out minor,
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR,
                device);
        }

        #endregion

        #region Context Methods

        /// <summary>
        /// Creates a new context.
        /// </summary>
        /// <param name="context">The created context.</param>
        /// <param name="flags">The flags to use.</param>
        /// <param name="device">The target device.</param>
        /// <returns>The error status.</returns>
        public CudaError CreateContext(
            out IntPtr context,
            CudaAcceleratorFlags flags,
            int device) =>
            cuCtxCreate_v2(out context, flags, device);

        /// <summary>
        /// Creates a new context with D3D11 support
        /// </summary>
        /// <param name="context">The created context.</param>
        /// <param name="flags">The flags to use.</param>
        /// <param name="device">The target device.</param>
        /// <param name="d3dDevice">The associated D3D11 device.</param>
        /// <returns>The error status.</returns>
        public CudaError CreateContextD3D11(
            out IntPtr context,
            out int device,
            CudaAcceleratorFlags flags,
            IntPtr d3dDevice) =>
            cuD3D11CtxCreate_v2(
                out context,
                out device,
                flags,
                d3dDevice);

        /// <summary>
        /// Destroys the given context.
        /// </summary>
        /// <param name="context">The context to destroy.</param>
        /// <returns>The error status.</returns>
        public CudaError DestroyContext(IntPtr context) =>
            cuCtxDestroy_v2(context);

        /// <summary>
        /// Make the given context the current one.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The error status.</returns>
        public CudaError SetCurrentContext(IntPtr context) =>
            cuCtxSetCurrent(context);

        /// <summary>
        /// Synchronizes the current context.
        /// </summary>
        /// <returns>The error status.</returns>
        public CudaError SynchronizeContext() =>
            cuCtxSynchronize();

        /// <summary>
        /// Resolves the cache configuration.
        /// </summary>
        /// <param name="config">The resolved cache configuration.</param>
        /// <returns>The error status.</returns>
        public CudaError GetCacheConfig(out CudaCacheConfiguration config) =>
            cuCtxGetCacheConfig(out config);

        /// <summary>
        /// Updates the cache configuration.
        /// </summary>
        /// <param name="config">The updated cache configuration.</param>
        /// <returns>The error status.</returns>
        public CudaError SetCacheConfig(CudaCacheConfiguration config) =>
            cuCtxSetCacheConfig(config);

        /// <summary>
        /// Resolves the shared-memory configuration.
        /// </summary>
        /// <param name="config">The resolved shared-memory configuration.</param>
        /// <returns>The error status.</returns>
        public CudaError GetSharedMemoryConfig(
            out CudaSharedMemoryConfiguration config) =>
            cuCtxGetSharedMemConfig(out config);

        /// <summary>
        /// Updates the shared-memory configuration.
        /// </summary>
        /// <param name="config">The updated shared-memory configuration.</param>
        /// <returns>The error status.</returns>
        public CudaError SetSharedMemoryConfig(
            CudaSharedMemoryConfiguration config) =>
            cuCtxSetSharedMemConfig(config);

        /// <summary>
        /// Resolves whether the given device can access the given peer device.
        /// </summary>
        /// <param name="canAccess">
        /// True, if the device can access the peer device.
        /// </param>
        /// <param name="device">The device.</param>
        /// <param name="peerDevice">The peer device.</param>
        /// <returns>The error status.</returns>
        public CudaError CanAccessPeer(
            out int canAccess,
            int device,
            int peerDevice) =>
            cuDeviceCanAccessPeer(out canAccess, device, peerDevice);

        /// <summary>
        /// Enables peer access to the given context.
        /// </summary>
        /// <param name="peerContext">The peer context.</param>
        /// <param name="flags">The flags to use.</param>
        /// <returns>The error status.</returns>
        public CudaError EnablePeerAccess(IntPtr peerContext, int flags) =>
            cuCtxEnablePeerAccess(peerContext, flags);

        /// <summary>
        /// Disables peer access to the given context.
        /// </summary>
        /// <param name="peerContext">The peer context.</param>
        /// <returns>The error status.</returns>
        public CudaError DisablePeerAccess(IntPtr peerContext) =>
            cuCtxDisablePeerAccess(peerContext);

        /// <summary>
        /// Resolves the given peer attribute.
        /// </summary>
        /// <param name="value">The resolved value.</param>
        /// <param name="attribute">The attribute to resolve.</param>
        /// <param name="sourceDevice">The source device.</param>
        /// <param name="destinationDevice">The destination device.</param>
        /// <returns>The error status.</returns>
        internal CudaError GetPeerAttribute(
            out int value,
            Peer2PeerAttribute attribute,
            int sourceDevice,
            int destinationDevice) =>
            cuDeviceGetP2PAttribute(
                out value,
                attribute,
                sourceDevice,
                destinationDevice);

        #endregion

        #region Memory Methods

        /// <summary>
        /// Resolves memory information.
        /// </summary>
        /// <param name="free">The amount of free memory.</param>
        /// <param name="total">The total amount of memory.</param>
        /// <returns>The error status.</returns>
        public CudaError GetMemoryInfo(out IntPtr free, out IntPtr total) =>
            cuMemGetInfo_v2(out free, out total);

        /// <summary>
        /// Resolves memory information.
        /// </summary>
        /// <param name="free">The amount of free memory.</param>
        /// <param name="total">The total amount of memory.</param>
        /// <returns>The error status.</returns>
        public CudaError GetMemoryInfo(out long free, out long total)
        {
            var error = GetMemoryInfo(out IntPtr freePtr, out IntPtr totalPtr);
            free = freePtr.ToInt64();
            total = totalPtr.ToInt64();
            return error;
        }

        /// <summary>
        /// Allocates memory on the current device.
        /// </summary>
        /// <param name="devicePtr">The resulting device pointer.</param>
        /// <param name="bytesize">The size of the allocation in bytes.</param>
        /// <returns>The error status.</returns>
        public CudaError AllocateMemory(out IntPtr devicePtr, IntPtr bytesize) =>
            cuMemAlloc_v2(out devicePtr, bytesize);

        /// <summary>
        /// Frees the given device pointer.
        /// </summary>
        /// <param name="devicePtr">The device pointer.</param>
        /// <returns>The error status.</returns>
        public CudaError FreeMemory(IntPtr devicePtr) =>
            cuMemFree_v2(devicePtr);

        /// <summary>
        /// Allocates memory on the host.
        /// </summary>
        /// <param name="hostPtr">The resulting host pointer.</param>
        /// <param name="bytesize">The size of the allocation in bytes.</param>
        /// <returns>The error status.</returns>
        public CudaError AllocateHostMemory(out IntPtr hostPtr, IntPtr bytesize) =>
            cuMemAllocHost_v2(out hostPtr, bytesize);

        /// <summary>
        /// Frees the given host pointer.
        /// </summary>
        /// <param name="hostPtr">The host pointer.</param>
        /// <returns>The error status.</returns>
        public CudaError FreeHostMemory(IntPtr hostPtr) =>
            cuMemFreeHost(hostPtr);

        /// <summary>
        /// Performs a memory-copy operation.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="source">The source.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <returns>The error status.</returns>
        public CudaError Memcpy(
            IntPtr destination,
            IntPtr source,
            IntPtr length) =>
            cuMemcpy(destination, source, length);

        /// <summary>
        /// Performs a memory-copy operation from host to device memory.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="sourceHost">The source in host memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CudaError MemcpyHostToDevice(
            IntPtr destinationDevice,
            IntPtr sourceHost,
            IntPtr length,
            AcceleratorStream stream)
        {
            CudaStream cudaStream = stream as CudaStream;
            return MemcpyHostToDevice(
                destinationDevice,
                sourceHost,
                length,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        /// <summary>
        /// Performs a memory-copy operation from host to device memory.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="sourceHost">The source in host memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        public CudaError MemcpyHostToDevice(
            IntPtr destinationDevice,
            IntPtr sourceHost,
            IntPtr length,
            IntPtr stream) =>
            cuMemcpyHtoDAsync_v2(
                destinationDevice,
                sourceHost,
                length,
                stream);

        /// <summary>
        /// Performs a memory-copy operation from device to host memory.
        /// </summary>
        /// <param name="destinationHost">The destination in host memory.</param>
        /// <param name="sourceDevice">The source in device memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CudaError MemcpyDeviceToHost(
            IntPtr destinationHost,
            IntPtr sourceDevice,
            IntPtr length,
            AcceleratorStream stream)
        {
            CudaStream cudaStream = stream as CudaStream;
            return MemcpyDeviceToHost(
                destinationHost,
                sourceDevice,
                length,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        /// <summary>
        /// Performs a memory-copy operation from device to host memory.
        /// </summary>
        /// <param name="destinationHost">The destination in host memory.</param>
        /// <param name="sourceDevice">The source in device memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        public CudaError MemcpyDeviceToHost(
            IntPtr destinationHost,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream) =>
            cuMemcpyDtoHAsync_v2(
                destinationHost,
                sourceDevice,
                length,
                stream);

        /// <summary>
        /// Performs a memory-copy operation from device to device memory.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="sourceDevice">The source in device memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CudaError MemcpyDeviceToDevice(
            IntPtr destinationDevice,
            IntPtr sourceDevice,
            IntPtr length,
            AcceleratorStream stream)
        {
            CudaStream cudaStream = stream as CudaStream;
            return MemcpyDeviceToDevice(
                destinationDevice,
                sourceDevice,
                length,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        /// <summary>
        /// Performs a memory-copy operation from device to device memory.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="sourceDevice">The source in device memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        public CudaError MemcpyDeviceToDevice(
            IntPtr destinationDevice,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream) =>
            cuMemcpyDtoDAsync_v2(
                destinationDevice,
                sourceDevice,
                length,
                stream);

        /// <summary>
        /// Performs a memory-set operation.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="length">The length in bytes.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CudaError Memset(
            IntPtr destinationDevice,
            byte value,
            IntPtr length,
            AcceleratorStream stream)
        {
            var cudaStream = stream as CudaStream;
            return Memset(
                destinationDevice,
                value,
                length,
                cudaStream?.StreamPtr ?? IntPtr.Zero);
        }

        /// <summary>
        /// Performs a memory-set operation.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="length">The length in bytes.</param>
        /// <param name="stream">
        /// The accelerator stream for asynchronous processing.
        /// </param>
        /// <returns>The error status.</returns>
        public CudaError Memset(
            IntPtr destinationDevice,
            byte value,
            IntPtr length,
            IntPtr stream) =>
            cuMemsetD8Async(destinationDevice, value, length, stream);

        /// <summary>
        /// Resolves a pointer-attribute value.
        /// </summary>
        /// <param name="targetPtr">The target pointer.</param>
        /// <param name="attribute">The attribute to resolve.</param>
        /// <param name="devicePtr">The pointer in device memory.</param>
        /// <returns>The error status.</returns>
        internal CudaError GetPointerAttribute(
            IntPtr targetPtr,
            PointerAttribute attribute,
            IntPtr devicePtr) =>
            cuPointerGetAttribute(
                targetPtr,
                attribute,
                devicePtr);

        #endregion

        #region Stream Methods

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <param name="stream">The created stream.</param>
        /// <param name="flags">The flags to use.</param>
        /// <returns>The error status.</returns>
        internal CudaError CreateStream(
            out IntPtr stream,
            StreamFlags flags) =>
            cuStreamCreate(out stream, flags);

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <param name="stream">The created stream.</param>
        /// <param name="flags">The flags to use.</param>
        /// <param name="priority">The priority to use.</param>
        /// <returns>The error status.</returns>
        internal CudaError CreateStreamWithPriority(
            out IntPtr stream,
            StreamFlags flags,
            int priority) =>
            cuStreamCreateWithPriority(out stream, flags, priority);

        /// <summary>
        /// Destroys the given stream.
        /// </summary>
        /// <param name="stream">The stream to destroy.</param>
        /// <returns>The error status.</returns>
        public CudaError DestroyStream(IntPtr stream) =>
            cuStreamDestroy_v2(stream);

        /// <summary>
        /// Synchronizes with the given stream.
        /// </summary>
        /// <param name="stream">The stream to synchronize with.</param>
        /// <returns>The error status.</returns>
        public CudaError SynchronizeStream(IntPtr stream) =>
            cuStreamSynchronize(stream);

        #endregion

        #region Kernel Methods

        /// <summary>
        /// Loads the given kernel module into driver memory.
        /// </summary>
        /// <param name="kernelModule">The loaded module.</param>
        /// <param name="moduleData">The module data to load.</param>
        /// <returns>The error status.</returns>
        public CudaError LoadModule(out IntPtr kernelModule, string moduleData) =>
            cuModuleLoadData(out kernelModule, moduleData);

        /// <summary>
        /// Loads the given kernel module into driver memory.
        /// </summary>
        /// <param name="kernelModule">The loaded module.</param>
        /// <param name="moduleData">The module data to load.</param>
        /// <param name="numOptions">The number of JIT options.</param>
        /// <param name="jitOptions">The JIT options.</param>
        /// <param name="jitOptionValues">The JIT values.</param>
        /// <returns>The error status.</returns>
        public CudaError LoadModule(
            out IntPtr kernelModule,
            string moduleData,
            int numOptions,
            IntPtr jitOptions,
            IntPtr jitOptionValues) =>
            cuModuleLoadDataEx(
                out kernelModule,
                moduleData,
                numOptions,
                jitOptions,
                jitOptionValues);

        /// <summary>
        /// Loads the given kernel module into driver memory.
        /// </summary>
        /// <param name="kernelModule">The loaded module.</param>
        /// <param name="moduleData">The module data to load.</param>
        /// <param name="errorLog">The error log.</param>
        /// <returns>The error status.</returns>
        public unsafe CudaError LoadModule(
            out IntPtr kernelModule,
            string moduleData,
            out string errorLog)
        {
            const int BufferSize = 1024;
            const int NumOptions = 2;

            // TODO: add support for debug information

            var options = stackalloc int[NumOptions];
            options[0] = 5; // CU_JIT_ERROR_LOG_BUFFER
            options[1] = 6; // CU_JIT_ERROR_LOG_BUFFER_SIZE_BYTES

            var errorBuffer = stackalloc byte[BufferSize];

            var optionValues = stackalloc byte[NumOptions * sizeof(void*)];
            var values = (void**)optionValues;
            values[0] = errorBuffer;
            values[1] = (void*)BufferSize;

            var result = LoadModule(
                out kernelModule,
                moduleData,
                NumOptions,
                new IntPtr(options),
                new IntPtr(optionValues));

            errorLog = result != CudaError.CUDA_SUCCESS
                ? Encoding.ASCII.GetString(errorBuffer, BufferSize)
                : null;
            return result;
        }

        /// <summary>
        /// Unloads the given module.
        /// </summary>
        /// <param name="kernelModule">The module to unload.</param>
        /// <returns>The error status.</returns>
        public CudaError DestroyModule(IntPtr kernelModule) =>
            cuModuleUnload(kernelModule);

        /// <summary>
        /// Resolves the requested function handle in the scope of the given module.
        /// </summary>
        /// <param name="kernelFunction">The resolved function.</param>
        /// <param name="kernelModule">The module.</param>
        /// <param name="functionName">The name of the function.</param>
        /// <returns>The error status.</returns>
        public CudaError GetModuleFunction(
            out IntPtr kernelFunction,
            IntPtr kernelModule,
            string functionName) =>
            cuModuleGetFunction(out kernelFunction, kernelModule, functionName);

        /// <summary>
        /// Launches the given kernel function.
        /// </summary>
        /// <param name="stream">The current stream.</param>
        /// <param name="kernel">The current kernel.</param>
        /// <param name="config">The current kernel configuration.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="kernelArgs">The kernel arguments.</param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CudaError LaunchKernelWithStreamBinding(
            CudaStream stream,
            CudaKernel kernel,
            RuntimeKernelConfig config,
            IntPtr args,
            IntPtr kernelArgs)
        {
            var binding = stream.BindScoped();

            var result = LaunchKernel(
                kernel.FunctionPtr,
                config.GridDim.X,
                config.GridDim.Y,
                config.GridDim.Z,
                config.GroupDim.X,
                config.GroupDim.Y,
                config.GroupDim.Z,
                config.SharedMemoryConfig.DynamicArraySize,
                stream.StreamPtr,
                args,
                kernelArgs);

            binding.Recover();
            return result;
        }

        /// <summary>
        /// Launches the given kernel function.
        /// </summary>
        /// <param name="kernelFunction">The function to launch.</param>
        /// <param name="gridDimX">The grid dimension in X dimension.</param>
        /// <param name="gridDimY">The grid dimension in Y dimension.</param>
        /// <param name="gridDimZ">The grid dimension in Z dimension.</param>
        /// <param name="blockDimX">The block dimension in X dimension.</param>
        /// <param name="blockDimY">The block dimension in Y dimension.</param>
        /// <param name="blockDimZ">The block dimension in Z dimension.</param>
        /// <param name="sharedMemSizeInBytes">The shared-memory size in bytes.</param>
        /// <param name="stream">The associated accelerator stream.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="kernelArgs">The kernel arguments.</param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CudaError LaunchKernel(
            IntPtr kernelFunction,
            int gridDimX,
            int gridDimY,
            int gridDimZ,
            int blockDimX,
            int blockDimY,
            int blockDimZ,
            int sharedMemSizeInBytes,
            IntPtr stream,
            IntPtr args,
            IntPtr kernelArgs) =>
            cuLaunchKernel(
                kernelFunction,
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

        /// <summary>
        /// Launches the given kernel function using a bulk structure.
        /// </summary>
        /// <param name="kernelFunction">The function to launch.</param>
        /// <param name="gridDimX">The grid dimension in X dimension.</param>
        /// <param name="gridDimY">The grid dimension in Y dimension.</param>
        /// <param name="gridDimZ">The grid dimension in Z dimension.</param>
        /// <param name="blockDimX">The block dimension in X dimension.</param>
        /// <param name="blockDimY">The block dimension in Y dimension.</param>
        /// <param name="blockDimZ">The block dimension in Z dimension.</param>
        /// <param name="sharedMemSizeInBytes">The shared-memory size in bytes.</param>
        /// <param name="stream">The associated accelerator stream.</param>
        /// <param name="argument">The argument structure.</param>
        /// <param name="argumentLength">
        /// The length of the memory region in bytes.
        /// </param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe CudaError LaunchKernelWithStruct<T>(
            IntPtr kernelFunction,
            int gridDimX,
            int gridDimY,
            int gridDimZ,
            int blockDimX,
            int blockDimY,
            int blockDimZ,
            int sharedMemSizeInBytes,
            IntPtr stream,
            ref T argument,
            int argumentLength)
            where T : unmanaged
        {
            var argumentLengthPtrSize = new IntPtr(argumentLength);
            var kernelArgs = stackalloc byte[sizeof(void*) * 5];
            var kernelArgsPtr = (void**)kernelArgs;
            kernelArgsPtr[0] = (void*)0x1;
            kernelArgsPtr[1] = Unsafe.AsPointer(ref argument);
            kernelArgsPtr[2] = (void*)0x2;
            kernelArgsPtr[3] = &argumentLengthPtrSize;
            kernelArgsPtr[4] = (void*)0x0;

            return LaunchKernel(
                kernelFunction,
                gridDimX,
                gridDimY,
                gridDimZ,
                blockDimX,
                blockDimY,
                blockDimZ,
                sharedMemSizeInBytes,
                stream,
                IntPtr.Zero,
                new IntPtr(kernelArgs));
        }

        /// <summary>
        /// Computes the maximum number of blocks for maximum occupancy. 
        /// </summary>
        /// <param name="numBlocks">The number of blocks.</param>
        /// <param name="func">The function.</param>
        /// <param name="blockSize">The desired block size.</param>
        /// <param name="dynamicSMemSize">
        /// The size of the required shared memory.
        /// </param>
        /// <returns>The error status.</returns>
        public CudaError ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            IntPtr dynamicSMemSize) =>
            cuOccupancyMaxActiveBlocksPerMultiprocessor(
                out numBlocks,
                func,
                blockSize,
                dynamicSMemSize);

        /// <summary>
        /// Computes the maximum potential block size to for maximum occupancy.
        /// </summary>
        /// <param name="minGridSize">
        /// The minimum grid size for maximum occupancy.
        /// </param>
        /// <param name="blockSize">The block size for maximum occupancy.</param>
        /// <param name="func">The function.</param>
        /// <param name="blockSizeToDynamicSMemSize">
        /// Computes the amount of required shared-memory for the given block size.
        /// </param>
        /// <param name="dynamicSMemSize">
        /// The size of the required shared memory (independent of the block size).
        /// </param>
        /// <param name="blockSizeLimit">The block-size limit.</param>
        /// <returns>The error status.</returns>
        public CudaError ComputeOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            ComputeDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            IntPtr dynamicSMemSize,
            int blockSizeLimit) =>
            cuOccupancyMaxPotentialBlockSize(
                out minGridSize,
                out blockSize,
                func,
                blockSizeToDynamicSMemSize,
                dynamicSMemSize,
                blockSizeLimit);

        /// <summary>
        /// Computes the maximum potential block size to for maximum occupancy.
        /// </summary>
        /// <param name="minGridSize">
        /// The minimum grid size for maximum occupancy.
        /// </param>
        /// <param name="blockSize">The block size for maximum occupancy.</param>
        /// <param name="func">The function.</param>
        /// <param name="blockSizeToDynamicSMemSize">
        /// Computes the amount of required shared-memory for the given block size.
        /// </param>
        /// <param name="dynamicSMemSize">
        /// The size of the required shared memory (independent of the block size).
        /// </param>
        /// <param name="blockSizeLimit">The block-size limit.</param>
        /// <returns>The error status.</returns>
        public CudaError ComputeOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            ComputeManagedDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            int dynamicSMemSize,
            int blockSizeLimit) =>
            ComputeOccupancyMaxPotentialBlockSize(
                out minGridSize,
                out blockSize,
                func,
                size => new IntPtr(blockSizeToDynamicSMemSize(size)),
                new IntPtr(dynamicSMemSize),
                blockSizeLimit);

        #endregion
    }
}

#pragma warning restore IDE1006 // Naming
