// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvmlAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Text;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the NVML API.
    /// </summary>
    [CLSCompliant(false)]
    public abstract partial class NvmlAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The NVML version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static NvmlAPI Create(NvmlAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static NvmlAPI CreateLatest()
        {
            Exception firstException = null;
            var versions = Enum.GetValues(typeof(NvmlAPIVersion));

            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = (NvmlAPIVersion)versions.GetValue(i);
                var api = CreateInternal(version);
                if (api is null)
                    continue;

                try
                {
                    var status = api.DeviceGetCount(out _);
                    if (status == NvmlReturn.NVML_SUCCESS ||
                        status == NvmlReturn.NVML_ERROR_UNINITIALIZED)
                    {
                        return api;
                    }
                }
                catch (Exception ex) when (
                    ex is DllNotFoundException ||
                    ex is EntryPointNotFoundException)
                {
                    firstException ??= ex;
                }
            }

            throw firstException ?? new DllNotFoundException(nameof(NvmlAPI));
        }

        /// <summary>
        /// Helper method to read a null terminated string from the NVML Interop API.
        /// </summary>
        /// <param name="interopFunc">The interop function.</param>
        /// <param name="length">The max length to retrieve.</param>
        /// <param name="nvmlString">Filled in with the result string.</param>
        /// <returns>The interop status code.</returns>
        internal unsafe static NvmlReturn GetNvmlString(
            Func<IntPtr, uint, NvmlReturn> interopFunc,
            uint length,
            out string nvmlString)
        {
            NvmlReturn result;
            ReadOnlySpan<byte> buffer =
                stackalloc byte[(int)length + 1];
            fixed (byte* ptr = buffer)
            {
                result = interopFunc(new IntPtr(ptr), length);
                if (result == NvmlReturn.NVML_SUCCESS)
                {
                    var strlen = buffer.IndexOf<byte>(0);
                    nvmlString = Encoding.UTF8.GetString(ptr, strlen);
                }
                else
                {
                    nvmlString = default;
                }
            }

            return result;
        }

        #endregion

        #region Device Queries

        /// <summary>
        /// Provides access to <see cref="DeviceGetBoardPartNumber_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetBoardPartNumber(
            IntPtr device,
            out string partNumber) =>
            GetNvmlString(
                (str, len) => DeviceGetBoardPartNumber_Interop(device, str, len),
                NvmlConstants.NVML_DEVICE_PART_NUMBER_BUFFER_SIZE,
                out partNumber);

        /// <summary>
        /// Provides access to <see cref="DeviceGetBridgeChipInfo_Interop"/>
        /// without using raw pointers.
        /// </summary>
        [CLSCompliant(false)]
        public unsafe NvmlReturn DeviceGetBridgeChipInfo(
            IntPtr device,
            out NvmlBridgeChipHierarchy bridgeHierarchy)
        {
            var result = DeviceGetBridgeChipInfo_Interop(device, out var interopResult);
            if (result == NvmlReturn.NVML_SUCCESS)
            {
                bridgeHierarchy =
                    new NvmlBridgeChipHierarchy()
                    {
                        BridgeCount = interopResult.BridgeCount,
                        BridgeChipInfo = new NvmlBridgeChipInfo[interopResult.BridgeCount]
                    };
                for (int i = 0; i < interopResult.BridgeCount; i++)
                    bridgeHierarchy.BridgeChipInfo[i] = interopResult.BridgeChipInfo[i];
            }
            else
            {
                bridgeHierarchy = default;
            }

            return result;
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetInforomImageVersion_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetInforomImageVersion(
            IntPtr device,
            out string version) =>
            GetNvmlString(
                (str, len) => DeviceGetInforomImageVersion_Interop(device, str, len),
                NvmlConstants.NVML_DEVICE_INFOROM_VERSION_BUFFER_SIZE,
                out version);

        /// <summary>
        /// Provides access to <see cref="DeviceGetInforomVersion_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetInforomVersion(
            IntPtr device,
            NvmlInforomObject inforomObject,
            out string version) =>
            GetNvmlString(
                (str, len) =>
                {
                    return DeviceGetInforomVersion_Interop(
                        device,
                        inforomObject,
                        str,
                        len);
                },
                NvmlConstants.NVML_DEVICE_INFOROM_VERSION_BUFFER_SIZE,
                out version);

        /// <summary>
        /// Provides access to <see cref="DeviceGetName_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetName(
            IntPtr device,
            out string name) =>
            GetNvmlString(
                (str, len) => DeviceGetName_Interop(device, str, len),
                NvmlConstants.NVML_DEVICE_NAME_V2_BUFFER_SIZE,
                out name);

        /// <summary>
        /// Provides access to <see cref="DeviceGetSerial_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetSerial(
            IntPtr device,
            out string serial) =>
            GetNvmlString(
                (str, len) => DeviceGetSerial_Interop(device, str, len),
                NvmlConstants.NVML_DEVICE_SERIAL_BUFFER_SIZE,
                out serial);

        /// <summary>
        /// Provides access to <see cref="DeviceGetUUID_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetUUID(
            IntPtr device,
            out string uuid) =>
            GetNvmlString(
                (str, len) => DeviceGetUUID_Interop(device, str, len),
                NvmlConstants.NVML_DEVICE_UUID_V2_BUFFER_SIZE,
                out uuid);

        /// <summary>
        /// Provides access to <see cref="DeviceGetPciInfo_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetPciInfo(
            IntPtr device,
            out NvmlPciInfo pci)
        {
            var result = DeviceGetPciInfo_Interop(device, out var interopResult);
            if (result == NvmlReturn.NVML_SUCCESS)
            {
                var busIdLegacySpan = new Span<byte>(
                    interopResult.BusIdLegacy,
                    (int)NvmlConstants.NVML_DEVICE_PCI_BUS_ID_BUFFER_V2_SIZE);
                var busIdLegacyStrLen = busIdLegacySpan.IndexOf<byte>(0);
                var busIdLegacy = Encoding.UTF8.GetString(
                    interopResult.BusIdLegacy,
                    busIdLegacyStrLen);

                var busIdSpan = new Span<byte>(
                    interopResult.BusId,
                    (int)NvmlConstants.NVML_DEVICE_PCI_BUS_ID_BUFFER_SIZE);
                var busIdStrLen = busIdSpan.IndexOf<byte>(0);
                var busId = Encoding.UTF8.GetString(
                    interopResult.BusId,
                    busIdStrLen);

                pci =
                    new NvmlPciInfo()
                    {
                        BusIdLegacy = busIdLegacy,
                        Domain = interopResult.Domain,
                        Bus = interopResult.Bus,
                        Device = interopResult.Device,
                        PciDeviceId = interopResult.PciDeviceId,
                        PciSubSystemId = interopResult.PciSubSystemId,
                        BusId = busId,
                    };
            }
            else
            {
                pci = default;
            }

            return result;
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetVbiosVersion_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetVbiosVersion(
            IntPtr device,
            out string version) =>
            GetNvmlString(
                (str, len) => DeviceGetVbiosVersion_Interop(device, str, len),
                NvmlConstants.NVML_DEVICE_VBIOS_VERSION_BUFFER_SIZE,
                out version);

        /// <summary>
        /// Provides access to <see cref="VgpuInstanceGetMdevUUID_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn VgpuInstanceGetMdevUUID(
            uint vgpuInstance,
            out string version) =>
            GetNvmlString(
                (str, len) => VgpuInstanceGetMdevUUID_Interop(vgpuInstance, str, len),
                NvmlConstants.NVML_DEVICE_UUID_V2_BUFFER_SIZE,
                out version);

        #endregion
    }
}
