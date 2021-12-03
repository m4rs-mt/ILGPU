// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvmlDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an NVML Device from the Nvidia Management Library.
    /// </summary>
    public sealed class NvmlDevice : DisposeBase
    {
        #region Static

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library
        /// from a Cuda accelerator.
        /// </summary>
        public static NvmlDevice CreateFromAccelerator(CudaAccelerator accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));
            return CreateFromPciBusId(accelerator.NVMLPCIBusId);
        }

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library
        /// from the PCI Bus Id.
        /// </summary>
        public static NvmlDevice CreateFromPciBusId(string pciBusId)
        {
            var api = CreateInitAPI(new NvmlAPIVersion?());
            NvmlException.ThrowIfFailed(
                api.DeviceGetHandleByPciBusId(
                    pciBusId,
                    out IntPtr deviceHandle));
            return new NvmlDevice(api, deviceHandle);
        }

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library
        /// from the board serial number.
        /// </summary>
        public static NvmlDevice CreateFromSerial(string serial)
        {
            var api = CreateInitAPI(new NvmlAPIVersion?());
            NvmlException.ThrowIfFailed(
                api.DeviceGetHandleBySerial(
                    serial,
                    out IntPtr deviceHandle));
            return new NvmlDevice(api, deviceHandle);
        }

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library
        /// from the UUID of the GPU.
        /// </summary>
        public static NvmlDevice CreateFromUUID(string uuid)
        {
            var api = CreateInitAPI(new NvmlAPIVersion?());
            NvmlException.ThrowIfFailed(
                api.DeviceGetHandleByUUID(
                    uuid,
                    out IntPtr deviceHandle));
            return new NvmlDevice(api, deviceHandle);
        }

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library
        /// from the index of the GPU.
        /// </summary>
        [CLSCompliant(false)]
        public static NvmlDevice CreateFromIndex(uint index)
        {
            var api = CreateInitAPI(new NvmlAPIVersion?());
            NvmlException.ThrowIfFailed(
                api.DeviceGetHandleByIndex(
                    index,
                    out IntPtr deviceHandle));
            return new NvmlDevice(api, deviceHandle);
        }

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library
        /// from an existing device handle.
        /// </summary>
        public static NvmlDevice CreateFromDeviceHandle(IntPtr deviceHandle)
        {
            var api = CreateInitAPI(new NvmlAPIVersion?());
            return new NvmlDevice(api, deviceHandle);
        }

        /// <summary>
        /// Helper function to create and initialize a new instance of NvmlAPI.
        /// </summary>
        private static NvmlAPI CreateInitAPI(NvmlAPIVersion? apiVersion)
        {
            var api = NvmlAPI.Create(apiVersion);
            NvmlException.ThrowIfFailed(api.Init());
            return api;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated NVML API instance.
        /// </summary>
        [CLSCompliant(false)]
        public NvmlAPI API { get; }

        /// <summary>
        /// The NVML native device handle.
        /// </summary>
        public IntPtr DeviceHandle { get; private set; }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new instance to access the Nvidia Management Library.
        /// </summary>
        /// <param name="api">The NVML API instance to use.</param>
        /// <param name="deviceHandle">The NVML device handle.</param>
        private NvmlDevice(NvmlAPI api, IntPtr deviceHandle)
        {
            API = api;
            DeviceHandle = deviceHandle;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                NvmlException.ThrowIfFailed(API.Shutdown());
            base.Dispose(disposing);
        }

        /// <summary>
        /// Returns the the intended operating speed of the device's fan.
        /// </summary>
        /// <returns>The fan speed percentage.</returns>
        [CLSCompliant(false)]
        public uint GetFanSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetFanSpeed(DeviceHandle, out uint speed));
            return speed;
        }

        /// <summary>
        /// Returns the the intended operating speed of the device's fan.
        /// </summary>
        /// <param name="fan">The fan to query.</param>
        /// <returns>The fan speed percentage.</returns>
        [CLSCompliant(false)]
        public uint GetFanSpeed(uint fan)
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetFanSpeed(DeviceHandle, fan, out uint speed));
            return speed;
        }

        /// <summary>
        /// Returns the current temperature readings for the device, in degrees C.
        /// </summary>
        [CLSCompliant(false)]
        public uint GetGpuTemperature()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetTemperature(
                    DeviceHandle,
                    NvmlTemperatureSensors.NVML_TEMPERATURE_GPU,
                    out uint temp));
            return temp;
        }

        /// <summary>
        /// Returns the temperature threshold for the device, in degrees C.
        /// </summary>
        /// <param name="threshold">The threshold to query.</param>
        [CLSCompliant(false)]
        public uint GetTemperatureThreshold(NvmlTemperatureThresholds threshold)
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetTemperatureThreshold(
                    DeviceHandle,
                    threshold,
                    out uint temp));
            return temp;
        }

        /// <summary>
        /// Returns the current speed of the graphics clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetGraphicsClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_GRAPHICS,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the current speed of the SM clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetStreamingMultiprocessorClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_SM,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the current speed of the memory clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetMemoryClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_MEM,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the current speed of the video clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetVideoClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_VIDEO,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the max speed of the graphics clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetMaxGraphicsClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetMaxClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_GRAPHICS,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the max speed of the SM clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetMaxStreamingMultiprocessorClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetMaxClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_SM,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the max speed of the memory clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetMaxMemoryClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetMaxClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_MEM,
                    out uint clock));
            return clock;
        }

        /// <summary>
        /// Returns the max speed of the video clock for the device, in MHz.
        /// </summary>
        /// <returns>The speed in MHz.</returns>
        [CLSCompliant(false)]
        public uint GetMaxVideoClockSpeed()
        {
            NvmlException.ThrowIfFailed(
                API.DeviceGetMaxClockInfo(
                    DeviceHandle,
                    NvmlClockType.NVML_CLOCK_VIDEO,
                    out uint clock));
            return clock;
        }

        #endregion
    }
}
