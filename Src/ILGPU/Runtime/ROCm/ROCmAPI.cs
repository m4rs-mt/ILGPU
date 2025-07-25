// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a HIP API error code.
/// </summary>
public enum ROCmError
{
    /// <summary>
    /// No error.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Invalid value.
    /// </summary>
    InvalidValue = 1,

    /// <summary>
    /// Out of memory.
    /// </summary>
    OutOfMemory = 2,

    /// <summary>
    /// Not initialized.
    /// </summary>
    NotInitialized = 3,

    /// <summary>
    /// An unspecified error.
    /// </summary>
    Unknown = 999,
}

/// <summary>
/// Represents the HIP memory copy direction.
/// </summary>
public enum HipMemcpyKind
{
    /// <summary>Host to Host.</summary>
    HostToHost = 0,
    /// <summary>Host to Device.</summary>
    HostToDevice = 1,
    /// <summary>Device to Host.</summary>
    DeviceToHost = 2,
    /// <summary>Device to Device.</summary>
    DeviceToDevice = 3,
    /// <summary>Unified virtual addressing copy direction.</summary>
    Default = 4,
}

/// <summary>
/// Represents HIP device attribute kinds.
/// </summary>
public enum HipDeviceAttributeKind
{
    /// <summary>Maximum threads per block.</summary>
    MaxThreadsPerBlock = 1,
    /// <summary>Maximum block dimension X.</summary>
    MaxBlockDimX = 2,
    /// <summary>Maximum block dimension Y.</summary>
    MaxBlockDimY = 3,
    /// <summary>Maximum block dimension Z.</summary>
    MaxBlockDimZ = 4,
    /// <summary>Maximum grid dimension X.</summary>
    MaxGridDimX = 5,
    /// <summary>Maximum grid dimension Y.</summary>
    MaxGridDimY = 6,
    /// <summary>Maximum grid dimension Z.</summary>
    MaxGridDimZ = 7,
    /// <summary>Maximum shared memory per block in bytes.</summary>
    MaxSharedMemoryPerBlock = 8,
    /// <summary>Total constant memory in bytes.</summary>
    TotalConstantMemory = 9,
    /// <summary>Warp size in threads.</summary>
    WarpSize = 10,
    /// <summary>Maximum registers per block.</summary>
    MaxRegistersPerBlock = 12,
    /// <summary>Clock rate in kHz.</summary>
    ClockRate = 13,
    /// <summary>Multiprocessor count.</summary>
    MultiprocessorCount = 16,
    /// <summary>Whether the device is integrated.</summary>
    Integrated = 18,
    /// <summary>Whether the device can map host memory.</summary>
    CanMapHostMemory = 19,
    /// <summary>Compute mode.</summary>
    ComputeMode = 20,
    /// <summary>Maximum threads per multiprocessor.</summary>
    MaxThreadsPerMultiprocessor = 39,
    /// <summary>Whether the device supports managed memory.</summary>
    ManagedMemory = 83,
    /// <summary>L2 cache size in bytes.</summary>
    L2CacheSize = 44,
    /// <summary>Max shared memory per multiprocessor.</summary>
    MaxSharedMemoryPerMultiprocessor = 81,
}

/// <summary>
/// Wraps HIP runtime API calls for ROCm/AMD GPU compute.
/// </summary>
/// <remarks>
/// HIP has a CUDA-compatible API surface. The launcher uses the same
/// flat-struct argument buffer pattern via HIP_LAUNCH_PARAM_BUFFER_POINTER.
/// </remarks>
unsafe partial class ROCmAPI
{
    #region Initialization

    /// <summary>
    /// Initializes the HIP runtime.
    /// </summary>
    public override bool Init() =>
        hipInit(0) == ROCmError.Success;

    #endregion

    #region Stream Management

    /// <summary>HIP stream flag: default behavior.</summary>
    public const uint HipStreamDefault = 0x00;

    /// <summary>
    /// HIP stream flag: non-blocking stream that does not synchronize with
    /// the default (null) stream.
    /// </summary>
    public const uint HipStreamNonBlocking = 0x01;

    /// <summary>
    /// Creates a new HIP stream.
    /// </summary>
    /// <param name="stream">The created stream handle.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError StreamCreate(out IntPtr stream) =>
        hipStreamCreate(out stream);

    /// <summary>
    /// Creates a new HIP stream with the specified flags.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError StreamCreateWithFlags(out IntPtr stream, uint flags) =>
        hipStreamCreateWithFlags(out stream, flags);

    /// <summary>
    /// Destroys a HIP stream.
    /// </summary>
    /// <param name="stream">The stream handle.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError StreamDestroy(IntPtr stream) =>
        hipStreamDestroy(stream);

    /// <summary>
    /// Synchronizes a HIP stream (waits for all operations to complete).
    /// </summary>
    /// <param name="stream">The stream handle.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError StreamSynchronize(IntPtr stream) =>
        hipStreamSynchronize(stream);

    #endregion

    #region Module Management

    /// <summary>
    /// Loads a HIP module from binary data.
    /// </summary>
    /// <param name="image">Pointer to the module binary data.</param>
    /// <param name="module">The loaded module handle.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError ModuleLoadData(byte* image, out IntPtr module) =>
        hipModuleLoadData(out module, image);

    /// <summary>
    /// Gets a function from a loaded module.
    /// </summary>
    /// <param name="module">The module handle.</param>
    /// <param name="name">The function name.</param>
    /// <param name="function">The function handle.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError ModuleGetFunction(
        IntPtr module,
        string name,
        out IntPtr function) =>
        hipModuleGetFunction(out function, module, name);

    /// <summary>
    /// Unloads a HIP module.
    /// </summary>
    /// <param name="module">The module handle.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError ModuleUnload(IntPtr module) =>
        hipModuleUnload(module);

    #endregion

    #region Kernel Launch

    /// <summary>
    /// Launches a kernel using a flat struct argument buffer.
    /// Uses HIP_LAUNCH_PARAM_BUFFER_POINTER pattern (identical to CUDA).
    /// </summary>
    /// <typeparam name="T">The kernel arguments struct type.</typeparam>
    /// <param name="stream">The HIP stream.</param>
    /// <param name="kernel">The HIP kernel.</param>
    /// <param name="config">The kernel launch configuration.</param>
    /// <param name="args">The kernel arguments struct.</param>
    /// <param name="argsSizeInBytes">The size of the arguments in bytes.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError LaunchKernelWithStruct<T>(
        ROCmStream stream,
        ROCmKernel kernel,
        in KernelConfig config,
        ref T args,
        int argsSizeInBytes)
        where T : unmanaged
    {
        var size = new IntPtr(argsSizeInBytes);
        Debug.Assert(
            argsSizeInBytes <= Interop.SizeOf<T>(),
            "Invalid argument size");

        fixed (T* pArgs = &args)
        {
            // Setup launch configuration (same pattern as CUDA)
            var launchConfig = stackalloc void*[5];
            launchConfig[0] = (void*)1; // HIP_LAUNCH_PARAM_BUFFER_POINTER
            launchConfig[1] = pArgs;
            launchConfig[2] = (void*)2; // HIP_LAUNCH_PARAM_BUFFER_SIZE
            launchConfig[3] = &size;
            launchConfig[4] = (void*)0; // HIP_LAUNCH_PARAM_END

            return hipModuleLaunchKernel(
                kernel.FunctionHandle,
                (uint)config.GridSize, 1U, 1U,
                (uint)config.GroupSize, 1U, 1U,
                (uint)config.SharedMemoryBytes,
                stream.StreamHandle,
                IntPtr.Zero,
                new IntPtr(launchConfig));
        }
    }

    #endregion

    #region Memory Management

    /// <summary>
    /// Allocates device memory.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <param name="ptr">The allocated pointer.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError Malloc(long size, out IntPtr ptr) =>
        hipMalloc(out ptr, new IntPtr(size));

    /// <summary>
    /// Frees device memory.
    /// </summary>
    /// <param name="ptr">The pointer to free.</param>
    /// <returns>The error status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError Free(IntPtr ptr) => hipFree(ptr);

    /// <summary>
    /// Copies memory between host and device asynchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError MemcpyAsync(
        IntPtr dst,
        IntPtr src,
        IntPtr sizeBytes,
        HipMemcpyKind kind,
        IntPtr stream) =>
        hipMemcpyAsync(dst, src, sizeBytes, kind, stream);

    /// <summary>
    /// Copies host memory to device memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError MemcpyHtoD(IntPtr dst, IntPtr src, IntPtr sizeBytes) =>
        hipMemcpyAsync(dst, src, sizeBytes, HipMemcpyKind.HostToDevice, IntPtr.Zero);

    /// <summary>
    /// Copies device memory to host memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError MemcpyDtoH(IntPtr dst, IntPtr src, IntPtr sizeBytes) =>
        hipMemcpyAsync(dst, src, sizeBytes, HipMemcpyKind.DeviceToHost, IntPtr.Zero);

    /// <summary>
    /// Sets device memory to a byte value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError Memset(IntPtr dst, byte value, IntPtr count) =>
        hipMemset(dst, value, count);

    /// <summary>
    /// Sets device memory to a byte value asynchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError MemsetAsync(
        IntPtr dst,
        byte value,
        IntPtr count,
        IntPtr stream) =>
        hipMemsetAsync(dst, value, count, stream);

    #endregion

    #region Device Management

    /// <summary>
    /// Returns the number of HIP-capable devices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError GetDeviceCount(out int count) =>
        hipGetDeviceCount(out count);

    /// <summary>
    /// Returns the currently active device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError GetDevice(out int device) =>
        hipGetDevice(out device);

    /// <summary>
    /// Sets the active device for the current thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError SetDevice(int device) =>
        hipSetDevice(device);

    /// <summary>
    /// Returns the name of the specified device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError GetDeviceName(out string name, int device)
    {
        const int maxNameLength = 256;
        var buffer = stackalloc byte[maxNameLength];
        var error = hipDeviceGetName(buffer, maxNameLength, device);
        name = error == ROCmError.Success
            ? new string((sbyte*)buffer)
            : string.Empty;
        return error;
    }

    /// <summary>
    /// Returns the value of a device attribute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError GetDeviceAttribute(
        out int value,
        HipDeviceAttributeKind attr,
        int device) =>
        hipDeviceGetAttribute(out value, attr, device);

    /// <summary>
    /// Returns the value of a device attribute, throwing on failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetDeviceAttributeValue(HipDeviceAttributeKind attr, int device)
    {
        ROCmException.ThrowIfFailed(
            hipDeviceGetAttribute(out int value, attr, device));
        return value;
    }

    /// <summary>
    /// Returns the total memory of the specified device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError GetDeviceTotalMem(out long bytes, int device)
    {
        var error = hipDeviceTotalMem(out UIntPtr raw, device);
        bytes = (long)(ulong)raw;
        return error;
    }

    /// <summary>
    /// Blocks until the device has completed all preceding requested tasks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError DeviceSynchronize() =>
        hipDeviceSynchronize();

    /// <summary>
    /// Resets the current device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError DeviceReset() =>
        hipDeviceReset();

    #endregion

    #region Occupancy

    /// <summary>
    /// Returns the maximum number of active blocks per SM for a given kernel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError OccupancyMaxActiveBlocksPerMultiprocessor(
        out int numBlocks,
        IntPtr function,
        int blockSize,
        int dynamicSharedMemorySize) =>
        hipOccupancyMaxActiveBlocksPerMultiprocessor(
            out numBlocks,
            function,
            blockSize,
            new UIntPtr((uint)dynamicSharedMemorySize));

    /// <summary>
    /// Returns the maximum potential block size for a given kernel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError OccupancyMaxPotentialBlockSize(
        out int gridSize,
        out int blockSize,
        IntPtr function,
        int dynamicSharedMemorySize,
        int blockSizeLimit) =>
        hipOccupancyMaxPotentialBlockSize(
            out gridSize,
            out blockSize,
            function,
            new UIntPtr((uint)dynamicSharedMemorySize),
            blockSizeLimit);

    #endregion

    #region Peer Access

    /// <summary>
    /// Queries if a device may directly access a peer device's memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError CanAccessPeer(
        out int canAccessPeer,
        int device,
        int peerDevice) =>
        hipDeviceCanAccessPeer(out canAccessPeer, device, peerDevice);

    /// <summary>
    /// Enables direct access to memory allocations on a peer device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError EnablePeerAccess(int peerDevice, uint flags) =>
        hipDeviceEnablePeerAccess(peerDevice, flags);

    /// <summary>
    /// Disables direct access to memory allocations on a peer device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ROCmError DisablePeerAccess(int peerDevice) =>
        hipDeviceDisablePeerAccess(peerDevice);

    #endregion
}
