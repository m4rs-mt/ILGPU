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
    /// Cuda specific context extensions.
    /// </summary>
    public static class VelocityContextExtensions
    {
        #region Builder

        /// <summary>
        /// Enables all velocity devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="maxSharedMemoryPerGroup">
        /// The maximum number bytes of shared memory per group.
        /// </param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder Velocity(
            this Context.Builder builder,
            int maxSharedMemoryPerGroup = VelocityDevice.MinSharedMemoryPerGroup)
        {
            if (!Backend.RuntimePlatform.Is64Bit())
            {
                throw new NotSupportedException(string.Format(
                    RuntimeErrorMessages.VelocityPlatform64,
                    Backend.RuntimePlatform));
            }

            builder.DeviceRegistry.Register(new VelocityDevice());
            return builder;
        }

        #endregion

        #region Context

        /// <summary>
        /// Gets a registered Velocity device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>The registered Velocity device.</returns>
        public static VelocityDevice GetVelocityDevice(this Context context) =>
            context.GetDevice<VelocityDevice>(0);

        /// <summary>
        /// Creates a new Velocity accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>The created Velocity accelerator.</returns>
        public static VelocityAccelerator CreateVelocityAccelerator(
            this Context context) =>
            context.GetVelocityDevice().CreateVelocityAccelerator(context);

        #endregion
    }

}

