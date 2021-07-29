// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using System;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// OpenCL specific context extensions.
    /// </summary>
    public static class CLContextExtensions
    {
        #region Builder

        /// <summary>
        /// Enables all compatible OpenCL devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder OpenCL(this Context.Builder builder) =>
            builder.OpenCL(id => id.CLStdVersion >= CLBackend.MinimumVersion &&
                                 id.Capabilities.GenericAddressSpace);

        /// <summary>
        /// Enables all OpenCL devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder OpenCL(
            this Context.Builder builder,
            Predicate<CLDevice> predicate)
        {
            CLDevice.GetDevices(
                predicate,
                builder.DeviceRegistry);
            return builder;
        }

        #endregion

        #region Context

        /// <summary>
        /// Gets the i-th registered OpenCL device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="clDeviceIndex">
        /// The relative device index for the OpenCL device. 0 here refers to the first
        /// OpenCL device, 1 to the second, etc.
        /// </param>
        /// <returns>The registered OpenCL device.</returns>
        public static CLDevice GetCLDevice(
            this Context context,
            int clDeviceIndex) =>
            context.GetDevice<CLDevice>(clDeviceIndex);

        /// <summary>
        /// Gets all registered OpenCL devices.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>All registered OpenCL devices.</returns>
        public static Context.DeviceCollection<CLDevice> GetCLDevices(
            this Context context) =>
            context.GetDevices<CLDevice>();

        /// <summary>
        /// Creates a new OpenCL accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="clDeviceIndex">
        /// The relative device index for the OpenCL device. 0 here refers to the first
        /// OpenCL device, 1 to the second, etc.
        /// </param>
        /// <returns>The created OpenCL accelerator.</returns>
        public static CLAccelerator CreateCLAccelerator(
            this Context context,
            int clDeviceIndex) =>
            context.GetCLDevice(clDeviceIndex)
                .CreateCLAccelerator(context);

        #endregion
    }
}
