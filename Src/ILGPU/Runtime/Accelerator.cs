// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ILGPU.Runtime;

/// <summary>
/// Represents the general type of an accelerator.
/// </summary>
public enum AcceleratorType : int
{
    /// <summary>
    /// Represents an implicitly defined accelerator that is omnipresent.
    /// </summary>
    None,

    /// <summary>
    /// Represents a debug accelerator.
    /// </summary>
    Debug,

    /// <summary>
    /// Represents a Cuda accelerator.
    /// </summary>
    Cuda,

    /// <summary>
    /// Represents an OpenCL accelerator.
    /// </summary>
    OpenCL,
}

/// <summary>
/// An abstract builder type for accelerators.
/// </summary>
public interface IAcceleratorBuilder
{
    /// <summary>
    /// Returns the type of the associated accelerator.
    /// </summary>
    AcceleratorType AcceleratorType { get; }

    /// <summary>
    /// Creates a new accelerator instance.
    /// </summary>
    /// <param name="context">The context instance.</param>
    /// <returns>The created accelerator instance.</returns>
    Accelerator CreateAccelerator(Context context);
}

/// <summary>
/// Represents a general abstract accelerator.
/// </summary>
/// <remarks>Members of this class are not thread safe.</remarks>
public abstract partial class Accelerator : DisposeBase, IDevice
{
    #region Events

    /// <summary>
    /// Will be raised if the accelerator is disposed.
    /// </summary>
    public event EventHandler? Disposed;

    #endregion

    #region Instance

    /// <summary>
    /// Main object for accelerator synchronization.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly object _lock = new();

    /// <summary>
    /// The current volatile native pointer of this instance.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private volatile IntPtr _nativePtr;

    /// <summary>
    /// Constructs a new accelerator.
    /// </summary>
    /// <param name="context">The target context.</param>
    /// <param name="device">The device.</param>
    protected Accelerator(Context context, Device device)
    {
        Context = context;
        Device = device;
        WarpSize = device.WarpSize;

        MaxNumThreads = device.MaxNumThreads;
        MaxNumThreadsPerGroup = device.MaxNumThreadsPerGroup;
        OptimalKernelSize = device.OptimalKernelSize;
        _kernels = new(Context.NumCompiledKernels);

        InitGC();
        LoadKernels();

        // NB: Initialized later by derived classes.
        DefaultStream = Utilities.InitNotNullable<AcceleratorStream>();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the internal unique accelerator instance id.
    /// </summary>
    internal InstanceId InstanceId { get; } = InstanceId.CreateNew();

    /// <summary>
    /// Returns the associated ILGPU context.
    /// </summary>
    public Context Context { get; }

    /// <summary>
    /// Returns the parent device.
    /// </summary>
    public Device Device { get; }

    /// <summary>
    /// Returns the default stream of this accelerator.
    /// </summary>
    public AcceleratorStream DefaultStream { get; protected set; }

    /// <summary>
    /// Returns the current native accelerator pointer.
    /// </summary>
    public IntPtr NativePtr
    {
        get => _nativePtr;
        protected set => _nativePtr = value;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoked when the accelerator instance has been created.
    /// </summary>
    protected void OnAcceleratorCreated() => Context.OnAcceleratorCreated(this);

    /// <summary>
    /// Creates a new accelerator stream.
    /// </summary>
    /// <param name="flags">The stream flags to use.</param>
    /// <returns>The created accelerator stream.</returns>
    public AcceleratorStream CreateStream(
        AcceleratorStreamFlags flags = AcceleratorStreamFlags.Async)
    {
        Bind(); return CreateStreamInternal(flags);
    }

    /// <summary>
    /// Creates a new accelerator stream.
    /// </summary>
    /// <param name="flags">The stream flags to use.</param>
    /// <returns>The created accelerator stream.</returns>
    protected abstract AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags = AcceleratorStreamFlags.Async);

    /// <summary>
    /// Synchronizes pending operations.
    /// </summary>
    public void Synchronize()
    {
        Bind(); SynchronizeInternal();
    }

    /// <summary>
    /// Synchronizes pending operations.
    /// </summary>
    protected abstract void SynchronizeInternal();

    #endregion

    #region Allocation

    /// <summary>
    /// Allocates a buffer with the specified size in bytes on this accelerator.
    /// </summary>
    /// <param name="length">The number of elements to allocate.</param>
    /// <param name="elementSize">The size of a single element in bytes.</param>
    /// <returns>An allocated buffer on this accelerator.</returns>
    public MemoryBuffer AllocateRaw(long length, int elementSize)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (elementSize < 1)
            throw new ArgumentOutOfRangeException(nameof(elementSize));

        Bind();
        return AllocateRawInternal(length, elementSize);
    }

    /// <summary>
    /// Allocates a buffer with the specified number of elements on this accelerator.
    /// </summary>
    /// <param name="length">The number of elements to allocate.</param>
    /// <param name="elementSize">The size of a single element in bytes.</param>
    /// <returns>An allocated buffer on this accelerator.</returns>
    protected abstract MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize);

    #endregion

    #region Occupancy

    /// <summary>
    /// Estimates the occupancy of the given kernel with the given group size of a
    /// single multiprocessor.
    /// </summary>
    /// <typeparam name="TIndex">The index type of the group dimension.</typeparam>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="groupDim">The group dimension.</param>
    /// <returns>
    /// The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.
    /// </returns>
    protected internal float EstimateOccupancyPerMultiprocessor<TIndex>(
        Kernel kernel,
        TIndex groupDim)
        where TIndex : struct, IIndex =>
        EstimateOccupancyPerMultiprocessor(kernel, groupDim.GetIntSize());

    /// <summary>
    /// Estimates the occupancy of the given kernel with the given group size of a
    /// single multiprocessor.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="groupSize">The number of threads per group.</param>
    /// <returns>
    /// The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.
    /// </returns>
    protected internal float EstimateOccupancyPerMultiprocessor(
        Kernel kernel,
        int groupSize) =>
        EstimateOccupancyPerMultiprocessor(kernel, groupSize, 0);

    /// <summary>
    /// Estimates the occupancy of the given kernel with the given group size of a
    /// single multiprocessor.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="groupSize">The number of threads per group.</param>
    /// <param name="dynamicSharedMemorySizeInBytes">
    /// The required dynamic shared-memory size in bytes.
    /// </param>
    /// <returns>
    /// The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.
    /// </returns>
    protected internal float EstimateOccupancyPerMultiprocessor(
        Kernel kernel,
        int groupSize,
        int dynamicSharedMemorySizeInBytes)
    {
        var maxActiveGroups = EstimateMaxActiveGroupsPerMultiprocessor(
            kernel,
            groupSize,
            dynamicSharedMemorySizeInBytes);
        return (float)(maxActiveGroups * groupSize) / MaxNumThreadsPerGroup;
    }

    /// <summary>
    /// Estimates the maximum number of active groups per multiprocessor for the
    /// given kernel.
    /// </summary>
    /// <typeparam name="TIndex">The index type of the group dimension.</typeparam>
    /// <param name="kernel">The kernel used for the computation of the maximum
    /// number of active groups.</param>
    /// <param name="groupDim">The group dimension.</param>
    /// <returns>
    /// The maximum number of active groups per multiprocessor for the given kernel.
    /// </returns>
    protected internal int EstimateMaxActiveGroupsPerMultiprocessor<TIndex>(
        Kernel kernel,
        TIndex groupDim)
        where TIndex : struct, IIndex =>
        EstimateMaxActiveGroupsPerMultiprocessor(kernel, groupDim.GetIntSize());

    /// <summary>
    /// Estimates the maximum number of active groups per multiprocessor for the
    /// given kernel.
    /// </summary>
    /// <param name="kernel">The kernel used for the computation of the maximum
    /// number of active groups.</param>
    /// <param name="groupSize">The number of threads per group.</param>
    /// <returns>
    /// The maximum number of active groups per multiprocessor for the given kernel.
    /// </returns>
    protected internal int EstimateMaxActiveGroupsPerMultiprocessor(
        Kernel kernel,
        int groupSize) =>
        EstimateMaxActiveGroupsPerMultiprocessor(kernel, groupSize, 0);

    /// <summary>
    /// Estimates the maximum number of active groups per multiprocessor for the
    /// given kernel.
    /// </summary>
    /// <param name="kernel">The kernel used for the computation of the maximum
    /// number of active groups.</param>
    /// <param name="groupSize">The number of threads per group.</param>
    /// <param name="dynamicSharedMemorySizeInBytes">
    /// The required dynamic shared-memory size in bytes.
    /// </param>
    /// <returns>
    /// The maximum number of active groups per multiprocessor for the given kernel.
    /// </returns>
    protected internal int EstimateMaxActiveGroupsPerMultiprocessor(
        Kernel kernel,
        int groupSize,
        int dynamicSharedMemorySizeInBytes)
    {
        if (groupSize < 1)
            throw new ArgumentNullException(nameof(groupSize));
        if (dynamicSharedMemorySizeInBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dynamicSharedMemorySizeInBytes));
        }
        Bind();
        return EstimateMaxActiveGroupsPerMultiprocessorInternal(
            kernel,
            groupSize,
            dynamicSharedMemorySizeInBytes);
    }

    /// <summary>
    /// Estimates the maximum number of active groups per multiprocessor for the
    /// given kernel.
    /// </summary>
    /// <param name="kernel">The kernel used for the computation of the maximum
    /// number of active groups.</param>
    /// <param name="groupSize">The number of threads per group.</param>
    /// <param name="dynamicSharedMemorySizeInBytes">
    /// The required dynamic shared-memory size in bytes.
    /// </param>
    /// <remarks>
    /// Note that the arguments do not have to be verified since they are already
    /// verified.
    /// </remarks>
    /// <returns>
    /// The maximum number of active groups per multiprocessor for the given kernel.
    /// </returns>
    protected internal abstract int EstimateMaxActiveGroupsPerMultiprocessorInternal(
        Kernel kernel,
        int groupSize,
        int dynamicSharedMemorySizeInBytes);

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal int EstimateGroupSize(Kernel kernel) =>
        EstimateGroupSize(kernel, 0, 0, out var _);

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal int EstimateGroupSize(Kernel kernel, out int minGridSize) =>
        EstimateGroupSize(kernel, 0, 0, out minGridSize);

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="dynamicSharedMemorySizeInBytes">
    /// The required dynamic shared-memory size in bytes.
    /// </param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal int EstimateGroupSize(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        out int minGridSize) =>
        EstimateGroupSize(
            kernel,
            dynamicSharedMemorySizeInBytes,
            0,
            out minGridSize);

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="computeSharedMemorySize">
    /// A callback to compute the required amount of shared memory in bytes for a
    /// given group size.
    /// </param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal int EstimateGroupSize(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        out int minGridSize) =>
        EstimateGroupSize(
            kernel,
            computeSharedMemorySize,
            0,
            out minGridSize);

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="computeSharedMemorySize">
    /// A callback to compute the required amount of shared memory in bytes for a
    /// given group size.
    /// </param>
    /// <param name="maxGroupSize">
    /// The maximum group-size limit on a single multiprocessor.
    /// </param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal int EstimateGroupSize(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize)
    {
        if (maxGroupSize < 0)
            throw new ArgumentOutOfRangeException(nameof(maxGroupSize));
        Bind();
        return EstimateGroupSizeInternal(
            kernel,
            computeSharedMemorySize,
            maxGroupSize,
            out minGridSize);
    }

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="computeSharedMemorySize">
    /// A callback to compute the required amount of shared memory in bytes for a
    /// given group size.
    /// </param>
    /// <param name="maxGroupSize">
    /// The maximum group-size limit on a single multiprocessor.
    /// </param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <remarks>
    /// Note that the arguments do not have to be verified since they are already
    /// verified.
    /// </remarks>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal abstract int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize);

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="dynamicSharedMemorySizeInBytes">
    /// The required dynamic shared-memory size in bytes.
    /// </param>
    /// <param name="maxGroupSize">
    /// The maximum group-size limit on a single multiprocessor.
    /// </param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal int EstimateGroupSize(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize)
    {
        if (maxGroupSize < 0)
            throw new ArgumentOutOfRangeException(nameof(maxGroupSize));
        if (dynamicSharedMemorySizeInBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dynamicSharedMemorySizeInBytes));
        }
        Bind();
        return EstimateGroupSizeInternal(
            kernel,
            dynamicSharedMemorySizeInBytes,
            maxGroupSize,
            out minGridSize);
    }

    /// <summary>
    /// Estimates a group size to gain maximum occupancy on this device.
    /// </summary>
    /// <param name="kernel">The kernel used for the estimation.</param>
    /// <param name="dynamicSharedMemorySizeInBytes">
    /// The required dynamic shared-memory size in bytes.
    /// </param>
    /// <param name="maxGroupSize">
    /// The maximum group-size limit on a single multiprocessor.
    /// </param>
    /// <param name="minGridSize">
    /// The minimum grid size to gain maximum occupancy on this device.
    /// </param>
    /// <remarks>
    /// Note that the arguments do not have to be verified since they are already
    /// verified.
    /// </remarks>
    /// <returns>
    /// An estimated group size to gain maximum occupancy on this device.
    /// </returns>
    protected internal abstract int EstimateGroupSizeInternal(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize);

    #endregion

    #region IDevice

    /// <summary>
    /// Returns the type of the accelerator.
    /// </summary>
    public AcceleratorType AcceleratorType => Device.AcceleratorType;

    /// <summary>
    /// Returns the name of the device.
    /// </summary>
    public string Name => Device.Name;

    /// <summary>
    /// Returns the memory size in bytes.
    /// </summary>
    public long MemorySize => Device.MemorySize;

    /// <summary>
    /// Returns the maximum number of threads of this accelerator.
    /// </summary>
    public int MaxNumThreads { get; }

    /// <summary>
    /// Returns the maximum number of threads in a group.
    /// </summary>
    public int MaxNumThreadsPerGroup { get; }

    /// <summary>
    /// Returns the maximum number of shared memory per thread group in bytes.
    /// </summary>
    public int MaxSharedMemoryPerGroup => Device.MaxSharedMemoryPerGroup;

    /// <summary>
    /// Returns the maximum number of constant memory in bytes.
    /// </summary>
    public int MaxConstantMemory => Device.MaxConstantMemory;

    /// <summary>
    /// Return the warp size.
    /// </summary>
    public int WarpSize { get; protected set; }

    /// <summary>
    /// Returns the number of available multiprocessors.
    /// </summary>
    public int NumMultiprocessors => Device.NumMultiprocessors;

    /// <summary>
    /// Returns the maximum number of threads per multiprocessor.
    /// </summary>
    public int MaxNumThreadsPerMultiprocessor { get; }

    /// <summary>
    /// Returns the supported capabilities of this accelerator.
    /// </summary>
    public CapabilityContext Capabilities => Device.Capabilities;

    /// <summary>
    /// Returns a kernel extent (a grouped index) with the maximum number of groups
    /// using the maximum number of threads per group to target all threads in parallel
    /// on this device.
    /// </summary>
    public KernelSize OptimalKernelSize { get; }

    /// <summary>
    /// Prints device information to the given text writer.
    /// </summary>
    /// <param name="writer">The target text writer to write to.</param>
    public void PrintInformation(TextWriter writer) =>
        Device.PrintInformation(writer);

    #endregion

    #region Page Lock Scope

    /// <summary>
    /// Creates a page lock on the already pinned array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="pinned">The already pinned array.</param>
    /// <param name="numElements">The number of elements in the array.</param>
    /// <returns>A page lock scope.</returns>
    /// <remarks>
    /// The array must already be pinned, otherwise the behavior is undefined.
    /// </remarks>
    public unsafe PageLockScope<T> CreatePageLockFromPinned<T>(
        IntPtr pinned,
        long numElements)
        where T : unmanaged =>
        numElements < 0L
        ? throw new ArgumentOutOfRangeException(nameof(numElements))
        : CreatePageLockFromPinnedInternal<T>(pinned, numElements);

    /// <summary>
    /// Creates a page lock on the already pinned array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="pinned">The already pinned array.</param>
    /// <returns>A page lock scope.</returns>
    /// <remarks>
    /// The array must already be pinned, otherwise the behavior is undefined.
    /// </remarks>
    public unsafe PageLockScope<T> CreatePageLockFromPinned<T>(T[] pinned)
        where T : unmanaged
    {
        // The array is already pinned - "fixing" it to obtain the memory address.
        fixed (T* ptr = pinned)
        {
            return CreatePageLockFromPinned<T>(
                new IntPtr(ptr),
                pinned.Length);
        }
    }

    /// <summary>
    /// Creates a page lock on the already pinned array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="pinned">The already pinned array.</param>
    /// <param name="numElements">The number of elements in the array.</param>
    /// <returns>A page lock scope.</returns>
    /// <remarks>
    /// The array must already be pinned, otherwise the behavior is undefined.
    /// </remarks>
    protected abstract PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements)
        where T : unmanaged;

    /// <summary>
    /// Computes number of groups with optimal group size.
    /// </summary>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <returns>The number of groups to launch.</returns>
    public long ComputeNumGroupsWithOptimalGroupSize(long numDataElements) =>
        numDataElements < 1
        ? throw new ArgumentOutOfRangeException(nameof(numDataElements))
        : OptimalKernelSize.ComputeNumGroups(numDataElements);

    /// <summary>
    /// Computes a kernel config using optimal group size.
    /// </summary>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <returns>The kernel configuration for launch.</returns>
    public KernelConfig ComputeKernelConfig(long numDataElements) => new(
        ComputeNumGroupsWithOptimalGroupSize(numDataElements),
        OptimalKernelSize.GroupSize);

    /// <summary>
    /// Returns a kernel extent (a grouped index) with the maximum number of groups
    /// using the maximum number of threads per group to launch common grid-stride
    /// loop kernels.
    /// </summary>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <param name="numIterationsPerGroup">
    /// The number of loop iterations per group.
    /// </param>
    public KernelConfig ComputeGridStrideKernelConfig(
        long numDataElements,
        out int numIterationsPerGroup) =>
        OptimalKernelSize.ComputeGridStrideKernelConfig(
            numDataElements,
            out numIterationsPerGroup);

    #endregion

    #region IDisposable

    /// <summary cref="DisposeBase.Dispose(bool)"/>
    protected sealed override void Dispose(bool disposing)
    {
        Debug.Assert(NativePtr != IntPtr.Zero, "Invalid native pointer");

        // Dispose all accelerator extensions
        base.Dispose(disposing);

        // The main disposal functionality is locked to avoid parallel dispose
        // operations that can happen due to a parallel invocation of the .Net GC
        lock (_lock)
        {
            // Invoke the disposal event
            Disposed?.Invoke(this, EventArgs.Empty);

            // Bind the current instance
            Bind();

            // Dispose all extensions
            DisposeExtensions_Locked();

            // Dispose all child objects
            DisposeChildObjects_Locked(disposing);

            // Dispose the accelerator instance
            DisposeAccelerator_Locked(disposing);

            // Dispose all kernels
            DisposeKernels_Locked();

            // Wait for the GC thread to terminate
            DisposeGC_Locked();

            // Unbind the current accelerator and reset it to no accelerator
            OnUnbind();
            _currentAccelerator = null;

            // Clear the native pointer
            NativePtr = IntPtr.Zero;

            // Commit all changes
            Thread.MemoryBarrier();
        }
    }

    /// <summary>
    /// Disposes this accelerator instance (synchronized with the current main
    /// synchronization object of this accelerator).
    /// </summary>
    /// <param name="disposing">
    /// True, if the method is not called by the finalizer.
    /// </param>
    protected abstract void DisposeAccelerator_Locked(bool disposing);

    #endregion

    #region Object

    /// <summary>
    /// Returns the string representation of this accelerator.
    /// </summary>
    /// <returns>The string representation of this accelerator.</returns>
    public override string ToString() => Device.ToString();

    #endregion
}
