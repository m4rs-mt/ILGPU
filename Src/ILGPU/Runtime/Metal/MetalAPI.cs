// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents a Metal API error code.
/// </summary>
public enum MetalError
{
    /// <summary>
    /// No error.
    /// </summary>
    Success = 0,

    /// <summary>
    /// An unspecified error occurred.
    /// </summary>
    Error = 1,

    /// <summary>
    /// No Metal-capable device was found.
    /// </summary>
    DeviceNotFound = 2,

    /// <summary>
    /// Failed to create a command queue.
    /// </summary>
    CommandQueueCreationFailed = 3,

    /// <summary>
    /// Failed to compile a Metal library from source.
    /// </summary>
    LibraryCompilationFailed = 4,

    /// <summary>
    /// The requested function was not found in the library.
    /// </summary>
    FunctionNotFound = 5,

    /// <summary>
    /// Failed to create a compute pipeline state.
    /// </summary>
    PipelineCreationFailed = 6,

    /// <summary>
    /// Failed to allocate a GPU buffer.
    /// </summary>
    BufferAllocationFailed = 7,
}

/// <summary>
/// Metal resource storage mode options.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1028:Enum Storage should be Int32",
    Justification = "Metal API uses NSUInteger (ulong) for resource options.")]
public enum MetalResourceOptions : ulong
{
    /// <summary>
    /// Shared storage mode (CPU and GPU share memory on Apple Silicon).
    /// </summary>
    StorageModeShared = 0,

    /// <summary>
    /// Managed storage mode (requires explicit synchronization).
    /// </summary>
    StorageModeManaged = 1 << 4,

    /// <summary>
    /// Private storage mode (GPU-only access).
    /// </summary>
    StorageModePrivate = 2 << 4,
}

/// <summary>
/// Represents an MTLSize structure (3 x NSUInteger).
/// </summary>
/// <remarks>
/// Creates a new MTLSize with the given dimensions.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct MTLSize(ulong width, ulong height, ulong depth)
{
    /// <summary>The width dimension.</summary>
    public ulong Width = width;

    /// <summary>The height dimension.</summary>
    public ulong Height = height;

    /// <summary>The depth dimension.</summary>
    public ulong Depth = depth;
}

/// <summary>
/// Wraps Metal compute API calls via Objective-C runtime interop.
/// </summary>
/// <remarks>
/// Metal uses Objective-C messaging via objc_msgSend. This class provides
/// managed wrappers for the compute-relevant subset of the Metal API.
/// </remarks>
public static unsafe class MetalAPI
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string MetalLib =
        "/System/Library/Frameworks/Metal.framework/Metal";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";

    /// <summary>
    /// Creates a dispatch_data_t from raw bytes. Used to wrap metallib
    /// binary data for <c>newLibraryWithData:error:</c>.
    /// </summary>
    [DllImport(LibSystem, EntryPoint = "dispatch_data_create")]
    private static extern IntPtr DispatchDataCreate(
        void* buffer, nuint size, IntPtr queue, IntPtr destructor);

    /// <summary>
    /// Releases a dispatch_data_t object.
    /// </summary>
    [DllImport(LibSystem, EntryPoint = "dispatch_release")]
    private static extern void DispatchRelease(IntPtr obj);

    #region Objective-C Runtime

    [DllImport(ObjCLib, EntryPoint = "objc_getClass", CharSet = CharSet.Ansi)]
    [SuppressMessage("Globalization", "CA2101", Justification = "ObjC class names are ASCII.")]
    private static extern IntPtr ObjcGetClass(string name);

    [DllImport(ObjCLib, EntryPoint = "sel_registerName", CharSet = CharSet.Ansi)]
    [SuppressMessage("Globalization", "CA2101", Justification = "ObjC selector names are ASCII.")]
    private static extern IntPtr SelRegisterName(string name);

    // IntPtr return, no args
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(IntPtr receiver, IntPtr selector);

    // IntPtr return, 1 IntPtr arg
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(
        IntPtr receiver, IntPtr selector, IntPtr arg1);

    // IntPtr return, IntPtr + ulong args
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(
        IntPtr receiver, IntPtr selector, IntPtr arg1, ulong arg2);

    // IntPtr return, void* + ulong + ulong args (setBytes:length:atIndex:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(
        IntPtr receiver, IntPtr selector, void* arg1, ulong arg2, ulong arg3);

    // IntPtr return, IntPtr + IntPtr + IntPtr* args
    // (newLibraryWithSource:options:error:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(
        IntPtr receiver, IntPtr selector,
        IntPtr arg1, IntPtr arg2, IntPtr* arg3);

    // IntPtr return, IntPtr + IntPtr* args
    // (newComputePipelineStateWithFunction:error:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(
        IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr* arg2);

    // IntPtr return, ulong + ulong args (newBufferWithLength:options:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSendBuffer(
        IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2);

    // ulong return, no args (for ulong property getters)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern ulong ObjcMsgSendUlong(
        IntPtr receiver, IntPtr selector);

    // MTLSize return, no args (for maxThreadsPerThreadgroup on MTLDevice)
    // MTLSize is 24 bytes; on arm64 the runtime passes a hidden pointer in x8
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern MTLSize ObjcMsgSendMTLSize(
        IntPtr receiver, IntPtr selector);

    // bool return, ulong arg (supportsFamily:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern byte ObjcMsgSendBool(
        IntPtr receiver, IntPtr selector, ulong arg1);

    // IntPtr return, IntPtr arg (initWithUTF8String:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSendInitString(
        IntPtr receiver, IntPtr selector, byte* arg1);

    // void return, no args
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSendVoid(
        IntPtr receiver, IntPtr selector);

    // void return, 1 IntPtr arg
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSendVoid(
        IntPtr receiver, IntPtr selector, IntPtr arg1);

    // void return, ulong + ulong args
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSendVoid(
        IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2);

    // void return, IntPtr + ulong + ulong args
    // (setBuffer:offset:atIndex:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSendVoid(
        IntPtr receiver, IntPtr selector,
        IntPtr arg1, ulong arg2, ulong arg3);

    // void return, MTLSize + MTLSize args
    // (dispatchThreadgroups:threadsPerThreadgroup:)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSendDispatch(
        IntPtr receiver, IntPtr selector,
        MTLSize arg1, MTLSize arg2);

    // byte* return, no args (UTF8String)
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern byte* ObjcMsgSendUtf8(
        IntPtr receiver, IntPtr selector);

    #endregion

    #region Cached Selectors

    private static readonly IntPtr SelNewCommandQueue =
        SelRegisterName("newCommandQueue");
    private static readonly IntPtr SelCommandBuffer =
        SelRegisterName("commandBuffer");
    private static readonly IntPtr SelComputeCommandEncoder =
        SelRegisterName("computeCommandEncoder");
    private static readonly IntPtr SelSetComputePipelineState =
        SelRegisterName("setComputePipelineState:");
    private static readonly IntPtr SelSetBytesLengthAtIndex =
        SelRegisterName("setBytes:length:atIndex:");
    private static readonly IntPtr SelSetBufferOffsetAtIndex =
        SelRegisterName("setBuffer:offset:atIndex:");
    private static readonly IntPtr SelSetThreadgroupMemoryLengthAtIndex =
        SelRegisterName("setThreadgroupMemoryLength:atIndex:");
    private static readonly IntPtr SelDispatchThreadgroups =
        SelRegisterName("dispatchThreadgroups:threadsPerThreadgroup:");
    private static readonly IntPtr SelDispatchThreads =
        SelRegisterName("dispatchThreads:threadsPerThreadgroup:");
    private static readonly IntPtr SelEndEncoding =
        SelRegisterName("endEncoding");
    private static readonly IntPtr SelCommit =
        SelRegisterName("commit");
    private static readonly IntPtr SelWaitUntilCompleted =
        SelRegisterName("waitUntilCompleted");
    private static readonly IntPtr SelNewLibraryWithSource =
        SelRegisterName("newLibraryWithSource:options:error:");
    private static readonly IntPtr SelNewLibraryWithData =
        SelRegisterName("newLibraryWithData:error:");
    private static readonly IntPtr SelNewLibraryWithURL =
        SelRegisterName("newLibraryWithURL:error:");
    private static readonly IntPtr SelFileURLWithPath =
        SelRegisterName("fileURLWithPath:");
    private static readonly IntPtr SelNewFunctionWithName =
        SelRegisterName("newFunctionWithName:");
    private static readonly IntPtr SelNewComputePipelineStateWithFunction =
        SelRegisterName("newComputePipelineStateWithFunction:error:");

    // ObjC lifecycle
    private static readonly IntPtr SelRetain = SelRegisterName("retain");
    private static readonly IntPtr SelRelease = SelRegisterName("release");
    private static readonly IntPtr SelAlloc = SelRegisterName("alloc");

    // NSString
    private static readonly IntPtr SelInitWithUtf8String =
        SelRegisterName("initWithUTF8String:");
    private static readonly IntPtr SelUtf8String =
        SelRegisterName("UTF8String");

    // NSError
    private static readonly IntPtr SelLocalizedDescription =
        SelRegisterName("localizedDescription");

    // Device properties
    private static readonly IntPtr SelName = SelRegisterName("name");
    private static readonly IntPtr SelMaxThreadgroupMemoryLength =
        SelRegisterName("maxThreadgroupMemoryLength");
    private static readonly IntPtr SelRecommendedMaxWorkingSetSize =
        SelRegisterName("recommendedMaxWorkingSetSize");
    private static readonly IntPtr SelSupportsFamily =
        SelRegisterName("supportsFamily:");

    // Device properties (MTLDevice)
    private static readonly IntPtr SelMaxThreadsPerThreadgroup =
        SelRegisterName("maxThreadsPerThreadgroup");

    // Pipeline state properties (MTLComputePipelineState)
    private static readonly IntPtr SelMaxTotalThreadsPerThreadgroup =
        SelRegisterName("maxTotalThreadsPerThreadgroup");
    private static readonly IntPtr SelThreadExecutionWidth =
        SelRegisterName("threadExecutionWidth");

    // Buffer
    private static readonly IntPtr SelNewBufferWithLength =
        SelRegisterName("newBufferWithLength:options:");
    private static readonly IntPtr SelContents = SelRegisterName("contents");

    // NSString class
    private static readonly IntPtr ClassNSString = ObjcGetClass("NSString");

    #endregion

    #region ObjC Lifecycle

    /// <summary>
    /// Retains an Objective-C object (increments reference count).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr Retain(IntPtr obj)
    {
        if (obj != IntPtr.Zero)
            ObjcMsgSend(obj, SelRetain);
        return obj;
    }

    /// <summary>
    /// Releases an Objective-C object (decrements reference count).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero)
            ObjcMsgSendVoid(obj, SelRelease);
    }

    #endregion

    #region NSString

    /// <summary>
    /// Creates an NSString from a managed string.
    /// </summary>
    /// <param name="str">The managed string.</param>
    /// <returns>Handle to the NSString. Caller must release.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr CreateNSString(string str)
    {
        var allocated = ObjcMsgSend(ClassNSString, SelAlloc);
        fixed (byte* utf8 = System.Text.Encoding.UTF8.GetBytes(str + '\0'))
        {
            return ObjcMsgSendInitString(allocated, SelInitWithUtf8String, utf8);
        }
    }

    /// <summary>
    /// Extracts a managed string from an NSString handle.
    /// </summary>
    /// <param name="nsString">The NSString handle.</param>
    /// <returns>The managed string, or null if the handle is zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetNSString(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero)
            return null;
        var utf8Ptr = ObjcMsgSendUtf8(nsString, SelUtf8String);
        return Marshal.PtrToStringUTF8((IntPtr)utf8Ptr);
    }

    #endregion

    #region NSError

    /// <summary>
    /// Gets the localized description from an NSError object.
    /// </summary>
    /// <param name="nsError">The NSError handle.</param>
    /// <returns>The error description, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetNSErrorDescription(IntPtr nsError)
    {
        if (nsError == IntPtr.Zero)
            return null;
        var descNSString = ObjcMsgSend(nsError, SelLocalizedDescription);
        return GetNSString(descNSString);
    }

    #endregion

    #region Device

    /// <summary>
    /// Creates the default Metal device.
    /// </summary>
    /// <returns>
    /// Handle to the default MTLDevice, or IntPtr.Zero if unavailable.
    /// </returns>
    [DllImport(MetalLib, EntryPoint = "MTLCreateSystemDefaultDevice")]
    internal static extern IntPtr CreateSystemDefaultDevice();

    /// <summary>
    /// Creates a new command queue on the given device.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <returns>Handle to the new command queue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewCommandQueue(IntPtr device) =>
        ObjcMsgSend(device, SelNewCommandQueue);

    /// <summary>
    /// Gets the device name as a managed string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetDeviceName(IntPtr device)
    {
        var nameNSString = ObjcMsgSend(device, SelName);
        return GetNSString(nameNSString);
    }

    /// <summary>
    /// Gets the maximum threadgroup memory length in bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetMaxThreadgroupMemoryLength(IntPtr device) =>
        ObjcMsgSendUlong(device, SelMaxThreadgroupMemoryLength);

    /// <summary>
    /// Gets the recommended maximum working set size in bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetRecommendedMaxWorkingSetSize(IntPtr device) =>
        ObjcMsgSendUlong(device, SelRecommendedMaxWorkingSetSize);

    /// <summary>
    /// Gets the maximum threads per threadgroup as an MTLSize.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MTLSize GetMaxThreadsPerThreadgroup(IntPtr device) =>
        ObjcMsgSendMTLSize(device, SelMaxThreadsPerThreadgroup);

    /// <summary>
    /// Gets the max total threads per threadgroup (1D limit) from the device.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetMaxTotalThreadsPerThreadgroup(IntPtr device) =>
        ObjcMsgSendMTLSize(device, SelMaxThreadsPerThreadgroup).Width;

    /// <summary>
    /// Queries whether the device supports the given GPU family.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <param name="gpuFamily">The MTLGPUFamily enum value.</param>
    /// <returns>True if the family is supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SupportsFamily(IntPtr device, int gpuFamily) =>
        ObjcMsgSendBool(device, SelSupportsFamily, (ulong)gpuFamily) != 0;

    #endregion

    #region Pipeline State Properties

    /// <summary>
    /// Gets the max total threads per threadgroup from a pipeline state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetPipelineMaxThreadsPerThreadgroup(
        IntPtr pipelineState) =>
        ObjcMsgSendUlong(pipelineState, SelMaxTotalThreadsPerThreadgroup);

    /// <summary>
    /// Gets the thread execution width (warp/SIMD size) from a pipeline state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetPipelineThreadExecutionWidth(
        IntPtr pipelineState) =>
        ObjcMsgSendUlong(pipelineState, SelThreadExecutionWidth);

    #endregion

    #region Pipeline

    /// <summary>
    /// Creates a new library from Metal source code.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <param name="source">The Metal source string (NSString).</param>
    /// <param name="options">Compile options (or IntPtr.Zero).</param>
    /// <param name="error">Output NSError handle (zero if no error).</param>
    /// <returns>Handle to the compiled library.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewLibraryWithSource(
        IntPtr device,
        IntPtr source,
        IntPtr options,
        out IntPtr error)
    {
        IntPtr nsError = IntPtr.Zero;
        var result = ObjcMsgSend(
            device, SelNewLibraryWithSource,
            source, options, &nsError);
        error = nsError;
        return result;
    }

    /// <summary>
    /// Creates a Metal library from pre-compiled metallib binary data using
    /// <c>[MTLDevice newLibraryWithData:error:]</c>.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <param name="data">The metallib binary data.</param>
    /// <param name="error">Output NSError handle (zero if no error).</param>
    /// <returns>Handle to the loaded library.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewLibraryWithData(
        IntPtr device,
        ReadOnlySpan<byte> data,
        out IntPtr error)
    {
        // Write metallib to a temp file and load via newLibraryWithURL:
        // This avoids dispatch_data_t interop complexity.
        var tmpPath = System.IO.Path.GetTempFileName() + ".metallib";
        try
        {
            System.IO.File.WriteAllBytes(tmpPath, data.ToArray());
            var nsPath = CreateNSString(tmpPath);
            try
            {
                var nsUrl = ObjcMsgSend(
                    ObjcGetClass("NSURL"),
                    SelFileURLWithPath,
                    nsPath);
                IntPtr nsError = IntPtr.Zero;
                var result = ObjcMsgSend(
                    device, SelNewLibraryWithURL, nsUrl, &nsError);
                error = nsError;
                return result;
            }
            finally
            {
                Release(nsPath);
            }
        }
        finally
        {
            try { System.IO.File.Delete(tmpPath); } catch { }
        }
    }

    /// <summary>
    /// Gets a function from a library by name.
    /// </summary>
    /// <param name="library">The library handle.</param>
    /// <param name="name">The function name (NSString).</param>
    /// <returns>Handle to the Metal function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewFunctionWithName(IntPtr library, IntPtr name) =>
        ObjcMsgSend(library, SelNewFunctionWithName, name);

    /// <summary>
    /// Creates a compute pipeline state from a function.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <param name="function">The compute function handle.</param>
    /// <param name="error">Output NSError handle (zero if no error).</param>
    /// <returns>Handle to the compute pipeline state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewComputePipelineState(
        IntPtr device,
        IntPtr function,
        out IntPtr error)
    {
        IntPtr nsError = IntPtr.Zero;
        var result = ObjcMsgSend(
            device, SelNewComputePipelineStateWithFunction,
            function, &nsError);
        error = nsError;
        return result;
    }

    #endregion

    #region Buffer

    /// <summary>
    /// Creates a new MTLBuffer with the specified length and options.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <param name="length">Buffer length in bytes.</param>
    /// <param name="options">Resource options (storage mode).</param>
    /// <returns>Handle to the new MTLBuffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewBuffer(
        IntPtr device,
        ulong length,
        MetalResourceOptions options) =>
        ObjcMsgSendBuffer(
            device, SelNewBufferWithLength, length, (ulong)options);

    /// <summary>
    /// Gets the CPU-accessible pointer to the buffer contents.
    /// </summary>
    /// <param name="buffer">The MTLBuffer handle.</param>
    /// <returns>Pointer to the buffer contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr GetBufferContents(IntPtr buffer) =>
        ObjcMsgSend(buffer, SelContents);

    #endregion

    #region Command Buffer & Encoder

    /// <summary>
    /// Creates a new command buffer from a command queue.
    /// </summary>
    /// <param name="commandQueue">The command queue handle.</param>
    /// <returns>Handle to the new command buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewCommandBuffer(IntPtr commandQueue) =>
        ObjcMsgSend(commandQueue, SelCommandBuffer);

    /// <summary>
    /// Creates a compute command encoder from a command buffer.
    /// </summary>
    /// <param name="commandBuffer">The command buffer handle.</param>
    /// <returns>Handle to the compute command encoder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr NewComputeCommandEncoder(IntPtr commandBuffer) =>
        ObjcMsgSend(commandBuffer, SelComputeCommandEncoder);

    /// <summary>
    /// Sets the compute pipeline state on an encoder.
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="pipelineState">The pipeline state handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetComputePipelineState(
        IntPtr encoder,
        IntPtr pipelineState) =>
        ObjcMsgSendVoid(encoder, SelSetComputePipelineState, pipelineState);

    #endregion

    #region Argument Setting

    /// <summary>
    /// Sets bytes (small value arguments) on the compute command encoder.
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="bytes">Pointer to the bytes to set.</param>
    /// <param name="length">Length in bytes.</param>
    /// <param name="index">The argument index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBytes(
        IntPtr encoder,
        void* bytes,
        ulong length,
        ulong index) =>
        ObjcMsgSend(encoder, SelSetBytesLengthAtIndex, bytes, length, index);

    /// <summary>
    /// Sets bytes for a value type argument.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="length">The size in bytes.</param>
    /// <param name="index">The argument index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBytes<T>(
        IntPtr encoder,
        T value,
        int length,
        int index) where T : unmanaged =>
        SetBytes(encoder, &value, (ulong)length, (ulong)index);

    /// <summary>
    /// Sets a buffer (pointer argument) on the compute command encoder.
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="buffer">The buffer handle.</param>
    /// <param name="offset">The byte offset into the buffer.</param>
    /// <param name="index">The argument index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBuffer(
        IntPtr encoder,
        IntPtr buffer,
        ulong offset,
        int index) =>
        ObjcMsgSendVoid(
            encoder, SelSetBufferOffsetAtIndex,
            buffer, offset, (ulong)index);

    /// <summary>
    /// Sets threadgroup (shared) memory length on the compute command encoder.
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="length">The shared memory length in bytes.</param>
    /// <param name="index">The argument index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetThreadgroupMemoryLength(
        IntPtr encoder,
        ulong length,
        int index) =>
        ObjcMsgSendVoid(encoder, SelSetThreadgroupMemoryLengthAtIndex,
            length, (ulong)index);

    #endregion

    #region Dispatch

    /// <summary>
    /// Dispatches threadgroups for compute execution.
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="gridDim">The grid dimensions.</param>
    /// <param name="groupDim">The threadgroup dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DispatchThreadgroups(
        IntPtr encoder,
        Index3D gridDim,
        Index3D groupDim)
    {
        var gridSize = new MTLSize(
            (ulong)gridDim.X, (ulong)gridDim.Y, (ulong)gridDim.Z);
        var groupSize = new MTLSize(
            (ulong)groupDim.X, (ulong)groupDim.Y, (ulong)groupDim.Z);
        ObjcMsgSendDispatch(
            encoder, SelDispatchThreadgroups, gridSize, groupSize);
    }

    /// <summary>
    /// Dispatches an exact number of threads for compute execution.
    /// Uses non-uniform threadgroup sizes so the last threadgroup may have
    /// fewer threads. Requires Apple GPU Family 4+ (non-uniform threadgroups).
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    /// <param name="totalThreads">The total threads per dimension.</param>
    /// <param name="groupDim">The threadgroup dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DispatchThreads(
        IntPtr encoder,
        Index3D totalThreads,
        Index3D groupDim)
    {
        var threadsSize = new MTLSize(
            (ulong)totalThreads.X, (ulong)totalThreads.Y, (ulong)totalThreads.Z);
        var groupSize = new MTLSize(
            (ulong)groupDim.X, (ulong)groupDim.Y, (ulong)groupDim.Z);
        ObjcMsgSendDispatch(
            encoder, SelDispatchThreads, threadsSize, groupSize);
    }

    /// <summary>
    /// Ends encoding on the compute command encoder.
    /// </summary>
    /// <param name="encoder">The compute command encoder handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EndEncoding(IntPtr encoder) =>
        ObjcMsgSendVoid(encoder, SelEndEncoding);

    /// <summary>
    /// Commits the command buffer for execution.
    /// </summary>
    /// <param name="commandBuffer">The command buffer handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Commit(IntPtr commandBuffer) =>
        ObjcMsgSendVoid(commandBuffer, SelCommit);

    /// <summary>
    /// Waits until the command buffer has completed execution.
    /// </summary>
    /// <param name="commandBuffer">The command buffer handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WaitUntilCompleted(IntPtr commandBuffer) =>
        ObjcMsgSendVoid(commandBuffer, SelWaitUntilCompleted);

    #endregion
}
