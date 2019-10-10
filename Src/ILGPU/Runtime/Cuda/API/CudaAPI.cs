// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaAPI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ILGPU.Runtime.Cuda.API
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
    /// Since the current implementation of dotnetcore does not support
    /// platform-dependent DLL imports with different entry point and libs,
    /// we have to wrap the direct low-level calls with (slow) virtual dispatchers.
    /// This will be removed as soon as dotnetcore adds additional support.
    /// </remarks>
    public abstract class CudaAPI
    {
        #region Static

#pragma warning disable IDE0002 // Simplify Member Access
        // Access cannot be simplified (dotnetcore build)
        private static CudaAPI InitializeAPI()
        {
            CudaAPI result;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    result = new CudaAPIWindows();
                else
                    result = new CudaAPIUnix();
                if (result.InitAPI() != CudaError.CUDA_SUCCESS)
                    result = new NotSupportedCudaAPI();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                // In case of a critical initialization exception
                // fall back to the not supported Cuda api.
                result = new NotSupportedCudaAPI();
            }
            return result;
        }
#pragma warning restore IDE0002 // Simplify Member Access

        /// <summary>
        /// Returns the driver API for the current platform.
        /// </summary>
        public static CudaAPI Current { get; } = InitializeAPI();

        #endregion

        #region General Methods

        /// <summary>
        /// Initializes the driver API.
        /// </summary>
        /// <returns>The error status.</returns>
        protected abstract CudaError InitAPI();

        /// <summary>
        /// Resolves the current driver version.
        /// </summary>
        /// <param name="driverVersion">The resolved driver version.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetDriverVersion(out int driverVersion);

        /// <summary>
        /// Resolves the error string for the given error status.
        /// </summary>
        /// <param name="error">The error to resolve.</param>
        /// <param name="pStr">The resolved error string.</param>
        /// <returns>The error status.</returns>
        internal abstract CudaError GetErrorString(
            CudaError error,
            out IntPtr pStr);

        /// <summary>
        /// Resolves the error string for the given error status.
        /// </summary>
        /// <param name="error">The error to resolve.</param>
        /// <returns>The resolved error string.</returns>
        public string GetErrorString(CudaError error)
        {
            if (GetErrorString(error, out IntPtr ptr) != CudaError.CUDA_SUCCESS)
                return RuntimeErrorMessages.CannotResolveErrorString;
            return Marshal.PtrToStringAnsi(ptr);
        }

        #endregion

        #region Device Methods

        /// <summary>
        /// Resolves the device id for the given ordinal.
        /// </summary>
        /// <param name="device">The device id.</param>
        /// <param name="ordinal">The device ordinal.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetDevice(out int device, int ordinal);

        /// <summary>
        /// Resolves the number of available devices.
        /// </summary>
        /// <param name="count">The number of devices</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetDeviceCount(out int count);

        /// <summary>
        /// Resolves the name of a device.
        /// </summary>
        /// <param name="bytes">The memory buffer in bytes.</param>
        /// <param name="length">The maximum length to resolve.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        protected abstract CudaError GetDeviceName(byte[] bytes, int length, int device);

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
            var error = GetDeviceName(nameBuffer, nameBuffer.Length, device);
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
        public abstract CudaError GetTotalDeviceMemory(out IntPtr bytes, int device);

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
        /// <param name="value">The resolved value.</param>
        /// <param name="attribute">The device attribute.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        internal abstract CudaError GetDeviceAttribute(
            out int value,
            DeviceAttribute attribute,
            int device);

        /// <summary>
        /// Resolves the value of the given device attribute.
        /// </summary>
        /// <param name="attribute">The device attribute.</param>
        /// <param name="device">The device.</param>
        /// <returns>The resolved value.</returns>
        internal int GetDeviceAttribute(DeviceAttribute attribute, int device)
        {
            CudaException.ThrowIfFailed(
                GetDeviceAttribute(out int value, attribute, device));
            return value;
        }

        /// <summary>
        /// Resolves the compute capability of the given device.
        /// </summary>
        /// <param name="major">The major capability.</param>
        /// <param name="minor">The minor capability.</param>
        /// <param name="device">The device.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetDeviceComputeCapability(
            out int major,
            out int minor,
            int device);

        #endregion

        #region Context Methods

        /// <summary>
        /// Creates a new context.
        /// </summary>
        /// <param name="context">The created context.</param>
        /// <param name="flags">The flags to use.</param>
        /// <param name="device">The target device.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError CreateContext(
            out IntPtr context,
            CudaAcceleratorFlags flags,
            int device);

        /// <summary>
        /// Creates a new context with D3D11 support
        /// </summary>
        /// <param name="context">The created context.</param>
        /// <param name="flags">The flags to use.</param>
        /// <param name="device">The target device.</param>
        /// <param name="d3dDevice">The associated D3D11 device.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError CreateContextD3D11(
            out IntPtr context,
            out int device,
            CudaAcceleratorFlags flags,
            IntPtr d3dDevice);

        /// <summary>
        /// Destroys the given context.
        /// </summary>
        /// <param name="context">The context to destroy.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError DestroyContext(IntPtr context);

        /// <summary>
        /// Make the given context the current one.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError SetCurrentContext(IntPtr context);

        /// <summary>
        /// Synchronizes the current context.
        /// </summary>
        /// <returns>The error status.</returns>
        public abstract CudaError SynchronizeContext();

        /// <summary>
        /// Resolves the cache configuration.
        /// </summary>
        /// <param name="config">The resolved cache configuration.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetCacheConfig(out CudaCacheConfiguration config);

        /// <summary>
        /// Updates the cache configuration.
        /// </summary>
        /// <param name="config">The updated cache configuration.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError SetCacheConfig(CudaCacheConfiguration config);

        /// <summary>
        /// Resolves the shared-memory configuration.
        /// </summary>
        /// <param name="config">The resolved shared-memory configuration.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetSharedMemoryConfig(out CudaSharedMemoryConfiguration config);

        /// <summary>
        /// Updates the shared-memory configuration.
        /// </summary>
        /// <param name="config">The updated shared-memory configuration.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError SetSharedMemoryConfig(CudaSharedMemoryConfiguration config);

        /// <summary>
        /// Resolves whether the given device can access the given peer device.
        /// </summary>
        /// <param name="canAccess">True, iff the device can access the peer device.</param>
        /// <param name="device">The device.</param>
        /// <param name="peerDevice">The peer device.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError CanAccessPeer(
            out int canAccess,
            int device,
            int peerDevice);

        /// <summary>
        /// Enables peer access to the given context.
        /// </summary>
        /// <param name="peerContext">The peer context.</param>
        /// <param name="flags">The flags to use.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError EnablePeerAccess(IntPtr peerContext, int flags);

        /// <summary>
        /// Disables peer access to the given context.
        /// </summary>
        /// <param name="peerContext">The peer context.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError DisablePeerAccess(IntPtr peerContext);

        /// <summary>
        /// Resolves the given peer attribute.
        /// </summary>
        /// <param name="value">The resolved value.</param>
        /// <param name="attribute">The attribute to resolve.</param>
        /// <param name="sourceDevice">The source device.</param>
        /// <param name="destinationDevice">The destination device.</param>
        /// <returns>The error status.</returns>
        internal abstract CudaError GetPeerAttribute(
            out int value,
            Peer2PeerAttribute attribute,
            int sourceDevice,
            int destinationDevice);

        #endregion

        #region Memory Methods

        /// <summary>
        /// Resolves memory information.
        /// </summary>
        /// <param name="free">The amount of free memory.</param>
        /// <param name="total">The total amount of memory.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetMemoryInfo(out IntPtr free, out IntPtr total);

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
        public abstract CudaError AllocateMemory(out IntPtr devicePtr, IntPtr bytesize);

        /// <summary>
        /// Frees the given device pointer.
        /// </summary>
        /// <param name="devicePtr">The device pointer.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError FreeMemory(IntPtr devicePtr);

        /// <summary>
        /// Performs a memory-copy operation.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="source">The source.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError Memcpy(
            IntPtr destination,
            IntPtr source,
            IntPtr length);

        /// <summary>
        /// Performs a memory-copy operation from host to device memory.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="sourceHost">The source in host memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">The accelerator stream for async processing.</param>
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
        /// <param name="stream">The accelerator stream for async processing.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError MemcpyHostToDevice(
            IntPtr destinationDevice,
            IntPtr sourceHost,
            IntPtr length,
            IntPtr stream);

        /// <summary>
        /// Performs a memory-copy operation from device to host memory.
        /// </summary>
        /// <param name="destinationHost">The destination in host memory.</param>
        /// <param name="sourceDevice">The source in device memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">The accelerator stream for async processing.</param>
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
        /// <param name="stream">The accelerator stream for async processing.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError MemcpyDeviceToHost(
            IntPtr destinationHost,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream);

        /// <summary>
        /// Performs a memory-copy operation from device to device memory.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="sourceDevice">The source in device memory.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="stream">The accelerator stream for async processing.</param>
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
        /// <param name="stream">The accelerator stream for async processing.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError MemcpyDeviceToDevice(
            IntPtr destinationDevice,
            IntPtr sourceDevice,
            IntPtr length,
            IntPtr stream);

        /// <summary>
        /// Performs a memory-set operation.
        /// </summary>
        /// <param name="destinationDevice">The destination in device memory.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="length">The length in bytes.</param>
        /// <param name="stream">The accelerator stream for async processing.</param>
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
        /// <param name="stream">The accelerator stream for async processing.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError Memset(
            IntPtr destinationDevice,
            byte value,
            IntPtr length,
            IntPtr stream);

        /// <summary>
        /// Resolves a pointer-attribute value.
        /// </summary>
        /// <param name="targetPtr">The target pointer.</param>
        /// <param name="attribute">The attribute to resolve.</param>
        /// <param name="devicePtr">The pointer in device memory.</param>
        /// <returns>The error status.</returns>
        internal abstract CudaError GetPointerAttribute(
            IntPtr targetPtr,
            PointerAttribute attribute,
            IntPtr devicePtr);

        #endregion

        #region Stream Methods

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <param name="stream">The created stream.</param>
        /// <param name="flags">The flags to use.</param>
        /// <returns>The error status.</returns>
        internal abstract CudaError CreateStream(
            out IntPtr stream,
            StreamFlags flags);

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <param name="stream">The created stream.</param>
        /// <param name="flags">The flags to use.</param>
        /// <param name="priority">The priority to use.</param>
        /// <returns>The error status.</returns>
        internal abstract CudaError CreateStreamWithPriority(
            out IntPtr stream,
            StreamFlags flags,
            int priority);

        /// <summary>
        /// Destroys the given stream.
        /// </summary>
        /// <param name="stream">The stream to destroy.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError DestroyStream(IntPtr stream);

        /// <summary>
        /// Synchronizes with the given stream.
        /// </summary>
        /// <param name="stream">The stream to synchronize with.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError SynchronizeStream(IntPtr stream);

        #endregion

        #region Kernel Methods

        /// <summary>
        /// Loads the given kernel module into driver memory.
        /// </summary>
        /// <param name="kernelModule">The loaded module.</param>
        /// <param name="moduleData">The module data to load.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError LoadModule(out IntPtr kernelModule, string moduleData);

        /// <summary>
        /// Loads the given kernel module into driver memory.
        /// </summary>
        /// <param name="kernelModule">The loaded module.</param>
        /// <param name="moduleData">The module data to load.</param>
        /// <param name="numOptions">The number of jit options.</param>
        /// <param name="jitOptions">The jit options.</param>
        /// <param name="jitOptionValues">The jit values.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError LoadModule(
            out IntPtr kernelModule,
            string moduleData,
            int numOptions,
            IntPtr jitOptions,
            IntPtr jitOptionValues);

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

            if (result != CudaError.CUDA_SUCCESS)
                errorLog = Encoding.ASCII.GetString(errorBuffer, BufferSize);
            else
                errorLog = null;
            return result;
        }

        /// <summary>
        /// Unlods the given module.
        /// </summary>
        /// <param name="kernelModule">The module to unload.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError DestroyModule(IntPtr kernelModule);

        /// <summary>
        /// Resolves the requested function handle in the scope of the given module.
        /// </summary>
        /// <param name="kernelFunction">The resolved function.</param>
        /// <param name="kernelModule">The module.</param>
        /// <param name="functionName">The name of the function.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError GetModuleFunction(
            out IntPtr kernelFunction,
            IntPtr kernelModule,
            string functionName);

        /// <summary>
        /// Launches the given kernel function.
        /// </summary>
        /// <param name="stream">The current stream.</param>
        /// <param name="kernel">The current kernel.</param>
        /// <param name="gridDimX">The grid dimension in X dimension.</param>
        /// <param name="gridDimY">The grid dimension in Y dimension.</param>
        /// <param name="gridDimZ">The grid dimension in Z dimension.</param>
        /// <param name="blockDimX">The block dimension in X dimension.</param>
        /// <param name="blockDimY">The block dimension in Y dimension.</param>
        /// <param name="blockDimZ">The block dimension in Z dimension.</param>
        /// <param name="sharedMemSizeInBytes">The shared-memory size in bytes.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="kernelArgs">The kernel arguments.</param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CudaError LaunchKernelWithStreamBinding(
            CudaStream stream,
            CudaKernel kernel,
            int gridDimX,
            int gridDimY,
            int gridDimZ,
            int blockDimX,
            int blockDimY,
            int blockDimZ,
            int sharedMemSizeInBytes,
            IntPtr args,
            IntPtr kernelArgs)
        {
            var binding = stream.BindScoped();

            var result = LaunchKernel(
                kernel.FunctionPtr,
                gridDimX,
                gridDimY,
                gridDimZ,
                blockDimX,
                blockDimY,
                blockDimZ,
                sharedMemSizeInBytes,
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
        public abstract CudaError LaunchKernel(
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
            IntPtr kernelArgs);

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
        /// <param name="argumentLength">The length of the memory region in bytes.</param>
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
            where T : struct
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
        /// <param name="dynamicSMemSize">The size of the required shared memory.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            IntPtr dynamicSMemSize);

        /// <summary>
        /// Computes the maximum potential block size to for maximum occupancy.
        /// </summary>
        /// <param name="minGridSize">The minimum grid size for maximum occupancy.</param>
        /// <param name="blockSize">The block size for maximum occupancy.</param>
        /// <param name="func">The function.</param>
        /// <param name="blockSizeToDynamicSMemSize">Computes the amount of required shared-memory for the given block size.</param>
        /// <param name="dynamicSMemSize">The size of the required shared memory (independent of the block size).</param>
        /// <param name="blockSizeLimit">The block-size limit.</param>
        /// <returns>The error status.</returns>
        public abstract CudaError ComputeOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            ComputeDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            IntPtr dynamicSMemSize,
            int blockSizeLimit);

        /// <summary>
        /// Computes the maximum potential block size to for maximum occupancy.
        /// </summary>
        /// <param name="minGridSize">The minimum grid size for maximum occupancy.</param>
        /// <param name="blockSize">The block size for maximum occupancy.</param>
        /// <param name="func">The function.</param>
        /// <param name="blockSizeToDynamicSMemSize">Computes the amount of required shared-memory for the given block size.</param>
        /// <param name="dynamicSMemSize">The size of the required shared memory (independent of the block size).</param>
        /// <param name="blockSizeLimit">The block-size limit.</param>
        /// <returns>The error status.</returns>
        public CudaError ComputeOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            ComputeManagedDynamicMemorySizeForBlockSize blockSizeToDynamicSMemSize,
            int dynamicSMemSize,
            int blockSizeLimit)
        {
            return ComputeOccupancyMaxPotentialBlockSize(
                out minGridSize,
                out blockSize,
                func,
                size => new IntPtr(blockSizeToDynamicSMemSize(size)),
                new IntPtr(dynamicSMemSize),
                blockSizeLimit);
        }

        #endregion
    }
}
