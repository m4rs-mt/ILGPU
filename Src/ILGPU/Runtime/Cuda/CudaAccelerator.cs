// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Backends.PTX;
using ILGPU.Resources;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda accelerator.
    /// </summary>
    public sealed class CudaAccelerator :
        KernelAccelerator<PTXCompiledKernel, CudaKernel>
    {
        #region Static

        /// <summary>
        /// Represents the <see cref="CurrentAPI"/> property.
        /// </summary>
        private static readonly MethodInfo GetCudaAPIMethod =
            typeof(CudaAPI).GetProperty(
                nameof(CurrentAPI),
                BindingFlags.Public | BindingFlags.Static).GetGetMethod();

        /// <summary>
        /// Represents the <see cref="CudaAPI.LaunchKernelWithStruct{T}(
        /// CudaStream, CudaKernel, RuntimeKernelConfig, ref T, int)"/> method.
        /// </summary>
        private static readonly MethodInfo LaunchKernelMethod =
            typeof(CudaAPI).GetMethod(
                nameof(CudaAPI.LaunchKernelWithStruct),
                BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Represents the <see cref="CudaException.ThrowIfFailed(CudaError)" /> method.
        /// </summary>
        private static readonly MethodInfo ThrowIfFailedMethod =
            typeof(CudaException).GetMethod(
                nameof(CudaException.ThrowIfFailed),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Detects all cuda accelerators.
        /// </summary>
        static CudaAccelerator()
        {
            CudaAccelerators = ImmutableArray<CudaAcceleratorId>.Empty;

            // Resolve all devices
            if (CurrentAPI.GetDeviceCount(out int numDevices) !=
                CudaError.CUDA_SUCCESS ||
                numDevices < 1)
            {
                return;
            }

            var accelerators = ImmutableArray.CreateBuilder<CudaAcceleratorId>(
                numDevices);
            for (int i = 0; i < numDevices; ++i)
            {
                if (CurrentAPI.GetDevice(out int device, i) != CudaError.CUDA_SUCCESS)
                    continue;
                accelerators.Add(new CudaAcceleratorId(device));
            }
            CudaAccelerators = accelerators.ToImmutable();
        }

        /// <summary>
        /// Represents the list of available Cuda accelerators.
        /// </summary>
        public static ImmutableArray<CudaAcceleratorId> CudaAccelerators { get; }

        /// <summary>
        /// Resolves the memory type of the given device pointer.
        /// </summary>
        /// <param name="value">The device pointer to check.</param>
        /// <returns>The resolved memory type</returns>
        public static unsafe CudaMemoryType GetCudaMemoryType(IntPtr value)
        {
            // This functionality requires unified addresses (X64)
            Backends.Backend.EnsureRunningOnPlatform(TargetPlatform.X64);

            int data = 0;
            var err = CurrentAPI.GetPointerAttribute(
                    new IntPtr(Unsafe.AsPointer(ref data)),
                    PointerAttribute.CU_POINTER_ATTRIBUTE_MEMORY_TYPE,
                    value);
            if (err == CudaError.CUDA_ERROR_INVALID_VALUE)
                return CudaMemoryType.None;
            CudaException.ThrowIfFailed(err);
            return (CudaMemoryType)data;
        }

        /// <summary>
        /// Returns the PTX instruction set to use, based on the PTX architecture and
        /// installed CUDA drivers.
        /// </summary>
        /// <param name="architecture">The PTX architecture</param>
        /// <param name="installedDriverVersion">The CUDA driver version</param>
        /// <returns>The PTX instruction set</returns>
        public static PTXInstructionSet GetInstructionSet(
            PTXArchitecture architecture,
            CudaDriverVersion installedDriverVersion)
        {
            var architectureMinDriverVersion = CudaDriverVersionUtils
                .GetMinimumDriverVersion(architecture);
            var minDriverVersion = architectureMinDriverVersion;
            foreach (var instructionSet in PTXCodeGenerator.SupportedInstructionSets)
            {
                var instructionSetMinDriverVersion = CudaDriverVersionUtils.
                    GetMinimumDriverVersion(instructionSet);
                minDriverVersion =
                    architectureMinDriverVersion >= instructionSetMinDriverVersion
                    ? architectureMinDriverVersion
                    : instructionSetMinDriverVersion;
                if (installedDriverVersion >= minDriverVersion)
                    return instructionSet;
            }

            throw new NotSupportedException(
                string.Format(
                    RuntimeErrorMessages.NotSupportedDriverVersion,
                    installedDriverVersion,
                    minDriverVersion));
        }

        #endregion

        #region Instance

        private CudaSharedMemoryConfiguration sharedMemoryConfiguration;
        private CudaCacheConfiguration cacheConfiguration;

        /// <summary>
        /// Constructs a new Cuda accelerator targeting the default device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        public CudaAccelerator(Context context)
            : this(context, 0)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The accelerator id.</param>
        public CudaAccelerator(Context context, CudaAcceleratorId acceleratorId)
            : this(context, acceleratorId.DeviceId)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        public CudaAccelerator(Context context, int deviceId)
            : this(context, deviceId, CudaAcceleratorFlags.ScheduleAuto)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        public CudaAccelerator(
            Context context,
            int deviceId,
            CudaAcceleratorFlags acceleratorFlags)
            : base(context, AcceleratorType.Cuda)
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.CreateContext(
                    out var contextPtr,
                    acceleratorFlags,
                    deviceId));
            NativePtr = contextPtr;
            DeviceId = deviceId;

            SetupAccelerator();
        }

        /// <summary>
        /// Setups all required settings.
        /// </summary>
        private void SetupAccelerator()
        {
            Bind();

            CudaException.ThrowIfFailed(
                CurrentAPI.GetDeviceName(out string name, DeviceId));
            Name = name;
            DefaultStream = new CudaStream(this, IntPtr.Zero, false);

            CudaException.ThrowIfFailed(
                CurrentAPI.GetTotalDeviceMemory(out long total, DeviceId));
            MemorySize = total;

            // Resolve max grid size
            MaxGridSize = new Index3(
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_X, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Y, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Z, DeviceId));

            // Resolve max group size
            MaxGroupSize = new Index3(
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_X, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Y, DeviceId),
                CurrentAPI.GetDeviceAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Z, DeviceId));

            // Resolve max threads per group
            MaxNumThreadsPerGroup = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK, DeviceId);

            // Resolve max shared memory per block
            MaxSharedMemoryPerGroup = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK,
                DeviceId);

            // Resolve total constant memory
            MaxConstantMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_TOTAL_CONSTANT_MEMORY, DeviceId);

            // Resolve clock rate
            ClockRate = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_CLOCK_RATE, DeviceId) / 1000;

            // Resolve memory clock rate
            MemoryClockRate = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MEMORY_CLOCK_RATE, DeviceId) / 1000;

            // Resolve the bus width
            MemoryBusWidth = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_GLOBAL_MEMORY_BUS_WIDTH, DeviceId);

            // Resolve warp size
            WarpSize = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_WARP_SIZE, DeviceId);

            // Resolve number of multiprocessors
            NumMultiprocessors = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT, DeviceId);

            // Result max number of threads per multiprocessor
            MaxNumThreadsPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR,
                DeviceId);

            // Resolve the L2 cache size
            L2CacheSize = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_L2_CACHE_SIZE, DeviceId);

            // Resolve the maximum amount of shared memory per multiprocessor
            MaxSharedMemoryPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_MULTIPROCESSOR,
                DeviceId);

            // Resolve the total number of registers per multiprocessor
            TotalNumRegistersPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_MULTIPROCESSOR,
                DeviceId);

            // Resolve the total number of registers per group
            TotalNumRegistersPerGroup = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_BLOCK, DeviceId);

            // Resolve the max memory pitch
            MaxMemoryPitch = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_PITCH, DeviceId);

            // Resolve the number of concurrent copy engines
            NumConcurrentCopyEngines = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_ASYNC_ENGINE_COUNT, DeviceId);

            // Resolve whether this device has ECC support
            HasECCSupport = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_ECC_ENABLED, DeviceId) != 0;

            // Resolve whether this device supports managed memory
            SupportsManagedMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MANAGED_MEMORY, DeviceId) != 0;

            // Resolve whether this device supports compute preemption
            SupportsComputePreemption = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_COMPUTE_PREEMPTION_SUPPORTED,
                DeviceId) != 0;

            // Resolve the current driver mode
            DriverMode = (DeviceDriverMode)CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_TCC_DRIVER,
                DeviceId);

            // Resolve the PCI domain id
            PCIDomainId = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_PCI_DOMAIN_ID,
                DeviceId);

            // Resolve the PCI device id
            PCIBusId = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_PCI_BUS_ID,
                DeviceId);

            // Resolve the PCI device id
            PCIDeviceId = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_PCI_DEVICE_ID,
                DeviceId);

            // Resolve cache configuration
            CudaException.ThrowIfFailed(
                CurrentAPI.GetSharedMemoryConfig(out sharedMemoryConfiguration));
            CudaException.ThrowIfFailed(
                CurrentAPI.GetCacheConfig(out cacheConfiguration));

            // Setup architecture and backend
            CudaException.ThrowIfFailed(
                CurrentAPI.GetDeviceComputeCapability(
                    out int major,
                    out int minor,
                DeviceId));
            Architecture = PTXArchitectureUtils.GetArchitecture(major, minor);

            CudaException.ThrowIfFailed(
                CurrentAPI.GetDriverVersion(out var driverVersion));
            DriverVersion = driverVersion;
            InstructionSet = GetInstructionSet(Architecture, driverVersion);
            base.Capabilities = new CudaCapabilityContext(Architecture);

            Init(new PTXBackend(
                Context,
                Capabilities,
                Architecture,
                InstructionSet));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native Cuda-context ptr.
        /// </summary>
        [Obsolete("Use NativePtr instead")]
        public IntPtr ContextPtr => NativePtr;

        /// <summary>
        /// Returns the device id.
        /// </summary>
        public int DeviceId { get; }

        /// <summary>
        /// Returns the PTX architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; private set; }

        /// <summary>
        /// Returns the current driver version.
        /// </summary>
        public CudaDriverVersion DriverVersion { get; private set; }

        /// <summary>
        /// Returns the PTX instruction set.
        /// </summary>
        public PTXInstructionSet InstructionSet { get; private set; }

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
        /// Returns true if this device support compute preemption.
        /// </summary>
        public bool SupportsComputePreemption { get; private set; }

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
        /// Gets or sets the current shared-memory configuration.
        /// </summary>
        public CudaSharedMemoryConfiguration SharedMemoryConfiguration
        {
            get => sharedMemoryConfiguration;
            set
            {
                if (value < CudaSharedMemoryConfiguration.Default ||
                    value > CudaSharedMemoryConfiguration.EightByteBankSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                sharedMemoryConfiguration = value;
                Bind();
                CudaException.ThrowIfFailed(
                    CurrentAPI.SetSharedMemoryConfig(value));
            }
        }

        /// <summary>
        /// Gets or sets the current cache configuration.
        /// </summary>
        public CudaCacheConfiguration CacheConfiguration
        {
            get => cacheConfiguration;
            set
            {
                if (value < CudaCacheConfiguration.Default ||
                    value > CudaCacheConfiguration.PreferEqual)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                cacheConfiguration = value;
                Bind();
                CudaException.ThrowIfFailed(
                    CurrentAPI.SetCacheConfig(value));
            }
        }

        /// <summary>
        /// Returns the PTX backend of this accelerator.
        /// </summary>
        public new PTXBackend Backend => base.Backend as PTXBackend;

        /// <summary>
        /// Returns the capabilities of this accelerator.
        /// </summary>
        public new CudaCapabilityContext Capabilities =>
            base.Capabilities as CudaCapabilityContext;

        #endregion

        #region Methods

        /// <summary>
        /// Returns an NVML library compatible PCI bus id.
        /// </summary>
        public string GetNVMLPCIBusId() =>
            $"{PCIDomainId:X4}:{PCIBusId:X2}:{PCIDeviceId:X2}.0";

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
            writer.WriteLine(GetNVMLPCIBusId());
        }

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}(
        /// TExtensionProvider)"/>
        public override TExtension CreateExtension<TExtension, TExtensionProvider>(
            TExtensionProvider provider) =>
            provider.CreateCudaExtension(this);

        /// <summary cref="Accelerator.Allocate{T, TIndex}(TIndex)"/>
        protected override MemoryBuffer<T, TIndex> AllocateInternal<T, TIndex>(
            TIndex extent) =>
            new CudaMemoryBuffer<T, TIndex>(this, extent);

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.CreateKernel(
        /// TCompiledKernel)"/>
        protected override CudaKernel CreateKernel(PTXCompiledKernel compiledKernel) =>
            new CudaKernel(this, compiledKernel, null);

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.CreateKernel(
        /// TCompiledKernel, MethodInfo)"/>
        protected override CudaKernel CreateKernel(
            PTXCompiledKernel compiledKernel,
            MethodInfo launcher) =>
            new CudaKernel(this, compiledKernel, launcher);

        /// <summary cref="Accelerator.CreateStream()"/>
        protected override AcceleratorStream CreateStreamInternal() =>
            new CudaStream(this, StreamFlags.CU_STREAM_NON_BLOCKING);

        /// <summary>
        /// Creates a <see cref="CudaStream"/> object using
        /// specified <see cref="StreamFlags"/>.
        /// </summary>
        /// <param name="flag">The flag to use.</param>
        /// <returns>The created stream.</returns>
        public CudaStream CreateStream(StreamFlags flag) => new CudaStream(this, flag);

        /// <summary>
        /// Creates a <see cref="CudaStream"/> object using an externally created stream.
        /// </summary>
        /// <param name="ptr">A pointer to the externally created stream.</param>
        /// <param name="responsible">
        /// Whether ILGPU is responsible of disposing this stream.
        /// </param>
        /// <returns>The created stream.</returns>
        public CudaStream CreateStream(IntPtr ptr, bool responsible) =>
            new CudaStream(this, ptr, responsible);

        /// <summary cref="Accelerator.Synchronize"/>
        protected override void SynchronizeInternal() =>
            CudaException.ThrowIfFailed(
                CurrentAPI.SynchronizeContext());

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind() =>
            CudaException.ThrowIfFailed(
                CurrentAPI.SetCurrentContext(NativePtr));

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind() =>
            CudaException.ThrowIfFailed(
                CurrentAPI.SetCurrentContext(IntPtr.Zero));

        /// <summary>
        /// Queries the amount of free memory.
        /// </summary>
        /// <returns>The amount of free memory in bytes.</returns>
        public long GetFreeMemory()
        {
            Bind();
            CudaException.ThrowIfFailed(
                CurrentAPI.GetMemoryInfo(out long free, out long _));
            return free;
        }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator)
        {
            if (!(otherAccelerator is CudaAccelerator cudaAccelerator))
                return false;

            CudaException.ThrowIfFailed(
                CurrentAPI.CanAccessPeer(
                    out int canAccess,
                    DeviceId,
                    cudaAccelerator.DeviceId));
            return canAccess != 0;
        }

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (HasPeerAccess(otherAccelerator))
                return;

            if (!(otherAccelerator is CudaAccelerator cudaAccelerator))
            {
                throw new InvalidOperationException(
                    RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);
            }

            CudaException.ThrowIfFailed(
                CurrentAPI.EnablePeerAccess(cudaAccelerator.NativePtr, 0));
        }

        /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
        protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (!HasPeerAccess(otherAccelerator))
                return;

            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            Debug.Assert(cudaAccelerator != null, "Invalid EnablePeerAccess method");

            CudaException.ThrowIfFailed(
                CurrentAPI.DisablePeerAccess(cudaAccelerator.NativePtr));
        }

        #endregion

        #region General Launch Methods

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}
        /// .GenerateKernelLauncherMethod(TCompiledKernel, int)"/>
        protected override MethodInfo GenerateKernelLauncherMethod(
            PTXCompiledKernel kernel,
            int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;
            AdjustAndVerifyKernelGroupSize(ref customGroupSize, entryPoint);

            // Add support for by ref parameters
            if (entryPoint.HasByRefParameters)
            {
                throw new NotSupportedException(
                    ErrorMessages.NotSupportedByRefKernelParameters);
            }

            using var scopedLock = entryPoint.CreateLauncherMethod(
                Context.RuntimeSystem,
                out var launcher);
            var emitter = new ILEmitter(launcher.ILGenerator);

            // Allocate array of pointers as kernel argument(s)
            var argumentMapper = Backend.ArgumentMapper;
            var (argumentBuffer, argumentSize) = argumentMapper.Map(
                emitter,
                entryPoint);

            // Emit kernel launch

            // Load current driver API
            emitter.EmitCall(GetCudaAPIMethod);

            // Load stream
            KernelLauncherBuilder.EmitLoadAcceleratorStream<CudaStream, ILEmitter>(
                Kernel.KernelStreamParamIdx,
                emitter);

            // Load function ptr
            KernelLauncherBuilder.EmitLoadKernelArgument<CudaKernel, ILEmitter>(
                Kernel.KernelInstanceParamIdx,
                emitter);

            // Load kernel config
            KernelLauncherBuilder.EmitLoadRuntimeKernelConfig(
                entryPoint,
                emitter,
                Kernel.KernelParamDimensionIdx,
                customGroupSize);

            // Load kernel arguments
            emitter.Emit(LocalOperation.LoadAddress, argumentBuffer);
            emitter.EmitConstant(argumentSize);

            // Dispatch kernel
            var launchMethod = LaunchKernelMethod.MakeGenericMethod(
                argumentBuffer.VariableType);
            emitter.EmitCall(launchMethod);

            // Emit ThrowIfFailed
            emitter.EmitCall(ThrowIfFailedMethod);

            emitter.Emit(OpCodes.Ret);
            emitter.Finish();

            return launcher.Finish();
        }

        #endregion

        #region Occupancy

        /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(
        /// Kernel, int, int)"/>
        protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            if (!(kernel is CudaKernel cudaKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            CudaException.ThrowIfFailed(
                CurrentAPI.ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
                    out int numGroups,
                    cudaKernel.FunctionPtr,
                    groupSize,
                    new IntPtr(dynamicSharedMemorySizeInBytes)));
            return numGroups;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            if (!(kernel is CudaKernel cudaKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            Backends.Backend.EnsureRunningOnNativePlatform();

            CudaException.ThrowIfFailed(
                CurrentAPI.ComputeOccupancyMaxPotentialBlockSize(
                    out minGridSize,
                    out int groupSize,
                    cudaKernel.FunctionPtr,
                    new ComputeDynamicMemorySizeForBlockSize(
                        targetGroupSize =>
                            new IntPtr(computeSharedMemorySize(targetGroupSize))),
                    IntPtr.Zero,
                    maxGroupSize));
            return groupSize;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (!(kernel is CudaKernel cudaKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            Backends.Backend.EnsureRunningOnNativePlatform();

            CudaException.ThrowIfFailed(
                CurrentAPI.ComputeOccupancyMaxPotentialBlockSize(
                    out minGridSize,
                    out int groupSize,
                    cudaKernel.FunctionPtr,
                    null,
                    new IntPtr(dynamicSharedMemorySizeInBytes),
                    maxGroupSize));
            return groupSize;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the current Cuda context.
        /// </summary>
        protected override void DisposeAccelerator_SyncRoot(bool disposing) =>
            // Dispose the current context
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyContext(NativePtr));

        #endregion
    }
}
