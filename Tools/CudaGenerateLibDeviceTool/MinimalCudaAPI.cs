// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: MinimalCudaAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace CudaGenerateLibDeviceTool
{
    /// <summary>
    /// Minimal Cuda API binding to allow detecting the current Cuda driver version.
    /// </summary>
    internal static class MinimalCudaAPI
    {
        delegate int CudaInit(int flags);
        delegate int CudaDriverGetVersion(out int driverVersion);

        public static int GetCudaDriverVersion()
        {
            var cudaLibName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "nvcuda"
                : "cuda";
            var cudaAPI = NativeLibrary.Load(cudaLibName);
            try
            {
                var cuInit =
                    Marshal.GetDelegateForFunctionPointer<CudaInit>(
                        NativeLibrary.GetExport(cudaAPI, "cuInit"));
                var cuDriverGetVersion =
                    Marshal.GetDelegateForFunctionPointer<CudaDriverGetVersion>(
                        NativeLibrary.GetExport(cudaAPI, "cuDriverGetVersion"));

                if (cuInit(0) == 0 && cuDriverGetVersion(out int driverVersion) == 0)
                    return driverVersion;
            }
            finally
            {
                NativeLibrary.Free(cudaAPI);
            }

            throw new NotImplementedException();
        }
    }
}
