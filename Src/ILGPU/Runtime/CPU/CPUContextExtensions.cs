// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// CPU specific context extensions.
    /// </summary>
    public static class CPUContextExtensions
    {
        #region Builder

        /// <summary>
        /// Enables the default CPU device (see <see cref="CPUDevice.Default"/>).
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder DefaultCPU(this Context.Builder builder) =>
            builder.CPU(CPUDeviceKind.Default);

        /// <summary>
        /// Enables a CPU device of the given kind.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="kind">The CPU device kind.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder CPU(
            this Context.Builder builder,
            CPUDeviceKind kind)
        {
            builder.DeviceRegistry.Register(CPUDevice.GetDevice(kind));
            return builder;
        }

        /// <summary>
        /// Enables all CPU devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder CPU(this Context.Builder builder) =>
            builder.CPU(desc => true);

        /// <summary>
        /// Enables all CPU devices.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <returns>The updated builder instance.</returns>
        public static Context.Builder CPU(
            this Context.Builder builder,
            Predicate<CPUDevice> predicate)
        {
            CPUDevice.GetDevices(
                predicate,
                builder.DeviceRegistry);
            return builder;
        }

        #endregion

        #region Context

        /// <summary>
        /// Returns the implicitly created CPU accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>
        /// The implicitly defined CPU accelerator with 0 threads per warp, 0 warps per
        /// MP and 0 MPs.
        /// </returns>
        /// <remarks>
        /// CAUTION: This accelerator is not intended for simulation purposes.
        /// </remarks>
        public static CPUAccelerator GetImplicitCPUAccelerator(this Context context) =>
            context.CPUAccelerator;

        /// <summary>
        /// Gets the i-th registered CPU device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cpuDeviceIndex">
        /// The relative device index for the CPU device. 0 here refers to the first
        /// CPU device, 1 to the second, etc.
        /// </param>
        /// <returns>The registered CPU device.</returns>
        public static CPUDevice GetCPUDevice(
            this Context context,
            int cpuDeviceIndex) =>
            context.GetDevice<CPUDevice>(cpuDeviceIndex);

        /// <summary>
        /// Gets all registered CPU devices.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>All registered CPU devices.</returns>
        public static Context.DeviceCollection<CPUDevice> GetCPUDevices(
            this Context context) =>
            context.GetDevices<CPUDevice>();

        /// <summary>
        /// Creates a new CPU accelerator using <see cref="CPUAcceleratorMode.Auto"/>
        /// and default thread priority.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cpuDeviceIndex">
        /// The relative device index for the CPU device. 0 here refers to the first
        /// CPU device, 1 to the second, etc.
        /// </param>
        /// <returns>The created CPU accelerator.</returns>
        public static CPUAccelerator CreateCPUAccelerator(
            this Context context,
            int cpuDeviceIndex) =>
            context.GetCPUDevice(cpuDeviceIndex)
                .CreateCPUAccelerator(context);

        /// <summary>
        /// Creates a new CPU accelerator with default thread priority.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cpuDeviceIndex">
        /// The relative device index for the CPU device. 0 here refers to the first
        /// CPU device, 1 to the second, etc.
        /// </param>
        /// <param name="mode">The CPU accelerator mode.</param>
        /// <returns>The created CPU accelerator.</returns>
        public static CPUAccelerator CreateCPUAccelerator(
            this Context context,
            int cpuDeviceIndex,
            CPUAcceleratorMode mode) =>
            context.GetCPUDevice(cpuDeviceIndex)
                .CreateCPUAccelerator(context, mode);

        /// <summary>
        /// Creates a new CPU accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="cpuDeviceIndex">
        /// The relative device index for the CPU device. 0 here refers to the first
        /// CPU device, 1 to the second, etc.
        /// </param>
        /// <param name="mode">The CPU accelerator mode.</param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        /// <returns>The created CPU accelerator.</returns>
        public static CPUAccelerator CreateCPUAccelerator(
            this Context context,
            int cpuDeviceIndex,
            CPUAcceleratorMode mode,
            ThreadPriority threadPriority) =>
            context.GetCPUDevice(cpuDeviceIndex)
                .CreateCPUAccelerator(context, mode, threadPriority);

        #endregion
    }
}
