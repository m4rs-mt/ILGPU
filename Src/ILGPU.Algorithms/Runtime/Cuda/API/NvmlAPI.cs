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

        #endregion

        #region Device Queries

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

        #endregion
    }
}
