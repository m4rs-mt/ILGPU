// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2020 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime.Cuda;
using System;

namespace AlgorithmsCuBlas
{
    class Program
    {
        static void Main()
        {
            const int DataSize = 1024;

            using var context = Context.Create(builder => builder.Cuda().EnableAlgorithms());

            // Check for Cuda support
            foreach (var device in context.GetCudaDevices())
            {
                using var accelerator = device.CreateCudaAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                var buf = accelerator.Allocate1D<float>(DataSize);
                var buf2 = accelerator.Allocate1D<float>(DataSize);

                accelerator.Initialize(accelerator.DefaultStream, buf.View, 1.0f);
                accelerator.Initialize(accelerator.DefaultStream, buf2.View, 1.0f);

                // Initialize the CuBlas library using manual pointer mode handling
                // (default behavior)
                using (var blas = new CuBlas(accelerator))
                {
                    // Set pointer mode to Host to enable data transfer to CPU memory
                    blas.PointerMode = CuBlasPointerMode.Host;
                    float output = blas.Nrm2(buf.View.AsGeneral());

                    // Set pointer mode to Device to enable data transfer to GPU memory
                    blas.PointerMode = CuBlasPointerMode.Device;
                    blas.Nrm2(buf.View.AsGeneral(), buf2.View);

                    // Use pointer mode scopes to recover the previous pointer mode
                    using var scope = blas.BeginPointerScope(CuBlasPointerMode.Host);
                    float output2 = blas.Nrm2(buf.View.AsGeneral());
                }

                // Initialize the CuBlas<T> library using custom pointer mode handlers
                using (var blas = new CuBlas<CuBlasPointerModeHandlers.AutomaticMode>(accelerator))
                {
                    // Automatic transfer to host
                    float output = blas.Nrm2(buf.View.AsGeneral());

                    // Automatic transfer to device
                    blas.Nrm2(buf.View.AsGeneral(), buf2.View);
                }
            }
        }
    }
}
