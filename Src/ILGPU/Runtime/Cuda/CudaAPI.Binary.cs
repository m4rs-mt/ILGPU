// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaAPI.Binary.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

#pragma warning disable IDE1006 // Naming
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

namespace ILGPU.Runtime.Cuda;

// Binary-data variants of cuModuleLoadData/cuModuleLoadDataEx.
// The CUDA driver functions accept const void* — these overloads use IntPtr
// instead of string to avoid string marshalling for binary (cubin) data.

unsafe partial class CudaAPI
{
    internal virtual CudaError cuModuleLoadDataBinary(
        out IntPtr module,
        IntPtr moduleData)
    {
        module = IntPtr.Zero;
        throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
    }

    internal virtual CudaError cuModuleLoadDataExBinary(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues)
    {
        module = IntPtr.Zero;
        throw new NotSupportedException(RuntimeErrorMessages.CudaNotSupported);
    }
}

[SuppressMessage(
    "Security",
    "CA5393:Do not use unsafe DllImportSearchPath value")]
sealed unsafe partial class CudaAPI_0
{
#if NET7_0_OR_GREATER
    [LibraryImport(LibNameWindows, EntryPoint = "cuModuleLoadData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static partial CudaError cuModuleLoadDataBinary_Import(
        out IntPtr module,
        IntPtr moduleData);
#else
    [DllImport(LibNameWindows, EntryPoint = "cuModuleLoadData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static extern CudaError cuModuleLoadDataBinary_Import(
        out IntPtr module,
        IntPtr moduleData);
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(LibNameWindows, EntryPoint = "cuModuleLoadDataEx")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static partial CudaError cuModuleLoadDataExBinary_Import(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues);
#else
    [DllImport(LibNameWindows, EntryPoint = "cuModuleLoadDataEx")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static extern CudaError cuModuleLoadDataExBinary_Import(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues);
#endif

    internal sealed override CudaError cuModuleLoadDataBinary(
        out IntPtr module,
        IntPtr moduleData) =>
        cuModuleLoadDataBinary_Import(out module, moduleData);

    internal sealed override CudaError cuModuleLoadDataExBinary(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues) =>
        cuModuleLoadDataExBinary_Import(
            out module,
            moduleData,
            numOptions,
            jitOptions,
            jitOptionValues);
}

[SuppressMessage(
    "Security",
    "CA5393:Do not use unsafe DllImportSearchPath value")]
sealed unsafe partial class CudaAPI_1
{
#if NET7_0_OR_GREATER
    [LibraryImport(LibNameLinux, EntryPoint = "cuModuleLoadData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static partial CudaError cuModuleLoadDataBinary_Import(
        out IntPtr module,
        IntPtr moduleData);
#else
    [DllImport(LibNameLinux, EntryPoint = "cuModuleLoadData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static extern CudaError cuModuleLoadDataBinary_Import(
        out IntPtr module,
        IntPtr moduleData);
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(LibNameLinux, EntryPoint = "cuModuleLoadDataEx")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static partial CudaError cuModuleLoadDataExBinary_Import(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues);
#else
    [DllImport(LibNameLinux, EntryPoint = "cuModuleLoadDataEx")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    private static extern CudaError cuModuleLoadDataExBinary_Import(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues);
#endif

    internal sealed override CudaError cuModuleLoadDataBinary(
        out IntPtr module,
        IntPtr moduleData) =>
        cuModuleLoadDataBinary_Import(out module, moduleData);

    internal sealed override CudaError cuModuleLoadDataExBinary(
        out IntPtr module,
        IntPtr moduleData,
        int numOptions,
        IntPtr jitOptions,
        IntPtr jitOptionValues) =>
        cuModuleLoadDataExBinary_Import(
            out module,
            moduleData,
            numOptions,
            jitOptions,
            jitOptionValues);
}

#pragma warning restore CA2101
#pragma warning restore IDE1006
