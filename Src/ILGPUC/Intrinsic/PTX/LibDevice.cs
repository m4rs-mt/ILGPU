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

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ILGPU.Intrinsic.PTX;

class LibDevice
{
    /// <summary>
    /// Turns on LibDevice support.
    /// Automatically detects the CUDA SDK location.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public void Find() =>
        LibDevice(throwIfNotFound: true);

    /// <summary>
    /// Turns on LibDevice support.
    /// Automatically detects the CUDA SDK location.
    /// </summary>
    /// <param name="throwIfNotFound">Determines error handling.</param>
    /// <returns>The current builder instance.</returns>
    internal void Find(bool throwIfNotFound)
    {
        // Find the CUDA installation path.
        var cudaEnvName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "CUDA_PATH"
            : "CUDA_HOME";
        var cudaPath = Environment.GetEnvironmentVariable(cudaEnvName);
        if (string.IsNullOrEmpty(cudaPath))
        {
            return throwIfNotFound
            ? throw new NotSupportedException(string.Format(
                RuntimeErrorMessages.NotSupportedLibDeviceEnvironmentVariable,
                cudaEnvName))
            : this;
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
        var libNvvmPath = nvvmFiles.FirstOrDefault();
        if (libNvvmPath is null)
        {
            return throwIfNotFound
            ? throw new NotSupportedException(string.Format(
                RuntimeErrorMessages.NotSupportedLibDeviceNotFoundNvvmDll,
                nvvmBinDir))
            : this;
        }

        // Find the LibDevice Bitcode.
        var libDeviceDir = Path.Combine(nvvmRoot, "libdevice");
        var libDeviceFiles = Directory.EnumerateFiles(
            libDeviceDir,
            "libdevice.*.bc");
        var libDevicePath = libDeviceFiles.FirstOrDefault();
        if (libDevicePath is null)
        {
            return throwIfNotFound
            ? throw new NotSupportedException(string.Format(
                RuntimeErrorMessages.NotSupportedLibDeviceNotFoundBitCode,
                libDeviceDir))
            : this;
        }

        LibNvvmPath = libNvvmPath;
        LibDevicePath = libDevicePath;
    }

    /// <summary>
    /// Turns on LibDevice support.
    /// Explicitly specifies the LibDevice location.
    /// </summary>
    /// <param name="libNvvmPath">Path to LibNvvm DLL.</param>
    /// <param name="libDevicePath">Path to LibDevice bitcode.</param>
    /// <returns>The current builder instance.</returns>
    public Builder LibDevice(string libNvvmPath, string libDevicePath)
    {
        LibNvvmPath = libNvvmPath;
        LibDevicePath = libDevicePath;
        return this;
    }
}
