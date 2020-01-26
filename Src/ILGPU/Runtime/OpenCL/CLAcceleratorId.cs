// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLAcceleratorId.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using ILGPU.Runtime.OpenCL.API;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents a single OpenCL accelerator reference.
    /// </summary>
    public sealed unsafe class CLAcceleratorId : AcceleratorId
    {
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

        #region Instance

        private readonly clGetKernelSubGroupInfoKHR getKernelSubGroupInfo;
        private readonly HashSet<string> extensionSet;

        /// <summary>
        /// Constructs a new OpenCL accelerator reference.
        /// </summary>
        /// <param name="platformId">The OpenCL platform id.</param>
        /// <param name="deviceId">The OpenCL device id.</param>
        public CLAcceleratorId(IntPtr platformId, IntPtr deviceId)
            : base(AcceleratorType.OpenCL)
        {
            if (platformId == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(platformId));
            if (deviceId == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(deviceId));

            PlatformId = platformId;
            DeviceId = deviceId;

            DeviceType = (CLDeviceType)CLAPI.GetDeviceInfo<long>(
                deviceId,
                CLDeviceInfoType.CL_DEVICE_TYPE);

            // Resolve extensions
            var extensionString = CLAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_EXTENSIONS);
            extensionSet = new HashSet<string>(
                extensionString.ToLower().Split(' '));
            Extensions = extensionSet.ToImmutableArray();

            // Determine the supported OpenCL C version
            var clVersionString = CLAPI.GetDeviceInfo(
                DeviceId,
                CLDeviceInfoType.CL_DEVICE_OPENCL_C_VERSION);
            if (!CLCVersion.TryParse(clVersionString, out CLCVersion version))
                version = CLCVersion.CL10;
            CVersion = version;

            // Resolve extension method
            getKernelSubGroupInfo = CLAPI.GetExtension<clGetKernelSubGroupInfoKHR>(platformId);
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
        public IntPtr DeviceId { get; private set; }

        /// <summary>
        /// Returns the OpenCL device type.
        /// </summary>
        public CLDeviceType DeviceType { get; }

        /// <summary>
        /// Returns the supported OpenCL C version.
        /// </summary>
        public CLCVersion CVersion { get; }

        /// <summary>
        /// Returns all extensions.
        /// </summary>
        public ImmutableArray<string> Extensions { get; }

        #endregion

        #region Methods

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
        /// Tries to resolves kernel sub-group information as typed structure value of type <typeparamref name="T"/>
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
            where T : struct
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
        /// Resolves kernel sub-group information as typed structure value of type <typeparamref name="T"/>
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
            where T : struct
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

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current accelerator id.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current accelerator id.</returns>
        public override bool Equals(object obj) =>
            obj is CLAcceleratorId acceleratorId &&
            acceleratorId.PlatformId == PlatformId &&
            acceleratorId.DeviceId == DeviceId;

        /// <summary>
        /// Returns the hash code of this accelerator id.
        /// </summary>
        /// <returns>The hash code of this accelerator id.</returns>
        public override int GetHashCode() =>
            PlatformId.GetHashCode() ^ DeviceId.GetHashCode() ^ base.GetHashCode();

        /// <summary>
        /// Returns the string representation of this accelerator id.
        /// </summary>
        /// <returns>The string representation of this accelerator id.</returns>
        public override string ToString() =>
            $"Platform {PlatformId}, Device {DeviceId}, {base.ToString()}";

        #endregion
    }
}
