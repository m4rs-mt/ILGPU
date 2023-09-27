// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Resources;
using System;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Velocity specific context extensions.
    /// </summary>
    public static class VelocityContextExtensions
    {
        #region Builder

        /// <summary>
        /// Enables all Velocity devices supporting all types of vectorization executable
        /// on the current hardware.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder AllVelocity(this Context.Builder builder)
        {
            foreach (var deviceType in Enum.GetValues<VelocityDeviceType>())
                builder.Velocity(deviceType);
            return builder;
        }

        /// <summary>
        /// Enables the Velocity device with the maximum vector length supported by the
        /// current hardware.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder Velocity(this Context.Builder builder) =>
            builder.Velocity(VelocityDeviceType.Scalar2);

        /// <summary>
        /// Enables a specific Velocity device.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="deviceType">The type of the Velocity device.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder Velocity(
            this Context.Builder builder,
            VelocityDeviceType deviceType)
        {
            if (!Backend.RuntimePlatform.Is64Bit())
            {
                throw new NotSupportedException(string.Format(
                    RuntimeErrorMessages.VelocityPlatform64,
                    Backend.RuntimePlatform));
            }

            builder.DeviceRegistry.Register(new VelocityDevice(deviceType));
            return builder;
        }

        #endregion

        #region Context

        /// <summary>
        /// Gets a registered Velocity device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="index">The Velocity device index.</param>
        /// <returns>The registered Velocity device.</returns>
        public static VelocityDevice GetVelocityDevice(
            this Context context,
            int index = 0) =>
            context.GetDevice<VelocityDevice>(index);

        /// <summary>
        /// Creates a new Velocity accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="index">The Velocity device index.</param>
        public static VelocityAccelerator CreateVelocityAccelerator(
            this Context context,
            int index = 0) =>
            context.GetVelocityDevice(index).CreateVelocityAccelerator(context);

        #endregion
    }

}

