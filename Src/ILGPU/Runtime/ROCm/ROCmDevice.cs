// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using static ILGPU.Runtime.ROCm.ROCmAPI;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a single ROCm/HIP device.
/// </summary>
public sealed class ROCmDevice : Device, IDeviceAcceleratorTypeInfo
{
    #region Static

    /// <summary>
    /// Returns the accelerator type for ROCm devices.
    /// </summary>
    static AcceleratorType IDeviceAcceleratorTypeInfo.AcceleratorType =>
        AcceleratorType.ROCm;

    /// <summary>
    /// Detects ROCm devices.
    /// </summary>
    /// <returns>All detected ROCm devices.</returns>
    public static ImmutableArray<Device> GetDevices()
    {
        var registry = new DeviceRegistry();
        GetDevices(_ => true, registry);
        return registry.ToImmutable();
    }

    /// <summary>
    /// Detects ROCm devices matching the given predicate.
    /// </summary>
    /// <param name="predicate">The predicate to include a given device.</param>
    /// <returns>All detected ROCm devices matching the predicate.</returns>
    public static ImmutableArray<Device> GetDevices(Predicate<ROCmDevice> predicate)
    {
        var registry = new DeviceRegistry();
        GetDevices(predicate, registry);
        return registry.ToImmutable();
    }

    /// <summary>
    /// Detects ROCm devices and registers them.
    /// </summary>
    /// <param name="predicate">The predicate to include a given device.</param>
    /// <param name="registry">The registry to add all devices to.</param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "We want to hide all exceptions at this level")]
    internal static void GetDevices(
        Predicate<ROCmDevice> predicate,
        DeviceRegistry registry)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        try
        {
            GetDevicesInternal(predicate, registry);
        }
        catch (Exception)
        {
            // Ignore API-specific exceptions at this point
        }
    }

    private static void GetDevicesInternal(
        Predicate<ROCmDevice> predicate,
        DeviceRegistry registry)
    {
        if (!CurrentAPI.Init())
            return;

        if (CurrentAPI.GetDeviceCount(out int numDevices) != ROCmError.Success ||
            numDevices < 1)
        {
            return;
        }

        for (int i = 0; i < numDevices; ++i)
        {
            var device = new ROCmDevice(i);
            if (predicate(device))
                registry.Register(device);
        }
    }

    #endregion

    #region Instance

    /// <summary>
    /// Constructs a new ROCm device descriptor.
    /// </summary>
    /// <param name="deviceId">The HIP device id.</param>
    internal ROCmDevice(int deviceId) : base(AcceleratorType.ROCm)
    {
        if (deviceId < 0)
            throw new ArgumentOutOfRangeException(nameof(deviceId));

        DeviceId = deviceId;
        InitDeviceInfo();
        InitCapabilities();
    }

    /// <summary>
    /// Returns the HIP device id.
    /// </summary>
    public int DeviceId { get; }

    /// <summary>
    /// Returns the target architecture string (e.g., "gfx1100").
    /// </summary>
    public string Architecture { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public override Accelerator CreateAccelerator(Context context) =>
        CreateROCmAccelerator(context);

    /// <summary>
    /// Creates a new ROCm accelerator for this device.
    /// </summary>
    public ROCmAccelerator CreateROCmAccelerator(Context context) =>
        new ROCmAccelerator(context, this);

    #endregion

    #region Initialization

    private void InitDeviceInfo()
    {
        ROCmException.ThrowIfFailed(
            CurrentAPI.GetDeviceName(out string name, DeviceId));
        Name = name;
        Architecture = name;

        WarpSize = CurrentAPI.GetDeviceAttributeValue(
            HipDeviceAttributeKind.WarpSize, DeviceId);

        MaxNumThreadsPerGroup = CurrentAPI.GetDeviceAttributeValue(
            HipDeviceAttributeKind.MaxThreadsPerBlock, DeviceId);

        MaxSharedMemoryPerGroup = CurrentAPI.GetDeviceAttributeValue(
            HipDeviceAttributeKind.MaxSharedMemoryPerBlock, DeviceId);

        MaxConstantMemory = CurrentAPI.GetDeviceAttributeValue(
            HipDeviceAttributeKind.TotalConstantMemory, DeviceId);

        ROCmException.ThrowIfFailed(
            CurrentAPI.GetDeviceTotalMem(out long totalMem, DeviceId));
        MemorySize = totalMem;

        NumMultiprocessors = CurrentAPI.GetDeviceAttributeValue(
            HipDeviceAttributeKind.MultiprocessorCount, DeviceId);

        MaxNumThreadsPerMultiprocessor = CurrentAPI.GetDeviceAttributeValue(
            HipDeviceAttributeKind.MaxThreadsPerMultiprocessor, DeviceId);

        OptimalKernelSize = new KernelSize(
            NumMultiprocessors * 4L,
            MaxNumThreadsPerMultiprocessor / 2);
    }

    private void InitCapabilities()
    {
        Capabilities = ROCmAcceleratorCapabilities.FromArchitectureString(Architecture);
    }

    #endregion

    #region Print

    /// <inheritdoc/>
    protected override void PrintHeader(TextWriter writer)
    {
        base.PrintHeader(writer);
        writer.Write("ROCm Device: ");
        writer.WriteLine(Name);
    }

    /// <inheritdoc/>
    protected override void PrintGeneralInfo(TextWriter writer)
    {
        base.PrintGeneralInfo(writer);
        writer.Write("  Architecture: ");
        writer.WriteLine(Architecture);
    }

    #endregion
}

