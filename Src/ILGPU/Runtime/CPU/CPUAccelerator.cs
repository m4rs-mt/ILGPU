// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Represents a CPU compute accelerator for vectorized CPU execution.
/// </summary>
/// <remarks>
/// CPU kernels are compiled to C# by ILGPUC and execute via static
/// <c>Launch()</c> methods on each compiled kernel class. The accelerator
/// provides the runtime context (streams, memory allocation, device info)
/// but does not dispatch kernels directly.
/// </remarks>
public sealed class CPUAccelerator : Accelerator
{
    /// <summary>
    /// Creates a new CPU accelerator from a device descriptor.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="device">The CPU device descriptor.</param>
    internal CPUAccelerator(Context context, CPUDevice device)
        : base(context, device)
    {
        // Sentinel value — CPU has no native device pointer, but the base
        // class Dispose asserts NativePtr != IntPtr.Zero.
        NativePtr = new IntPtr(1);

        Bind();
        DefaultStream = new CPUStream(this, AcceleratorStreamFlags.None);

        OnAcceleratorCreated();
    }

    /// <summary>
    /// Returns the CPU device descriptor.
    /// </summary>
    public new CPUDevice Device => base.Device.AsNotNullCast<CPUDevice>();

    #region Streams

    /// <inheritdoc/>
    protected override AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags = AcceleratorStreamFlags.Async) =>
        new CPUStream(this, flags);

    /// <inheritdoc/>
    protected override void SynchronizeInternal()
    {
        // No-op: CPU execution is synchronous.
    }

    #endregion

    #region Memory

    /// <inheritdoc/>
    protected override MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize) =>
        CPUMemoryBuffer.Create(this, length, elementSize);

    #endregion

    #region Kernels

    /// <inheritdoc/>
    protected override Kernel LoadKernel(CompiledKernel compiledKernel) =>
        new CPUKernel(this, (CPUCompiledKernel)compiledKernel);

    #endregion

    #region Binding

    /// <inheritdoc/>
    protected override void OnBind()
    {
        // CPU has no thread-local context model.
    }

    /// <inheritdoc/>
    protected override void OnUnbind()
    {
        // CPU has no thread-local context model.
    }

    #endregion

    #region Peer Access

    /// <inheritdoc/>
    protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) =>
        otherAccelerator is CPUAccelerator;

    /// <inheritdoc/>
    protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
    {
        // CPU memory is always accessible from any CPU accelerator.
    }

    /// <inheritdoc/>
    protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
    {
        // CPU memory is always accessible from any CPU accelerator.
    }

    #endregion

    #region Page Locking

    /// <inheritdoc/>
    protected override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements) =>
        new CPUPageLockScope<T>(pinned, numElements);

    #endregion

    #region Occupancy

    /// <inheritdoc/>
    protected internal override int
        EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
    {
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
        minGridSize = Device.NumMultiprocessors;
        return GetOptimalGroupSize(maxGroupSize);
    }

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize)
    {
        minGridSize = Device.NumMultiprocessors;
        return GetOptimalGroupSize(maxGroupSize);
    }

    /// <summary>
    /// Computes an optimal group size, clamped to the device limit.
    /// </summary>
    private int GetOptimalGroupSize(int maxGroupSize)
    {
        int limit = MaxNumThreadsPerGroup;
        if (maxGroupSize > 0)
            limit = Math.Min(limit, maxGroupSize);
        return limit;
    }

    #endregion

    #region Dispose

    /// <inheritdoc/>
    protected override void DisposeAccelerator_Locked(bool disposing)
    {
        // No native resources to release.
    }

    #endregion
}
