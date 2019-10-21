// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Backends.PTX;
using ILGPU.Resources;
using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda accelerator.
    /// </summary>
    public sealed class CudaAccelerator : Accelerator
    {
        #region Static

        /// <summary>
        /// Represents a zero integer pointer field.
        /// </summary>
        private static readonly FieldInfo ZeroIntPtrField = typeof(IntPtr).GetField(
            nameof(IntPtr.Zero), BindingFlags.Static | BindingFlags.Public);

        /// <summary>
        /// Represents the <see cref="CudaAPI.Current"/> property.
        /// </summary>
        private static readonly MethodInfo GetCudaAPIMethod = typeof(CudaAPI).GetProperty(
            nameof(CudaAPI.Current),
            BindingFlags.Public | BindingFlags.Static).GetGetMethod();

        /// <summary>
        /// Represents the <see cref="CudaAPI.LaunchKernelWithStreamBinding(CudaStream, CudaKernel, int, int, int, int, int, int, int, IntPtr, IntPtr)"/>
        /// method.
        /// </summary>
        private static readonly MethodInfo LaunchKernelMethod = typeof(CudaAPI).GetMethod(
            nameof(CudaAPI.LaunchKernelWithStreamBinding),
            BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Represents the <see cref="CudaException.ThrowIfFailed(CudaError)" /> method.
        /// </summary>
        private static readonly MethodInfo ThrowIfFailedMethod = typeof(CudaException).GetMethod(
            nameof(CudaException.ThrowIfFailed),
            BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Detects all cuda accelerators.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Complex initialization logic is required in this case")]
        static CudaAccelerator()
        {
            CudaAccelerators = ImmutableArray<AcceleratorId>.Empty;

            // Resolve all devices
            if (CurrentAPI.GetDeviceCount(out int numDevices) != CudaError.CUDA_SUCCESS ||
                numDevices < 1)
                return;

            var accelerators = ImmutableArray.CreateBuilder<AcceleratorId>(numDevices);
            for (int i = 0; i < numDevices; ++i)
            {
                if (CurrentAPI.GetDevice(out int device, i) != CudaError.CUDA_SUCCESS)
                    continue;
                accelerators.Add(new AcceleratorId(AcceleratorType.Cuda, device));
            }
            CudaAccelerators = accelerators.ToImmutable();
        }

        /// <summary>
        /// Returns the current Cuda-driver API.
        /// </summary>
        public static CudaAPI CurrentAPI => CudaAPI.Current;

        /// <summary>
        /// Represents the list of available Cuda accelerators.
        /// </summary>
        public static ImmutableArray<AcceleratorId> CudaAccelerators { get; }

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

        #endregion

        #region Instance

        private IntPtr contextPtr;
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
                CurrentAPI.CreateContext(out contextPtr, acceleratorFlags, deviceId));
            DeviceId = deviceId;

            SetupAccelerator();
        }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        public CudaAccelerator(Context context, IntPtr d3d11Device)
            : this(context, d3d11Device, CudaAcceleratorFlags.ScheduleAuto)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        public CudaAccelerator(
            Context context,
            IntPtr d3d11Device,
            CudaAcceleratorFlags acceleratorFlags)
            : base(context, AcceleratorType.Cuda)
        {
            if (d3d11Device == IntPtr.Zero)
                throw new ArgumentNullException(nameof(d3d11Device));

            CudaException.ThrowIfFailed(
                CurrentAPI.CreateContextD3D11(out contextPtr, out int deviceId, acceleratorFlags, d3d11Device));
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
            DefaultStream = new CudaStream(this, IntPtr.Zero);

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
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK, DeviceId);

            // Resolve total constant memory
            MaxConstantMemory = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_TOTAL_CONSTANT_MEMORY, DeviceId);

            // Resolve clock rate
            ClockRate = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_CLOCK_RATE, DeviceId);

            // Resolve warp size
            WarpSize = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_WARP_SIZE, DeviceId);

            // Resolve number of multiprocessors
            NumMultiprocessors = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT, DeviceId);

            // Result max number of threads per multiprocessor
            MaxNumThreadsPerMultiprocessor = CurrentAPI.GetDeviceAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR, DeviceId);

            // Resolve cache configuration
            CudaException.ThrowIfFailed(
                CurrentAPI.GetSharedMemoryConfig(out sharedMemoryConfiguration));
            CudaException.ThrowIfFailed(
                CurrentAPI.GetCacheConfig(out cacheConfiguration));

            // Setup architecture and backend
            CudaException.ThrowIfFailed(
                CurrentAPI.GetDeviceComputeCapability(out int major, out int minor, DeviceId));
            Architecture = PTXArchitectureUtils.GetArchitecture(major, minor);

            CudaException.ThrowIfFailed(
                CurrentAPI.GetDriverVersion(out var driverVersion));
            InstructionSet = GetInstructionSet(Architecture, driverVersion);
            base.Backend = new PTXBackend(Context, Architecture, InstructionSet, Backends.Backend.OSPlatform);
        }

        /// <summary>
        /// Returns the PTX instruction set to use, based on the PTX architecture and installed CUDA drivers.
        /// </summary>
        /// <param name="architecture">The PTX architecture</param>
        /// <param name="installedDriverVersion">The CUDA driver version</param>
        /// <returns>The PTX instruction set</returns>
        private static PTXInstructionSet GetInstructionSet(PTXArchitecture architecture, CudaDriverVersion installedDriverVersion)
        {
            var architectureMinDriverVersion = CudaDriverVersionUtils.GetMinimumDriverVersion(architecture);
            var minDriverVersion = architectureMinDriverVersion;
            foreach (var instructionSet in PTXCodeGenerator.SupportedInstructionSets)
            {
                var instructionSetMinDriverVersion = CudaDriverVersionUtils.GetMinimumDriverVersion(instructionSet);
                minDriverVersion = architectureMinDriverVersion >= instructionSetMinDriverVersion ? architectureMinDriverVersion : instructionSetMinDriverVersion;
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

        #region Properties

        /// <summary>
        /// Returns the native Cuda-context ptr.
        /// </summary>
        public IntPtr ContextPtr => contextPtr;

        /// <summary>
        /// Returns the device id.
        /// </summary>
        public int DeviceId { get; }

        /// <summary>
        /// Returns the PTX architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; private set; }

        /// <summary>
        /// Returns the PTX instruction set.
        /// </summary>
        public PTXInstructionSet InstructionSet { get; private set; }

        /// <summary>
        /// Returns the max group size.
        /// </summary>
        public Index3 MaxGroupSize { get; private set; }

        /// <summary>
        /// Returns the clock rate.
        /// </summary>
        public int ClockRate { get; private set; }

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
                    throw new ArgumentOutOfRangeException(nameof(value));
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
                    throw new ArgumentOutOfRangeException(nameof(value));
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

        #endregion

        #region Methods

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}(TExtensionProvider)"/>
        public override TExtension CreateExtension<TExtension, TExtensionProvider>(TExtensionProvider provider)
        {
            return provider.CreateCudaExtension(this);
        }

        /// <summary cref="Accelerator.Allocate{T, TIndex}(TIndex)"/>
        protected override MemoryBuffer<T, TIndex> AllocateInternal<T, TIndex>(TIndex extent)
        {
            return new CudaMemoryBuffer<T, TIndex>(this, extent);
        }

        /// <summary cref="Accelerator.LoadKernelInternal(CompiledKernel)"/>
        protected override Kernel LoadKernelInternal(CompiledKernel kernel)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (!(kernel is PTXCompiledKernel ptxKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
            return new CudaKernel(
                this,
                ptxKernel,
                GenerateKernelLauncherMethod(ptxKernel, 0));
        }

        /// <summary cref="Accelerator.LoadImplicitlyGroupedKernelInternal(CompiledKernel, int)"/>
        protected override Kernel LoadImplicitlyGroupedKernelInternal(
            CompiledKernel kernel,
            int customGroupSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (customGroupSize < 0 || customGroupSize > MaxNumThreadsPerGroup)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));
            if (!(kernel is PTXCompiledKernel ptxKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
            if (kernel.EntryPoint.IsGroupedIndexEntry)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedExplicitlyGroupedKernel);
            return new CudaKernel(
                this,
                ptxKernel,
                GenerateKernelLauncherMethod(ptxKernel, customGroupSize));
        }

        /// <summary cref="Accelerator.LoadAutoGroupedKernelInternal(CompiledKernel, out int, out int)"/>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "The object must not be disposed here")]
        protected override Kernel LoadAutoGroupedKernelInternal(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (!(kernel is PTXCompiledKernel ptxKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
            if (kernel.EntryPoint.IsGroupedIndexEntry)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedExplicitlyGroupedKernel);

            var result = new CudaKernel(this, ptxKernel, null);
            groupSize = EstimateGroupSizeInternal(result, 0, 0, out minGridSize);
            result.Launcher = GenerateKernelLauncherMethod(ptxKernel, groupSize);
            return result;
        }

        /// <summary cref="Accelerator.CreateStream"/>
        protected override AcceleratorStream CreateStreamInternal()
        {
            return new CudaStream(this);
        }

        /// <summary cref="Accelerator.Synchronize"/>
        protected override void SynchronizeInternal()
        {
            CudaException.ThrowIfFailed(CurrentAPI.SynchronizeContext());
        }

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind()
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.SetCurrentContext(contextPtr));
        }

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind()
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.SetCurrentContext(IntPtr.Zero));
        }

        /// <summary>
        /// Queries the amount of free memory.
        /// </summary>
        /// <returns>The amount of free memory in bytes.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate",
            Justification = "This method implies a native method invocation")]
        public long GetFreeMemory()
        {
            Bind();
            CudaException.ThrowIfFailed(
                CurrentAPI.GetMemoryInfo(out long free, out long total));
            return free;
        }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator)
        {
            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            if (cudaAccelerator == null)
                return false;

            CudaException.ThrowIfFailed(
                CurrentAPI.CanAccessPeer(out int canAccess, DeviceId, cudaAccelerator.DeviceId));
            return canAccess != 0;
        }

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (HasPeerAccess(otherAccelerator))
                return;

            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            if (cudaAccelerator == null)
                throw new InvalidOperationException(
                    RuntimeErrorMessages.CannotEnablePeerAccessToDifferentAcceleratorKind);

            CudaException.ThrowIfFailed(
                CurrentAPI.EnablePeerAccess(cudaAccelerator.ContextPtr, 0));
        }

        /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
        protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (!HasPeerAccess(otherAccelerator))
                return;

            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            Debug.Assert(cudaAccelerator != null, "Invalid EnablePeerAccess method");

            CudaException.ThrowIfFailed(
                CurrentAPI.DisablePeerAccess(cudaAccelerator.ContextPtr));
        }

        #endregion

        #region General Launch Methods

        /// <summary>
        /// Generates a dynamic kernel-launcher method that will be just-in-time compiled
        /// during the first invocation. Using the generated launcher lowers the overhead
        /// for kernel launching dramatically, since unnecessary operations (like boxing)
        /// can be avoided.
        /// </summary>
        /// <param name="kernel">The kernel to generate a launcher for.</param>
        /// <param name="customGroupSize">The custom group size used for automatic blocking.</param>
        /// <returns>The generated launcher method.</returns>
        private MethodInfo GenerateKernelLauncherMethod(PTXCompiledKernel kernel, int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;
            AdjustAndVerifyKernelGroupSize(ref customGroupSize, entryPoint);

            // Add support for by ref parameters
            if (entryPoint.HasByRefParameters)
                throw new NotSupportedException(ErrorMessages.NotSupportedByRefKernelParameters);

            var launcher = entryPoint.CreateLauncherMethod(Context);
            var emitter = new ILEmitter(launcher.ILGenerator);

            // Allocate array of pointers as kernel argument(s)
            var argumentMapper = Backend.ArgumentMapper;
            var argumentBuffer = argumentMapper.Map(emitter, entryPoint);

            // Add the actual dispatch-size information to the kernel parameters
            if (!entryPoint.IsGroupedIndexEntry)
                PTXArgumentMapper.StoreKernelLength(emitter, argumentBuffer);

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

            // Load dimensions
            KernelLauncherBuilder.EmitLoadDimensions(
                entryPoint,
                emitter,
                Kernel.KernelParamDimensionIdx,
                () => { },
                customGroupSize);

            // Load shared-mem size (0 bytes in dynamic shared memory)
            emitter.Emit(OpCodes.Ldc_I4_0);

            // Load kernel args
            emitter.Emit(LocalOperation.Load, argumentBuffer);

            // Load empty kernel args
            emitter.Emit(OpCodes.Ldsfld, ZeroIntPtrField);

            // Dispatch kernel
            emitter.EmitCall(LaunchKernelMethod);

            // Emit ThrowIfFailed
            emitter.EmitCall(ThrowIfFailedMethod);

            emitter.Emit(OpCodes.Ret);
            emitter.Finish();

            return launcher.Finish();
        }

        #endregion

        #region Occupancy

        /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(Kernel, int, int)"/>
        protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            var cudaKernel = kernel as CudaKernel;
            if (cudaKernel == null)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            CudaException.ThrowIfFailed(
                CurrentAPI.ComputeOccupancyMaxActiveBlocksPerMultiprocessor(
                    out int numGroups,
                    cudaKernel.FunctionPtr,
                    groupSize,
                    new IntPtr(dynamicSharedMemorySizeInBytes)));
            return numGroups;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            var cudaKernel = kernel as CudaKernel;
            if (cudaKernel == null)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            Backends.Backend.EnsureRunningOnNativePlatform();

            CudaException.ThrowIfFailed(
                CurrentAPI.ComputeOccupancyMaxPotentialBlockSize(
                    out minGridSize,
                    out int groupSize,
                    cudaKernel.FunctionPtr,
                    new ComputeDynamicMemorySizeForBlockSize(
                        targetGroupSize => new IntPtr(computeSharedMemorySize(targetGroupSize))),
                    IntPtr.Zero,
                    maxGroupSize));
            return groupSize;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            var cudaKernel = kernel as CudaKernel;
            if (cudaKernel == null)
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

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            CudaException.ThrowIfFailed(CurrentAPI.DestroyContext(contextPtr));
            contextPtr = IntPtr.Zero;
        }

        #endregion
    }
}
