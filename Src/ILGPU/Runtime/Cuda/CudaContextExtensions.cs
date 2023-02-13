// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.PTX;
using ILGPU.Resources;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Cuda specific context extensions.
    /// </summary>
    public static class CudaContextExtensions
    {
        #region Builder

        /// <summary>
        /// Enables all compatible Cuda devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder Cuda(this Context.Builder builder) =>
            builder.Cuda(@override => { });

        /// <summary>
        /// Enables all Cuda devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder Cuda(
            this Context.Builder builder,
            Predicate<CudaDevice> predicate) =>
            builder.CudaInternal(@override => { }, predicate);

        /// <summary>
        /// Enables and configures all Cuda devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="configure">
        /// The action to configure a given device.
        /// </param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder Cuda(
            this Context.Builder builder,
            Action<CudaDeviceOverride> configure) =>
            builder.CudaInternal(
                configure,
                desc =>
                    desc.Architecture.HasValue &&
                    desc.InstructionSet.HasValue &&
                    PTXCodeGenerator.SupportedInstructionSets.Contains(
                        desc.InstructionSet.Value));

        /// <summary>
        /// Enables and configures all Cuda devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="configure">
        /// The action to configure a given device.
        /// </param>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder CudaInternal(
            this Context.Builder builder,
            Action<CudaDeviceOverride> configure,
            Predicate<CudaDevice> predicate)
        {
            if (!Backend.RuntimePlatform.Is64Bit())
            {
                throw new NotSupportedException(string.Format(
                    RuntimeErrorMessages.CudaPlatform64,
                    Backend.RuntimePlatform));
            }

            CudaDevice.GetDevices(
                configure,
                predicate,
                builder.DeviceRegistry);
            return builder;
        }

        #endregion

        #region Context

        /// <summary>
        /// Gets the i-th registered Cuda device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cudaDeviceIndex">
        /// The relative device index for the Cuda device. 0 here refers to the first
        /// Cuda device, 1 to the second, etc.
        /// </param>
        /// <returns>The registered Cuda device.</returns>
        public static CudaDevice GetCudaDevice(
            this Context context,
            int cudaDeviceIndex) =>
            context.GetDevice<CudaDevice>(cudaDeviceIndex);

        /// <summary>
        /// Gets all registered Cuda devices.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>All registered Cuda devices.</returns>
        public static Context.DeviceCollection<CudaDevice> GetCudaDevices(
            this Context context) =>
            context.GetDevices<CudaDevice>();

        /// <summary>
        /// Creates a new Cuda accelerator using
        /// <see cref="CudaAcceleratorFlags.ScheduleAuto"/>.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cudaDeviceIndex">
        /// The relative device index for the Cuda device. 0 here refers to the first
        /// Cuda device, 1 to the second, etc.
        /// </param>
        /// <returns>The created Cuda accelerator.</returns>
        public static CudaAccelerator CreateCudaAccelerator(
            this Context context,
            int cudaDeviceIndex) =>
            context.GetCudaDevice(cudaDeviceIndex)
                .CreateCudaAccelerator(context);

        /// <summary>
        /// Creates a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cudaDeviceIndex">
        /// The relative device index for the Cuda device. 0 here refers to the first
        /// Cuda device, 1 to the second, etc.
        /// </param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        /// <returns>The created Cuda accelerator.</returns>
        public static CudaAccelerator CreateCudaAccelerator(
            this Context context,
            int cudaDeviceIndex,
            CudaAcceleratorFlags acceleratorFlags) =>
            context.GetCudaDevice(cudaDeviceIndex)
                .CreateCudaAccelerator(context, acceleratorFlags);

        #endregion
    }
}
