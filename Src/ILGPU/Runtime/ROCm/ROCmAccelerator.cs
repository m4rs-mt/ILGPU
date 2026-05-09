// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using static ILGPU.Runtime.ROCm.ROCmAPI;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a ROCm/HIP accelerator.
/// </summary>
public sealed class ROCmAccelerator : Accelerator
{
    #region Instance

    /// <summary>
    /// Constructs a new ROCm accelerator.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="device">The ROCm device.</param>
    internal ROCmAccelerator(Context context, ROCmDevice device)
        : base(context, device)
    {
        // Set the active device for the current thread.
        ROCmException.ThrowIfFailed(CurrentAPI.SetDevice(device.DeviceId));

        // HIP uses per-device thread-local state, not explicit context handles.
        // Use the device id as a pseudo-native pointer for identification.
        NativePtr = new IntPtr(device.DeviceId);

        Bind();
        DefaultStream = new ROCmStream(this, IntPtr.Zero, false);

        OnAcceleratorCreated();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the ROCm device descriptor.
    /// </summary>
    public new ROCmDevice Device => (ROCmDevice)base.Device;

    /// <summary>
    /// Returns the HIP device id.
    /// </summary>
    public int DeviceId => Device.DeviceId;

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags = AcceleratorStreamFlags.Async) =>
        new ROCmStream(this, flags);

    /// <inheritdoc/>
    protected override void SynchronizeInternal() =>
        ROCmException.ThrowIfFailed(CurrentAPI.DeviceSynchronize());

    /// <inheritdoc/>
    protected override MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize) =>
        new ROCmMemoryBuffer(this, length, elementSize);

    /// <inheritdoc/>
    protected override Kernel LoadKernel(CompiledKernel compiledKernel) =>
        new ROCmKernel(this, (ROCmCompiledKernel)compiledKernel);

    /// <inheritdoc/>
    protected override void OnBind() =>
        ROCmException.ThrowIfFailed(CurrentAPI.SetDevice(DeviceId));

    /// <inheritdoc/>
    protected override void OnUnbind()
    {
        // HIP has no explicit unbind. The device stays set until changed.
    }

    #endregion

    #region Occupancy

    /// <inheritdoc/>
    protected internal override int
        EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
    {
        if (kernel is not ROCmKernel rocmKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        ROCmException.ThrowIfFailed(
            CurrentAPI.OccupancyMaxActiveBlocksPerMultiprocessor(
                out int numGroups,
                rocmKernel.FunctionHandle,
                groupSize,
                dynamicSharedMemorySizeInBytes));
        return numGroups;
    }

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize)
    {
        if (kernel is not ROCmKernel rocmKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        // HIP does not have a callback-based overload like CUDA.
        // Use the simpler fixed-size overload with 0 dynamic shared memory.
        ROCmException.ThrowIfFailed(
            CurrentAPI.OccupancyMaxPotentialBlockSize(
                out minGridSize,
                out int groupSize,
                rocmKernel.FunctionHandle,
                0,
                maxGroupSize));
        return groupSize;
    }

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize)
    {
        if (kernel is not ROCmKernel rocmKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        ROCmException.ThrowIfFailed(
            CurrentAPI.OccupancyMaxPotentialBlockSize(
                out minGridSize,
                out int groupSize,
                rocmKernel.FunctionHandle,
                dynamicSharedMemorySizeInBytes,
                maxGroupSize));
        return groupSize;
    }

    #endregion

    #region Peer Access

    /// <inheritdoc/>
    protected override bool CanAccessPeerInternal(Accelerator otherAccelerator)
    {
        if (otherAccelerator is not ROCmAccelerator rocmAccelerator)
            return false;

        ROCmException.ThrowIfFailed(
            CurrentAPI.CanAccessPeer(
                out int canAccess,
                DeviceId,
                rocmAccelerator.DeviceId));
        return canAccess != 0;
    }

    /// <inheritdoc/>
    protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
    {
        if (otherAccelerator is not ROCmAccelerator rocmAccelerator)
        {
            throw new InvalidOperationException(
                RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);
        }

        ROCmException.ThrowIfFailed(
            CurrentAPI.EnablePeerAccess(rocmAccelerator.DeviceId, 0));
    }

    /// <inheritdoc/>
    protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
    {
        if (otherAccelerator is not ROCmAccelerator rocmAccelerator)
            return;

        ROCmException.ThrowIfFailed(
            CurrentAPI.DisablePeerAccess(rocmAccelerator.DeviceId));
    }

    #endregion

    #region Page Lock Scope

    /// <inheritdoc/>
    protected override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements) =>
        new NullPageLockScope<T>(this, pinned, numElements);

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    protected override void DisposeAccelerator_Locked(bool disposing)
    {
        // HIP manages device state per-process, not per-context.
        // No explicit teardown needed; the HIP runtime handles cleanup.
    }

    #endregion
}
