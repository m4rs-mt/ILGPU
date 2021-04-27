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
using System.Reflection;
using System.Reflection.Emit;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL accelerator (CPU or GPU device).
    /// </summary>
    public sealed class CLAccelerator : KernelAccelerator<CLCompiledKernel, CLKernel>
    {
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

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="description">The accelerator description.</param>
        internal CLAccelerator(Context context, CLDevice description)
            : base(context, description)
        {
            Backends.Backend.EnsureRunningOnNativePlatform();
            if (!description.Capabilities.GenericAddressSpace)
                throw CLCapabilityContext.GetNotSupportedGenericAddressSpaceException();

            // Create new context
            CLException.ThrowIfFailed(
                CurrentAPI.CreateContext(DeviceId, out var contextPtr));
            NativePtr = contextPtr;

            Bind();
            DefaultStream = CreateStreamInternal();

            InitVendorFeatures();
            InitSubGroupSupport(description);
            Init(new CLBackend(Context, Capabilities, Vendor, CLStdVersion));
        }

        /// <summary>
        /// Initializes major vendor features.
        /// </summary>
        private void InitVendorFeatures()
        {
            // Check major vendor features
            if (Device.Vendor == CLDeviceVendor.Nvidia ||
                Device.Vendor == CLDeviceVendor.AMD)
            {
                return;
            }
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

        /// <summary>
        /// Initializes support for sub groups.
        /// </summary>
        /// <param name="acceleratorId">The current accelerator id.</param>
        private void InitSubGroupSupport(CLDevice acceleratorId)
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
        /// Returns the parent OpenCL device.
        /// </summary>
        public new CLDevice Device => base.Device as CLDevice;

        /// <summary>
        /// Returns the native OpenCL platform id.
        /// </summary>
        public IntPtr PlatformId => Device.PlatformId;

        /// <summary>
        /// Returns the associated platform name.
        /// </summary>
        public string PlatformName => Device.PlatformName;

        /// <summary>
        /// Returns the associated platform version.
        /// </summary>
        public CLPlatformVersion PlatformVersion => Device.PlatformVersion;

        /// <summary>
        /// Returns the associated vendor.
        /// </summary>
        public string VendorName => Device.VendorName;

        /// <summary>
        /// Returns the main accelerator vendor type.
        /// </summary>
        public CLDeviceVendor Vendor => Device.Vendor;

        /// <summary>
        /// Returns the native OpenCL device id.
        /// </summary>
        public IntPtr DeviceId => Device.DeviceId;

        /// <summary>
        /// Returns the OpenCL device type.
        /// </summary>
        public CLDeviceType DeviceType => Device.DeviceType;

        /// <summary>
        /// Returns the clock rate.
        /// </summary>
        public int ClockRate => Device.ClockRate;

        /// <summary>
        /// Returns the supported OpenCL C version.
        /// </summary>
        public CLCVersion CVersion => Device.CVersion;

        /// <summary>
        /// Returns the OpenCL C version passed to -cl-std.
        /// </summary>
        public CLCVersion CLStdVersion => Device.CLStdVersion;

        /// <summary>
        /// Returns the OpenCL backend of this accelerator.
        /// </summary>
        public new CLBackend Backend => base.Backend as CLBackend;

        /// <summary>
        /// Returns the capabilities of this accelerator.
        /// </summary>
        public new CLCapabilityContext Capabilities => Device.Capabilities;

        #endregion

        #region Methods

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
            if (compiledKernel.CVersion > CLStdVersion)
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
                MaxGridSize,
                MaxGroupSize,
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
