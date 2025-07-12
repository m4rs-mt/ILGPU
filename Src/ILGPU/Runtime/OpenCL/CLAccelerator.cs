// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL;

/// <summary>
/// Represents an OpenCL accelerator (CPU or GPU device).
/// </summary>
public sealed class CLAccelerator : Accelerator
{
    #region Static

    /// <summary>
    /// Represents the minimum OpenCL C version that is required.
    /// </summary>
    public static readonly CLCVersion MinimumCVersion = CLCVersion.CL20;

    /// <summary>
    /// Parameter index of long kernel grid index offsets.
    /// </summary>
    public const int LongGridKernelParameterIndex = 0;

    /// <summary>
    /// Parameter index of dynamic shared-memory parameters.
    /// </summary>
    public const int DynamicSharedMemoryParameterIndex = 1;

    /// <summary>
    /// Parameter index of dynamic shared-memory length parameters.
    /// </summary>
    public const int DynamicSharedMemoryLengthParameterIndex = 2;

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
        ["cl_khr_subgroups", "cl_intel_subgroups"];

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
        if (!description.Capabilities.GenericAddressSpace)
            throw CLCapabilityContext.GetNotSupportedGenericAddressSpaceException();

        // Create new context
        CLException.ThrowIfFailed(CurrentAPI.CreateContext(DeviceId, out var contextPtr));
        NativePtr = contextPtr;

        Bind();
        DefaultStream = CreateStreamInternal(AcceleratorStreamFlags.None);

        InitVendorFeatures();
        InitSubGroupSupport(description);
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
                var localGroupSizes = new IntPtr[] { new(MaxNumThreadsPerGroup) };
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
    public new CLDevice Device => base.Device.AsNotNullCast<CLDevice>();

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
    /// Returns the capabilities of this accelerator.
    /// </summary>
    public new CLCapabilityContext Capabilities => Device.Capabilities;

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize) =>
        new CLMemoryBuffer(this, length, elementSize);

    /// <inheritdoc/>
    protected override Kernel LoadKernel(CompiledKernel compiledKernel)
    {
        var clCompiledKernel = (CLCompiledKernel)compiledKernel;

        // Verify OpenCL C version
        if (clCompiledKernel.CVersion > CLStdVersion)
        {
            throw new NotSupportedException(
                string.Format(
                    RuntimeErrorMessages.NotSupportedOpenCLCVersion,
                    clCompiledKernel.CVersion));
        }

        // Load new compiled program kernel
        return new CLKernel(this, clCompiledKernel);
    }

    /// <summary>
    /// Launches the given OpenCL kernel using the stream and launch config provided.
    /// </summary>
    /// <param name="stream">The current accelerator stream.</param>
    /// <param name="kernel">The kernel to launch.</param>
    /// <param name="kernelConfig">The kernel configuration to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void LaunchKernel(
        CLStream stream,
        CLKernel kernel,
        in KernelConfig kernelConfig)
    {
        // Check kernel configuration dimensions
        if (kernelConfig.GroupSize > MaxNumThreadsPerGroup)
            throw new ArgumentOutOfRangeException(nameof(kernelConfig));

        // Check for hybrid mode which requires static shared memory information to
        // be added to dynamic memory information
        var launchConfig = kernel.GetCombinedSharedMemoryConfig(kernelConfig);
        if (launchConfig.SharedMemoryBytes > MaxSharedMemoryPerGroup)
            throw new ArgumentOutOfRangeException(nameof(kernelConfig));

        // Select specialized kernel binding method depending on shared memory mode
        if (kernel.SharedMemoryMode == CompiledKernelSharedMemoryMode.Static)
        {
            // Iterate over long-grid configurations and bind required offset parameter
            for (long i = 0; i < kernelConfig.GridSize; i += OptimalKernelSize.GridSize)
            {
                // Setup kernel grid offset
                CurrentAPI.SetKernelArgument(
                    kernel.KernelPtr,
                    LongGridKernelParameterIndex,
                    i);

                // Launch kernel with current configuration
                CLException.ThrowIfFailed(
                    CurrentAPI.LaunchKernelWithStreamBinding<DefaultLaunchHandler>(
                        stream,
                        kernel,
                        kernelConfig));
            }
        }
        else
        {
            // Iterate over long-grid configurations and bind required offset parameter
            for (long i = 0; i < kernelConfig.GridSize; i += OptimalKernelSize.GridSize)
            {
                // Setup kernel grid offset
                CurrentAPI.SetKernelArgument(
                    kernel.KernelPtr,
                    LongGridKernelParameterIndex,
                    i);

                // Launch kernel with current configuration
                CLException.ThrowIfFailed(
                    CurrentAPI.LaunchKernelWithStreamBinding<DynamicSharedMemoryHandler>(
                        stream,
                        kernel,
                        kernelConfig));
            }
        }
    }

    /// <inheritdoc/>
    protected override AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags) =>
        new CLStream(this, flags);

    /// <summary>
    /// Creates a <see cref="CLStream"/> object from an externally created stream/queue
    /// using its pointer.
    /// </summary>
    /// <param name="ptr">The pointer to use while creating the new stream.</param>
    /// <param name="responsible">
    /// Whether ILGPU is responsible of disposing this stream.
    /// </param>
    /// <returns>The created stream.</returns>
    public CLStream CreateStream(IntPtr ptr, bool responsible) =>
        new(this, ptr, responsible);

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

    #region Occupancy

    /// <inheritdoc/>
    protected internal override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
        Kernel kernel,
        int groupSize,
        int dynamicSharedMemorySizeInBytes)
    {
        if (dynamicSharedMemorySizeInBytes > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dynamicSharedMemorySizeInBytes));
        }

        groupSize = XMath.Min(groupSize, MaxNumThreadsPerGroup);
        return MaxNumThreadsPerGroup / groupSize;
    }

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    protected internal override int EstimateGroupSizeInternal(
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

        var clKernel = kernel.AsNotNullCast<CLKernel>();
        var workGroupSizeNative = CurrentAPI.GetKernelWorkGroupInfo<IntPtr>(
            clKernel.KernelPtr,
            DeviceId,
            CLKernelWorkGroupInfoType.CL_KERNEL_WORK_GROUP_SIZE);
        int workGroupSize = workGroupSizeNative.ToInt32();
        workGroupSize = XMath.Min(workGroupSize, maxGroupSize);
        minGridSize = XMath.DivRoundUp(MaxNumThreads, workGroupSize);

        return workGroupSize;
    }

    #endregion

    #region Page Lock Scope

    /// <inheritdoc/>
    protected unsafe override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements)
    {
        Trace.WriteLine(RuntimeErrorMessages.NotSupportedPageLock);
        return new NullPageLockScope<T>(this, pinned, numElements);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the current OpenCL context.
    /// </summary>
    protected override void DisposeAccelerator_Locked(bool disposing) =>
        CLException.VerifyDisposed(
            disposing,
            CurrentAPI.ReleaseContext(NativePtr));

    #endregion
}
