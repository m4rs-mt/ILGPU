// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Resources;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using static ILGPU.Runtime.Cuda.CudaAPI;
using static ILGPU.Runtime.Cuda.CudaException;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a single Cuda device.
    /// </summary>
    [DeviceType(AcceleratorType.Cuda)]
    public sealed class CudaDevice : Device
    {
        #region Static

        /// <summary>
        /// Detects Cuda devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <returns>All detected Cuda devices.</returns>
        public static ImmutableArray<Device> GetDevices(
            Predicate<CudaDevice> predicate)
        {
            var registry = new DeviceRegistry();
            GetDevices(predicate, registry);
            return registry.ToImmutable();
        }


        /// <summary>
        /// Detects Cuda devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <param name="registry">The registry to add all devices to.</param>
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "We want to hide all exceptions at this level")]
        internal static void GetDevices(
            Predicate<CudaDevice> predicate,
            DeviceRegistry registry)
        {
            if (registry is null)
                throw new ArgumentNullException(nameof(registry));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            try
            {
                GetDevicesInternal(predicate, registry);
            }
            catch (Exception)
            {
                // Ignore API-specific exceptions at this point
            }
        }

        /// <summary>
        /// Detects Cuda devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <param name="registry">The registry to add all devices to.</param>
        private static void GetDevicesInternal(
            Predicate<CudaDevice> predicate,
            DeviceRegistry registry)
        {
            // Resolve all devices
            if (CurrentAPI.GetDeviceCount(out int numDevices) !=
                CudaError.CUDA_SUCCESS ||
                numDevices < 1)
            {
                return;
            }

            for (int i = 0; i < numDevices; ++i)
            {
                if (CurrentAPI.GetDevice(out int device, i) != CudaError.CUDA_SUCCESS)
                    continue;

                var desc = new CudaDevice(device);
                if (predicate(desc))
                    registry.Register(desc);
            }
        }

        /// <summary>
        /// Tries to get the Cuda architecture for the given major and minor versions.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <param name="architecture">The determined architecture (if any).</param>
        /// <returns>True, if the architecture is supported.</returns>
        public static bool TryGetArchitecture(
            int major,
            int minor,
            out CudaArchitecture architecture)
        {
            architecture = new CudaArchitecture(major, minor);
            return architecture >= CudaArchitecture.SM_30;
        }

        /// <summary>
        /// Resolves the Cuda architecture for the given major and minor versions.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <returns>The resolved Cuda version.</returns>
        public static CudaArchitecture GetArchitecture(int major, int minor) =>
            TryGetArchitecture(major, minor, out var arch)
            ? arch
            : throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedPTXArchitecture);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Cuda accelerator reference.
        /// </summary>
        /// <param name="deviceId">The Cuda device id.</param>
        internal CudaDevice(int deviceId)
        {
            if (deviceId < 0)
                throw new ArgumentOutOfRangeException(nameof(deviceId));

            Backend.EnsureRunningOnPlatform(TargetPlatform.X64);

            DeviceId = deviceId;

            InitDeviceInfo();
            InitArchitectureInfo();
            InitGridInfo();
            InitMemoryInfo();
            InitMiscInfo();
            InitPCIInfo();

            Capabilities = new CudaCapabilityContext(
                Architecture ?? CudaArchitecture.SM_30);
        }

        /// <summary>
        /// Init general device information.
        /// </summary>
        private void InitDeviceInfo()
        {
            // Get the device name
            ThrowIfFailed(
                CurrentAPI.GetDeviceName(out string name, DeviceId));
            Name = name;

            // Resolve clock rate
            ClockRate = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_CLOCK_RATE, DeviceId) / 1000;

            // Resolve warp size
            WarpSize = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_WARP_SIZE, DeviceId);

            // Resolve number of multiprocessors
            NumMultiprocessors = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT, DeviceId);

            // Result max number of threads per multiprocessor
            MaxNumThreadsPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR,
                DeviceId);

            // Resolve the current driver mode
            DriverMode = (DeviceDriverMode)CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_TCC_DRIVER,
                DeviceId);
        }

        /// <summary>
        /// Init architecture information.
        /// </summary>
        private void InitArchitectureInfo()
        {
            // Determine the driver version
            ThrowIfFailed(
                CurrentAPI.GetDriverVersion(out var driverVersion));
            DriverVersion = driverVersion;

            // Setup architecture and instruction set
            ThrowIfFailed(
                CurrentAPI.GetDeviceComputeCapability(
                    out int major,
                    out int minor,
                DeviceId));
            Architecture = GetArchitecture(major, minor);

            if (Architecture.HasValue && CudaAccelerator.TryGetInstructionSet(
                Architecture.Value,
                driverVersion,
                out var _,
                out var instructionSet))
            {
                InstructionSet = instructionSet;
            }
        }

        /// <summary>
        /// Init grid information.
        /// </summary>
        private void InitGridInfo()
        {
            // Resolve max grid size
            MaxGridSize = new Index3D(
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_X, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Y, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Z, DeviceId));

            // Resolve max group size
            MaxGroupSize = new Index3D(
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_X, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Y, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Z, DeviceId));

            // Resolve max threads per group
            MaxNumThreadsPerGroup = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK, DeviceId);
        }

        /// <summary>
        /// Init memory information.
        /// </summary>
        private void InitMemoryInfo()
        {
            // Resolve the total memory size
            ThrowIfFailed(
                CurrentAPI.GetTotalDeviceMemory(out long total, DeviceId));
            MemorySize = total;

            // Resolve max shared memory per block
            MaxSharedMemoryPerGroup = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK,
                DeviceId);

            // Resolve the maximum amount of shared memory per multiprocessor
            MaxSharedMemoryPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.
                    CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_MULTIPROCESSOR,
                DeviceId);

            // Resolve total constant memory
            MaxConstantMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_TOTAL_CONSTANT_MEMORY, DeviceId);

            // Resolve memory clock rate
            MemoryClockRate = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MEMORY_CLOCK_RATE, DeviceId)
                / 1000;

            // Resolve the bus width
            MemoryBusWidth = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_GLOBAL_MEMORY_BUS_WIDTH,
                DeviceId);
        }

        /// <summary>
        /// Init misc information.
        /// </summary>
        private void InitMiscInfo()
        {
            // Resolve the L2 cache size
            L2CacheSize = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_L2_CACHE_SIZE, DeviceId);

            // Resolve the total number of registers per multiprocessor
            TotalNumRegistersPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_MULTIPROCESSOR,
                DeviceId);

            // Resolve the total number of registers per group
            TotalNumRegistersPerGroup = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_BLOCK,
                DeviceId);

            // Resolve the max memory pitch
            MaxMemoryPitch = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MAX_PITCH, DeviceId);

            // Resolve the number of concurrent copy engines
            NumConcurrentCopyEngines = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_ASYNC_ENGINE_COUNT, DeviceId);

            // Resolve whether this device has ECC support
            HasECCSupport = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_ECC_ENABLED, DeviceId) != 0;

            // Resolve whether this device supports managed memory
            SupportsManagedMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_MANAGED_MEMORY, DeviceId) != 0;

            // Resolve whether this device supports compute preemption
            SupportsComputePreemption = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_COMPUTE_PREEMPTION_SUPPORTED,
                DeviceId) != 0;

            // Resolve whether this device supports using host memory
            SupportsMappingHostMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_CAN_MAP_HOST_MEMORY,
                DeviceId) != 0;
            SupportsUsingHostPointerForRegisteredMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.
                    CU_DEVICE_ATTRIBUTE_CAN_USE_HOST_POINTER_FOR_REGISTERED_MEM,
                DeviceId) != 0;
        }

        /// <summary>
        /// Init PCI information.
        /// </summary>
        private void InitPCIInfo()
        {
            // Resolve the PCI domain id
            PCIDomainId = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_PCI_DOMAIN_ID,
                DeviceId);

            // Resolve the PCI device id
            PCIBusId = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_PCI_BUS_ID,
                DeviceId);

            // Resolve the PCI device id
            PCIDeviceId = CurrentAPI.GetDeviceAttribute(
                DeviceAttributeKind.CU_DEVICE_ATTRIBUTE_PCI_DEVICE_ID,
                DeviceId);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Cuda device id.
        /// </summary>
        public int DeviceId { get; }

        /// <summary>
        /// Returns the current driver version.
        /// </summary>
        public CudaDriverVersion DriverVersion { get; private set; }

        /// <summary>
        /// Returns the PTX architecture (if supported).
        /// </summary>
        public CudaArchitecture? Architecture { get; private set; }

        /// <summary>
        /// Returns the PTX instruction set (if supported).
        /// </summary>
        public CudaInstructionSet? InstructionSet { get; private set; }

        /// <summary>
        /// Returns the clock rate.
        /// </summary>
        public int ClockRate { get; private set; }

        /// <summary>
        /// Returns the memory clock rate.
        /// </summary>
        public int MemoryClockRate { get; private set; }

        /// <summary>
        /// Returns the memory clock rate.
        /// </summary>
        public int MemoryBusWidth { get; private set; }

        /// <summary>
        /// Returns L2 cache size.
        /// </summary>
        public int L2CacheSize { get; private set; }

        /// <summary>
        /// Returns the maximum shared memory size per multiprocessor.
        /// </summary>
        public int MaxSharedMemoryPerMultiprocessor { get; private set; }

        /// <summary>
        /// Returns the total number of registers per multiprocessor.
        /// </summary>
        public int TotalNumRegistersPerMultiprocessor { get; private set; }

        /// <summary>
        /// Returns the total number of registers per group.
        /// </summary>
        public int TotalNumRegistersPerGroup { get; private set; }

        /// <summary>
        /// Returns the maximum memory pitch in bytes.
        /// </summary>
        public long MaxMemoryPitch { get; private set; }

        /// <summary>
        /// Returns the number of concurrent copy engines (if any, result > 0).
        /// </summary>
        public int NumConcurrentCopyEngines { get; private set; }

        /// <summary>
        /// Returns true if this device has ECC support.
        /// </summary>
        public bool HasECCSupport { get; private set; }

        /// <summary>
        /// Returns true if this device supports managed memory allocations.
        /// </summary>
        public bool SupportsManagedMemory { get; private set; }

        /// <summary>
        /// Returns true if this device supports compute preemption.
        /// </summary>
        public bool SupportsComputePreemption { get; private set; }

        /// <summary>
        /// Returns true if this device supports mapping host memory.
        /// </summary>
        public bool SupportsMappingHostMemory { get; private set; }

        /// <summary>
        /// Returns true if this device supports using the host pointer for registered
        /// memory.
        /// </summary>
        public bool SupportsUsingHostPointerForRegisteredMemory { get; private set; }

        /// <summary>
        /// Returns the current device driver mode.
        /// </summary>
        public DeviceDriverMode DriverMode { get; private set; }

        /// <summary>
        /// Returns the PCI domain id.
        /// </summary>
        public int PCIDomainId { get; private set; }

        /// <summary>
        /// Returns the PCI bus id.
        /// </summary>
        public int PCIBusId { get; private set; }

        /// <summary>
        /// Returns the PCI device id.
        /// </summary>
        public int PCIDeviceId { get; private set; }

        /// <summary>
        /// Returns an NVML library compatible PCI bus id.
        /// </summary>
        public string NVMLPCIBusId =>
            $"{PCIDomainId:X4}:{PCIBusId:X2}:{PCIDeviceId:X2}.0";

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override Accelerator CreateAccelerator(Context context) =>
            CreateCudaAccelerator(context);

        /// <summary>
        /// Creates a new Cuda accelerator using
        /// <see cref="CudaAcceleratorFlags.ScheduleAuto"/>.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>The created Cuda accelerator.</returns>
        public CudaAccelerator CreateCudaAccelerator(Context context) =>
            CreateCudaAccelerator(context, CudaAcceleratorFlags.ScheduleAuto);

        /// <summary>
        /// Creates a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        /// <returns>The created Cuda accelerator.</returns>
        public CudaAccelerator CreateCudaAccelerator(
            Context context,
            CudaAcceleratorFlags acceleratorFlags) =>
            new CudaAccelerator(context, this, acceleratorFlags);

        /// <inheritdoc/>
        protected override void PrintHeader(TextWriter writer)
        {
            base.PrintHeader(writer);

            writer.Write("  Cuda device id:                          ");
            writer.WriteLine(DeviceId);

            writer.Write("  Cuda driver version:                     ");
            writer.WriteLine("{0}.{1}", DriverVersion.Major, DriverVersion.Minor);

            writer.Write("  Cuda architecture:                       ");
            writer.WriteLine(Architecture.ToString());

            writer.Write("  Instruction set:                         ");
            writer.WriteLine(InstructionSet.ToString());

            writer.Write("  Clock rate:                              ");
            writer.Write(ClockRate);
            writer.WriteLine(" MHz");

            writer.Write("  Memory clock rate:                       ");
            writer.Write(MemoryClockRate);
            writer.WriteLine(" MHz");

            writer.Write("  Memory bus width:                        ");
            writer.Write(MemoryBusWidth);
            writer.WriteLine("-bit");
        }

        /// <inheritdoc/>
        protected override void PrintGeneralInfo(TextWriter writer)
        {
            base.PrintGeneralInfo(writer);

            writer.Write("  Total amount of shared memory per mp:    ");
            writer.WriteLine(
                "{0} bytes, {1} KB",
                MaxSharedMemoryPerMultiprocessor,
                MaxSharedMemoryPerMultiprocessor / 1024);

            writer.Write("  L2 cache size:                           ");
            writer.WriteLine(
                "{0} bytes, {1} KB",
                L2CacheSize,
                L2CacheSize / 1024);

            writer.Write("  Max memory pitch:                        ");
            writer.Write(MaxMemoryPitch);
            writer.WriteLine(" bytes");

            writer.Write("  Total number of registers per mp:        ");
            writer.WriteLine(TotalNumRegistersPerMultiprocessor);

            writer.Write("  Total number of registers per group:     ");
            writer.WriteLine(TotalNumRegistersPerGroup);

            writer.Write("  Concurrent copy and kernel execution:    ");
            if (NumConcurrentCopyEngines < 1)
                writer.WriteLine("False");
            else
                writer.WriteLine("True, with {0} copy engines", NumConcurrentCopyEngines);

            writer.Write("  Driver mode:                             ");
            writer.WriteLine(DriverMode.ToString());

            writer.Write("  Has ECC support:                         ");
            writer.WriteLine(HasECCSupport);

            writer.Write("  Supports managed memory:                 ");
            writer.WriteLine(SupportsManagedMemory);

            writer.Write("  Supports compute preemption:             ");
            writer.WriteLine(SupportsComputePreemption);

            writer.Write("  PCI domain id / bus id / device id:      ");
            writer.WriteLine("{0} / {1} / {2}", PCIDomainId, PCIBusId, PCIDeviceId);

            writer.Write("  NVML PCI bus id:                         ");
            writer.WriteLine(NVMLPCIBusId);
        }

        #endregion

        #region Object

        /// <inheritdoc/>
        public override bool Equals(object obj) =>
            obj is CudaDevice device &&
            device.PCIDomainId == PCIDomainId &&
            device.PCIBusId == PCIBusId &&
            device.PCIDeviceId == PCIDeviceId;

        /// <inheritdoc/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ PCIDomainId ^ PCIBusId ^ PCIDeviceId;

        #endregion
    }
}
