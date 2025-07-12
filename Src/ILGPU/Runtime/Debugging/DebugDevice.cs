// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace ILGPU.Runtime.Debugging;

/// <summary>
/// Specifies a simulator kind of a <see cref="DebugDevice"/> instance.
/// </summary>
public enum DebugDeviceKind
{
    /// <summary>
    /// A debug accelerator that simulates a common configuration of a default GPU
    /// simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
    /// multiprocessor.
    /// </summary>
    Default,

    /// <summary>
    /// A debug accelerator that simulates a common configuration of an NVIDIA GPU
    /// with 1 multiprocessor.
    /// </summary>
    Nvidia,

    /// <summary>
    /// A debug accelerator that simulates a common configuration of an AMD GPU with
    /// 1 multiprocessor.
    /// </summary>
    AMD,

    /// <summary>
    /// A debug accelerator that simulates a common configuration of a legacy GCN AMD
    /// GPU with 1 multiprocessor.
    /// </summary>
    LegacyAMD,

    /// <summary>
    /// A debug accelerator that simulates a common configuration of an Intel GPU
    /// with 1 multiprocessor.
    /// </summary>
    Intel
}

/// <summary>
/// Represents a single debug device.
/// </summary>
public sealed class DebugDevice : Device, IDeviceAcceleratorTypeInfo
{
    #region Constants

    /// <summary>
    /// The default warp size of 4 threads per group.
    /// </summary>
    private const int DefaultWarpSize = 4;

    /// <summary>
    /// The default number of 4 warps per multiprocessor.
    /// </summary>
    private const int DefaultNumWarpsPerMultiprocessor = 4;

    /// <summary>
    /// The default number of 1 multiprocessor.
    /// </summary>
    private const int DefaultNumMultiprocessors = 1;

    #endregion

    #region Static

    /// <summary>
    /// A debug accelerator that simulates a common configuration of a default GPU
    /// simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
    /// multiprocessor.
    /// </summary>
    public static readonly DebugDevice Default =
        new(DefaultWarpSize, DefaultNumWarpsPerMultiprocessor, DefaultNumMultiprocessors);

    /// <summary>
    /// A debug accelerator that simulates a common configuration of an NVIDIA GPU with
    /// 1 multiprocessor.
    /// </summary>
    public static readonly DebugDevice Nvidia =
        new(numThreadsPerWarp: 32, numWarpsPerMultiprocessor: 32, numMultiprocessors: 1);

    /// <summary>
    /// A debug accelerator that simulates a common configuration of an AMD GPU with 1
    /// multiprocessor.
    /// </summary>
    public static readonly DebugDevice AMD =
        new(numThreadsPerWarp: 32, numWarpsPerMultiprocessor: 8, numMultiprocessors: 1);

    /// A debug accelerator that simulates a common configuration of a legacy GCN AMD
    /// GPU with 1 multiprocessor.
    public static readonly DebugDevice LegacyAMD =
        new(numThreadsPerWarp: 64, numWarpsPerMultiprocessor: 4, numMultiprocessors: 1);

    /// <summary>
    /// A debug accelerator that simulates a common configuration of an Intel GPU with
    /// 1 multiprocessor.
    /// </summary>
    public static readonly DebugDevice Intel =
        new(numThreadsPerWarp: 16, numWarpsPerMultiprocessor: 8, numMultiprocessors: 1);

    /// <summary>
    /// Maps <see cref="DebugDeviceKind"/> values to
    /// <see cref="DebugDevice"/> instances.
    /// </summary>
    public static readonly ImmutableArray<DebugDevice> All =
        [Default, Nvidia, AMD, LegacyAMD, Intel];

    /// <summary>
    /// Gets a specific debug device.
    /// </summary>
    /// <param name="kind">The debug device kind.</param>
    /// <returns>The debug device.</returns>
    public static DebugDevice GetDevice(
        DebugDeviceKind kind) =>
        kind < DebugDeviceKind.Default || kind > DebugDeviceKind.Intel
        ? throw new ArgumentOutOfRangeException(nameof(kind))
        : All[(int)kind];

    /// <summary>
    /// Returns debug devices.
    /// </summary>
    /// <param name="predicate">
    /// The predicate to include a given device.
    /// </param>
    /// <returns>All debug devices.</returns>
    public static ImmutableArray<Device> GetDevices(Predicate<DebugDevice> predicate)
    {
        var registry = new DeviceRegistry();
        GetDevices(predicate, registry);
        return registry.ToImmutable();
    }

    /// <summary>
    /// Registers debug devices.
    /// </summary>
    /// <param name="predicate">
    /// The predicate to include a given device.
    /// </param>
    /// <param name="registry">The registry to add all devices to.</param>
    internal static void GetDevices(
        Predicate<DebugDevice> predicate,
        DeviceRegistry registry)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (var desc in All)
            registry.Register(desc, predicate);
    }

    #endregion

    #region Instance

    /// <summary>
    /// Constructs a new debug accelerator description instance.
    /// </summary>
    /// <param name="numThreadsPerWarp">
    /// The number of threads per warp within a group.
    /// </param>
    /// <param name="numWarpsPerMultiprocessor">
    /// The number of warps per multiprocessor.
    /// </param>
    /// <param name="numMultiprocessors">
    /// The number of multiprocessors (number of parallel groups) to simulate.
    /// </param>
    /// <param name="skipChecks">True to skip internal bounds checks.</param>
    private DebugDevice(
        int numThreadsPerWarp,
        int numWarpsPerMultiprocessor,
        int numMultiprocessors,
        bool skipChecks) : base(AcceleratorType.Debug)
    {
        if (!skipChecks && (numThreadsPerWarp < 2 ||
            !XMath.IsPowerOf2(numWarpsPerMultiprocessor)))
        {
            throw new ArgumentOutOfRangeException(nameof(numThreadsPerWarp));
        }
        if (!skipChecks && numWarpsPerMultiprocessor < 1)
            throw new ArgumentOutOfRangeException(nameof(numWarpsPerMultiprocessor));
        if (!skipChecks && numMultiprocessors < 1)
            throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));

        // Check for existing limitations with respect to barrier participants
        if (numThreadsPerWarp * numWarpsPerMultiprocessor > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(numWarpsPerMultiprocessor));
        if (NumMultiprocessors > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));

        Name = nameof(DebugAccelerator);
        WarpSize = numThreadsPerWarp;
        MaxNumThreadsPerGroup = numThreadsPerWarp * numWarpsPerMultiprocessor;
        MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;
        NumMultiprocessors = numMultiprocessors;
        OptimalKernelSize = new(NumMultiprocessors, MaxNumThreadsPerGroup);

        MemorySize = long.MaxValue;
        MaxSharedMemoryPerGroup = int.MaxValue;
        MaxConstantMemory = int.MaxValue;
        NumThreads = MaxNumThreads;
        Capabilities = new CPUCapabilityContext();
    }

    /// <summary>
    /// Constructs a new debug accelerator description instance.
    /// </summary>
    /// <param name="numThreadsPerWarp">
    /// The number of threads per warp within a group.
    /// </param>
    /// <param name="numWarpsPerMultiprocessor">
    /// The number of warps per multiprocessor.
    /// </param>
    /// <param name="numMultiprocessors">
    /// The number of multiprocessors (number of parallel groups) to simulate.
    /// </param>
    public DebugDevice(
        int numThreadsPerWarp,
        int numWarpsPerMultiprocessor,
        int numMultiprocessors)
        : this(
              numThreadsPerWarp,
              numWarpsPerMultiprocessor,
              numMultiprocessors,
              skipChecks: false)
    { }

    #endregion

    #region Properties

    /// <summary>
    /// Returns <see cref="AcceleratorType.Debug"/>.
    /// </summary>
    static AcceleratorType IDeviceAcceleratorTypeInfo.AcceleratorType =>
        AcceleratorType.Debug;

    /// <summary>
    /// Returns the number of threads.
    /// </summary>
    public int NumThreads { get; }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public override Accelerator CreateAccelerator(Context context) =>
        CreateDebugAccelerator(context);

    /// <summary>
    /// Creates a new debug accelerator using <see cref="DebugAccelerationMode.Auto"/>
    /// and default thread priority.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <returns>The created debug accelerator.</returns>
    public DebugAccelerator CreateDebugAccelerator(Context context) =>
        CreateDebugAccelerator(context, DebugAccelerationMode.Auto);

    /// <summary>
    /// Creates a new debug accelerator with default thread priority.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="mode">The debug accelerator mode.</param>
    /// <returns>The created debug accelerator.</returns>
    public DebugAccelerator CreateDebugAccelerator(
        Context context,
        DebugAccelerationMode mode) =>
        CreateDebugAccelerator(context, mode, ThreadPriority.Normal);

    /// <summary>
    /// Creates a new debug accelerator.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="mode">The debug accelerator mode.</param>
    /// <param name="threadPriority">
    /// The thread priority of the execution threads.
    /// </param>
    /// <returns>The created debug accelerator.</returns>
    public DebugAccelerator CreateDebugAccelerator(
        Context context,
        DebugAccelerationMode mode,
        ThreadPriority threadPriority) =>
        new(context, this, mode, threadPriority);

    #endregion

    #region Object

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is DebugDevice device &&
        device.WarpSize == WarpSize &&
        device.MaxNumThreadsPerGroup == MaxNumThreadsPerGroup &&
        device.NumMultiprocessors == NumMultiprocessors &&
        base.Equals(obj);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        base.GetHashCode(),
        WarpSize,
        MaxNumThreadsPerGroup,
        NumMultiprocessors);

    #endregion
}
