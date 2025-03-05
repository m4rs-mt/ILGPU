// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LibDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Cuda.Libraries;
using ILGPUC.IR;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ILGPUC.Backends.PTX.API;

sealed class LibDevice
{
    /// <summary>
    /// Returns true if the given method is a lib device method.
    /// </summary>
    /// <param name="method">The method to be tested.</param>
    /// <returns>True if the given method is a lib device method.</returns>
    public static bool IsLibDeviceMethod(Method method) =>
        method.HasSource &&
        method.Source.DeclaringType == typeof(NvvmLibDeviceMethods);

    /// <summary>
    /// Turns on LibDevice support.
    /// Automatically detects the CUDA SDK location.
    /// </summary>
    public LibDevice()
    {
        // Find the CUDA installation path.
        var cudaEnvName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "CUDA_PATH"
            : "CUDA_HOME";
        var cudaPath = Environment.GetEnvironmentVariable(cudaEnvName);
        if (string.IsNullOrEmpty(cudaPath))
        {
            throw new NotSupportedException(string.Format(
                RuntimeErrorMessages.NotSupportedLibDeviceEnvironmentVariable,
                cudaEnvName));
        }
        var nvvmRoot = Path.Combine(cudaPath, "nvvm");

        // Find the NVVM DLL.
        var nvvmBinName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "bin"
            : "lib64";
        var nvvmBinDir = Path.Combine(nvvmRoot, nvvmBinName);
        var nvvmSearchPattern =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "nvvm64*.dll"
            : "libnvvm*.so";
        var nvvmFiles = Directory.EnumerateFiles(nvvmBinDir, nvvmSearchPattern);
        var libNvvmPath = nvvmFiles.FirstOrDefault() ??
            throw new NotSupportedException(string.Format(
                RuntimeErrorMessages.NotSupportedLibDeviceNotFoundNvvmDll,
                nvvmBinDir));

        // Find the LibDevice Bitcode.
        var libDeviceDir = Path.Combine(nvvmRoot, "libdevice");
        var libDeviceFiles = Directory.EnumerateFiles(
            libDeviceDir,
            "libdevice.*.bc");
        var libDevicePath = libDeviceFiles.FirstOrDefault() ??
            throw new NotSupportedException(string.Format(
                RuntimeErrorMessages.NotSupportedLibDeviceNotFoundBitCode,
                libDeviceDir));
        LibNvvmPath = libNvvmPath;
        LibDevicePath = libDevicePath;
    }

    /// <summary>
    /// Returns the current LibNvvmPath.
    /// </summary>
    public string LibNvvmPath { get; }

    /// <summary>
    /// Returns the current LibDevicePath.
    /// </summary>
    public string LibDevicePath { get; }
}
