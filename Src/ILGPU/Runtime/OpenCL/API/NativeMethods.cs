// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: NativeMethods.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.OpenCL.API
{
    /// <summary>
    /// Native methods for the <see cref="CLAPI"/> class.
    /// </summary>
    static unsafe class NativeMethods
    {
        #region Constants

        /// <summary>
        /// Represents the driver library name.
        /// </summary>
        public const string LibName = "opencl";

        #endregion

        #region Devices

        [DllImport(LibName, EntryPoint = "clGetPlatformIDs")]
        public static extern CLError GetPlatformIDs(
            [In] int maxNumPlatforms,
            [Out] IntPtr* platforms,
            [Out] out int numPlatforms);

        [DllImport(LibName, EntryPoint = "clGetPlatformInfo")]
        public static extern CLError GetPlatformInfo(
            [In] IntPtr platform,
            [In] CLPlatformInfoType platformInfoType,
            [In] IntPtr maxSize,
            [Out] void* value,
            [Out] IntPtr size);

        [DllImport(LibName, EntryPoint = "clGetDeviceIDs")]
        public static extern CLError GetDeviceIDs(
            [In] IntPtr platform,
            [In] CLDeviceType deviceType,
            [In] int maxNumDevices,
            [Out] IntPtr* devices,
            [Out] out int numDevices);

        [DllImport(LibName, EntryPoint = "clReleaseDevice")]
        public static extern CLError ReleaseDevice(
            [In] IntPtr deviceId);

        [DllImport(LibName, EntryPoint = "clGetDeviceInfo")]
        public static extern CLError GetDeviceInfo(
            [In] IntPtr deviceId,
            [In] CLDeviceInfoType deviceInfoType,
            [In] IntPtr maxSize,
            [Out] void* value,
            [Out] IntPtr size);

        [DllImport(LibName, EntryPoint = "clGetExtensionFunctionAddressForPlatform", BestFitMapping = false)]
        public static extern IntPtr GetExtensionFunctionAddressForPlatform(
            [In] IntPtr platformId,
            [In, MarshalAs(UnmanagedType.LPStr)] string name);

        #endregion

        #region Context

        [DllImport(LibName, EntryPoint = "clCreateContext")]
        public static extern IntPtr CreateContext(
            [In] IntPtr* properties,
            [In] int numDevices,
            [In] IntPtr* devices,
            [In] IntPtr callback,
            [In] IntPtr userData,
            [Out] out CLError errorCode);

        [DllImport(LibName, EntryPoint = "clReleaseContext")]
        public static extern CLError ReleaseContext(
            [In] IntPtr context);

        #endregion

        #region Command Queues

        [DllImport(LibName, EntryPoint = "clCreateCommandQueue")]
        public static extern IntPtr CreateCommandQueue(
            [In] IntPtr context,
            [In] IntPtr device,
            [In] IntPtr properties,
            [Out] out CLError errorCode);

        [DllImport(LibName, EntryPoint = "clCreateCommandQueueWithProperties")]
        public static extern IntPtr CreateCommandQueueWithProperties(
            [In] IntPtr context,
            [In] IntPtr deviceId,
            [In] IntPtr properties,
            [Out] out CLError errorCode);

        [DllImport(LibName, EntryPoint = "clReleaseCommandQueue")]
        public static extern CLError ReleaseCommandQueue(
            [In] IntPtr queue);

        [DllImport(LibName, EntryPoint = "clFlush")]
        public static extern CLError Flush(
            [In] IntPtr queue);

        [DllImport(LibName, EntryPoint = "clFinish")]
        public static extern CLError Finish(
            [In] IntPtr queue);

        #endregion

        #region Kernels

        [DllImport(LibName, EntryPoint = "clCreateProgramWithSource", BestFitMapping = false)]
        public static extern IntPtr CreateProgramWithSource(
            [In] IntPtr context,
            [In] int numPrograms,
            [In, MarshalAs(UnmanagedType.LPStr)] ref string source,
            [In] ref IntPtr lengths,
            [Out] out CLError errorCode);

        [DllImport(LibName, EntryPoint = "clBuildProgram", BestFitMapping = false)]
        public static extern CLError BuildProgram(
            [In] IntPtr program,
            [In] int numDevices,
            [In] IntPtr* devices,
            [In, MarshalAs(UnmanagedType.LPStr)] string options,
            [In] IntPtr callback,
            [In] IntPtr userData);

        [DllImport(LibName, EntryPoint = "clReleaseProgram")]
        public static extern CLError ReleaseProgram(
            [In] IntPtr program);

        [DllImport(LibName, EntryPoint = "clGetProgramBuildInfo")]
        public static extern CLError GetProgramBuildInfo(
            [In] IntPtr program,
            [In] IntPtr device,
            [In] CLProgramBuildInfo paramName,
            [In] IntPtr paramValueSize,
            [Out] void* paramValue,
            [Out] out IntPtr paramValueSizeRet);

        [DllImport(LibName, EntryPoint = "clCreateKernel", BestFitMapping = false)]
        public static extern IntPtr CreateKernel(
            [In] IntPtr program,
            [In, MarshalAs(UnmanagedType.LPStr)] string kernelName,
            [Out] out CLError errorCode);

        [DllImport(LibName, EntryPoint = "clReleaseKernel")]
        public static extern CLError ReleaseKernel(
            [In] IntPtr kernel);

        [DllImport(LibName, EntryPoint = "clSetKernelArg")]
        public static extern CLError SetKernelArg(
            [In] IntPtr kernel,
            [In] int index,
            [In] IntPtr size,
            [In] void* value);

        [DllImport(LibName, EntryPoint = "clEnqueueNDRangeKernel")]
        public static extern CLError EnqueueNDRangeKernel(
            [In] IntPtr queue,
            [In] IntPtr kernel,
            [In] int workDimensions,
            [In] IntPtr* workOffsets,
            [In] IntPtr* globalWorkSizes,
            [In] IntPtr* localWorkSizes,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* creatingEvent);

        [DllImport(LibName, EntryPoint = "clGetKernelWorkGroupInfo")]
        public static extern CLError GetKernelWorkGroupInfo(
            [In] IntPtr kernel,
            [In] IntPtr device,
            [In] CLKernelWorkGroupInfoType workGroupInfoType,
            [In] IntPtr maxSize,
            [Out] void* paramValue,
            [Out] IntPtr size);

        #endregion

        #region Buffers

        [DllImport(LibName, EntryPoint = "clCreateBuffer")]
        public static extern IntPtr CreateBuffer(
            IntPtr context,
            CLBufferFlags flags,
            IntPtr size,
            IntPtr hostPointer,
            out CLError errorCode);

        [DllImport(LibName, EntryPoint = "clReleaseMemObject")]
        public static extern CLError ReleaseMemObject(
            [In] IntPtr buffer);

        [DllImport(LibName, EntryPoint = "clEnqueueReadBuffer")]
        public static extern CLError EnqueueReadBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In, MarshalAs(UnmanagedType.Bool)] bool blockingRead,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] IntPtr ptr,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DllImport(LibName, EntryPoint = "clEnqueueWriteBuffer")]
        public static extern CLError EnqueueWriteBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In, MarshalAs(UnmanagedType.Bool)] bool blockingWrite,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] IntPtr ptr,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DllImport(LibName, EntryPoint = "clEnqueueFillBuffer")]
        public static extern CLError EnqueueFillBuffer(
            [In] IntPtr queue,
            [In] IntPtr buffer,
            [In] void* pattern,
            [In] IntPtr patternSize,
            [In] IntPtr offset,
            [In] IntPtr size,
            [In] int numEvents,
            [In] IntPtr* events,
            [In, Out] IntPtr* resultEvent);

        [DllImport(LibName, EntryPoint = "clEnqueueCopyBuffer")]
        public static extern CLError EnqueueCopyBuffer(
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
    }
}
