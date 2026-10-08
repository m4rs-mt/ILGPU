// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents a Metal GPU device descriptor.
/// </summary>
public sealed class MetalDevice : Device, IDeviceAcceleratorTypeInfo
{
    // MTLGPUFamily enum values from Metal headers.
    private const int MTLGPUFamilyApple7 = 1007;
    private const int MTLGPUFamilyApple8 = 1008;
    private const int MTLGPUFamilyApple9 = 1009;

    /// <summary>
    /// Creates a new Metal device descriptor.
    /// </summary>
    /// <param name="deviceHandle">
    /// Handle to the MTLDevice. The device retains its own reference.
    /// </param>
    internal MetalDevice(IntPtr deviceHandle) : base(AcceleratorType.Metal)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                "Invalid Metal device handle.", nameof(deviceHandle));
        }

        DeviceHandle = deviceHandle;
        MetalAPI.Retain(DeviceHandle);

        InitDeviceInfo();
        InitGPUFamily();
        InitCapabilities();
    }

    /// <summary>
    /// The detected GPU family.
    /// </summary>
    public MetalGPUFamily GPUFamily { get; private set; } = MetalGPUFamily.Apple7;

    /// <summary>
    /// Returns the native device handle.
    /// </summary>
    internal IntPtr DeviceHandle { get; private set; }

    /// <summary>
    /// Returns the accelerator type for this device type.
    /// </summary>
    static AcceleratorType IDeviceAcceleratorTypeInfo.AcceleratorType =>
        AcceleratorType.Metal;

    /// <summary>
    /// Creates a new Metal accelerator for this device.
    /// </summary>
    public override Accelerator CreateAccelerator(Context context) =>
        CreateMetalAccelerator(context);

    /// <summary>
    /// Creates a new Metal accelerator for this device.
    /// </summary>
    public MetalAccelerator CreateMetalAccelerator(Context context) =>
        new MetalAccelerator(context, this);

    /// <summary>
    /// Enumerates all available Metal devices.
    /// </summary>
    /// <returns>An immutable array of Metal devices.</returns>
    public static ImmutableArray<Device> GetDevices()
    {
        var registry = new DeviceRegistry();
        GetDevices(_ => true, registry);
        return registry.ToImmutable();
    }

    /// <summary>
    /// Enumerates Metal devices matching the given predicate.
    /// </summary>
    public static ImmutableArray<Device> GetDevices(
        Predicate<MetalDevice> predicate)
    {
        var registry = new DeviceRegistry();
        GetDevices(predicate, registry);
        return registry.ToImmutable();
    }

    /// <summary>
    /// Detects Metal devices matching the given predicate and registers them.
    /// </summary>
    /// <param name="predicate">The predicate to include a given device.</param>
    /// <param name="registry">The registry to add all devices to.</param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "We want to hide all exceptions at this level")]
    internal static void GetDevices(
        Predicate<MetalDevice> predicate,
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

    /// <summary>
    /// Internal wrapper to get Metal devices.
    /// </summary>
    /// <param name="predicate">The predicate to apply.</param>
    /// <param name="registry">The device registry.</param>
    private static void GetDevicesInternal(
        Predicate<MetalDevice> predicate,
        DeviceRegistry registry)
    {
        var deviceHandle = MetalAPI.CreateSystemDefaultDevice();
        if (deviceHandle == IntPtr.Zero)
            return;

        var device = new MetalDevice(deviceHandle);
        if (predicate(device))
            registry.Register(device);
    }

    /// <summary>
    /// Initializes device information.
    /// </summary>
    private void InitDeviceInfo()
    {
        Name = MetalAPI.GetDeviceName(DeviceHandle) ?? "Apple GPU";

        // Thread execution width is 32 on all Apple Silicon GPUs.
        WarpSize = 32;

        // Max threads per threadgroup (typically 1024 on Apple Silicon).
        var maxThreads = MetalAPI.GetMaxTotalThreadsPerThreadgroup(DeviceHandle);
        MaxNumThreadsPerGroup = (int)Math.Min(maxThreads, int.MaxValue);

        // Max threadgroup memory (shared memory, typically 32KB).
        var maxShared = MetalAPI.GetMaxThreadgroupMemoryLength(DeviceHandle);
        MaxSharedMemoryPerGroup = (int)Math.Min(maxShared, int.MaxValue);

        // Metal doesn't expose constant memory size; use a reasonable default.
        MaxConstantMemory = 64 * 1024;

        // Total device memory.
        var memSize = MetalAPI.GetRecommendedMaxWorkingSetSize(DeviceHandle);
        MemorySize = (long)Math.Min(memSize, long.MaxValue);

        // Apple Silicon doesn't expose multiprocessor count directly.
        // Use heuristic: Apple GPUs have 4-10 GPU cores depending on model.
        // Default to 8 as a reasonable mid-range estimate.
        NumMultiprocessors = 8;
        MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;

        // Optimal kernel size: reasonable defaults for Apple Silicon.
        OptimalKernelSize = new KernelSize(
            NumMultiprocessors * 4L,
            MaxNumThreadsPerMultiprocessor / 2);
    }

    /// <summary>
    /// Initializes the GPU family.
    /// </summary>
    private void InitGPUFamily()
    {
        if (MetalAPI.SupportsFamily(DeviceHandle, MTLGPUFamilyApple9))
            GPUFamily = MetalGPUFamily.Apple9;
        else if (MetalAPI.SupportsFamily(DeviceHandle, MTLGPUFamilyApple8))
            GPUFamily = MetalGPUFamily.Apple8;
        else if (MetalAPI.SupportsFamily(DeviceHandle, MTLGPUFamilyApple7))
            GPUFamily = MetalGPUFamily.Apple7;
    }

    /// <summary>
    /// Initializes internal accelerator capabilities.
    /// </summary>
    private void InitCapabilities() =>
        Capabilities = MetalAcceleratorCapabilities.FromGPUFamily(GPUFamily);

    /// <inheritdoc/>
    protected override void PrintHeader(System.IO.TextWriter writer)
    {
        base.PrintHeader(writer);
        writer.Write("Metal Device: ");
        writer.WriteLine(Name);
    }

    /// <inheritdoc/>
    protected override void PrintGeneralInfo(System.IO.TextWriter writer)
    {
        base.PrintGeneralInfo(writer);
        writer.Write("  GPU Family: ");
        writer.WriteLine(GPUFamily);
    }
}

