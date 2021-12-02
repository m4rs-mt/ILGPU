// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvmlEnums.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.Cuda
{
    public enum NvmlBrandType : int
    {
        NVML_BRAND_UNKNOWN = 0,
        NVML_BRAND_QUADRO = 1,
        NVML_BRAND_TESLA = 2,
        NVML_BRAND_NVS = 3,
        NVML_BRAND_GRID = 4,
        NVML_BRAND_GEFORCE = 5,
        NVML_BRAND_TITAN = 6,
        NVML_BRAND_COUNT
    }

    public enum NvmlBridgeChipType : int
    {
        NVML_BRIDGE_CHIP_PLX = 0,
        NVML_BRIDGE_CHIP_BRO4 = 1
    }

    public enum NvmlClockId : int
    {
        NVML_CLOCK_ID_CURRENT = 0,
        NVML_CLOCK_ID_APP_CLOCK_TARGET = 1,
        NVML_CLOCK_ID_APP_CLOCK_DEFAULT = 2,
        NVML_CLOCK_ID_CUSTOMER_BOOST_MAX = 3,
        NVML_CLOCK_ID_COUNT
    }

    public enum NvmlClockType : int
    {
        NVML_CLOCK_GRAPHICS = 0,
        NVML_CLOCK_SM = 1,
        NVML_CLOCK_MEM = 2,
        NVML_CLOCK_VIDEO = 3,
        NVML_CLOCK_COUNT
    }

    [Flags]
    public enum NvmlClocksThrottleReasons : int
    {
        None = 0,
        GpuIdle = 1 << 0,
        ApplicationsClocksSetting = 1 << 1,
        UserDefinedClocks = ApplicationsClocksSetting,
        SwPowerCap = 1 << 2,
        HwSlowdown = 1 << 3,
        SyncBoost = 1 << 4,
        SwThermalSlowdown = 1 << 5,
        HwThermalSlowdown = 1 << 6,
        HwPowerBrakeSlowdown = 1 << 7,
        DisplayClockSetting = 1 << 8,
    }

    public enum NvmlComputeMode : int
    {
        NVML_COMPUTEMODE_DEFAULT = 0,
        NVML_COMPUTEMODE_EXCLUSIVE_THREAD = 1,
        NVML_COMPUTEMODE_PROHIBITED = 2,
        NVML_COMPUTEMODE_EXCLUSIVE_PROCESS = 3,
        NVML_COMPUTEMODE_COUNT
    }

    public enum NvmlDeviceArchitecture : int
    {
        NVML_DEVICE_ARCH_KEPLER = 2,
        NVML_DEVICE_ARCH_MAXWELL = 3,
        NVML_DEVICE_ARCH_PASCAL = 4,
        NVML_DEVICE_ARCH_VOLTA = 5,
        NVML_DEVICE_ARCH_TURING = 6,
        NVML_DEVICE_ARCH_AMPERE = 7,
        NVML_DEVICE_ARCH_UNKNOWN = unchecked((int)0xffffffff)
    }

    public enum NvmlDriverModel : int
    {
        NVML_DRIVER_WDDM = 0,
        NVML_DRIVER_WDM = 1,
    }

    public enum NvmlEccCounterType : int
    {
        NVML_VOLATILE_ECC = 0,
        NVML_AGGREGATE_ECC = 1,
        NVML_ECC_COUNTER_TYPE_COUNT
    }

    public enum NvmlEnableState : int
    {
        NVML_FEATURE_DISABLED = 0,
        NVML_FEATURE_ENABLED = 1,
    }

    public enum NvmlEncoderType
    {
        NVML_ENCODER_QUERY_H264 = 0,
        NVML_ENCODER_QUERY_HEVC = 1,
    }

    public enum NvmlGpuOperationMode : int
    {
        NVML_GOM_ALL_ON = 0,
        NVML_GOM_COMPUTE = 1,
        NVML_GOM_LOW_DP = 2
    }

    public enum NvmlGpuP2PCapsIndex : int
    {
        NVML_P2P_CAPS_INDEX_READ = 0,
        NVML_P2P_CAPS_INDEX_WRITE,
        NVML_P2P_CAPS_INDEX_NVLINK,
        NVML_P2P_CAPS_INDEX_ATOMICS,
        NVML_P2P_CAPS_INDEX_PROP,
        NVML_P2P_CAPS_INDEX_UNKNOWN
    }

    public enum NvmlGpuP2PStatus : int
    {
        NVML_P2P_STATUS_OK = 0,
        NVML_P2P_STATUS_CHIPSET_NOT_SUPPORED,
        NVML_P2P_STATUS_GPU_NOT_SUPPORTED,
        NVML_P2P_STATUS_IOH_TOPOLOGY_NOT_SUPPORTED,
        NVML_P2P_STATUS_DISABLED_BY_REGKEY,
        NVML_P2P_STATUS_NOT_SUPPORTED,
        NVML_P2P_STATUS_UNKNOWN
    }

    public enum NvmlGpuTopologyLevel : int
    {
        NVML_TOPOLOGY_INTERNAL = 0,
        NVML_TOPOLOGY_SINGLE = 10,
        NVML_TOPOLOGY_MULTIPLE = 20,
        NVML_TOPOLOGY_HOSTBRIDGE = 30,
        NVML_TOPOLOGY_NODE = 40,
        NVML_TOPOLOGY_SYSTEM = 50,
    }

    public enum NvmlInforomObject : int
    {
        NVML_INFOROM_OEM = 0,
        NVML_INFOROM_ECC = 1,
        NVML_INFOROM_POWER = 2,
        NVML_INFOROM_COUNT
    }

    public enum NvmlMemoryErrorType : int
    {
        NVML_MEMORY_ERROR_TYPE_CORRECTED = 0,
        NVML_MEMORY_ERROR_TYPE_UNCORRECTED = 1,
        NVML_MEMORY_ERROR_TYPE_COUNT
    }

    [SuppressMessage(
        "Design",
        "CA1027:Mark enums with FlagsAttribute",
        Justification = "This is not a flag enumeration")]
    public enum NvmlMemoryLocation : int
    {
        NVML_MEMORY_LOCATION_L1_CACHE = 0,
        NVML_MEMORY_LOCATION_L2_CACHE = 1,
        NVML_MEMORY_LOCATION_DRAM = 2,
        NVML_MEMORY_LOCATION_DEVICE_MEMORY = NVML_MEMORY_LOCATION_DRAM,
        NVML_MEMORY_LOCATION_REGISTER_FILE = 3,
        NVML_MEMORY_LOCATION_TEXTURE_MEMORY = 4,
        NVML_MEMORY_LOCATION_TEXTURE_SHM = 5,
        NVML_MEMORY_LOCATION_CBU = 6,
        NVML_MEMORY_LOCATION_SRAM = 7,
        NVML_MEMORY_LOCATION_COUNT
    }

    public enum NvmlPcieUtilCounter : int
    {
        NVML_PCIE_UTIL_TX_BYTES = 0, // 1KB granularity
        NVML_PCIE_UTIL_RX_BYTES = 1, // 1KB granularity
        NVML_PCIE_UTIL_COUNT
    }

    public enum NvmlPerfPolicyType : int
    {
        NVML_PERF_POLICY_POWER = 0,
        NVML_PERF_POLICY_THERMAL = 1,
        NVML_PERF_POLICY_SYNC_BOOST = 2,
        NVML_PERF_POLICY_BOARD_LIMIT = 3,
        NVML_PERF_POLICY_LOW_UTILIZATION = 4,
        NVML_PERF_POLICY_RELIABILITY = 5,
        NVML_PERF_POLICY_TOTAL_APP_CLOCKS = 10,
        NVML_PERF_POLICY_TOTAL_BASE_CLOCKS = 11,
        NVML_PERF_POLICY_COUNT
    }

    [SuppressMessage(
        "Design",
        "CA1027:Mark enums with FlagsAttribute",
        Justification = "This is not a flag enumeration")]
    public enum NvmlPstates : int
    {
        NVML_PSTATE_0 = 0,
        NVML_PSTATE_1 = 1,
        NVML_PSTATE_2 = 2,
        NVML_PSTATE_3 = 3,
        NVML_PSTATE_4 = 4,
        NVML_PSTATE_5 = 5,
        NVML_PSTATE_6 = 6,
        NVML_PSTATE_7 = 7,
        NVML_PSTATE_8 = 8,
        NVML_PSTATE_9 = 9,
        NVML_PSTATE_10 = 10,
        NVML_PSTATE_11 = 11,
        NVML_PSTATE_12 = 12,
        NVML_PSTATE_13 = 13,
        NVML_PSTATE_14 = 14,
        NVML_PSTATE_15 = 15,
        NVML_PSTATE_UNKNOWN = 32
    }

    public enum NvmlRestrictedAPI : int
    {
        NVML_RESTRICTED_API_SET_APPLICATION_CLOCKS = 0,
        NVML_RESTRICTED_API_SET_AUTO_BOOSTED_CLOCKS = 1,
        NVML_RESTRICTED_API_COUNT
    }

    public enum NvmlReturn : int
    {
        NVML_SUCCESS = 0,
        NVML_ERROR_UNINITIALIZED = 1,
        NVML_ERROR_INVALID_ARGUMENT = 2,
        NVML_ERROR_NOT_SUPPORTED = 3,
        NVML_ERROR_NO_PERMISSION = 4,
        NVML_ERROR_ALREADY_INITIALIZED = 5,
        NVML_ERROR_NOT_FOUND = 6,
        NVML_ERROR_INSUFFICIENT_SIZE = 7,
        NVML_ERROR_INSUFFICIENT_POWER = 8,
        NVML_ERROR_DRIVER_NOT_LOADED = 9,
        NVML_ERROR_TIMEOUT = 10,
        NVML_ERROR_IRQ_ISSUE = 11,
        NVML_ERROR_LIBRARY_NOT_FOUND = 12,
        NVML_ERROR_FUNCTION_NOT_FOUND = 13,
        NVML_ERROR_CORRUPTED_INFOROM = 14,
        NVML_ERROR_GPU_IS_LOST = 15,
        NVML_ERROR_RESET_REQUIRED = 16,
        NVML_ERROR_OPERATING_SYSTEM = 17,
        NVML_ERROR_LIB_RM_VERSION_MISMATCH = 18,
        NVML_ERROR_IN_USE = 19,
        NVML_ERROR_MEMORY = 20,
        NVML_ERROR_NO_DATA = 21,
        NVML_ERROR_VGPU_ECC_NOT_SUPPORTED = 22,
        NVML_ERROR_INSUFFICIENT_RESOURCES = 23,
        NVML_ERROR_UNKNOWN = 999,
    }

    public enum NvmlTemperatureSensors : int
    {
        NVML_TEMPERATURE_GPU = 0,
        NVML_TEMPERATURE_COUNT
    }

    public enum NvmlTemperatureThresholds : int
    {
        NVML_TEMPERATURE_THRESHOLD_SHUTDOWN = 0,
        NVML_TEMPERATURE_THRESHOLD_SLOWDOWN = 1,
        NVML_TEMPERATURE_THRESHOLD_MEM_MAX = 2,
        NVML_TEMPERATURE_THRESHOLD_GPU_MAX = 3,
        NVML_TEMPERATURE_THRESHOLD_COUNT
    }

    [CLSCompliant(false)]
    public static class NvmlConstants
    {
        public const uint NVML_DEVICE_INFOROM_VERSION_BUFFER_SIZE = 16;
        public const uint NVML_DEVICE_NAME_V2_BUFFER_SIZE = 96;
        public const uint NVML_DEVICE_PART_NUMBER_BUFFER_SIZE = 80;
        public const uint NVML_DEVICE_PCI_BUS_ID_BUFFER_SIZE = 32;
        public const uint NVML_DEVICE_PCI_BUS_ID_BUFFER_V2_SIZE = 16;
        public const uint NVML_DEVICE_SERIAL_BUFFER_SIZE = 30;
        public const uint NVML_DEVICE_UUID_V2_BUFFER_SIZE = 96;
        public const uint NVML_DEVICE_VBIOS_VERSION_BUFFER_SIZE = 32;
        public const uint NVML_SYSTEM_DRIVER_VERSION_BUFFER_SIZE = 80;
        public const uint NVML_SYSTEM_NVML_VERSION_BUFFER_SIZE = 80;
    }
}

#pragma warning restore CA1008 // Enums should have zero value
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
