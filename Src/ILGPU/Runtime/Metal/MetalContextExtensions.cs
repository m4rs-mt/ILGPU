// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Metal specific context extensions.
/// </summary>
public static class MetalContextExtensions
{
    #region Builder

    /// <summary>
    /// Enables all Metal devices.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder Metal(this Context.Builder builder) =>
        builder.Metal(_ => true);

    /// <summary>
    /// Enables all Metal devices matching the given predicate.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <param name="predicate">
    /// The predicate to include a given device.
    /// </param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder Metal(
        this Context.Builder builder,
        Predicate<MetalDevice> predicate)
    {
        MetalDevice.GetDevices(predicate, builder.DeviceRegistry);
        return builder;
    }

    #endregion

    #region Context

    /// <summary>
    /// Gets the i-th registered Metal device.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="metalDeviceIndex">
    /// The relative device index for the Metal device. 0 here refers to the
    /// first Metal device, 1 to the second, etc.
    /// </param>
    /// <returns>The registered Metal device.</returns>
    public static MetalDevice GetMetalDevice(
        this Context context,
        int metalDeviceIndex) =>
        context.GetDevice<MetalDevice>(metalDeviceIndex);

    /// <summary>
    /// Gets all registered Metal devices.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <returns>All registered Metal devices.</returns>
    public static Context.DeviceCollection<MetalDevice> GetMetalDevices(
        this Context context) =>
        context.GetDevices<MetalDevice>();

    /// <summary>
    /// Creates a new Metal accelerator.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="metalDeviceIndex">
    /// The relative device index for the Metal device. 0 here refers to the
    /// first Metal device, 1 to the second, etc.
    /// </param>
    /// <returns>The created Metal accelerator.</returns>
    public static MetalAccelerator CreateMetalAccelerator(
        this Context context,
        int metalDeviceIndex) =>
        context.GetMetalDevice(metalDeviceIndex)
            .CreateMetalAccelerator(context);

    #endregion
}
