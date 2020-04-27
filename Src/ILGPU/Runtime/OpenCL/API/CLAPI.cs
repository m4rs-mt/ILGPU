// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.OpenCL.API
{
    /// <summary>
    /// Wraps the OpenCL-driver API.
    /// </summary>
    public static unsafe class CLAPI
    {
        #region Device Methods

        /// <summary>
        /// Resolves the number of available platforms.
        /// </summary>
        /// <returns>The error code.</returns>
        public static CLError GetNumPlatforms(out int numPlatforms)
        {
            if (Backends.Backend.RunningOnNativePlatform)
            {
                return NativeMethods.GetPlatformIDs(
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
        public static CLError GetPlatforms(IntPtr[] platforms, out int numPlatforms)
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
        public static CLError GetPlatforms(IntPtr* platforms, ref int numPlatforms) =>
            NativeMethods.GetPlatformIDs(numPlatforms, platforms, out numPlatforms);

        /// <summary>
        /// Resolves platform information as string value.
        /// </summary>
        /// <param name="platform">The platform.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved string value.</returns>
        public static string GetPlatformInfo(IntPtr platform, CLPlatformInfoType type)
        {
            const int MaxStringLength = 1024;
            var stringValue = stackalloc sbyte[MaxStringLength];
            var size = IntPtr.Zero;

            CLException.ThrowIfFailed(NativeMethods.GetPlatformInfo(
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
        public static T GetPlatformInfo<T>(IntPtr platform, CLPlatformInfoType type)
            where T : unmanaged
        {
            T value = default;
            CLException.ThrowIfFailed(NativeMethods.GetPlatformInfo(
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
        public static CLError GetDevices(
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
        public static CLError GetDevices(
            IntPtr platform,
            CLDeviceType deviceType,
            IntPtr* devices,
            ref int numDevices) =>
            NativeMethods.GetDeviceIDs(
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
        public static CLError ReleaseDevice(IntPtr device) =>
            NativeMethods.ReleaseDevice(device);

        /// <summary>
        /// Resolves device information as string value.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <returns>The resolved string value.</returns>
        public static string GetDeviceInfo(IntPtr device, CLDeviceInfoType type)
        {
            const int MaxStringLength = 8192;
            var stringValue = stackalloc sbyte[MaxStringLength];
            var size = IntPtr.Zero;

            CLException.ThrowIfFailed(NativeMethods.GetDeviceInfo(
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
        public static T GetDeviceInfo<T>(IntPtr device, CLDeviceInfoType type)
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
        public static CLError GetDeviceInfo<T>(
            IntPtr device,
            CLDeviceInfoType type,
            out T value)
            where T : unmanaged
        {
            value = default;
            return NativeMethods.GetDeviceInfo(
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
        public static void GetDeviceInfo<T>(
            IntPtr device,
            CLDeviceInfoType type,
            T[] elements)
            where T : unmanaged
        {
            fixed (T* ptr = &elements[0])
            {

                CLException.ThrowIfFailed(NativeMethods.GetDeviceInfo(
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
        public static T GetExtension<T>(IntPtr platform)
            where T : Delegate =>
            GetExtension<T>(platform, typeof(T).Name);

        /// <summary>
        /// Resolves an extension delegate for the given platform.
        /// </summary>
        /// <typeparam name="T">The delegate type.</typeparam>
        /// <param name="platform">The platform pointer.</param>
        /// <param name="name">The extension name.</param>
        /// <returns>The resolved extension.</returns>
        public static T GetExtension<T>(IntPtr platform, string name)
            where T : Delegate
        {
            var address = NativeMethods.GetExtensionFunctionAddressForPlatform(
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
        public static CLError CreateContext(IntPtr device, out IntPtr context)
        {
            context = NativeMethods.CreateContext(
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
        public static CLError ReleaseContext(IntPtr context) =>
            NativeMethods.ReleaseContext(context);

        /// <summary>
        /// Creates a new command queue.
        /// </summary>
        /// <param name="device">The associated device.</param>
        /// <param name="context">The parent context.</param>
        /// <param name="queue">The created queue.</param>
        /// <returns>The error code.</returns>
        public static CLError CreateCommandQueue(
            IntPtr device,
            IntPtr context,
            out IntPtr queue)
        {
            queue = NativeMethods.CreateCommandQueueWithProperties(
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
        public static CLError ReleaseCommandQueue(IntPtr queue) =>
            NativeMethods.ReleaseCommandQueue(queue);

        /// <summary>
        /// Flushes the given command queue.
        /// </summary>
        /// <param name="queue">The queue to flush.</param>
        /// <returns>The error code.</returns>
        public static CLError FlushCommandQueue(IntPtr queue) =>
            NativeMethods.Flush(queue);

        /// <summary>
        /// Finishes the given command queue.
        /// </summary>
        /// <param name="queue">The queue to finish.</param>
        /// <returns>The error code.</returns>
        public static CLError FinishCommandQueue(IntPtr queue) =>
            NativeMethods.Finish(queue);

        #endregion

        #region Kernels

        /// <summary>
        /// Creates a new program.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="source">The program source.</param>
        /// <param name="program">The created program.</param>
        /// <returns>The error code.</returns>
        public static CLError CreateProgram(
            IntPtr context,
            string source,
            out IntPtr program)
        {
            var length = new IntPtr(source.Length);
            program = NativeMethods.CreateProgramWithSource(
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
        public static CLError BuildProgram(
            IntPtr program,
            IntPtr device,
            string options) =>
            NativeMethods.BuildProgram(
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
        public static CLError BuildProgram(
            IntPtr program,
            IntPtr* devices,
            int numDevices,
            string options) =>
            NativeMethods.BuildProgram(
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
        public static CLError BuildProgram(
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
        public static CLError GetProgramInfo(
            IntPtr program,
            CLProgramInfo paramName,
            IntPtr paramValueSize,
            void* paramValue,
            out IntPtr paramValueSizeRet) =>
            NativeMethods.GetProgramInfo(
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
        public static CLError GetProgramBuildInfo(
            IntPtr program,
            IntPtr device,
            CLProgramBuildInfo paramName,
            IntPtr paramValueSize,
            void* paramValue,
            out IntPtr paramValueSizeRet) =>
            NativeMethods.GetProgramBuildInfo(
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
        public static CLError GetProgramBuildLog(
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
        public static CLError ReleaseProgram(IntPtr program) =>
            NativeMethods.ReleaseProgram(program);

        /// <summary>
        /// Creates a new kernel.
        /// </summary>
        /// <param name="program">The source program to use.</param>
        /// <param name="kernelName">The kernel name in the scope of the program.</param>
        /// <param name="kernel">The created kernel.</param>
        /// <returns>The error code.</returns>
        public static CLError CreateKernel(
            IntPtr program,
            string kernelName,
            out IntPtr kernel)
        {
            kernel = NativeMethods.CreateKernel(
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
        public static CLError ReleaseKernel(IntPtr kernel) =>
            NativeMethods.ReleaseKernel(kernel);

        /// <summary>
        /// Sets a kernel argument.
        /// </summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="kernel">The target kernel.</param>
        /// <param name="index">The argument index.</param>
        /// <param name="value">The managed value to set.</param>
        /// <returns>The error code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CLError SetKernelArgument<T>(
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
        public static CLError SetKernelArgumentUnsafe(
            IntPtr kernel,
            int index,
            int size,
            void* value) =>
            NativeMethods.SetKernelArg(
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
        public static CLError SetKernelArgumentUnsafeWithKernel(
            CLKernel kernel,
            int index,
            int size,
            void* value) =>
            NativeMethods.SetKernelArg(
                kernel.KernelPtr,
                index,
                new IntPtr(size),
                value);

        /// <summary>
        /// Launches the given kernel function.
        /// </summary>
        /// <param name="stream">The current stream.</param>
        /// <param name="kernel">The current kernel.</param>
        /// <param name="config">The current kernel configuration.</param>
        /// <returns>The error status.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe CLError LaunchKernelWithStreamBinding(
            CLStream stream,
            CLKernel kernel,
            RuntimeKernelConfig config)
        {
            var binding = stream.BindScoped();

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

            var result = LaunchKernelUnsafe(
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
        public static CLError LaunchKernelUnsafe(
            IntPtr queue,
            IntPtr kernel,
            int workDimensions,
            IntPtr* workOffsets,
            IntPtr* globalWorkSizes,
            IntPtr* localWorkSizes) =>
            NativeMethods.EnqueueNDRangeKernel(
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
        public static CLError LaunchKernel(
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
        public static T GetKernelWorkGroupInfo<T>(
            IntPtr kernel,
            IntPtr device,
            CLKernelWorkGroupInfoType type)
            where T : unmanaged
        {
            T value = default;
            CLException.ThrowIfFailed(NativeMethods.GetKernelWorkGroupInfo(
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
        public static void GetKernelWorkGroupInfo<T>(
            IntPtr kernel,
            IntPtr device,
            CLKernelWorkGroupInfoType type,
            T[] elements)
            where T : unmanaged
        {
            fixed (T* ptr = &elements[0])
            {
                CLException.ThrowIfFailed(NativeMethods.GetKernelWorkGroupInfo(
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
        public static CLError CreateBuffer(
            IntPtr context,
            CLBufferFlags flags,
            IntPtr size,
            IntPtr hostPointer,
            out IntPtr buffer)
        {
            buffer = NativeMethods.CreateBuffer(
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
        public static CLError ReleaseBuffer(IntPtr buffer) =>
            NativeMethods.ReleaseMemObject(buffer);

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
        public static CLError ReadBuffer(
            IntPtr queue,
            IntPtr buffer,
            bool blockingRead,
            IntPtr offset,
            IntPtr size,
            IntPtr ptr) =>
            NativeMethods.EnqueueReadBuffer(
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
        public static CLError WriteBuffer(
            IntPtr queue,
            IntPtr buffer,
            bool blockingWrite,
            IntPtr offset,
            IntPtr size,
            IntPtr ptr) =>
            NativeMethods.EnqueueWriteBuffer(
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
        public static CLError FillBuffer<T>(
            IntPtr queue,
            IntPtr buffer,
            T pattern,
            IntPtr offset,
            IntPtr size)
            where T : unmanaged =>
            NativeMethods.EnqueueFillBuffer(
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
        public static CLError CopyBuffer(
            IntPtr queue,
            IntPtr sourceBuffer,
            IntPtr targetBuffer,
            IntPtr sourceOffset,
            IntPtr targetOffset,
            IntPtr size) =>
            NativeMethods.EnqueueCopyBuffer(
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

