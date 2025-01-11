// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Represents a Cuda accelerator.
/// </summary>
public sealed class CudaAccelerator : Accelerator
{
    #region Constants

    /// <summary>
    /// The default pitched allocation alignment in bytes (128) for all Cuda devices.
    /// </summary>
    public static readonly int PitchedAllocationAlignmentInBytes = 128;

    #endregion

    #region Static

    /// <summary>
    /// The supported PTX instruction sets (in descending order).
    /// </summary>
    public static readonly IImmutableSet<CudaInstructionSet>
        SupportedInstructionSets = ImmutableSortedSet.Create(
            Comparer<CudaInstructionSet>.Create((first, second) =>
                second.CompareTo(first)),
            CudaDriverVersionUtils.InstructionSetLookup
                .Keys
                .Where(x => x >= CudaInstructionSet.ISA_60)
                .ToArray());

    /// <summary>
    /// Resolves the memory type of the given device pointer.
    /// </summary>
    /// <param name="value">The device pointer to check.</param>
    /// <returns>The resolved memory type</returns>
    public static unsafe CudaMemoryType GetCudaMemoryType(IntPtr value)
    {
        int data = 0;
        var err = CurrentAPI.GetPointerAttribute(
                new IntPtr(Unsafe.AsPointer(ref data)),
                PointerAttribute.CU_POINTER_ATTRIBUTE_MEMORY_TYPE,
                value);
        if (err == CudaError.CUDA_ERROR_INVALID_VALUE)
            return CudaMemoryType.None;
        CudaException.ThrowIfFailed(err);
        return (CudaMemoryType)data;
    }

    /// <summary>
    /// Tries to determine the PTX instruction set to use, based on the PTX
    /// architecture and installed Cuda drivers.
    /// </summary>
    /// <param name="architecture">The PTX architecture</param>
    /// <param name="installedDriverVersion">The Cuda driver version</param>
    /// <param name="minDriverVersion">The minimum driver version.</param>
    /// <param name="instructionSet">The instruction set (if any).</param>
    /// <returns>True, if the instruction set could be determined.</returns>
    public static bool TryGetInstructionSet(
        CudaArchitecture architecture,
        CudaDriverVersion installedDriverVersion,
        out CudaDriverVersion minDriverVersion,
        out CudaInstructionSet instructionSet)
    {
        instructionSet = default;
        var architectureMinDriverVersion = CudaDriverVersionUtils
            .GetMinimumDriverVersion(architecture);
        minDriverVersion = architectureMinDriverVersion;

        foreach (var supportedSet in SupportedInstructionSets)
        {
            var instructionSetMinDriverVersion = CudaDriverVersionUtils.
                GetMinimumDriverVersion(supportedSet);
            minDriverVersion =
                architectureMinDriverVersion >= instructionSetMinDriverVersion
                ? architectureMinDriverVersion
                : instructionSetMinDriverVersion;
            if (installedDriverVersion >= minDriverVersion)
            {
                instructionSet = supportedSet;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the PTX instruction set to use, based on the PTX architecture and
    /// installed Cuda drivers.
    /// </summary>
    /// <param name="architecture">The PTX architecture</param>
    /// <param name="installedDriverVersion">The Cuda driver version</param>
    /// <returns>The PTX instruction set</returns>
    public static CudaInstructionSet GetInstructionSet(
        CudaArchitecture architecture,
        CudaDriverVersion installedDriverVersion) =>
        TryGetInstructionSet(
            architecture,
            installedDriverVersion,
            out var minDriverVersion,
            out var instructionSet)
        ? instructionSet
        : throw new NotSupportedException(
            string.Format(
                RuntimeErrorMessages.NotSupportedDriverVersion,
                installedDriverVersion,
                minDriverVersion));

    #endregion

    #region Instance

    private CudaSharedMemoryConfiguration sharedMemoryConfiguration;
    private CudaCacheConfiguration cacheConfiguration;

    /// <summary>
    /// Constructs a new Cuda accelerator.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="device">The Cuda device.</param>
    /// <param name="acceleratorFlags">The accelerator flags.</param>
    internal CudaAccelerator(
        Context context,
        CudaDevice device,
        CudaAcceleratorFlags acceleratorFlags)
        : base(context, device)
    {
        // Create new context
        CudaException.ThrowIfFailed(
            CurrentAPI.CreateContext(
                out var contextPtr,
                acceleratorFlags,
                DeviceId));
        NativePtr = contextPtr;

        Bind();
        DefaultStream = new CudaStream(this, IntPtr.Zero, false);
        InstructionSet = device.InstructionSet.GetValueOrDefault();

        // Resolve cache configuration
        CudaException.ThrowIfFailed(
            CurrentAPI.GetSharedMemoryConfig(out sharedMemoryConfiguration));
        CudaException.ThrowIfFailed(
            CurrentAPI.GetCacheConfig(out cacheConfiguration));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the Cuda device.
    /// </summary>
    public new CudaDevice Device => base.Device.AsNotNullCast<CudaDevice>();

    /// <summary>
    /// Returns the device id.
    /// </summary>
    public int DeviceId => Device.DeviceId;

    /// <summary>
    /// Returns the current driver version.
    /// </summary>
    public CudaDriverVersion DriverVersion => Device.DriverVersion;

    /// <summary>
    /// Returns the PTX architecture.
    /// </summary>
    public CudaArchitecture Architecture =>
        Device.Architecture.GetValueOrDefault();

    /// <summary>
    /// Returns the PTX instruction set.
    /// </summary>
    public CudaInstructionSet InstructionSet { get; }

    /// <summary>
    /// Returns the clock rate.
    /// </summary>
    public int ClockRate => Device.ClockRate;

    /// <summary>
    /// Returns the memory clock rate.
    /// </summary>
    public int MemoryClockRate => Device.MemoryClockRate;

    /// <summary>
    /// Returns the memory clock rate.
    /// </summary>
    public int MemoryBusWidth => Device.MemoryBusWidth;

    /// <summary>
    /// Returns L2 cache size.
    /// </summary>
    public int L2CacheSize => Device.L2CacheSize;

    /// <summary>
    /// Returns the maximum shared memory size per multiprocessor.
    /// </summary>
    public int MaxSharedMemoryPerMultiprocessor =>
        Device.MaxSharedMemoryPerMultiprocessor;

    /// <summary>
    /// Returns the total number of registers per multiprocessor.
    /// </summary>
    public int TotalNumRegistersPerMultiprocessor =>
        Device.TotalNumRegistersPerMultiprocessor;

    /// <summary>
    /// Returns the total number of registers per group.
    /// </summary>
    public int TotalNumRegistersPerGroup => Device.TotalNumRegistersPerGroup;

    /// <summary>
    /// Returns the maximum memory pitch in bytes.
    /// </summary>
    public long MaxMemoryPitch => Device.MaxMemoryPitch;

    /// <summary>
    /// Returns the number of concurrent copy engines (if any, result > 0).
    /// </summary>
    public int NumConcurrentCopyEngines => Device.NumConcurrentCopyEngines;

    /// <summary>
    /// Returns true if this device has ECC support.
    /// </summary>
    public bool HasECCSupport => Device.HasECCSupport;

    /// <summary>
    /// Returns true if this device supports managed memory allocations.
    /// </summary>
    public bool SupportsManagedMemory => Device.SupportsManagedMemory;

    /// <summary>
    /// Returns true if this device support compute preemption.
    /// </summary>
    public bool SupportsComputePreemption => Device.SupportsComputePreemption;

    /// <summary>
    /// Returns the current device driver mode.
    /// </summary>
    public DeviceDriverMode DriverMode => Device.DriverMode;

    /// <summary>
    /// Returns the PCI domain id.
    /// </summary>
    public int PCIDomainId => Device.PCIDomainId;

    /// <summary>
    /// Returns the PCI bus id.
    /// </summary>
    public int PCIBusId => Device.PCIBusId;

    /// <summary>
    /// Returns the PCI device id.
    /// </summary>
    public int PCIDeviceId => Device.PCIDeviceId;

    /// <summary>
    /// Returns an NVML library compatible PCI bus id.
    /// </summary>
    public string NVMLPCIBusId => Device.NVMLPCIBusId;

    /// <summary>
    /// Gets or sets the current shared-memory configuration.
    /// </summary>
    public CudaSharedMemoryConfiguration SharedMemoryConfiguration
    {
        get => sharedMemoryConfiguration;
        set
        {
            if (value < CudaSharedMemoryConfiguration.Default ||
                value > CudaSharedMemoryConfiguration.EightByteBankSize)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            sharedMemoryConfiguration = value;
            Bind();
            CudaException.ThrowIfFailed(
                CurrentAPI.SetSharedMemoryConfig(value));
        }
    }

    /// <summary>
    /// Gets or sets the current cache configuration.
    /// </summary>
    public CudaCacheConfiguration CacheConfiguration
    {
        get => cacheConfiguration;
        set
        {
            if (value < CudaCacheConfiguration.Default ||
                value > CudaCacheConfiguration.PreferEqual)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            cacheConfiguration = value;
            Bind();
            CudaException.ThrowIfFailed(
                CurrentAPI.SetCacheConfig(value));
        }
    }

    /// <summary>
    /// Returns the capabilities of this accelerator.
    /// </summary>
    public new CudaCapabilityContext Capabilities =>
        base.Capabilities.AsNotNullCast<CudaCapabilityContext>();

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize) =>
        new CudaMemoryBuffer(this, length, elementSize);

    /// <inheritdoc/>
    protected override Kernel LoadKernel(CompiledKernel compiledKernel)
    {
        var cudaCompiledKernel = (CudaCompiledKernel)compiledKernel;

        // Load new compiled program kernel
        return new CudaKernel(this, cudaCompiledKernel);
    }

    /// <summary>
    /// Create a Cuda stream with the specified flags.
    /// </summary>
    protected override AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags = AcceleratorStreamFlags.Async) =>
        new CudaStream(this, flags);

    /// <summary>
    /// Creates a <see cref="CudaStream"/> object using an externally created stream.
    /// </summary>
    /// <param name="ptr">A pointer to the externally created stream.</param>
    /// <param name="responsible">
    /// Whether ILGPU is responsible of disposing this stream.
    /// </param>
    /// <returns>The created stream.</returns>
    public CudaStream CreateStream(IntPtr ptr, bool responsible) =>
        new(this, ptr, responsible);

    /// <summary cref="Accelerator.Synchronize"/>
    protected override void SynchronizeInternal() =>
        CudaException.ThrowIfFailed(
            CurrentAPI.SynchronizeContext());

    /// <summary cref="Accelerator.OnBind"/>
    protected override void OnBind() =>
        CudaException.ThrowIfFailed(
            CurrentAPI.SetCurrentContext(NativePtr));

    /// <summary cref="Accelerator.OnUnbind"/>
    protected override void OnUnbind() =>
        CudaException.ThrowIfFailed(
            CurrentAPI.SetCurrentContext(IntPtr.Zero));

    /// <summary>
    /// Queries the amount of free memory.
    /// </summary>
    /// <returns>The amount of free memory in bytes.</returns>
    public long GetFreeMemory()
    {
        Bind();
        CudaException.ThrowIfFailed(
            CurrentAPI.GetMemoryInfo(out long free, out long _));
        return free;
    }

    #endregion

    #region Peer Access

    /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
    protected override bool CanAccessPeerInternal(Accelerator otherAccelerator)
    {
        if (otherAccelerator is not CudaAccelerator cudaAccelerator)
            return false;

        CudaException.ThrowIfFailed(
            CurrentAPI.CanAccessPeer(
                out int canAccess,
                DeviceId,
                cudaAccelerator.DeviceId));
        return canAccess != 0;
    }

    /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
    protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
    {
        if (otherAccelerator is not CudaAccelerator cudaAccelerator)
        {
            throw new InvalidOperationException(
                RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);
        }

        CudaException.ThrowIfFailed(
            CurrentAPI.EnablePeerAccess(cudaAccelerator.NativePtr, 0));
    }

    /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
    protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
    {
        var cudaAccelerator = otherAccelerator as CudaAccelerator;
        Debug.Assert(cudaAccelerator != null, "Invalid EnablePeerAccess method");

        CudaException.ThrowIfFailed(
            CurrentAPI.DisablePeerAccess(cudaAccelerator.NativePtr));
    }

    #endregion

    #region Occupancy

    /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(
    /// Kernel, int, int)"/>
    protected internal override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
        Kernel kernel,
        int groupSize,
        int dynamicSharedMemorySizeInBytes)
    {
        if (kernel is not CudaKernel cudaKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        CudaException.ThrowIfFailed(
            CurrentAPI.ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
                out int numGroups,
                cudaKernel.FunctionPtr,
                groupSize,
                new IntPtr(dynamicSharedMemorySizeInBytes)));
        return numGroups;
    }

    /// <summary cref="Accelerator.EstimateGroupSizeInternal(
    /// Kernel, Func{int, int}, int, out int)"/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize)
    {
        if (kernel is not CudaKernel cudaKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        CudaException.ThrowIfFailed(
            CurrentAPI.ComputeOccupancyMaxPotentialBlockSize(
                out minGridSize,
                out int groupSize,
                cudaKernel.FunctionPtr,
                new ComputeDynamicMemorySizeForBlockSize(
                    targetGroupSize =>
                        new IntPtr(computeSharedMemorySize(targetGroupSize))),
                IntPtr.Zero,
                maxGroupSize));
        return groupSize;
    }

    /// <summary cref="Accelerator.EstimateGroupSizeInternal(
    /// Kernel, int, int, out int)"/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize)
    {
        if (kernel is not CudaKernel cudaKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        CudaException.ThrowIfFailed(
            CurrentAPI.ComputeOccupancyMaxPotentialBlockSize(
                out minGridSize,
                out int groupSize,
                cudaKernel.FunctionPtr,
                null,
                new IntPtr(dynamicSharedMemorySizeInBytes),
                maxGroupSize));
        return groupSize;
    }

    #endregion

    #region Page Lock Scope

    /// <inheritdoc/>
    protected unsafe override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements) =>
        Device.SupportsMappingHostMemory && numElements > 0
        ? new CudaPageLockScope<T>(this, pinned, numElements)
        : new NullPageLockScope<T>(this, pinned, numElements);

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the current Cuda context.
    /// </summary>
    protected override void DisposeAccelerator_Locked(bool disposing) =>
        // Dispose the current context
        CudaException.VerifyDisposed(
            disposing,
            CurrentAPI.DestroyContext(NativePtr));

    #endregion
}
