// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents the major OpenCL device vendor.
    /// </summary>
    public enum CLDeviceVendor
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
    /// Represents a single OpenCL device.
    /// </summary>
    [DeviceType(AcceleratorType.OpenCL)]
    public sealed unsafe class CLDevice : Device
    {
        #region Constants

        /// <summary>
        /// The maximum number of devices per platform.
        /// </summary>
        private const int MaxNumDevicesPerPlatform = 64;

        #endregion

        #region Nested Types

        private delegate CLError clGetKernelSubGroupInfoKHR(
            [In] IntPtr kernel,
            [In] IntPtr device,
            [In] CLKernelSubGroupInfoType subGroupInfoType,
            [In] IntPtr inputSize,
            [In] void* input,
            [In] IntPtr maxSize,
            [Out] void* paramValue,
            [Out] IntPtr size);

        #endregion

        #region Static

        /// <summary>
        /// Detects OpenCL devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given devices.
        /// </param>
        /// <returns>All detected OpenCL devices.</returns>
        public static ImmutableArray<Device> GetDevices(
            Predicate<CLDevice> predicate)
        {
            var registry = new DeviceRegistry();
            GetDevices(predicate, registry);
            return registry.ToImmutable();
        }

        /// <summary>
        /// Detects OpenCL devices.
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
            Predicate<CLDevice> predicate,
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
        /// Detects OpenCL devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <param name="registry">The registry to add all devices to.</param>
        private static void GetDevicesInternal(
            Predicate<CLDevice> predicate,
            DeviceRegistry registry)
        {
            var devices = new IntPtr[MaxNumDevicesPerPlatform];
            // Resolve all platforms
            if (!CurrentAPI.IsSupported ||
                CurrentAPI.GetNumPlatforms(out int numPlatforms) !=
                CLError.CL_SUCCESS ||
                numPlatforms < 1)
            {
                return;
            }

            var platforms = new IntPtr[numPlatforms];
            if (CurrentAPI.GetPlatforms(platforms, ref numPlatforms) !=
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

                    var desc = new CLDevice(platform, device);
                    registry.Register(desc, predicate);
                }
            }
        }

        #endregion

        #region Instance

        private readonly clGetKernelSubGroupInfoKHR getKernelSubGroupInfo;
        private readonly HashSet<string> extensionSet = new HashSet<string>();

        /// <summary>
        /// Constructs a new OpenCL accelerator reference.
        /// </summary>
        /// <param name="platformId">The OpenCL platform id.</param>
        /// <param name="deviceId">The OpenCL device id.</param>
        public CLDevice(IntPtr platformId, IntPtr deviceId)
        {
            if (platformId == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(platformId));
            if (deviceId == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(deviceId));

            Backends.Backend.EnsureRunningOnNativePlatform();

            PlatformId = platformId;
            DeviceId = deviceId;

            InitPlatformInfo();
            InitDeviceInfo();
            InitGridInfo();
            InitVendorAndWarpSizeInfo();
            InitMemoryInfo();
            InitCInfo();
            InitExtensions();

            // Resolve extension method
            getKernelSubGroupInfo = CurrentAPI.GetExtension<clGetKernelSubGroupInfoKHR>(
                platformId);

            // Init capabilities
            Capabilities = new CLCapabilityContext(this);
            InitGenericAddressSpaceSupport();
        }

        /// <summary>
        /// Init general platform information.
        /// </summary>
        private void InitPlatformInfo()
        {
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
        }

        /// <summary>
        /// Init general device information.
        /// </summary>
        private void InitDeviceInfo()
        {
            // Resolve general device information
            Name = CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_NAME);
            DeviceType = (CLDeviceType)CurrentAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_TYPE);
            DeviceVersion = CLDeviceVersion.TryParse(
                CurrentAPI.GetDeviceInfo(
                    DeviceId,
                    CLDeviceInfoType.CL_DEVICE_VERSION),
                out var deviceVersion)
                ? deviceVersion
                : CLDeviceVersion.CL10;

            // Resolve clock rate
            ClockRate = CurrentAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_CLOCK_FREQUENCY);

            // Resolve number of multiprocessors
            NumMultiprocessors = CurrentAPI.GetDeviceInfo<int>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_COMPUTE_UNITS);
        }

        /// <summary>
        /// Init grid information.
        /// </summary>
        private void InitGridInfo()
        {
            int workItemDimensions = IntrinsicMath.Max(CurrentAPI.GetDeviceInfo<int>(
              DeviceId, CLDeviceInfoType.CL_DEVICE_MAX_WORK_ITEM_DIMENSIONS), 3);

            //OpenCL does not report maximium grid sizes, MaxGridSize value is consistent the CPU accelators and values returned by CUDA accelerators
            //MaxGridSize is ultimately contrained by system and device memory and how each kernel manages memory.
            MaxGridSize = new Index3D(int.MaxValue, ushort.MaxValue, ushort.MaxValue);

            // Resolve max threads per group
            MaxNumThreadsPerGroup = CurrentAPI.GetDeviceInfo<IntPtr>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_MAX_WORK_GROUP_SIZE).ToInt32();

            // max work item thread dimensions
            var workItemSizes = new IntPtr[workItemDimensions];

            CurrentAPI.GetDeviceInfo(
              DeviceId,
              CLDeviceInfoType.CL_DEVICE_MAX_WORK_ITEM_SIZES,
              workItemSizes);

            MaxGroupSize = new Index3D(
                workItemSizes[0].ToInt32(),
                workItemSizes[1].ToInt32(),
                workItemSizes[2].ToInt32());

            // Result max number of threads per multiprocessor
            MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;
        }

        /// <summary>
        /// Init vendor-specific features.
        /// </summary>
        [SuppressMessage(
            "Globalization",
            "CA1307:Specify StringComparison",
            Justification = "string.GetHashCode(StringComparison) not " +
            "available in net471")]
        private void InitVendorAndWarpSizeInfo()
        {
            VendorName = CurrentAPI.GetPlatformInfo(
                PlatformId,
                CLPlatformInfoType.CL_PLATFORM_VENDOR);

            // Try to determine the actual vendor
            if (CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_WARP_SIZE_NV,
                out int warpSize) == CLError.CL_SUCCESS)
            {
                // Nvidia platform
                WarpSize = warpSize;
                Vendor = CLDeviceVendor.Nvidia;

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
                Vendor = CLDeviceVendor.AMD;
            }
            else
            {
                Vendor = VendorName.Contains(CLDeviceVendor.Intel.ToString()) ?
                    CLDeviceVendor.Intel :
                    CLDeviceVendor.Other;

                // Warp size cannot be resolve at this point
                WarpSize = 0;
            }
        }

        /// <summary>
        /// Init memory information.
        /// </summary>
        private void InitMemoryInfo()
        {
            // Resolve memory size
            MemorySize = CurrentAPI.GetDeviceInfo<long>(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_GLOBAL_MEM_SIZE);

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
        }

        /// <summary>
        /// Init OpenCL C language information.
        /// </summary>
        private void InitCInfo()
        {
            // Determine the supported OpenCL C version
            var clVersionString = CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_OPENCL_C_VERSION);
            if (!CLCVersion.TryParse(clVersionString, out CLCVersion version))
                version = CLCVersion.CL10;
            CVersion = version;
        }

        /// <summary>
        /// Init general OpenCL extensions.
        /// </summary>
        private void InitExtensions()
        {
            // Resolve extensions
            var extensionString = CurrentAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_EXTENSIONS);
            foreach (var extension in extensionString.ToLower().Split(' '))
                extensionSet.Add(extension);
            Extensions = extensionSet.ToImmutableArray();
        }

        private void InitGenericAddressSpaceSupport()
        {
            if (DeviceVersion < CLDeviceVersion.CL20)
            {
                Capabilities.GenericAddressSpace = false;
            }
            else if (DeviceVersion < CLDeviceVersion.CL30)
            {
                Capabilities.GenericAddressSpace = true;
            }
            else
            {
                try
                {
                    Capabilities.GenericAddressSpace =
                        CurrentAPI.GetDeviceInfo<int>(
                            DeviceId,
                            CLDeviceInfoType.CL_DEVICE_GENERIC_ADDRESS_SPACE_SUPPORT)
                        != 0;
                }
                catch (CLException)
                {
                    Capabilities.GenericAddressSpace = false;
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the OpenCL platform id.
        /// </summary>
        public IntPtr PlatformId { get; }

        /// <summary>
        /// Returns the OpenCL device id.
        /// </summary>
        public IntPtr DeviceId { get; }

        /// <summary>
        /// Returns the associated platform name.
        /// </summary>
        public string PlatformName { get; private set; }

        /// <summary>
        /// Returns the associated platform version.
        /// </summary>
        public CLPlatformVersion PlatformVersion { get; private set; }

        /// <summary>
        /// Returns the associated vendor.
        /// </summary>
        public string VendorName { get; private set; }

        /// <summary>
        /// Returns the main accelerator vendor type.
        /// </summary>
        public CLDeviceVendor Vendor { get; private set; }

        /// <summary>
        /// Returns the OpenCL device type.
        /// </summary>
        public CLDeviceType DeviceType { get; private set; }

        /// <summary>
        /// Returns the OpenCL device version.
        /// </summary>
        public CLDeviceVersion DeviceVersion { get; private set; }

        /// <summary>
        /// Returns the clock rate.
        /// </summary>
        public int ClockRate { get; private set; }

        /// <summary>
        /// Returns the supported OpenCL C version.
        /// </summary>
        public CLCVersion CVersion { get; private set; }

        /// <summary>
        /// Returns the OpenCL C version passed to -cl-std.
        /// </summary>
        public CLCVersion CLStdVersion =>
            DeviceVersion >= CLDeviceVersion.CL30
            ? CLCVersion.CL30
            : DeviceVersion >= CLDeviceVersion.CL20
                ? CLCVersion.CL20
                : CVersion;

        /// <summary>
        /// Returns all extensions.
        /// </summary>
        public ImmutableArray<string> Extensions { get; private set; }

        /// <summary>
        /// Returns the supported capabilities of this accelerator.
        /// </summary>
        public new CLCapabilityContext Capabilities
        {
            get => base.Capabilities as CLCapabilityContext;
            private set => base.Capabilities = value;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override Accelerator CreateAccelerator(Context context) =>
            CreateCLAccelerator(context);

        /// <summary>
        /// Creates a new OpenCL accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>The created OpenCL accelerator.</returns>
        public CLAccelerator CreateCLAccelerator(Context context) =>
            new CLAccelerator(context, this);

        /// <summary>
        /// Resolves device information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="type">The information type.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns>The error code.</returns>
        public CLError GetDeviceInfo<T>(CLDeviceInfoType type, out T value)
            where T : unmanaged => CurrentAPI.GetDeviceInfo(DeviceId, type, out value);

        /// <summary>
        /// Resolves device information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved value.</returns>
        public T GetDeviceInfo<T>(CLDeviceInfoType type)
            where T : unmanaged => CurrentAPI.GetDeviceInfo<T>(DeviceId, type);

        /// <summary>
        /// Returns true if the given extension is supported.
        /// </summary>
        /// <param name="extension">The extension to look for.</param>
        /// <returns>True, if the extension is supported.</returns>
        public bool HasExtension(string extension) =>
            extensionSet.Contains(extension);

        /// <summary>
        /// Returns true if all of the given extensions are supported.
        /// </summary>
        /// <param name="extensions">The extensions to look for.</param>
        /// <returns>True, if all of the given extensions are supported.</returns>
        public bool HasAllExtensions<TCollection>(TCollection extensions)
            where TCollection : IEnumerable<string>
        {
            foreach (var extension in extensions)
            {
                if (!HasExtension(extension))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if any of the given extensions is supported.
        /// </summary>
        /// <param name="extensions">The extensions to look for.</param>
        /// <returns>True, if any of the given extensions is supported.</returns>
        public bool HasAnyExtension<TCollection>(TCollection extensions)
            where TCollection : IEnumerable<string>
        {
            foreach (var extension in extensions)
            {
                if (HasExtension(extension))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to resolves kernel sub-group information as typed structure value of
        /// type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="kernel">The kernel.</param>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <param name="numInputs">The number of inputs.</param>
        /// <param name="inputs">All input values.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns>True, if the value could be resolved.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetKernelSubGroupInfo<T>(
            IntPtr kernel,
            IntPtr device,
            CLKernelSubGroupInfoType type,
            int numInputs,
            IntPtr* inputs,
            out T value)
            where T : unmanaged
        {
            value = default;
            return getKernelSubGroupInfo?.Invoke(
                kernel,
                device,
                type,
                new IntPtr(numInputs * IntPtr.Size),
                inputs,
                new IntPtr(Interop.SizeOf<T>()),
                Unsafe.AsPointer(ref value),
                IntPtr.Zero) == CLError.CL_SUCCESS;
        }

        /// <summary>
        /// Resolves kernel sub-group information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="kernel">The kernel.</param>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <param name="inputs">All input values.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns>True, if the value could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetKernelSubGroupInfo<T>(
            IntPtr kernel,
            IntPtr device,
            CLKernelSubGroupInfoType type,
            IntPtr[] inputs,
            out T value)
            where T : unmanaged
        {
            fixed (IntPtr* basePtr = &inputs[0])
            {
                return TryGetKernelSubGroupInfo(
                    kernel,
                    device,
                    type,
                    inputs.Length,
                    basePtr,
                    out value);
            }
        }

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

            writer.Write("  Device version:                          ");
            writer.WriteLine(DeviceVersion.ToString());

            writer.Write("  Clock rate:                              ");
            writer.Write(ClockRate);
            writer.WriteLine(" MHz");
        }

        /// <inheritdoc/>
        protected override void PrintGeneralInfo(TextWriter writer)
        {
            writer.Write("  OpenCL C version:                        ");
            writer.WriteLine(CVersion.ToString());

            writer.Write("  OpenCL C -cl-std version:                ");
            writer.WriteLine(CLStdVersion.ToString());

            writer.Write("  Has FP16 support:                        ");
            writer.WriteLine(Capabilities.Float16);

            writer.Write("  Has Int64 atomics support:               ");
            writer.WriteLine(Capabilities.Int64_Atomics);

            writer.Write("  Has sub group support:                   ");
            writer.WriteLine(Capabilities.SubGroups);

            writer.Write("  Has generic address space support:       ");
            writer.WriteLine(Capabilities.GenericAddressSpace);
        }

        #endregion

        #region Object

        /// <inheritdoc/>
        public override bool Equals(object obj) =>
            obj is CLDevice device &&
            device.PlatformId == PlatformId &&
            device.DeviceId == DeviceId &&
            base.Equals(obj);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ PlatformId.GetHashCode() ^ DeviceId.GetHashCode();

        #endregion
    }
}
