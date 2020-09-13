﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable IDE1006 // Naming

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Wraps the OpenCL-driver API.
    /// </summary>
    public abstract unsafe class CLAPI : RuntimeAPI
    {
        #region Nested Types

        /// <summary>
        /// An abstract launch handler to specialize kernel launches.
        /// </summary>
        internal interface ILaunchHandler
        {
            /// <summary>
            /// Performs pre-launch operations for a specific kernel.
            /// </summary>
            /// <param name="stream">The current stream.</param>
            /// <param name="kernel">The current kernel.</param>
            /// <param name="config">The current kernel configuration.</param>
            /// <returns>The error status.</returns>
            CLError PreLaunchKernel(
                CLStream stream,
                CLKernel kernel,
                RuntimeKernelConfig config);
        }

        /// <summary>
        /// The default launch handler that does not perform any specific launch
        /// operations.
        /// </summary>
        internal readonly struct DefaultLaunchHandler : ILaunchHandler
        {
            /// <summary>
            /// Does not perform any operations and returns
            /// <see cref="CLError.CL_SUCCESS"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly CLError PreLaunchKernel(
                CLStream stream,
                CLKernel kernel,
                RuntimeKernelConfig config) =>
                CLError.CL_SUCCESS;
        }

        /// <summary>
        /// A dynamic shared memory handler that setups a dynamic memory allocation.
        /// </summary>
        internal readonly struct DynamicSharedMemoryHandler : ILaunchHandler
        {
            /// <summary>
            /// Setups a dynamic shared memory allocation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly CLError PreLaunchKernel(
                CLStream stream,
                CLKernel kernel,
                RuntimeKernelConfig config) =>
                CLAPI.CurrentAPI.SetKernelArgumentUnsafeWithKernel(
                    kernel,
                    0,
                    config.SharedMemoryConfig.DynamicArraySize,
                    null);
        }

        #endregion

        #region Constants

        /// <summary>
        /// Represents the driver library name on Windows.
        /// </summary>
        public const string LibNameWindows = "opencl";

        /// <summary>
        /// Represents the driver library name on Linux.
        /// </summary>
        public const string LibNameLinux = "opencl";

        /// <summary>
        /// Represents the driver library name on MacOS.
        /// </summary>
        public const string LibNameMacOS = "opencl";

        #endregion

        #region Native Methods

        // Devices

        [DynamicImport]
        internal abstract CLError clGetPlatformIDs(
            [In] int maxNumPlatforms,
            [Out] IntPtr* platforms,
            [Out] out int numPlatforms);

        [DynamicImport]
        internal abstract CLError clGetPlatformInfo(
            [In] IntPtr platform,
            [In] CLPlatformInfoType platformInfoType,
            [In] IntPtr maxSize,
            [Out] void* value,
            [Out] IntPtr size);

        [DynamicImport]
        internal abstract CLError clGetDeviceIDs(
            [In] IntPtr platform,
            [In] CLDeviceType deviceType,
            [In] int maxNumDevices,
            [Out] IntPtr* devices,
            [Out] out int numDevices);

        [DynamicImport]
        internal abstract CLError clReleaseDevice(
            [In] IntPtr deviceId);

        [DynamicImport]
        internal abstract CLError clGetDeviceInfo(
            [In] IntPtr deviceId,
            [In] CLDeviceInfoType deviceInfoType,
            [In] IntPtr maxSize,
            [Out] void* value,
            [Out] IntPtr size);

        [DynamicImport(CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal abstract IntPtr clGetExtensionFunctionAddressForPlatform(
            [In] IntPtr platformId,
            [In] string name);

        // Context

        [DynamicImport]
        internal abstract IntPtr clCreateContext(
            [In] IntPtr* properties,
            [In] int numDevices,
            [In] IntPtr* devices,
            [In] IntPtr callback,
            [In] IntPtr userData,
            [Out] out CLError errorCode);

        [DynamicImport]
        internal abstract CLError clReleaseContext(
            [In] IntPtr context);

        // Command Queues

        [DynamicImport]
        internal abstract IntPtr clCreateCommandQueue(
            [In] IntPtr context,
            [In] IntPtr device,
            [In] IntPtr properties,
            [Out] out CLError errorCode);

        [DynamicImport]
        internal abstract IntPtr clCreateCommandQueueWithProperties(
            [In] IntPtr context,
            [In] IntPtr deviceId,
            [In] IntPtr properties,
            [Out] out CLError errorCode);

        [DynamicImport]
        internal abstract CLError clReleaseCommandQueue(
            [In] IntPtr queue);

        [DynamicImport]
        internal abstract CLError clFlush(
            [In] IntPtr queue);

        [DynamicImport]
        internal abstract CLError clFinish(
            [In] IntPtr queue);

        // Kernels

        [DynamicImport(CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal abstract IntPtr clCreateProgramWithSource(
            [In] IntPtr context,
            [In] int numPrograms,
            [In] ref string source,
            [In] ref IntPtr lengths,
            [Out] out CLError errorCode);

        [DynamicImport(CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal abstract CLError clBuildProgram(
            [In] IntPtr program,
            [In] int numDevices,
            [In] IntPtr* devices,
            [In] string options,
            [In] IntPtr callback,
            [In] IntPtr userData);

        [DynamicImport]
        internal abstract CLError clReleaseProgram(
            [In] IntPtr program);

        [DynamicImport]
        internal abstract CLError clGetProgramInfo(
            [In] IntPtr program,
            [In] CLProgramInfo param_name,
            [In] IntPtr paramValueSize,
            [Out] void* paramValue,
            [Out] out IntPtr paramValueSizeRet);

        [DynamicImport]
        internal abstract CLError clGetProgramBuildInfo(
            [In] IntPtr program,
            [In] IntPtr device,
            [In] CLProgramBuildInfo paramName,
            [In] IntPtr paramValueSize,
            [Out] void* paramValue,
            [Out] out IntPtr paramValueSizeRet);

        [DynamicImport(CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal abstract IntPtr clCreateKernel(
            [In] IntPtr program,
            [In] string kernelName,
            [Out] out CLError errorCode);

        [DynamicImport]
        internal abstract CLError clReleaseKernel(
            [In] IntPtr kernel);

        [DynamicImport]
        internal abstract CLError clSetKernelArg(
            [In] IntPtr kernel,
            [In] int index,
            [In] IntPtr size,
            [In] void* value);

        [DynamicImport]
        internal abstract CLError clEnqueueNDRangeKernel(
            [In] IntPtr queue,
            [In] IntPtr kernel,
            [In] int workDimensions,
            [In] IntPtr* workOffsets,
            [In] IntPtr* globalWorkSizes,
            [In] IntPtr* localWorkSizes,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* creatingEvent);

        [DynamicImport]
        internal abstract CLError clGetKernelWorkGroupInfo(
            [In] IntPtr kernel,
            [In] IntPtr device,
            [In] CLKernelWorkGroupInfoType workGroupInfoType,
            [In] IntPtr maxSize,
            [Out] void* paramValue,
            [Out] IntPtr size);

        // Buffers

        [DynamicImport]
        internal abstract IntPtr clCreateBuffer(
            IntPtr context,
            CLBufferFlags flags,
            IntPtr size,
            IntPtr hostPointer,
            out CLError errorCode);

        [DynamicImport]
        internal abstract CLError clReleaseMemObject(
            [In] IntPtr buffer);

        [DynamicImport]
        internal abstract CLError clEnqueueReadBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In] bool blockingRead,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] IntPtr ptr,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DynamicImport]
        internal abstract CLError clEnqueueWriteBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In] bool blockingWrite,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] IntPtr ptr,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DynamicImport]
        internal abstract CLError clEnqueueFillBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In] void* pattern,
            [In] IntPtr patternSize,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DynamicImport]
        internal abstract CLError clEnqueueCopyBuffer(
            [In] IntPtr queue,
            [In] IntPtr sourceBuffer,
            [In] IntPtr targetBuffer,
            [In] IntPtr sourceOffset,
            [In] IntPtr targetOffset,
            [In] IntPtr size,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        #endregion

        #region Static

        /// <summary>
        /// Returns the driver API for the current platform.
        /// </summary>
        public static CLAPI CurrentAPI { get; } =
            RuntimeSystem.Instance.CreateDllWrapper<CLAPI>(
                windows: LibNameWindows,
                linux: LibNameLinux,
                macos: LibNameMacOS,
                RuntimeErrorMessages.CLNotSupported);

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the driver API.
        /// </summary>
        /// <returns>The error status.</returns>
        public override bool Init() =>
            GetNumPlatforms(out int numPlatforms) == CLError.CL_SUCCESS &&
            numPlatforms > 0;

        #endregion

        #region Device Methods

        /// <summary>
        /// Resolves the number of available platforms.
        /// </summary>
        /// <returns>The error code.</returns>
        public CLError GetNumPlatforms(out int numPlatforms)
        {
            if (Backends.Backend.RunningOnNativePlatform)
            {
                return clGetPlatformIDs(
                    short.MaxValue,
                    null,
                    out numPlatforms);
            }
            else
            {
                numPlatforms = 0;
                return CLError.CL_DEVICE_NOT_AVAILABLE;
            }
        }

        /// <summary>
        /// Resolves the number of available platforms.
        /// </summary>
        /// <param name="platforms">The target platform ids to fill.</param>
        /// <param name="numPlatforms">The resolved number of platforms.</param>
        /// <returns>The error code.</returns>
        public CLError GetPlatforms(IntPtr[] platforms, out int numPlatforms)
        {
            Debug.Assert(platforms != null, "Invalid platform ids");
            fixed (IntPtr* ptr = &platforms[0])
            {
                numPlatforms = platforms.Length;
                return GetPlatforms(ptr, ref numPlatforms);
            }
        }

        /// <summary>
        /// Resolves the number of available platforms.
        /// </summary>
        /// <param name="platforms">The target platform ids to fill.</param>
        /// <param name="numPlatforms">The resolved number of platforms.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        public CLError GetPlatforms(IntPtr* platforms, ref int numPlatforms) =>
            clGetPlatformIDs(numPlatforms, platforms, out numPlatforms);

        /// <summary>
        /// Resolves platform information as string value.
        /// </summary>
        /// <param name="platform">The platform.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved string value.</returns>
        public string GetPlatformInfo(IntPtr platform, CLPlatformInfoType type)
        {
            const int MaxStringLength = 1024;
            var stringValue = stackalloc sbyte[MaxStringLength];
            var size = IntPtr.Zero;

            CLException.ThrowIfFailed(clGetPlatformInfo(
                platform,
                type,
                new IntPtr(MaxStringLength),
                stringValue,
                new IntPtr(&size)));
            int intSize = size.ToInt32();
            return intSize > 0 && intSize < MaxStringLength
                ? new string(stringValue, 0, intSize - 1)
                : string.Empty;
        }

        /// <summary>
        /// Resolves platform information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="platform">The platform.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved value.</returns>
        public T GetPlatformInfo<T>(IntPtr platform, CLPlatformInfoType type)
            where T : unmanaged
        {
            T value = default;
            CLException.ThrowIfFailed(clGetPlatformInfo(
                platform,
                type,
                new IntPtr(Interop.SizeOf<T>()),
                Unsafe.AsPointer(ref value),
                IntPtr.Zero));
            return value;
        }

        /// <summary>
        /// Resolves the number of available devices.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <param name="deviceType">The device type.</param>
        /// <param name="devices">The device ids to fill.</param>
        /// <param name="numDevices">The number of devices.</param>
        /// <returns>The error code.</returns>
        public CLError GetDevices(
            IntPtr platform,
            CLDeviceType deviceType,
            IntPtr[] devices,
            out int numDevices)
        {
            Debug.Assert(devices != null, "Invalid devices");
            fixed (IntPtr* ptr = &devices[0])
            {
                numDevices = devices.Length;
                return GetDevices(platform, deviceType, ptr, ref numDevices);
            }
        }

        /// <summary>
        /// Resolves the number of available devices.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <param name="deviceType">The device type.</param>
        /// <param name="devices">The device ids to fill.</param>
        /// <param name="numDevices">The number of devices.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        public CLError GetDevices(
            IntPtr platform,
            CLDeviceType deviceType,
            IntPtr* devices,
            ref int numDevices) =>
            clGetDeviceIDs(
                platform,
                deviceType,
                numDevices,
                devices,
                out numDevices);

        /// <summary>
        /// Releases the given device.
        /// </summary>
        /// <param name="device">The device</param>
        /// <returns>The error code.</returns>
        public CLError ReleaseDevice(IntPtr device) =>
            clReleaseDevice(device);

        /// <summary>
        /// Resolves device information as string value.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved string value.</returns>
        public string GetDeviceInfo(IntPtr device, CLDeviceInfoType type)
        {
            const int MaxStringLength = 8192;
            var stringValue = stackalloc sbyte[MaxStringLength];
            var size = IntPtr.Zero;

            CLException.ThrowIfFailed(clGetDeviceInfo(
                device,
                type,
                new IntPtr(MaxStringLength),
                stringValue,
                new IntPtr(&size)));
            int intSize = size.ToInt32();
            return intSize > 0 && intSize < MaxStringLength
                ? new string(stringValue, 0, intSize - 1)
                : string.Empty;
        }

        /// <summary>
        /// Resolves device information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved value.</returns>
        public T GetDeviceInfo<T>(IntPtr device, CLDeviceInfoType type)
            where T : unmanaged
        {
            CLException.ThrowIfFailed(GetDeviceInfo(
                device,
                type,
                out T value));
            return value;
        }

        /// <summary>
        /// Resolves device information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns>The error code.</returns>
        public CLError GetDeviceInfo<T>(
            IntPtr device,
            CLDeviceInfoType type,
            out T value)
            where T : unmanaged
        {
            value = default;
            return clGetDeviceInfo(
                device,
                type,
                new IntPtr(Interop.SizeOf<T>()),
                Unsafe.AsPointer(ref value),
                IntPtr.Zero);
        }

        /// <summary>
        /// Resolves device information as array of typed structure values of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <param name="elements">The elements to fill.</param>
        /// <returns>The resolved value.</returns>
        public void GetDeviceInfo<T>(
            IntPtr device,
            CLDeviceInfoType type,
            T[] elements)
            where T : unmanaged
        {
            fixed (T* ptr = &elements[0])
            {

                CLException.ThrowIfFailed(clGetDeviceInfo(
                    device,
                    type,
                    new IntPtr(Interop.SizeOf<T>() * elements.Length),
                    ptr,
                    IntPtr.Zero));
            }
        }

        /// <summary>
        /// Resolves an extension delegate for the given platform.
        /// </summary>
        /// <typeparam name="T">The delegate type.</typeparam>
        /// <param name="platform">The platform pointer.</param>
        /// <returns>The resolved extension.</returns>
        public T GetExtension<T>(IntPtr platform)
            where T : Delegate =>
            GetExtension<T>(platform, typeof(T).Name);

        /// <summary>
        /// Resolves an extension delegate for the given platform.
        /// </summary>
        /// <typeparam name="T">The delegate type.</typeparam>
        /// <param name="platform">The platform pointer.</param>
        /// <param name="name">The extension name.</param>
        /// <returns>The resolved extension.</returns>
        public T GetExtension<T>(IntPtr platform, string name)
            where T : Delegate
        {
            var address = clGetExtensionFunctionAddressForPlatform(
                platform,
                name);
            return address == IntPtr.Zero
                ? null
                : Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        /// <summary>
        /// Creates a new context.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="context">The created context.</param>
        /// <returns>The error code.</returns>
        public CLError CreateContext(IntPtr device, out IntPtr context)
        {
            context = clCreateContext(
                null,
                1,
                &device,
                IntPtr.Zero,
                IntPtr.Zero,
                out CLError errorStatus);
            return errorStatus;
        }

        /// <summary>
        /// Releases the given context.
        /// </summary>
        /// <param name="context">The context to release.</param>
        /// <returns>The error code.</returns>
        public CLError ReleaseContext(IntPtr context) =>
            clReleaseContext(context);

        /// <summary>
        /// Creates a new command queue.
        /// </summary>
        /// <param name="device">The associated device.</param>
        /// <param name="context">The parent context.</param>
        /// <param name="queue">The created queue.</param>
        /// <returns>The error code.</returns>
        public CLError CreateCommandQueue(
            IntPtr device,
            IntPtr context,
            out IntPtr queue)
        {
            queue = clCreateCommandQueueWithProperties(
                context,
                device,
                IntPtr.Zero,
                out CLError errorStatus);
            return errorStatus;
        }

        /// <summary>
        /// Releases the given command queue.
        /// </summary>
        /// <param name="queue">The queue to release.</param>
        /// <returns>The error code.</returns>
        public CLError ReleaseCommandQueue(IntPtr queue) =>
            clReleaseCommandQueue(queue);

        /// <summary>
        /// Flushes the given command queue.
        /// </summary>
        /// <param name="queue">The queue to flush.</param>
        /// <returns>The error code.</returns>
        public CLError FlushCommandQueue(IntPtr queue) =>
            clFlush(queue);

        /// <summary>
        /// Finishes the given command queue.
        /// </summary>
        /// <param name="queue">The queue to finish.</param>
        /// <returns>The error code.</returns>
        public CLError FinishCommandQueue(IntPtr queue) =>
            clFinish(queue);

        #endregion

        #region Kernels

        /// <summary>
        /// Creates a new program.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="source">The program source.</param>
        /// <param name="program">The created program.</param>
        /// <returns>The error code.</returns>
        public CLError CreateProgram(
            IntPtr context,
            string source,
            out IntPtr program)
        {
            var length = new IntPtr(source.Length);
            program = clCreateProgramWithSource(
                context,
                1,
                ref source,
                ref length,
                out CLError errorStatus);
            return errorStatus;
        }

        /// <summary>
        /// Builds a program.
        /// </summary>
        /// <param name="program">The program to build.</param>
        /// <param name="device">The associated device.</param>
        /// <param name="options">
        /// The program build options (refer to the OpenCL specification).
        /// </param>
        /// <returns>The error code.</returns>
        public CLError BuildProgram(
            IntPtr program,
            IntPtr device,
            string options) =>
            clBuildProgram(
                program,
                1,
                &device,
                options,
                IntPtr.Zero,
                IntPtr.Zero);

        /// <summary>
        /// Builds a program.
        /// </summary>
        /// <param name="program">The program to build.</param>
        /// <param name="devices">The associated devices.</param>
        /// <param name="numDevices">The number of associated devices.</param>
        /// <param name="options">
        /// The program build options (refer to the OpenCL specification).
        /// </param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        public CLError BuildProgram(
            IntPtr program,
            IntPtr* devices,
            int numDevices,
            string options) =>
            clBuildProgram(
                program,
                numDevices,
                devices,
                options,
                IntPtr.Zero,
                IntPtr.Zero);

        /// <summary>
        /// Builds a program.
        /// </summary>
        /// <param name="program">The program to build.</param>
        /// <param name="devices">The associated devices.</param>
        /// <param name="options">
        /// The program build options (refer to the OpenCL specification).
        /// </param>
        /// <returns>The error code.</returns>
        public CLError BuildProgram(
            IntPtr program,
            IntPtr[] devices,
            string options)
        {
            fixed (IntPtr* ptr = &devices[0])
            {
                return BuildProgram(
                    program,
                    ptr,
                    devices.Length,
                    options);
            }
        }

        /// <summary>
        /// Resolves program information.
        /// </summary>
        /// <param name="program">The program pointer.</param>
        /// <param name="paramName">The param name to query.</param>
        /// <param name="paramValueSize">The size of the parameter value.</param>
        /// <param name="paramValue">The parameter value to use.</param>
        /// <param name="paramValueSizeRet">The resulting parameter value size.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        public CLError GetProgramInfo(
            IntPtr program,
            CLProgramInfo paramName,
            IntPtr paramValueSize,
            void* paramValue,
            out IntPtr paramValueSizeRet) =>
            clGetProgramInfo(
                program,
                paramName,
                paramValueSize,
                paramValue,
                out paramValueSizeRet);

        /// <summary>
        /// Resolves program build information.
        /// </summary>
        /// <param name="program">The program pointer.</param>
        /// <param name="device">The associated device.</param>
        /// <param name="paramName">The param name to query.</param>
        /// <param name="paramValueSize">The size of the parameter value.</param>
        /// <param name="paramValue">The parameter value to use.</param>
        /// <param name="paramValueSizeRet">The resulting parameter value size.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        public CLError GetProgramBuildInfo(
            IntPtr program,
            IntPtr device,
            CLProgramBuildInfo paramName,
            IntPtr paramValueSize,
            void* paramValue,
            out IntPtr paramValueSizeRet) =>
            clGetProgramBuildInfo(
                program,
                device,
                paramName,
                paramValueSize,
                paramValue,
                out paramValueSizeRet);

        /// <summary>
        /// Resolves program build-log information.
        /// </summary>
        /// <param name="program">The program pointer.</param>
        /// <param name="device">The associated device.</param>
        /// <param name="buildLog">The build log (if any).</param>
        /// <returns>The error code.</returns>
        public CLError GetProgramBuildLog(
            IntPtr program,
            IntPtr device,
            out string buildLog)
        {
            const int LogSize = 32_000;
            var log = new sbyte[LogSize];
            fixed (sbyte* logPtr = &log[0])
            {
                var error = GetProgramBuildInfo(
                    program,
                    device,
                    CLProgramBuildInfo.CL_PROGRAM_BUILD_LOG,
                    new IntPtr(LogSize),
                    logPtr,
                    out IntPtr logLength);
                buildLog = string.Empty;
                if (error == CLError.CL_SUCCESS)
                {
                    buildLog = new string(
                        logPtr,
                        0,
                        logLength.ToInt32(),
                        System.Text.Encoding.ASCII);
                }

                return error;
            }
        }

        /// <summary>
        /// Releases the given program.
        /// </summary>
        /// <param name="program">The program to release.</param>
        /// <returns>The error code.</returns>
        public CLError ReleaseProgram(IntPtr program) =>
            clReleaseProgram(program);

        /// <summary>
        /// Creates a new kernel.
        /// </summary>
        /// <param name="program">The source program to use.</param>
        /// <param name="kernelName">The kernel name in the scope of the program.</param>
        /// <param name="kernel">The created kernel.</param>
        /// <returns>The error code.</returns>
        public CLError CreateKernel(
            IntPtr program,
            string kernelName,
            out IntPtr kernel)
        {
            kernel = clCreateKernel(
                program,
                kernelName,
                out CLError errorStatus);
            return errorStatus;
        }

        /// <summary>
        /// Releases the given kernel.
        /// </summary>
        /// <param name="kernel">The kernel to release.</param>
        /// <returns>The error code.</returns>
        public CLError ReleaseKernel(IntPtr kernel) =>
            clReleaseKernel(kernel);

        /// <summary>
        /// Sets a kernel argument.
        /// </summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="kernel">The target kernel.</param>
        /// <param name="index">The argument index.</param>
        /// <param name="value">The managed value to set.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError SetKernelArgument<T>(
            IntPtr kernel,
            int index,
            T value)
            where T : unmanaged =>
            SetKernelArgumentUnsafe(
                kernel,
                index,
                Interop.SizeOf<T>(),
                Unsafe.AsPointer(ref value));

        /// <summary>
        /// Sets a kernel argument.
        /// </summary>
        /// <param name="kernel">The target kernel.</param>
        /// <param name="index">The argument index.</param>
        /// <param name="size">The argument size in bytes.</param>
        /// <param name="value">A pointer to the value to set.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError SetKernelArgumentUnsafe(
            IntPtr kernel,
            int index,
            int size,
            void* value) =>
            clSetKernelArg(
                kernel,
                index,
                new IntPtr(size),
                value);

        /// <summary>
        /// Sets a kernel argument.
        /// </summary>
        /// <param name="kernel">The target kernel.</param>
        /// <param name="index">The argument index.</param>
        /// <param name="size">The argument size in bytes.</param>
        /// <param name="value">A pointer to the value to set.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError SetKernelArgumentUnsafeWithKernel(
            CLKernel kernel,
            int index,
            int size,
            void* value) =>
            clSetKernelArg(
                kernel.KernelPtr,
                index,
                new IntPtr(size),
                value);

        /// <summary>
        /// Launches the given kernel function.
        /// </summary>
        /// <typeparam name="THandler">
        /// The handler type to customize the launch process.
        /// </typeparam>
        /// <param name="stream">The current stream.</param>
        /// <param name="kernel">The current kernel.</param>
        /// <param name="config">The current kernel configuration.</param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe CLError LaunchKernelWithStreamBinding<THandler>(
            CLStream stream,
            CLKernel kernel,
            RuntimeKernelConfig config)
            where THandler : struct, ILaunchHandler
        {
            var binding = stream.BindScoped();

            THandler handler = default;
            var result = handler.PreLaunchKernel(stream, kernel, config);

            var gridDim = config.GridDim;
            var blockDim = config.GroupDim;

            IntPtr* globalWorkSizes = stackalloc IntPtr[3];
            globalWorkSizes[0] = new IntPtr(gridDim.X * blockDim.X);
            globalWorkSizes[1] = new IntPtr(gridDim.Y * blockDim.Y);
            globalWorkSizes[2] = new IntPtr(gridDim.Z * blockDim.Z);

            IntPtr* localWorkSizes = stackalloc IntPtr[3];
            localWorkSizes[0] = new IntPtr(blockDim.X);
            localWorkSizes[1] = new IntPtr(blockDim.Y);
            localWorkSizes[2] = new IntPtr(blockDim.Z);

            result |= LaunchKernelUnsafe(
                stream.CommandQueue,
                kernel.KernelPtr,
                3,
                null,
                globalWorkSizes,
                localWorkSizes);

            binding.Recover();
            return result;
        }

        /// <summary>
        /// Launches a kernel.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="kernel">The kernel to launch.</param>
        /// <param name="workDimensions">The general work dimensions.</param>
        /// <param name="workOffsets">All work offsets.</param>
        /// <param name="globalWorkSizes">All global work sizes.</param>
        /// <param name="localWorkSizes">All local work sizes.</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError LaunchKernelUnsafe(
            IntPtr queue,
            IntPtr kernel,
            int workDimensions,
            IntPtr* workOffsets,
            IntPtr* globalWorkSizes,
            IntPtr* localWorkSizes) =>
            clEnqueueNDRangeKernel(
                queue,
                kernel,
                workDimensions,
                workOffsets,
                globalWorkSizes,
                localWorkSizes,
                0,
                null,
                null);

        /// <summary>
        /// Launches a kernel.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="kernel">The kernel to launch.</param>
        /// <param name="workDimensions">The general work dimensions.</param>
        /// <param name="workOffsets">All work offsets.</param>
        /// <param name="globalWorkSizes">All global work sizes.</param>
        /// <param name="localWorkSizes">All local work sizes.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError LaunchKernel(
            IntPtr queue,
            IntPtr kernel,
            int workDimensions,
            IntPtr[] workOffsets,
            IntPtr[] globalWorkSizes,
            IntPtr[] localWorkSizes)
        {
            fixed (IntPtr* workOffsetsPtr = &workOffsets[0])
            fixed (IntPtr* globalWorkSizesPtr = &globalWorkSizes[0])
            fixed (IntPtr* localWorkSizesPtr = &localWorkSizes[0])
            {
                return LaunchKernelUnsafe(
                    queue,
                    kernel,
                    workDimensions,
                    workOffsetsPtr,
                    globalWorkSizesPtr,
                    localWorkSizesPtr);
            }

        }

        /// <summary>
        /// Resolves kernel work-group information as typed structure value of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="kernel">The kernel.</param>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved value.</returns>
        public T GetKernelWorkGroupInfo<T>(
            IntPtr kernel,
            IntPtr device,
            CLKernelWorkGroupInfoType type)
            where T : unmanaged
        {
            T value = default;
            CLException.ThrowIfFailed(clGetKernelWorkGroupInfo(
                kernel,
                device,
                type,
                new IntPtr(Interop.SizeOf<T>()),
                Unsafe.AsPointer(ref value),
                IntPtr.Zero));
            return value;
        }

        /// <summary>
        /// Resolves kernel work-group information as typed array of values of type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="kernel">The kernel.</param>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <param name="elements">The desired elements.</param>
        public void GetKernelWorkGroupInfo<T>(
            IntPtr kernel,
            IntPtr device,
            CLKernelWorkGroupInfoType type,
            T[] elements)
            where T : unmanaged
        {
            fixed (T* ptr = &elements[0])
            {
                CLException.ThrowIfFailed(clGetKernelWorkGroupInfo(
                    kernel,
                    device,
                    type,
                    new IntPtr(Interop.SizeOf<T>() * elements.Length),
                    ptr,
                    IntPtr.Zero));
            }
        }

        #endregion

        #region Buffers

        /// <summary>
        /// Creates a new buffer.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="flags">The buffer flags.</param>
        /// <param name="size">The buffer size in bytes.</param>
        /// <param name="hostPointer">The host pointer to copy from (if any).</param>
        /// <param name="buffer">The created buffer.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError CreateBuffer(
            IntPtr context,
            CLBufferFlags flags,
            IntPtr size,
            IntPtr hostPointer,
            out IntPtr buffer)
        {
            buffer = clCreateBuffer(
                context,
                flags,
                size,
                hostPointer,
                out CLError errorStatus);
            return errorStatus;
        }

        /// <summary>
        /// Releases the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The error code.</returns>
        public CLError ReleaseBuffer(IntPtr buffer) =>
            clReleaseMemObject(buffer);

        /// <summary>
        /// Reads from a buffer into host memory.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="buffer">The source buffer to read from.</param>
        /// <param name="blockingRead">
        /// True, if the operation blocks until completion.
        /// </param>
        /// <param name="offset">The source offset in bytes.</param>
        /// <param name="size">The data size in bytes.</param>
        /// <param name="ptr">The target pointer in host memory.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError ReadBuffer(
            IntPtr queue,
            IntPtr buffer,
            bool blockingRead,
            IntPtr offset,
            IntPtr size,
            IntPtr ptr) =>
            clEnqueueReadBuffer(
                queue,
                buffer,
                blockingRead,
                offset,
                size,
                ptr,
                0,
                null,
                null);

        /// <summary>
        /// Writes to a buffer from host memory.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="buffer">The target buffer to write to.</param>
        /// <param name="blockingWrite">
        /// True, if the operation blocks until completion.
        /// </param>
        /// <param name="offset">The target offset in bytes.</param>
        /// <param name="size">The data size in bytes.</param>
        /// <param name="ptr">The source pointer in host memory.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError WriteBuffer(
            IntPtr queue,
            IntPtr buffer,
            bool blockingWrite,
            IntPtr offset,
            IntPtr size,
            IntPtr ptr) =>
            clEnqueueWriteBuffer(
                queue,
                buffer,
                blockingWrite,
                offset,
                size,
                ptr,
                0,
                null,
                null);

        /// <summary>
        /// Fills the given buffer with the specified pattern.
        /// </summary>
        /// <typeparam name="T">The data type used for filling.</typeparam>
        /// <param name="queue">The queue.</param>
        /// <param name="buffer">The target buffer to fill.</param>
        /// <param name="pattern">The pattern value used for filling.</param>
        /// <param name="offset">The target offset in bytes.</param>
        /// <param name="size">The size in bytes to fill.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError FillBuffer<T>(
            IntPtr queue,
            IntPtr buffer,
            T pattern,
            IntPtr offset,
            IntPtr size)
            where T : unmanaged =>
            clEnqueueFillBuffer(
                queue,
                buffer,
                Unsafe.AsPointer(ref pattern),
                new IntPtr(Interop.SizeOf<T>()),
                offset,
                size,
                0,
                null,
                null);

        /// <summary>
        /// Copies the contents of the source buffer into the target buffer.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="sourceBuffer">The source buffer.</param>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="sourceOffset">
        /// The source offset inside the source buffer.
        /// </param>
        /// <param name="targetOffset">
        /// The target offset inside the target buffer.
        /// </param>
        /// <param name="size">The size to copy in bytes.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CLError CopyBuffer(
            IntPtr queue,
            IntPtr sourceBuffer,
            IntPtr targetBuffer,
            IntPtr sourceOffset,
            IntPtr targetOffset,
            IntPtr size) =>
            clEnqueueCopyBuffer(
                queue,
                sourceBuffer,
                targetBuffer,
                sourceOffset,
                targetOffset,
                size,
                0,
                null,
                null);

        #endregion
    }
}

#pragma warning restore IDE1006 // Naming
