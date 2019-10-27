// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLAPI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#pragma warning disable IDE1006 // Naming Styles

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
        #region Constants

        /// <summary>
        /// Represents the driver library name.
        /// </summary>
        public const string LibName = "opencl";

        #endregion

        #region Imports

        // Devices

        [DllImport(LibName)]
        private static extern CLError clGetPlatformIDs(
            [In] int maxNumPlatforms,
            [Out] IntPtr* platforms,
            [Out] out int numPlatforms);

        [DllImport(LibName)]
        private static extern CLError clGetDeviceIDs(
            [In] IntPtr platform,
            [In] CLDeviceType deviceType,
            [In] int maxNumDevices,
            [Out] IntPtr* devices,
            [Out] out int numDevices);

        [DllImport(LibName)]
        private static extern CLError clReleaseDevice(
            [In] IntPtr deviceId);

        [DllImport(LibName)]
        private static extern CLError clGetDeviceInfo(
            [In] IntPtr deviceId,
            [In] CLDeviceInfoType deviceInfoType,
            [In] IntPtr maxSize,
            [Out] void* value,
            [Out] IntPtr size);

        [DllImport(LibName, BestFitMapping = false)]
        private static extern IntPtr clGetExtensionFunctionAddressForPlatform(
            [In] IntPtr platformId,
            [In, MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName)]
        private static extern IntPtr clCreateContext(
            [In] IntPtr* properties,
            [In] int numDevices,
            [In] IntPtr* devices,
            [In] IntPtr callback,
            [In] IntPtr userData,
            [Out] out CLError errorCode);

        [DllImport(LibName)]
        private static extern CLError clReleaseContext(
            [In] IntPtr context);

        [DllImport(LibName)]
        private static extern IntPtr clCreateCommandQueueWithProperties(
            [In] IntPtr context,
            [In] IntPtr deviceId,
            [In] IntPtr properties,
            [Out] out CLError errorCode);

        [DllImport(LibName)]
        private static extern CLError clReleaseCommandQueue(
            [In] IntPtr queue);

        [DllImport(LibName)]
        private static extern CLError clFlush(
            [In] IntPtr queue);

        [DllImport(LibName)]
        private static extern CLError clFinish(
            [In] IntPtr queue);

        // Kernels

        [DllImport(LibName, BestFitMapping = false)]
        private static extern IntPtr clCreateProgramWithSource(
            [In] IntPtr context,
            [In] int numPrograms,
            [In, MarshalAs(UnmanagedType.LPStr)] ref string source,
            [In] ref IntPtr lengths,
            [Out] out CLError errorCode);

        [DllImport(LibName, BestFitMapping = false)]
        private static extern CLError clBuildProgram(
            [In] IntPtr program,
            [In] int numDevices,
            [In] IntPtr* devices,
            [In, MarshalAs(UnmanagedType.LPStr)] string options,
            [In] IntPtr callback,
            [In] IntPtr userData);

        [DllImport(LibName)]
        private static extern CLError clReleaseProgram(
            [In] IntPtr program);

        [DllImport(LibName, BestFitMapping = false)]
        private static extern IntPtr clCreateKernel(
            [In] IntPtr program,
            [In, MarshalAs(UnmanagedType.LPStr)] string kernelName,
            [Out] out CLError errorCode);

        [DllImport(LibName)]
        private static extern CLError clReleaseKernel(
            [In] IntPtr kernel);

        [DllImport(LibName)]
        private static extern CLError clSetKernelArg(
            [In] IntPtr kernel,
            [In] int index,
            [In] IntPtr size,
            [In] void* value);

        [DllImport(LibName)]
        private static extern CLError clEnqueueNDRangeKernel(
            IntPtr queue,
            IntPtr kernel,
            int workDimensions,
            IntPtr* workOffsets,
            IntPtr* globalWorkSizes,
            IntPtr* localWorkSizes,
            int numEvents,
            IntPtr* events);

        // Buffers

        [DllImport(LibName)]
        private static extern IntPtr clCreateBuffer(
            IntPtr context,
            CLBufferFlags flags,
            IntPtr size,
            IntPtr hostPointer,
            out CLError errorCode);

        [DllImport(LibName)]
        private static extern CLError clReleaseMemObject(
            [In] IntPtr buffer);

        [DllImport(LibName)]
        private static extern CLError clEnqueueReadBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In, MarshalAs(UnmanagedType.Bool)] bool blockingRead,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] IntPtr ptr,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DllImport(LibName)]
        private static extern CLError clEnqueueWriteBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In, MarshalAs(UnmanagedType.Bool)] bool blockingWrite,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] IntPtr ptr,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DllImport(LibName)]
        private static extern CLError clEnqueueFillBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In] void* pattern,
            [In] IntPtr patternSize,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DllImport(LibName)]
        private static extern CLError clEnqueueCopyBuffer(
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

        #region Device Methods

        /// <summary>
        /// Resolves the number of available platforms.
        /// </summary>
        /// <returns>The error code.</returns>
        public static CLError GetNumPlatforms(out int numPlatforms) =>
            clGetPlatformIDs(short.MaxValue, null, out numPlatforms);

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
            clGetPlatformIDs(numPlatforms, platforms, out numPlatforms);

        /// <summary>
        /// Resolves the number of available platforms.
        /// </summary>
        /// <returns>The error code.</returns>
        public static CLError GetNumDevices(IntPtr platform, CLDeviceType deviceType, out int numDevices) =>
            clGetDeviceIDs(
                platform,
                deviceType,
                short.MaxValue,
                null,
                out numDevices);

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
        public static CLError ReleaseDevice(IntPtr device) =>
            clReleaseDevice(device);

        /// <summary>
        /// Resolves device information as typed structure value of type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="device">The device.</param>
        /// <param name="type">The information type.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns>The error code.</returns>
        public static CLError GetDeviceInfo<T>(IntPtr device, CLDeviceInfoType type, out T value)
            where T : struct
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
            var address = clGetExtensionFunctionAddressForPlatform(platform, name);
            if (address == IntPtr.Zero)
                return null;
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        /// <summary>
        /// Creates a new context.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="context">The created context.</param>
        /// <returns>The error code.</returns>
        public static CLError CreateContext(IntPtr device, out IntPtr context)
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
        public static CLError ReleaseContext(IntPtr context) =>
            clReleaseContext(context);

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
        public static CLError ReleaseCommandQueue(IntPtr queue) =>
            clReleaseCommandQueue(queue);

        /// <summary>
        /// Flushes the given command queue.
        /// </summary>
        /// <param name="queue">The queue to flush.</param>
        /// <returns>The error code.</returns>
        public static CLError FlushCommandQueue(IntPtr queue) =>
            clFlush(queue);

        /// <summary>
        /// Finishes the given command queue.
        /// </summary>
        /// <param name="queue">The queue to finish.</param>
        /// <returns>The error code.</returns>
        public static CLError FinishCommandQueue(IntPtr queue) =>
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
        public static CLError CreateProgram(
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
        /// <param name="options">The program build options (refer to the OpenCL specification).</param>
        /// <returns>The error code.</returns>
        public static CLError BuildProgram(
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
        /// <param name="options">The program build options (refer to the OpenCL specification).</param>
        /// <returns>The error code.</returns>
        [CLSCompliant(false)]
        public static CLError BuildProgram(
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
        /// <param name="options">The program build options (refer to the OpenCL specification).</param>
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
        /// Releases the given program.
        /// </summary>
        /// <param name="program">The program to release.</param>
        /// <returns>The error code.</returns>
        public static CLError ReleaseProgram(IntPtr program) =>
            clReleaseProgram(program);

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
        public static CLError ReleaseKernel(IntPtr kernel) =>
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
        public static CLError SetKernelArgument<T>(
            IntPtr kernel,
            int index,
            T value)
            where T : struct =>
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
            clSetKernelArg(
                kernel,
                index,
                new IntPtr(size),
                value);

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
            clEnqueueNDRangeKernel(
                queue,
                kernel,
                workDimensions,
                workOffsets,
                globalWorkSizes,
                localWorkSizes,
                0,
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
        public static CLError ReleaseBuffer(IntPtr buffer) =>
            clReleaseMemObject(buffer);

        /// <summary>
        /// Reads from a buffer into host memory.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="buffer">The source buffer to read from.</param>
        /// <param name="blockingRead">True, if the operation blocks until completion.</param>
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
        /// <param name="blockingWrite">True, if the operation blocks until completion.</param>
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
        /// <param name="pattern">The pattern value used for filling.j</param>
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
            where T : struct =>
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
        /// <param name="sourceOffset">The source offset inside the source buffer.</param>
        /// <param name="targetOffset">The target offset inside the target buffer.</param>
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

#pragma warning restore IDE1006 // Naming Styles
