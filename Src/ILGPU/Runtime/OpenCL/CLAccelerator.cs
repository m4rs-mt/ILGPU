// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Backends.OpenCL;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents the major OpenCL accelerator vendor.
    /// </summary>
    public enum CLAcceleratorVendor
    {
        /// <summary>
        /// Represents an AMD accelerator.
        /// </summary>
        AMD,

        /// <summary>
        /// Represents an Intel accelerator.
        /// </summary>
        Intel,

        /// <summary>
        /// Represents an NVIDIA accelerator.
        /// </summary>
        Nvidia,

        /// <summary>
        /// Represents another OpenCL device vendor.
        /// </summary>
        Other
    }

    /// <summary>
    /// Represents an OpenCL accelerator (CPU or GPU device).
    /// </summary>
    public sealed class CLAccelerator : KernelAccelerator<CLCompiledKernel, CLKernel>
    {
        #region Constants

        /// <summary>
        /// The maximum number of devices per platform.
        /// </summary>
        private const int MaxNumDevicesPerPlatform = 64;

        #endregion

        #region Static

        /// <summary>
        /// Represents the <see cref="CurrentAPI"/> property.
        /// </summary>
        internal static readonly MethodInfo GetCLAPIMethod =
            typeof(CLAPI).GetProperty(
                nameof(CurrentAPI),
                BindingFlags.Public | BindingFlags.Static).GetGetMethod();

        /// <summary>
        /// Represents the <see cref="CLAPI.LaunchKernelWithStreamBinding(
        /// CLStream, CLKernel, RuntimeKernelConfig)"/> method.
        /// </summary>
        private static readonly MethodInfo GenericLaunchKernelMethod =
            typeof(CLAPI).GetMethod(
                nameof(CLAPI.LaunchKernelWithStreamBinding),
                BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Represents the <see cref="CLException.ThrowIfFailed(CLError)" /> method.
        /// </summary>
        internal static readonly MethodInfo ThrowIfFailedMethod =
            typeof(CLException).GetMethod(
                nameof(CLException.ThrowIfFailed),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Specifies the kernel entry point name for the following dummy kernels.
        /// </summary>
        private const string DummyKernelName = "ILGPUTestKernel";

        /// <summary>
        /// The first dummy kernel that is compiled during accelerator initialization.
        /// </summary>
        private const string DummyKernelSource =
            "__kernel void " + DummyKernelName + "(\n" +
            "   __global const int *a,\n" +
            "   __global const int *b,\n" +
            "   __global int *c) { \n" +
            "   size_t i = get_global_id(0);\n" +
            "   c[i] = a[i] + b[i];\n}";

        /// <summary>
        /// The second dummy kernel that is compiled during accelerator initialization.
        /// </summary>
        private const string DummySubGroupKernelSource =
            "__kernel void " + DummyKernelName + "(\n" +
            "   __global int *a," +
            "   const int n) { \n" +
            "   size_t i = get_global_id(0);\n" +
            "   size_t j = get_sub_group_id();\n" +
            "   a[i] = sub_group_broadcast(j, n);\n}";

        /// <summary>
        /// All subgroup extensions.
        /// </summary>
        private static readonly ImmutableArray<string> SubGroupExtensions =
            ImmutableArray.Create(
                "cl_khr_subgroups",
                "cl_intel_subgroups");

        /// <summary>
        /// Detects all OpenCL accelerators.
        /// </summary>
        static CLAccelerator()
        {
            var accelerators = ImmutableArray.CreateBuilder<CLAcceleratorId>();
            var allAccelerators = ImmutableArray.CreateBuilder<CLAcceleratorId>();
            var devices = new IntPtr[MaxNumDevicesPerPlatform];

            try
            {
                // Resolve all platforms
                if (!CurrentAPI.IsSupported ||
                    CurrentAPI.GetNumPlatforms(out int numPlatforms) !=
                    CLError.CL_SUCCESS ||
                    numPlatforms < 1)
                {
                    return;
                }

                var platforms = new IntPtr[numPlatforms];
                if (CurrentAPI.GetPlatforms(platforms, out numPlatforms) !=
                    CLError.CL_SUCCESS)
                {
                    return;
                }

                foreach (var platform in platforms)
                {
                    // Resolve all devices
                    int numDevices = devices.Length;
                    Array.Clear(devices, 0, numDevices);

                    if (CurrentAPI.GetDevices(
                        platform,
                        CLDeviceType.CL_DEVICE_TYPE_ALL,
                        devices,
                        out numDevices) != CLError.CL_SUCCESS)
                    {
                        continue;
                    }

                    for (int i = 0; i < numDevices; ++i)
                    {
                        // Resolve device and ignore invalid devices
                        var device = devices[i];
                        if (device == IntPtr.Zero)
                            continue;

                        // Check for available device
                        if (CurrentAPI.GetDeviceInfo<int>(
                            device,
                            CLDeviceInfoType.CL_DEVICE_AVAILABLE) == 0)
                        {
                            continue;
                        }

                        var acceleratorId = new CLAcceleratorId(platform, device);
                        allAccelerators.Add(acceleratorId);
                        if (acceleratorId.CVersion >= CLBackend.MinimumVersion)
                            accelerators.Add(acceleratorId);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore API-specific exceptions at this point
            }
            finally
            {
                CLAccelerators = accelerators.ToImmutable();
                AllCLAccelerators = allAccelerators.ToImmutable();
            }
        }

        /// <summary>
        /// Represents the list of available and supported OpenCL accelerators.
        /// </summary>
        public static ImmutableArray<CLAcceleratorId> CLAccelerators { get; }

        /// <summary>
        /// Represents the list of all available OpenCL accelerators.
        /// </summary>
        public static ImmutableArray<CLAcceleratorId> AllCLAccelerators { get; }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The accelerator id.</param>
        public CLAccelerator(Context context, CLAcceleratorId acceleratorId)
            : base(context, AcceleratorType.OpenCL)
        {
            if (acceleratorId == null)
                throw new ArgumentNullException(nameof(acceleratorId));

            Backends.Backend.EnsureRunningOnNativePlatform();

            PlatformId = acceleratorId.PlatformId;
            DeviceId = acceleratorId.DeviceId;
            CVersion = acceleratorId.CVersion;

            PlatformName = CurrentAPI.GetPlatformInfo(
                PlatformId,
                CLPlatformInfoType.CL_PLATFORM_NAME);
            PlatformVersion = CLPlatformVersion.TryParse(
                CurrentAPI.GetPlatformInfo(
                    PlatformId,
                    CLPlatformInfoType.CL_PLATFORM_VERSION),
                out var platformVersion)
                ? platformVersion
                : CLPlatformVersion.CL10;

            VendorName = CurrentAPI.GetPlatformInfo(
                PlatformId,
                CLPlatformInfoType.CL_PLATFORM_VENDOR);

            // Create new context
            CLException.ThrowIfFailed(
                CurrentAPI.CreateContext(DeviceId, out var contextPtr));
            NativePtr = contextPtr;

            // Resolve device info
            Name = CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_NAME);

            MemorySize = CurrentAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_GLOBAL_MEM_SIZE);

            DeviceType = (CLDeviceType)CurrentAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_TYPE);

            // Max grid size
            int workItemDimensions = IntrinsicMath.Max(CurrentAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_ITEM_DIMENSIONS), 3);
            var workItemSizes = new IntPtr[workItemDimensions];
            CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_ITEM_SIZES,
                workItemSizes);
            MaxGridSize = new Index3(
                workItemSizes[0].ToInt32(),
                workItemSizes[1].ToInt32(),
                workItemSizes[2].ToInt32());

            // Resolve max threads per group
            MaxNumThreadsPerGroup = CurrentAPI.GetDeviceInfo<IntPtr>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_GROUP_SIZE).ToInt32();
            MaxGroupSize = new Index3(
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup);

            // Resolve max shared memory per block
            MaxSharedMemoryPerGroup = (int)IntrinsicMath.Min(
                CurrentAPI.GetDeviceInfo<long>(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_LOCAL_MEM_SIZE),
                int.MaxValue);

            // Resolve total constant memory
            MaxConstantMemory = (int)CurrentAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_PARAMETER_SIZE);

            // Resolve clock rate
            ClockRate = CurrentAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_CLOCK_FREQUENCY);

            // Resolve number of multiprocessors
            NumMultiprocessors = CurrentAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_COMPUTE_UNITS);

            // Result max number of threads per multiprocessor
            MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;

            base.Capabilities = new CLCapabilityContext(acceleratorId);

            Bind();
            InitVendorFeatures();
            InitSubGroupSupport(acceleratorId);
            DefaultStream = CreateStreamInternal();
            Init(new CLBackend(Context, Capabilities, Vendor));
        }

        /// <summary>
        /// Initializes major vendor features.
        /// </summary>
        private void InitVendorFeatures()
        {
            // Check major vendor features
            if (CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_WARP_SIZE_NV,
                out int warpSize) == CLError.CL_SUCCESS)
            {
                // Nvidia platform
                WarpSize = warpSize;
                Vendor = CLAcceleratorVendor.Nvidia;

                int major = CurrentAPI.GetDeviceInfo<int>(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_COMPUTE_CAPABILITY_MAJOR_NV);
                int minor = CurrentAPI.GetDeviceInfo<int>(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_COMPUTE_CAPABILITY_MINOR_NV);
                if (major < 7 || major == 7 && minor < 5)
                    MaxNumThreadsPerMultiprocessor *= 2;
            }
            else if (CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_WAVEFRONT_WIDTH_AMD,
                out int wavefrontSize) == CLError.CL_SUCCESS)
            {
                // AMD platform
                WarpSize = wavefrontSize;
                Vendor = CLAcceleratorVendor.AMD;
            }
            else
            {
                Vendor = VendorName.Contains(CLAcceleratorVendor.Intel.ToString()) ?
                    CLAcceleratorVendor.Intel :
                    CLAcceleratorVendor.Other;

                // Compile dummy kernel to resolve additional information
                CLException.ThrowIfFailed(CLKernel.LoadKernel(
                    this,
                    DummyKernelName,
                    DummyKernelSource,
                    CVersion,
                    out IntPtr programPtr,
                    out IntPtr kernelPtr,
                    out var _));
                try
                {
                    // Resolve information
                    WarpSize = CurrentAPI.GetKernelWorkGroupInfo<IntPtr>(
                        kernelPtr,
                        DeviceId,
                        CLKernelWorkGroupInfoType
                            .CL_KERNEL_PREFERRED_WORK_GROUP_SIZE_MULTIPLE).ToInt32();
                }
                finally
                {
                    CLException.ThrowIfFailed(
                        CurrentAPI.ReleaseKernel(kernelPtr));
                    CLException.ThrowIfFailed(
                        CurrentAPI.ReleaseProgram(programPtr));
                }
            }
        }

        /// <summary>
        /// Initializes support for sub groups.
        /// </summary>
        /// <param name="acceleratorId">The current accelerator id.</param>
        private void InitSubGroupSupport(CLAcceleratorId acceleratorId)
        {
            // Check sub group support
            Capabilities.SubGroups = acceleratorId.HasAnyExtension(SubGroupExtensions);
            if (!Capabilities.SubGroups)
                return;

            // Verify support using a simple kernel
            if (CLKernel.LoadKernel(
                this,
                DummyKernelName,
                DummySubGroupKernelSource,
                CVersion,
                out IntPtr programPtr,
                out IntPtr kernelPtr,
                out var _) == CLError.CL_SUCCESS)
            {
                // Some drivers return an internal handler delegate
                // that crashes during invocation instead of telling that the
                // sub-group feature is not supported
                try
                {
                    var localGroupSizes = new IntPtr[]
                    {
                        new IntPtr(MaxNumThreadsPerGroup)
                    };
                    Capabilities.SubGroups = acceleratorId.TryGetKernelSubGroupInfo(
                        kernelPtr,
                        DeviceId,
                        CLKernelSubGroupInfoType
                            .CL_KERNEL_MAX_SUB_GROUP_SIZE_FOR_NDRANGE_KHR,
                        localGroupSizes,
                        out IntPtr subGroupSize);
                    WarpSize = subGroupSize.ToInt32();
                }
                catch (AccessViolationException)
                {
                    // This exception can be raised due to driver issues
                    // on several platforms -> we will just disable sub-group
                    // support for these platforms
                    Capabilities.SubGroups = false;
                }
                finally
                {
                    CLException.ThrowIfFailed(
                        CurrentAPI.ReleaseKernel(kernelPtr));
                    CLException.ThrowIfFailed(
                        CurrentAPI.ReleaseProgram(programPtr));
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native OpenCL platform id.
        /// </summary>
        public IntPtr PlatformId { get; }

        /// <summary>
        /// Returns the associated platform name.
        /// </summary>
        public string PlatformName { get; }

        /// <summary>
        /// Returns the associated platform version.
        /// </summary>
        public CLPlatformVersion PlatformVersion { get; }

        /// <summary>
        /// Returns the associated vendor.
        /// </summary>
        public string VendorName { get; }

        /// <summary>
        /// Returns the main accelerator vendor type.
        /// </summary>
        public CLAcceleratorVendor Vendor { get; private set; }

        /// <summary>
        /// Returns the native OpenCL device id.
        /// </summary>
        public IntPtr DeviceId { get; }

        /// <summary>
        /// Returns the OpenCL device type.
        /// </summary>
        public CLDeviceType DeviceType { get; }

        /// <summary>
        /// Returns the native OpenCL-context ptr.
        /// </summary>
        [Obsolete("Use NativePtr instead")]
        public IntPtr ContextPtr => NativePtr;

        /// <summary>
        /// Returns the clock rate.
        /// </summary>
        public int ClockRate { get; }

        /// <summary>
        /// Returns the supported OpenCL C version.
        /// </summary>
        public CLCVersion CVersion { get; }

        /// <summary>
        /// Returns true if this accelerator has sub-group support.
        /// </summary>
        [Obsolete("Use Capabilities instead")]
        public bool SubGroupSupport => Capabilities.SubGroups;

        /// <summary>
        /// Returns the OpenCL backend of this accelerator.
        /// </summary>
        public new CLBackend Backend => base.Backend as CLBackend;

        /// <summary>
        /// Returns the capabilities of this accelerator.
        /// </summary>
        public new CLCapabilityContext Capabilities =>
            base.Capabilities as CLCapabilityContext;

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected override void PrintHeader(TextWriter writer)
        {
            base.PrintHeader(writer);

            writer.Write("  Platform name:                           ");
            writer.WriteLine(PlatformName);

            writer.Write("  Platform version:                        ");
            writer.WriteLine(PlatformVersion.ToString());

            writer.Write("  Vendor name:                             ");
            writer.WriteLine(VendorName);

            writer.Write("  Vendor:                                  ");
            writer.WriteLine(Vendor.ToString());

            writer.Write("  Device type:                             ");
            writer.WriteLine(DeviceType.ToString());

            writer.Write("  Clock rate:                              ");
            writer.Write(ClockRate);
            writer.WriteLine(" MHz");
        }

        /// <inheritdoc/>
        protected override void PrintGeneralInfo(TextWriter writer)
        {
            writer.Write("  OpenCL C version:                        ");
            writer.WriteLine(CVersion.ToString());

            writer.Write("  Has FP16 support:                        ");
            writer.WriteLine(Capabilities.Float16);

            writer.Write("  Has Int64 atomics support:               ");
            writer.WriteLine(Capabilities.Int64_Atomics);

            writer.Write("  Has sub group support:                   ");
            writer.WriteLine(Capabilities.SubGroups);
        }

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}(
        /// TExtensionProvider)"/>
        public override TExtension CreateExtension<TExtension, TExtensionProvider>(
            TExtensionProvider provider) =>
            provider.CreateOpenCLExtension(this);

        /// <summary cref="Accelerator.AllocateInternal{T, TIndex}(TIndex)"/>
        protected override MemoryBuffer<T, TIndex> AllocateInternal<T, TIndex>(
            TIndex extent) =>
            new CLMemoryBuffer<T, TIndex>(this, extent);

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.CreateKernel(
        /// TCompiledKernel)"/>
        protected override CLKernel CreateKernel(CLCompiledKernel compiledKernel)
        {
            // Verify OpenCL C version
            if (compiledKernel.CVersion > CVersion)
            {
                throw new NotSupportedException(
                    string.Format(
                        RuntimeErrorMessages.NotSupportedOpenCLCVersion,
                        compiledKernel.CVersion));
            }

            return new CLKernel(this, compiledKernel, null);
        }

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.CreateKernel(
        /// TCompiledKernel, MethodInfo)"/>
        protected override CLKernel CreateKernel(
            CLCompiledKernel compiledKernel,
            MethodInfo launcher) =>
            new CLKernel(this, compiledKernel, launcher);

        /// <summary cref="Accelerator.CreateStream()"/>
        protected override AcceleratorStream CreateStreamInternal() =>
            new CLStream(this);

        /// <summary>
        /// Creates a <see cref="CLStream"/> object from an externally
        /// created stream/queue using its pointer.
        /// </summary>
        /// <param name="ptr">The pointer to use while creating the new stream.</param>
        /// <param name="responsible">
        /// Whether ILGPU is responsible of disposing this stream.
        /// </param>
        /// <returns>The created stream.</returns>
        public CLStream CreateStream(IntPtr ptr, bool responsible) =>
            new CLStream(this, ptr, responsible);

        /// <summary cref="Accelerator.Synchronize"/>
        protected unsafe override void SynchronizeInternal()
        {
            // All the events to wait on. Each event represents the completion
            // of all operations queued prior to said event.
            var streamInstances = InlineList<CLStream>.Create(4);
            var streamEvents = InlineList<IntPtr>.Create(4);
            try
            {
                ForEachChildObject<CLStream>(stream =>
                {
                    // Ignore disposed command queues at this point
                    if (stream.CommandQueue == IntPtr.Zero)
                        return;

                    // Low cost IntPtr* (cl_event*) allocation
                    IntPtr* resultEvent = stackalloc IntPtr[1];
                    CLException.ThrowIfFailed(
                        CurrentAPI.EnqueueBarrierWithWaitList(
                            stream.CommandQueue,
                            Array.Empty<IntPtr>(),
                            resultEvent));

                    // Dereference the pointer so we can store it
                    streamEvents.Add(*resultEvent);

                    // Keep the stream instance alive to avoid automatic disposal
                    streamInstances.Add(stream);
                });

                // Wait for all the events to fire, which would mean all operations
                // queued on an accelerator prior to synchronization have finished
                if (streamEvents.Count > 0)
                {
                    CLException.ThrowIfFailed(
                        CurrentAPI.WaitForEvents(streamEvents));
                }
            }
            finally
            {
                // Clean up the events we made
                foreach (var streamEvent in streamEvents)
                {
                    CLException.ThrowIfFailed(
                        CurrentAPI.ReleaseEvent(streamEvent));
                }
            }
        }

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind() { }

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind() { }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) =>
            false;

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator) =>
            throw new InvalidOperationException(
                RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);

        /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
        protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
        { }

        #endregion

        #region General Launch Methods

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}
        /// .GenerateKernelLauncherMethod(TCompiledKernel, int)"/>
        protected override MethodInfo GenerateKernelLauncherMethod(
            CLCompiledKernel kernel,
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

            // Load kernel instance
            var kernelLocal = emitter.DeclareLocal(typeof(CLKernel));
            KernelLauncherBuilder.EmitLoadKernelArgument<CLKernel, ILEmitter>(
                Kernel.KernelInstanceParamIdx,
                emitter);
            emitter.Emit(LocalOperation.Store, kernelLocal);

            // Map all kernel arguments
            var argumentMapper = Backend.ArgumentMapper;
            argumentMapper.Map(
                emitter,
                kernelLocal,
                Context.TypeContext,
                entryPoint);

            // Load current driver API
            emitter.EmitCall(GetCLAPIMethod);

            // Load stream
            KernelLauncherBuilder.EmitLoadAcceleratorStream<CLStream, ILEmitter>(
                Kernel.KernelStreamParamIdx,
                emitter);

            // Load kernel
            emitter.Emit(LocalOperation.Load, kernelLocal);

            // Load dimensions
            KernelLauncherBuilder.EmitLoadRuntimeKernelConfig(
                entryPoint,
                emitter,
                Kernel.KernelParamDimensionIdx,
                customGroupSize);

            // Dispatch kernel
            var launchMethod = GenericLaunchKernelMethod.MakeGenericMethod(
                entryPoint.SharedMemory.HasDynamicMemory
                ? typeof(DynamicSharedMemoryHandler)
                : typeof(DefaultLaunchHandler));
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
            if (dynamicSharedMemorySizeInBytes > 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dynamicSharedMemorySizeInBytes));
            }

            groupSize = IntrinsicMath.Min(groupSize, MaxNumThreadsPerGroup);
            return MaxNumThreadsPerGroup / groupSize;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize) =>
            throw new NotSupportedException();

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (dynamicSharedMemorySizeInBytes > 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dynamicSharedMemorySizeInBytes));
            }

            if (maxGroupSize < 1)
                maxGroupSize = MaxNumThreadsPerGroup;

            var clKernel = kernel as CLKernel;
            var workGroupSizeNative = CurrentAPI.GetKernelWorkGroupInfo<IntPtr>(
                clKernel.KernelPtr,
                DeviceId,
                CLKernelWorkGroupInfoType.CL_KERNEL_WORK_GROUP_SIZE);
            int workGroupSize = workGroupSizeNative.ToInt32();
            workGroupSize = IntrinsicMath.Min(workGroupSize, maxGroupSize);
            minGridSize = IntrinsicMath.DivRoundUp(MaxNumThreads, workGroupSize);

            return workGroupSize;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the current OpenCL context.
        /// </summary>
        protected override void DisposeAccelerator_SyncRoot(bool disposing) =>
            // Dispose the current context
            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.ReleaseContext(NativePtr));

        #endregion
    }
}
