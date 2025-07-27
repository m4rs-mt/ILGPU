// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// ROCm specific context extensions.
/// </summary>
public static class ROCmContextExtensions
{
    #region Builder

    /// <summary>
    /// Enables all ROCm devices.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder ROCm(this Context.Builder builder) =>
        builder.ROCm(_ => true);

    /// <summary>
    /// Enables all ROCm devices matching the given predicate.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <param name="predicate">
    /// The predicate to include a given device.
    /// </param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder ROCm(
        this Context.Builder builder,
        Predicate<ROCmDevice> predicate)
    {
        ROCmDevice.GetDevices(predicate, builder.DeviceRegistry);
        return builder;
    }

    #endregion

    #region Context

    /// <summary>
    /// Gets the i-th registered ROCm device.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="rocmDeviceIndex">
    /// The relative device index for the ROCm device. 0 here refers to the
    /// first ROCm device, 1 to the second, etc.
    /// </param>
    /// <returns>The registered ROCm device.</returns>
    public static ROCmDevice GetROCmDevice(
        this Context context,
        int rocmDeviceIndex) =>
        context.GetDevice<ROCmDevice>(rocmDeviceIndex);

    /// <summary>
    /// Gets all registered ROCm devices.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <returns>All registered ROCm devices.</returns>
    public static Context.DeviceCollection<ROCmDevice> GetROCmDevices(
        this Context context) =>
        context.GetDevices<ROCmDevice>();

    /// <summary>
    /// Creates a new ROCm accelerator.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="rocmDeviceIndex">
    /// The relative device index for the ROCm device. 0 here refers to the
    /// first ROCm device, 1 to the second, etc.
    /// </param>
    /// <returns>The created ROCm accelerator.</returns>
    public static ROCmAccelerator CreateROCmAccelerator(
        this Context context,
        int rocmDeviceIndex) =>
        context.GetROCmDevice(rocmDeviceIndex)
            .CreateROCmAccelerator(context);

    #endregion
}
