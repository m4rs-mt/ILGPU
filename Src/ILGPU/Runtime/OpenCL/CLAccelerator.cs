// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Backends.OpenCL;
using ILGPU.Resources;
using ILGPU.Runtime.OpenCL.API;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

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
        #region Static

        /// <summary>
        /// Represents the <see cref="CLAPI.LaunchKernelWithStreamBinding(CLStream, CLKernel, int, int, int, int, int, int)"/> method.
        /// </summary>
        private static readonly MethodInfo LaunchKernelMethod = typeof(CLAPI).GetMethod(
            nameof(CLAPI.LaunchKernelWithStreamBinding),
            BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents the <see cref="CLException.ThrowIfFailed(CLError)" /> method.
        /// </summary>
        internal static readonly MethodInfo ThrowIfFailedMethod = typeof(CLException).GetMethod(
            nameof(CLException.ThrowIfFailed),
            BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// The first dummy kernel that is compiled during accelerator intialization.
        /// </summary>
        private const string DummyKernelSource =
            "__kernel void " + CLCompiledKernel.EntryName + "(\n" +
            "   __global const int *a,\n" +
            "   __global const int *b,\n" +
            "   __global int *c) { \n" +
            "   size_t i = get_global_id(0);\n" +
            "   c[i] = a[i] + b[i];\n}";

        /// <summary>
        /// The second dummy kernel that is compiled during accelerator intialization.
        /// </summary>
        private const string DummySubGroupKernelSource =
            "__kernel void " + CLCompiledKernel.EntryName + "(\n" +
            "   __global int *a," +
            "   const int n) { \n" +
            "   size_t i = get_global_id(0);\n" +
            "   size_t j = get_sub_group_id();\n" +
            "   a[i] = sub_group_broadcast(j, n);\n}";

        /// <summary>
        /// All subgroup extensions.
        /// </summary>
        private readonly ImmutableArray<string> SubGroupExtensions = ImmutableArray.Create(
            "cl_subgroups_khr",
            "cl_intel_subgroups");

        /// <summary>
        /// Detects all cuda accelerators.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Complex initialization logic is required in this case")]
        [SuppressMessage("Microsoft.Design", "CA1031:Do not catch general exception types",
            Justification = "Must be catched to ignore external driver errors")]
        static CLAccelerator()
        {
            var accelerators = ImmutableArray.CreateBuilder<CLAcceleratorId>();

            try
            {
                // Resolve all platforms
                if (CLAPI.GetNumPlatforms(out int numPlatforms) != CLError.CL_SUCCESS ||
                    numPlatforms < 1)
                    return;

                var platforms = new IntPtr[numPlatforms];
                if (CLAPI.GetPlatforms(platforms, out numPlatforms) != CLError.CL_SUCCESS)
                    return;

                foreach (var platform in platforms)
                {
                    // Resolve all devices
                    if (CLAPI.GetNumDevices(
                        platform,
                        CLDeviceType.CL_DEVICE_TYPE_ALL,
                        out int numDevices) != CLError.CL_SUCCESS)
                        continue;

                    var devices = new IntPtr[numDevices];
                    if (CLAPI.GetDevices(
                        platform,
                        CLDeviceType.CL_DEVICE_TYPE_ALL,
                        devices,
                        out numDevices) != CLError.CL_SUCCESS)
                        continue;

                    foreach (var device in devices)
                    {
                        // Check for available device
                        if (CLAPI.GetDeviceInfo<int>(
                            device,
                            CLDeviceInfoType.CL_DEVICE_AVAILABLE) == 0)
                            continue;

                        accelerators.Add(new CLAcceleratorId(
                            platform,
                            device));
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
            }
        }

        /// <summary>
        /// Represents the list of available Cuda accelerators.
        /// </summary>
        public static ImmutableArray<CLAcceleratorId> CLAccelerators { get; }

        #endregion

        #region Instance

        private IntPtr contextPtr;

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

            PlatformId = acceleratorId.PlatformId;
            DeviceId = acceleratorId.DeviceId;

            PlatformName = CLAPI.GetPlatformInfo(
                PlatformId,
                CLPlatformInfoType.CL_PLATFORM_NAME);

            VendorName = CLAPI.GetPlatformInfo(
                PlatformId,
                CLPlatformInfoType.CL_PLATFORM_VENDOR);

            // Create new context
            CLException.ThrowIfFailed(
                CLAPI.CreateContext(DeviceId, out contextPtr));

            // Resolve device info
            Name = CLAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_NAME);

            MemorySize = CLAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_GLOBAL_MEM_SIZE);

            DeviceType = (CLDeviceType)CLAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_TYPE);

            // Determine the supported OpenCL C version
            var clVersionString = CLAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_OPENCL_C_VERSION);
            if (!CLCVersion.TryParse(clVersionString, out CLCVersion version))
                version = CLCVersion.CL10;
            CVersion = version;

            // Max grid size
            int workItemDimensions = IntrinsicMath.Max(CLAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_ITEM_DIMENSIONS), 3);
            var workItemSizes = new IntPtr[workItemDimensions];
            CLAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_ITEM_SIZES,
                workItemSizes);
            MaxGridSize = new Index3(
                workItemSizes[0].ToInt32(),
                workItemSizes[1].ToInt32(),
                workItemSizes[2].ToInt32());

            // Resolve max threads per group
            MaxNumThreadsPerGroup = CLAPI.GetDeviceInfo<IntPtr>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_GROUP_SIZE).ToInt32();

            // Resolve max shared memory per block
            MaxSharedMemoryPerGroup = (int)IntrinsicMath.Min(
                CLAPI.GetDeviceInfo<long>(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_LOCAL_MEM_SIZE),
                int.MaxValue);

            // Resolve total constant memory
            MaxConstantMemory = (int)CLAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_PARAMETER_SIZE);

            // Resolve clock rate
            ClockRate = CLAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_CLOCK_FREQUENCY);

            // Resolve number of multiprocessors
            NumMultiprocessors = CLAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_COMPUTE_UNITS);

            // Result max number of threads per multiprocessor
            MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;

            InitVendorFeatures();
            InitSubGroupSupport(acceleratorId);

            Bind();
            DefaultStream = CreateStreamInternal();
            base.Backend = new CLBackend(Context, Backends.Backend.OSPlatform, Vendor);
        }

        /// <summary>
        /// Initializes major vendor features.
        /// </summary>
        [SuppressMessage("Globalization", "CA1307:Specify StringComparison",
            Justification = "string.Contains(string, StringComparison) not available in net47 and netcoreapp2.0")]
        private void InitVendorFeatures()
        {
            // Check major vendor features
            if (CLAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_WARP_SIZE_NV,
                out int warpSize) == CLError.CL_SUCCESS)
            {
                // Nvidia platform
                WarpSize = warpSize;
                Vendor = CLAcceleratorVendor.Nvidia;

                int major = CLAPI.GetDeviceInfo<int>(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_COMPUTE_CAPABILITY_MAJOR_NV);
                int minor = CLAPI.GetDeviceInfo<int>(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_COMPUTE_CAPABILITY_MINOR_NV);
                if (major < 7 || major == 7 && minor < 5)
                    MaxNumThreadsPerMultiprocessor *= 2;
            }
            else if (CLAPI.GetDeviceInfo(
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
                    DummyKernelSource,
                    CVersion,
                    out IntPtr programPtr,
                    out IntPtr kernelPtr,
                    out var _));
                try
                {
                    // Resolve information
                    WarpSize = CLAPI.GetKernelWorkGroupInfo<IntPtr>(
                        kernelPtr,
                        DeviceId,
                        CLKernelWorkGroupInfoType.CL_KERNEL_PREFERRED_WORK_GROUP_SIZE_MULTIPLE).ToInt32();
                }
                finally
                {
                    CLException.ThrowIfFailed(
                        CLAPI.ReleaseKernel(kernelPtr) |
                        CLAPI.ReleaseProgram(programPtr));
                }
            }
        }


        /// <summary>
        /// Initializes support for sub groups.
        /// </summary>
        /// <param name="acceleratorId">The current accelerator id.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:Do not catch general exception types",
            Justification = "Must be catched to setup internal flags")]
        private void InitSubGroupSupport(CLAcceleratorId acceleratorId)
        {
            // Check sub group support
            if (!(SubGroupSupport = acceleratorId.HasAnyExtension(SubGroupExtensions)))
                return;

            // Verify support using a simple kernel
            if (CLKernel.LoadKernel(
                this,
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
                    var localGroupSizes = new IntPtr[] { new IntPtr(MaxNumThreadsPerGroup) };
                    SubGroupSupport = acceleratorId.TryGetKernelSubGroupInfo(
                        kernelPtr,
                        DeviceId,
                        CLKernelSubGroupInfoType.CL_KERNEL_MAX_SUB_GROUP_SIZE_FOR_NDRANGE_KHR,
                        localGroupSizes,
                        out IntPtr subGroupSize);
                    WarpSize = subGroupSize.ToInt32();
                }
                catch (AccessViolationException)
                {
                    // This exception can be raised due to driver issues
                    // on several platforms -> we will just disable sub-group
                    // support for these platforms
                    SubGroupSupport = false;
                }
                finally
                {
                    CLException.ThrowIfFailed(
                        CLAPI.ReleaseKernel(kernelPtr) |
                        CLAPI.ReleaseProgram(programPtr));
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
        public IntPtr ContextPtr => contextPtr;

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
        public bool SubGroupSupport { get; private set; }

        /// <summary>
        /// Returns the OpenCL backend of this accelerator.
        /// </summary>
        public new CLBackend Backend => base.Backend as CLBackend;

        #endregion

        #region Methods

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}(TExtensionProvider)"/>
        public override TExtension CreateExtension<TExtension, TExtensionProvider>(TExtensionProvider provider) =>
            provider.CreateOpenCLExtension(this);

        /// <summary cref="Accelerator.AllocateInternal{T, TIndex}(TIndex)"/>
        protected override MemoryBuffer<T, TIndex> AllocateInternal<T, TIndex>(TIndex extent) =>
            new CLMemoryBuffer<T, TIndex>(this, extent);

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.CreateKernel(TCompiledKernel)"/>
        protected override CLKernel CreateKernel(CLCompiledKernel compiledKernel)
        {
            // Verify OpenCL C version
            if (compiledKernel.CVersion > CVersion)
                throw new NotSupportedException(
                    string.Format(RuntimeErrorMessages.NotSupportedOpenCLCVersion, compiledKernel.CVersion));
            return new CLKernel(this, compiledKernel, null);
        }

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.CreateKernel(TCompiledKernel, MethodInfo)"/>
        protected override CLKernel CreateKernel(CLCompiledKernel compiledKernel, MethodInfo launcher) =>
            new CLKernel(this, compiledKernel, launcher);

        /// <summary cref="Accelerator.CreateStream"/>
        protected override AcceleratorStream CreateStreamInternal() =>
            new CLStream(this);

        /// <summary cref="Accelerator.Synchronize"/>
        protected override void SynchronizeInternal() =>
            DefaultStream.Synchronize();

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind() { }

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind() { }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) => false;

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator) =>
            throw new InvalidOperationException(
                RuntimeErrorMessages.CannotEnablePeerAccessToDifferentAcceleratorKind);

        /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
        protected override void DisablePeerAccessInternal(Accelerator otherAccelerator) { }

        #endregion

        #region General Launch Methods

        /// <summary cref="KernelAccelerator{TCompiledKernel, TKernel}.GenerateKernelLauncherMethod(TCompiledKernel, int)"/>
        protected override MethodInfo GenerateKernelLauncherMethod(CLCompiledKernel kernel, int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;
            AdjustAndVerifyKernelGroupSize(ref customGroupSize, entryPoint);

            // Add support for by ref parameters
            if (entryPoint.HasByRefParameters)
                throw new NotSupportedException(ErrorMessages.NotSupportedByRefKernelParameters);

            var launcher = entryPoint.CreateLauncherMethod(Context);
            var emitter = new ILEmitter(launcher.ILGenerator);

            // Load kernel instance
            var kernelLocal = emitter.DeclareLocal(typeof(CLKernel));
            KernelLauncherBuilder.EmitLoadKernelArgument<CLKernel, ILEmitter>(
                Kernel.KernelInstanceParamIdx,
                emitter);
            emitter.Emit(LocalOperation.Store, kernelLocal);

            // Map all kernel arguments
            var argumentMapper = Backend.ArgumentMapper;
            argumentMapper.Map(emitter, kernelLocal, entryPoint);

            // Load stream
            KernelLauncherBuilder.EmitLoadAcceleratorStream<CLStream, ILEmitter>(
                Kernel.KernelStreamParamIdx,
                emitter);

            // Load kernel
            emitter.Emit(LocalOperation.Load, kernelLocal);

            // Load dimensions
            KernelLauncherBuilder.EmitLoadDimensions(
                entryPoint,
                emitter,
                Kernel.KernelParamDimensionIdx,
                () => { },
                customGroupSize);

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
            if (dynamicSharedMemorySizeInBytes > 0)
                throw new ArgumentOutOfRangeException(nameof(dynamicSharedMemorySizeInBytes));

            groupSize = IntrinsicMath.Min(groupSize, MaxNumThreadsPerGroup);
            return MaxNumThreadsPerGroup / groupSize;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize) =>
            throw new NotSupportedException();

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (dynamicSharedMemorySizeInBytes > 0)
                throw new ArgumentOutOfRangeException(nameof(dynamicSharedMemorySizeInBytes));
            if (maxGroupSize < 1)
                maxGroupSize = MaxNumThreadsPerGroup;

            var clKernel = kernel as CLKernel;
            var workGroupSizeNative = CLAPI.GetKernelWorkGroupInfo<IntPtr>(
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

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (contextPtr != IntPtr.Zero)
            {
                CLException.ThrowIfFailed(
                    CLAPI.ReleaseContext(contextPtr));
                contextPtr = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
