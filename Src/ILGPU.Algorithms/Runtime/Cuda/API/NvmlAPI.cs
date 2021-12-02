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

#pragma warning disable CA1707 // Identifiers should not contain underscores

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

        /// <summary>
        /// Delegate that is initiallly called with length 0, and a null pointer to
        /// determine the array size. Then, called a second time with the desired length
        /// and array to be populated.
        /// </summary>
        internal unsafe delegate NvmlReturn GetNvmlArrayInterop<T>(
            ref uint len,
            T* ptr)
            where T : unmanaged;

        /// <summary>
        /// Helper method to read an array of values from the NVML Interop API.
        /// </summary>
        /// <param name="interopFunc">The interop function.</param>
        /// <param name="nvmlArray">Filled in with the result array.</param>
        /// <returns>The interop status code.</returns>
        internal unsafe static NvmlReturn GetNvmlArray<T>(
            GetNvmlArrayInterop<T> interopFunc,
            out T[] nvmlArray)
            where T : unmanaged
        {
            // Query the length of data available.
            // If the result is success, that means an empty array.
            uint length = 0;
            NvmlReturn result = interopFunc(ref length, null);

            if (result == NvmlReturn.NVML_SUCCESS)
            {
                nvmlArray = Array.Empty<T>();
                return result;
            }
            else if (result == NvmlReturn.NVML_ERROR_INSUFFICIENT_SIZE)
            {
                // Allocate the correct size, and call the interop again.
                T[] buffer = new T[length];
                fixed (T* ptr = buffer)
                {
                    result = interopFunc(ref length, ptr);
                    nvmlArray = result == NvmlReturn.NVML_SUCCESS
                        ? buffer
                        : default;
                    return result;
                }
            }
            else
            {
                nvmlArray = default;
                return result;
            }
        }

        /// <summary>
        /// Delegate that is initiallly called with length 0, and a null pointer to
        /// determine the array size. Then, called a second time with the desired length
        /// and array to be populated.
        ///
        /// This variant is for two arrays of the same length.
        /// </summary>
        internal unsafe delegate NvmlReturn GetNvmlArrayInterop<T1, T2>(
            ref uint len,
            T1* ptr1,
            T2* ptr2)
            where T1 : unmanaged
            where T2 : unmanaged;

        /// <summary>
        /// Helper method to read an array of values from the NVML Interop API.
        /// </summary>
        /// <param name="interopFunc">The interop function.</param>
        /// <param name="nvmlArray1">Filled in with the result array.</param>
        /// <param name="nvmlArray2">Filled in with the result array.</param>
        /// <returns>The interop status code.</returns>
        internal unsafe static NvmlReturn GetNvmlArray<T1, T2>(
            GetNvmlArrayInterop<T1, T2> interopFunc,
            out T1[] nvmlArray1,
            out T2[] nvmlArray2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            // Query the length of data available.
            // If the result is success, that means an empty array.
            uint length = 0;
            NvmlReturn result = interopFunc(ref length, null, null);

            if (result == NvmlReturn.NVML_SUCCESS)
            {
                nvmlArray1 = Array.Empty<T1>();
                nvmlArray2 = Array.Empty<T2>();
                return result;
            }
            else if (result == NvmlReturn.NVML_ERROR_INSUFFICIENT_SIZE)
            {
                // Allocate the correct size, and call the interop again.
                T1[] buffer1 = new T1[length];
                T2[] buffer2 = new T2[length];
                fixed (T1* ptr1 = buffer1)
                fixed (T2* ptr2 = buffer2)
                {
                    result = interopFunc(ref length, ptr1, ptr2);
                    if (result == NvmlReturn.NVML_SUCCESS)
                    {
                        nvmlArray1 = buffer1;
                        nvmlArray2 = buffer2;
                    }
                    else
                    {
                        nvmlArray1 = default;
                        nvmlArray2 = default;
                    }
                    return result;
                }
            }
            else
            {
                nvmlArray1 = default;
                nvmlArray2 = default;
                return result;
            }
        }

        /// <summary>
        /// Delegate that is called to fill the array, with a length indicating the
        /// available space. Unlike <see cref="GetNvmlArray{T}"/>, this delegate cannot
        /// query the API for the number of records available.
        /// </summary>
        internal unsafe delegate NvmlReturn FillNvmlArrayInterop<T>(
            uint len,
            T* ptr)
            where T : unmanaged;

        /// <summary>
        /// Helper method to fill a fixed sized array of values from the NVML Interop API.
        /// </summary>
        /// <param name="interopFunc">The interop function.</param>
        /// <param name="length">The desired length.</param>
        /// <param name="nvmlArray">Filled in with the result array.</param>
        /// <returns>The interop status code.</returns>
        internal unsafe static NvmlReturn FillNvmlArray<T>(
            FillNvmlArrayInterop<T> interopFunc,
            uint length,
            out T[] nvmlArray)
            where T : unmanaged
        {
            // Allocate enough space for the requested length.
            T[] buffer = new T[length];

            fixed (T* ptr = buffer)
            {
                NvmlReturn result = interopFunc(length, ptr);
                nvmlArray = (result == NvmlReturn.NVML_SUCCESS) ? buffer : default;
                return result;
            }
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
        /// Provides access to <see cref="DeviceGetComputeRunningProcesses_v2_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetComputeRunningProcesses(
            IntPtr device,
            out NvmlProcessInfo[] infos)
        {
            NvmlReturn Interop(ref uint len, NvmlProcessInfo* ptr) =>
                DeviceGetComputeRunningProcesses_v2_Interop(device, ref len, ptr);
            return GetNvmlArray(Interop, out infos);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetEncoderSessions_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetEncoderSessions(
            IntPtr device,
            out NvmlEncoderSessionInfo[] sessionInfos)
        {
            NvmlReturn Interop(ref uint len, NvmlEncoderSessionInfo* ptr) =>
                DeviceGetEncoderSessions_Interop(device, ref len, ptr);
            return GetNvmlArray(Interop, out sessionInfos);
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
        /// Provides access to <see cref="DeviceGetGraphicsRunningProcesses_v2_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetGraphicsRunningProcesses(
            IntPtr device,
            out NvmlProcessInfo[] infos)
        {
            NvmlReturn Interop(ref uint len, NvmlProcessInfo* ptr) =>
                DeviceGetGraphicsRunningProcesses_v2_Interop(device, ref len, ptr);
            return GetNvmlArray(Interop, out infos);
        }

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
        /// Provides access to <see cref="DeviceGetRetiredPages_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetRetiredPages(
            IntPtr device,
            NvmlPageRetirementCause cause,
            out ulong[] addresses)
        {
            NvmlReturn Interop(ref uint len, ulong* ptr) =>
                DeviceGetRetiredPages_Interop(device, cause, ref len, ptr);
            return GetNvmlArray(Interop, out addresses);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetRetiredPages_v2_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetRetiredPages_v2(
            IntPtr device,
            NvmlPageRetirementCause cause,
            out ulong[] addresses,
            out ulong[] timestamps)
        {
            NvmlReturn Interop(ref uint len, ulong* ptr1, ulong* ptr2) =>
                DeviceGetRetiredPages_v2_Interop(device, cause, ref len, ptr1, ptr2);
            return GetNvmlArray(Interop, out addresses, out timestamps);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetSamples_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetSamples(
            IntPtr device,
            NvmlSamplingType type,
            ulong lastSeenTimeStamp,
            out NvmlValueType sampleValType,
            uint sampleCount,
            out NvmlSample[] samples)
        {
            // Allocate enough space for sampleCount.
            uint length = sampleCount;
            NvmlSample[] buffer = new NvmlSample[length];

            fixed (NvmlSample* ptr = buffer)
            {
                NvmlReturn result = DeviceGetSamples_Interop(
                    device,
                    type,
                    lastSeenTimeStamp,
                    out sampleValType,
                    ref length,
                    ptr);
                if (result == NvmlReturn.NVML_SUCCESS)
                {
                    // Adjust the return buffer to the actual length.
                    if (length < sampleCount)
                        Array.Resize(ref buffer, (int)length);
                    samples = buffer;
                }
                else
                {
                    samples = default;
                }

                return result;
            }
        }

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
        /// Provides access to <see cref="DeviceGetSupportedGraphicsClocks_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetSupportedGraphicsClocks(
            IntPtr device,
            uint memoryClockMHz,
            out uint[] clocksMHz)
        {
            NvmlReturn Interop(ref uint len, uint* ptr) =>
                DeviceGetSupportedGraphicsClocks_Interop(
                    device,
                    memoryClockMHz,
                    ref len,
                    ptr);
            return GetNvmlArray(Interop, out clocksMHz);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetSupportedMemoryClocks_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetSupportedMemoryClocks(
            IntPtr device,
            out uint[] clocksMHz)
        {
            NvmlReturn Interop(ref uint len, uint* ptr) =>
                DeviceGetSupportedMemoryClocks_Interop(device, ref len, ptr);
            return GetNvmlArray(Interop, out clocksMHz);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetTopologyNearestGpus_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetTopologyNearestGpus(
            IntPtr device,
            NvmlGpuTopologyLevel level,
            out IntPtr[] deviceArray)
        {
            NvmlReturn Interop(ref uint len, IntPtr* ptr) =>
                DeviceGetTopologyNearestGpus_Interop(device, level, ref len, ptr);
            return GetNvmlArray(Interop, out deviceArray);
        }

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
        /// Provides access to <see cref="SystemGetTopologyGpuSet_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn SystemGetTopologyGpuSet(
            uint cpuNumber,
            out IntPtr[] deviceArray)
        {
            NvmlReturn Interop(ref uint len, IntPtr* ptr) =>
                SystemGetTopologyGpuSet_Interop(cpuNumber, ref len, ptr);
            return GetNvmlArray(Interop, out deviceArray);
        }

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

        #region Device Queries - CPU and Memory Affinity

        /// <summary>
        /// Provides access to <see cref="DeviceGetCpuAffinity_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetCpuAffinity(
            IntPtr device,
            uint cpuSetSize,
            out ulong[] cpuSet)
        {
            NvmlReturn Interop(uint len, ulong* ptr) =>
                DeviceGetCpuAffinity_Interop(device, len, ptr);
            return FillNvmlArray(Interop, cpuSetSize, out cpuSet);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetCpuAffinityWithinScope_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetCpuAffinityWithinScope(
            IntPtr device,
            uint cpuSetSize,
            out ulong[] cpuSet,
            NvmlAffinityScope scope)
        {
            NvmlReturn Interop(uint len, ulong* ptr) =>
                DeviceGetCpuAffinityWithinScope_Interop(device, len, ptr, scope);
            return FillNvmlArray(Interop, cpuSetSize, out cpuSet);
        }

        /// <summary>
        /// Provides access to <see cref="DeviceGetMemoryAffinity_Interop"/>
        /// without using raw pointers.
        /// </summary>
        public unsafe NvmlReturn DeviceGetMemoryAffinity(
            IntPtr device,
            uint nodeSetSize,
            out ulong[] nodeSet,
            NvmlAffinityScope scope)
        {
            NvmlReturn Interop(uint len, ulong* ptr) =>
                DeviceGetMemoryAffinity_Interop(device, len, ptr, scope);
            return FillNvmlArray(Interop, nodeSetSize, out nodeSet);
        }

        #endregion
    }
}

#pragma warning restore CA1707 // Identifiers should not contain underscores
