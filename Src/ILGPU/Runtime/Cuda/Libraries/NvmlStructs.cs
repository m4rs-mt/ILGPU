// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvmlStructs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.Cuda
{
    public struct NvmlDeviceAttributes
    {
        public uint MultiprocessorCount;
        public uint SharedCopyEngineCount;
        public uint SharedDecoderCount;
        public uint SharedEncoderCount;
        public uint SharedJpegCount;
        public uint SharedOfaCount;
        public uint GpuInstanceSliceCount;
        public uint ComputeInstanceSliceCount;
        public ulong MemorySizeMB;
    }

    public struct NvmlBAR1Memory
    {
        public ulong Bar1Total;
        public ulong Bar1Free;
        public ulong Bar1Used;
    }

    public struct NvmlBridgeChipInfo
    {
        internal const int INTEROP_SIZE = sizeof(NvmlBridgeChipType) + sizeof(uint);

        public NvmlBridgeChipType Type;
        public uint FwVersion;
    }

    [StructLayout(LayoutKind.Explicit, Size = INTEROP_SIZE)]
    public unsafe struct NvmlBridgeChipHierarchy_Interop
    {
        public const int NVML_MAX_PHYSICAL_BRIDGE = 128;

        internal const int INTEROP_SIZE =
            sizeof(byte) + (NVML_MAX_PHYSICAL_BRIDGE * NvmlBridgeChipInfo.INTEROP_SIZE);

        [FieldOffset(0)]
        public byte BridgeCount;

        [FieldOffset(1)]
        public NvmlBridgeChipInfo* BridgeChipInfo;
    }

    public struct NvmlBridgeChipHierarchy
    {
        public byte BridgeCount;
        public NvmlBridgeChipInfo[] BridgeChipInfo;
    }

    public struct NvmlEccErrorCounts
    {
        public ulong L1Cache;
        public ulong L2Cache;
        public ulong DeviceMemory;
        public ulong RegisterFile;
    }

    public struct NvmlEncoderSessionInfo
    {
        public uint SessionId;
        public uint Pid;
        public uint VgpuInstance;
        public NvmlEncoderType CodecType;
        public uint HResolution;
        public uint VResolution;
        public uint AverageFps;
        public uint AverageLatency;
    }

    public struct NvmlFBCSessionInfo
    {
        public uint SessionId;
        public uint Pid;
        public uint VgpuInstance;
        public uint DisplayOrdinal;
        public NvmlFBCSessionType SessionType;
        public uint SessionFlags;
        public uint HMaxResolution;
        public uint VMaxResolution;
        public uint HResolution;
        public uint VResolution;
        public uint AverageFPS;
        public uint AverageLatency;
    }

    public struct NvmlFBCStats
    {
        public uint SessionsCount;
        public uint AverageFPS;
        public uint AverageLatency;
    }

    public unsafe struct NvmlHwbcEntry_Interop
    {
        public const int NVML_MAX_FIRMWAREVERSION = 32;

        public uint HwbcId;
        public fixed byte FirmwareVersion[NVML_MAX_FIRMWAREVERSION];
    }

    public struct NvmlHwbcEntry
    {
        public uint HwbcId;
        public string FirmwareVersion;
    }

    public unsafe struct NvmlLedState_Interop
    {
        public const int NVML_MAX_CAUSE = 256;

        public fixed byte Cause[NVML_MAX_CAUSE];
        public NvmlLedColor Color;
    }

    public struct NvmlLedState
    {
        public string Cause;
        public NvmlLedColor Color;
    }

    public struct NvmlMemory
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    public unsafe struct NvmlPciInfo_Interop
    {
        public fixed byte BusIdLegacy[
            (int)NvmlConstants.NVML_DEVICE_PCI_BUS_ID_BUFFER_V2_SIZE];
        public uint Domain;
        public uint Bus;
        public uint Device;
        public uint PciDeviceId;
        public uint PciSubSystemId;
        public fixed byte BusId[(int)NvmlConstants.NVML_DEVICE_PCI_BUS_ID_BUFFER_SIZE];
    }

    public unsafe struct NvmlPciInfo
    {
        public string BusIdLegacy;
        public uint Domain;
        public uint Bus;
        public uint Device;
        public uint PciDeviceId;
        public uint PciSubSystemId;
        public string BusId;
    }

    public struct NvmlProcessInfo
    {
        public uint Pid;
        public ulong UsedGpuMemory;
        public uint GpuInstanceId;
        public uint ComputeInstanceId;
    }

    public unsafe struct NvmlPSUInfo_Interop
    {
        public const int NVML_MAX_STATE = 80;
        public uint Current;
        public uint Power;
        public fixed byte State[NVML_MAX_STATE];
        public uint Voltage;
    }

    public unsafe struct NvmlPSUInfo
    {
        public uint Current;
        public uint Power;
        public string State;
        public uint Voltage;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct NvmlSample
    {
        [FieldOffset(0)]
        public ulong TimeStamp;

        [FieldOffset(8)]
        public double DVal;

        [FieldOffset(8)]
        public uint UiVal;

        [FieldOffset(8)]
        public ulong UlVal;

        [FieldOffset(8)]
        public ulong UllVal;

        [FieldOffset(8)]
        public long SllVal;
    }

    public struct NvmlUnitFanInfo
    {
        internal const int INTEROP_SIZE = sizeof(uint) + sizeof(NvmlFanState);

        public uint Speed;
        public NvmlFanState State;
    }

    [StructLayout(LayoutKind.Explicit, Size = INTEROP_SIZE)]
    public unsafe struct NvmlUnitFanSpeeds_Interop
    {
        public const int NVML_MAX_FANS = 24;

        internal const int INTEROP_SIZE =
            sizeof(uint) + (NVML_MAX_FANS * NvmlUnitFanInfo.INTEROP_SIZE);

        [FieldOffset(0)]
        public uint Count;

        [FieldOffset(4)]
        public NvmlUnitFanInfo* Fans;
    }

    public unsafe struct NvmlUnitFanSpeeds
    {
        public uint Count;
        public NvmlUnitFanInfo[] Fans;
    }

    public unsafe struct NvmlUnitInfo_Interop
    {
        public const int NVML_MAX_STRLEN = 96;

        public fixed byte FirmwareVersion[NVML_MAX_STRLEN];
        public fixed byte Id[NVML_MAX_STRLEN];
        public fixed byte Name[NVML_MAX_STRLEN];
        public fixed byte Serial[NVML_MAX_STRLEN];
    }

    public struct NvmlUnitInfo
    {
        public string FirmwareVersion;
        public string Id;
        public string Name;
        public string Serial;
    }

    public struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    public struct NvmlViolationTime
    {
        public ulong ReferenceTime;
        public ulong ViolationTime;
    }
}

#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
