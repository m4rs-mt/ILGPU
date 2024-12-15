// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Threading;

namespace ILGPU.Runtime.Debugging;

/// <summary>
/// Debugging specific context extensions.
/// </summary>
public static class DebugContextExtensions
{
    #region Builder

    /// <summary>
    /// Enables the default debug device (see <see cref="DebugDevice.Default"/>).
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder DefaultDebug(this Context.Builder builder) =>
        builder.Debug(DebugDeviceKind.Default);

    /// <summary>
    /// Enables a debug device of the given kind.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <param name="kind">The CPU device kind.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder Debug(
        this Context.Builder builder,
        DebugDeviceKind kind)
    {
        builder.DeviceRegistry.Register(DebugDevice.GetDevice(kind));
        return builder;
    }

    /// <summary>
    /// Enables a debug device of the given kind.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <param name="device">The custom CPU device.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder Debug(
        this Context.Builder builder,
        DebugDevice device)
    {
        builder.DeviceRegistry.Register(device);
        return builder;
    }

    /// <summary>
    /// Enables all debug devices.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder Debug(this Context.Builder builder) =>
        builder.Debug(static _ => true);

    /// <summary>
    /// Enables all CPU devices.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <param name="predicate">
    /// The predicate to include a given device.
    /// </param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder Debug(
        this Context.Builder builder,
        Predicate<DebugDevice> predicate)
    {
        DebugDevice.GetDevices(predicate, builder.DeviceRegistry);
        return builder;
    }

    #endregion

    #region Context

    /// <summary>
    /// Gets the i-th registered debug device.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="debugDeviceIndex">
    /// The relative device index for the debug device. 0 here refers to the first
    /// debug device, 1 to the second, etc.
    /// </param>
    /// <returns>The registered debug device.</returns>
    public static DebugDevice GetDebugDevice(
        this Context context,
        int debugDeviceIndex) =>
        context.GetDevice<DebugDevice>(debugDeviceIndex);

    /// <summary>
    /// Gets all registered debug devices.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <returns>All registered debug devices.</returns>
    public static Context.DeviceCollection<DebugDevice> GetDebugDevices(
        this Context context) =>
        context.GetDevices<DebugDevice>();

    /// <summary>
    /// Creates a new debug accelerator using <see cref="DebugAccelerationMode.Auto"/>
    /// and default thread priority.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="debugDeviceIndex">
    /// The relative device index for the debug device. 0 here refers to the first
    /// debug device, 1 to the second, etc.
    /// </param>
    /// <returns>The created debug accelerator.</returns>
    public static DebugAccelerator CreateDebugAccelerator(
        this Context context,
        int debugDeviceIndex) => context
            .GetDebugDevice(debugDeviceIndex)
            .CreateDebugAccelerator(context);

    /// <summary>
    /// Creates a new CPU accelerator with default thread priority.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="debugDeviceIndex">
    /// The relative device index for the CPU device. 0 here refers to the first
    /// CPU device, 1 to the second, etc.
    /// </param>
    /// <param name="mode">The CPU accelerator mode.</param>
    /// <returns>The created CPU accelerator.</returns>
    public static DebugAccelerator CreateDebugAccelerator(
        this Context context,
        int debugDeviceIndex,
        DebugAccelerationMode mode) => context
            .GetDebugDevice(debugDeviceIndex)
            .CreateDebugAccelerator(context, mode);

    /// <summary>
    /// Creates a new CPU accelerator.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="debugDeviceIndex">
    /// The relative device index for the CPU device. 0 here refers to the first
    /// CPU device, 1 to the second, etc.
    /// </param>
    /// <param name="mode">The CPU accelerator mode.</param>
    /// <param name="threadPriority">
    /// The thread priority of the execution threads.
    /// </param>
    /// <returns>The created CPU accelerator.</returns>
    public static DebugAccelerator CreateDebugAccelerator(
        this Context context,
        int debugDeviceIndex,
        DebugAccelerationMode mode,
        ThreadPriority threadPriority) => context
            .GetDebugDevice(debugDeviceIndex)
            .CreateDebugAccelerator(context, mode, threadPriority);

    #endregion
}
