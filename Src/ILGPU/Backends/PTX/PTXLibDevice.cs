// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXLibDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ILGPU.Backends.PTX
{
    internal static class PTXLibDevice
    {
        /// <summary>
        /// Detects the location of the Cuda SDK and NVVM/LibDevice files.
        /// </summary>
        /// <param name="cudaEnvName">Filled with the environment variable used.</param>
        /// <param name="nvvmBinDir">Filled with the detected NVVM folder.</param>
        /// <param name="libNvvmPath">Filled with the detected NVVM file.</param>
        /// <param name="libDeviceDir">Filled with the detected LibDevice folder.</param>
        /// <param name="libDevicePath">Filled with the detected LibDevice file.</param>
        public static void FindLibDevicePaths(
            out string? cudaEnvName,
            out string? nvvmBinDir,
            out string? libNvvmPath,
            out string? libDeviceDir,
            out string? libDevicePath)
        {
            // Find the CUDA installation path.
            cudaEnvName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "CUDA_PATH"
                : "CUDA_HOME";
            var cudaPath = Environment.GetEnvironmentVariable(cudaEnvName);
            if (string.IsNullOrEmpty(cudaPath))
            {
                nvvmBinDir = null;
                libNvvmPath = null;
                libDeviceDir = null;
                libDevicePath = null;
                return;
            }

            var nvvmRoot = Path.Combine(cudaPath, "nvvm");

            // Find the NVVM DLL.
            var nvvmBinName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "bin"
                : "lib64";
            nvvmBinDir = Path.Combine(nvvmRoot, nvvmBinName);
            var nvvmSearchPattern =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "nvvm*.dll"
                : "libnvvm*.so";
            var nvvmFiles = Directory.EnumerateFiles(nvvmBinDir, nvvmSearchPattern);
            libNvvmPath = nvvmFiles.FirstOrDefault();

            // Find the LibDevice Bitcode.
            libDeviceDir = Path.Combine(nvvmRoot, "libdevice");
            var libDeviceFiles = Directory.EnumerateFiles(
                libDeviceDir,
                "libdevice.*.bc");
            libDevicePath = libDeviceFiles.FirstOrDefault();
        }

        /// <summary>
        /// Generates the LibDevice PTX code using NVVM.
        /// </summary>
        /// <param name="nvvmAPI">The NVVM API instance.</param>
        /// <param name="architecture">The target Cuda architure to generate for.</param>
        /// <param name="methods">The LibDevice method names to generate.</param>
        /// <param name="ptx">Filled in with the generated PTX code.</param>
        public static unsafe void GenerateLibDeviceCode(
            NvvmAPI nvvmAPI,
            in CudaArchitecture architecture,
            IEnumerable<string> methods,
            out string? ptx)
        {
            ptx = null;

            // Determine the NVVM IR Version to use.
            var result = nvvmAPI.GetIRVersion(out int majorIR, out _, out _, out _);
            if (result != NvvmResult.NVVM_SUCCESS)
                return;

            // Convert the methods in the context into NVVM.
            var nvvmModule = PTXLibDeviceNvvm.GenerateNvvm(majorIR, methods);

            if (string.IsNullOrEmpty(nvvmModule))
                return;

            // Create a new NVVM program.
            result = nvvmAPI.CreateProgram(out var program);

            try
            {
                // Add custom NVVM module.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    var nvvmModuleBytes = Encoding.ASCII.GetBytes(nvvmModule);
                    fixed (byte* nvvmPtr = nvvmModuleBytes)
                    {
                        result = nvvmAPI.AddModuleToProgram(
                            program,
                            new IntPtr(nvvmPtr),
                            new IntPtr(nvvmModuleBytes.Length),
                            null);
                    }
                }

                // Add the LibDevice bit code.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    fixed (byte* ptr = nvvmAPI.LibDeviceBytes)
                    {
                        result = nvvmAPI.LazyAddModuleToProgram(
                            program,
                            new IntPtr(ptr),
                            new IntPtr(nvvmAPI.LibDeviceBytes.Length),
                            null);
                    }
                }

                // Compile the NVVM into PTX for the backend architecture.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    var major = architecture.Major;
                    var minor = architecture.Minor;
                    var archOption = $"-arch=compute_{major}{minor}";
                    var archOptionAscii = Encoding.ASCII.GetBytes(archOption);
                    fixed (byte* archOptionPtr = archOptionAscii)
                    {
                        var numOptions = 1;
                        var optionValues = stackalloc byte[sizeof(void*) * numOptions];
                        var values = (void**)optionValues;
                        values[0] = archOptionPtr;

                        result = nvvmAPI.CompileProgram(
                            program,
                            numOptions,
                            new IntPtr(values));
                    }
                }

                // Extract the PTX result and comment out the initial declarations.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    result = nvvmAPI.GetCompiledResult(program, out var compiledPTX);
                    if (result == NvvmResult.NVVM_SUCCESS)
                    {
                        ptx = compiledPTX;
                    }
                }
            }
            finally
            {
                nvvmAPI.DestroyProgram(ref program);
            }
        }
    }
}
