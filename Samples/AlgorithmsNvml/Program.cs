// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Cuda.API;
using System;

namespace AlgorithmsNvml
{
    class Program
    {
        static void Main()
        {
            using var context = Context.Create(builder => builder.Cuda());

            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context) as CudaAccelerator;
                Console.WriteLine($"Performing operations on {accelerator}");

                // Create NvmlDevice wrapper
                using var nvmlDevice = NvmlDevice.CreateFromAccelerator(accelerator);
                var temp = nvmlDevice.GetGpuTemperature();

                // Calling low-level NvmlAPI directly
                NvmlException.ThrowIfFailed(
                    nvmlDevice.API.DeviceGetTemperature(
                        nvmlDevice.DeviceHandle,
                        NvmlTemperatureSensors.NVML_TEMPERATURE_GPU,
                        out temp));

                // Create separate NvmlAPI instance
                var nvml = NvmlAPI.Create(NvmlAPIVersion.V6);
                NvmlException.ThrowIfFailed(
                    nvml.UnitGetCount(out uint count));
            }
        }
    }
}
