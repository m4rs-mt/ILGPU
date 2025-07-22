// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents a Metal compute accelerator backed by an MTLDevice.
/// </summary>
public sealed class MetalAccelerator : Accelerator
{
    /// <summary>
    /// The default command queue handle.
    /// </summary>
    private IntPtr _commandQueue;

    /// <summary>
    /// Creates a new Metal accelerator from a device descriptor.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="device">The Metal device descriptor.</param>
    internal MetalAccelerator(Context context, MetalDevice device)
        : base(context, device)
    {
        DeviceHandle = device.DeviceHandle;
        MetalAPI.Retain(DeviceHandle);
        NativePtr = DeviceHandle;

        // Create the default command queue.
        _commandQueue = MetalAPI.NewCommandQueue(DeviceHandle);
        MetalException.ThrowIfNull(
            _commandQueue,
            IntPtr.Zero,
            MetalError.CommandQueueCreationFailed,
            "Failed to create default Metal command queue");

        Bind();
        DefaultStream = new MetalStream(this, _commandQueue, false);

        OnAcceleratorCreated();
    }

    /// <summary>
    /// Returns the Metal device descriptor.
    /// </summary>
    public new MetalDevice Device =>
        (MetalDevice)base.Device;

    /// <summary>
    /// Returns the native Metal device handle.
    /// </summary>
    internal IntPtr DeviceHandle { get; private set; }

    /// <inheritdoc/>
    protected override AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags = AcceleratorStreamFlags.Async) =>
        new MetalStream(this, flags);

    /// <inheritdoc/>
    protected override void SynchronizeInternal() =>
        DefaultStream.Synchronize();

    /// <inheritdoc/>
    protected override MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize) =>
        new MetalMemoryBuffer(this, length, elementSize);

    /// <inheritdoc/>
    protected override Kernel LoadKernel(CompiledKernel compiledKernel) =>
        new MetalKernel(this, (MetalCompiledKernel)compiledKernel);

    /// <inheritdoc/>
    protected override void OnBind()
    {
        // Metal has no thread-local context model. MTLDevice is thread-safe.
    }

    /// <inheritdoc/>
    protected override void OnUnbind()
    {
        // Metal has no thread-local context model. MTLDevice is thread-safe.
    }

    /// <inheritdoc/>
    protected internal override int
        EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
    {
        // Metal has no occupancy API. Use a simple heuristic based on the
        // device's max threads per threadgroup.
        if (groupSize <= 0)
            return 1;
        return Math.Max(1, MaxNumThreadsPerGroup / groupSize);
    }

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize)
    {
        // Use pipeline state's maxTotalThreadsPerThreadgroup if available,
        // otherwise fall back to device limits.
        int groupSize = GetOptimalGroupSize(kernel, maxGroupSize);
        minGridSize = Device.NumMultiprocessors;
        return groupSize;
    }

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize)
    {
        int groupSize = GetOptimalGroupSize(kernel, maxGroupSize);
        minGridSize = Device.NumMultiprocessors;
        return groupSize;
    }

    /// <inheritdoc/>
    protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) =>
        false;

    /// <inheritdoc/>
    protected override void EnablePeerAccessInternal(Accelerator otherAccelerator) =>
        throw new NotSupportedException("Metal does not support peer access");

    /// <inheritdoc/>
    protected override void DisablePeerAccessInternal(Accelerator otherAccelerator) =>
        throw new NotSupportedException("Metal does not support peer access");

    /// <inheritdoc/>
    protected override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements) =>
        new NullPageLockScope<T>(this, pinned, numElements);

    /// <inheritdoc/>
    protected override void DisposeAccelerator_Locked(bool disposing)
    {
        if (_commandQueue != IntPtr.Zero)
        {
            MetalAPI.Release(_commandQueue);
            _commandQueue = IntPtr.Zero;
        }

        if (DeviceHandle != IntPtr.Zero)
            MetalAPI.Release(DeviceHandle);
    }

    /// <summary>
    /// Computes an optimal group size for a kernel, rounded to a warp multiple.
    /// </summary>
    private int GetOptimalGroupSize(Kernel kernel, int maxGroupSize)
    {
        int limit = MaxNumThreadsPerGroup;

        // If we have a Metal kernel, use pipeline state limits.
        if (kernel is MetalKernel metalKernel &&
            metalKernel.PipelineState != IntPtr.Zero)
        {
            var pipelineMax = (int)MetalAPI
                .GetPipelineMaxThreadsPerThreadgroup(metalKernel.PipelineState);
            limit = Math.Min(limit, pipelineMax);
        }

        if (maxGroupSize > 0)
            limit = Math.Min(limit, maxGroupSize);

        // Round down to a warp size multiple.
        int warpSize = WarpSize;
        return Math.Max(warpSize, (limit / warpSize) * warpSize);
    }
}
