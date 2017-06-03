// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Mandelbrot.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Lightning;

namespace Mandelbrot
{
    class Mandelbrot
    {
        /// <summary>
        /// ILGPU kernel for Mandelbrot set.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        /// <param name="output"></param>
        static void MandelbrotKernel(
            Index index,
            int width, int height, int max_iterations,
            ArrayView<int> output)
        {
            float h_a = -2.0f;
            float h_b = 1.0f;
            float v_a = -1.0f;
            float v_b = 1.0f;

            if (index >= output.Length)
                return;

            int img_x = index % width;
            int img_y = index / width;

            float x0 = h_a + img_x * (h_b - h_a) / width;
            float y0 = v_a + img_y * (v_b - v_a) / height;
            float x = 0.0f;
            float y = 0.0f;
            int iteration = 0;
            while ((x * x + y * y < 2 * 2) && (iteration < max_iterations))
            {
                float xtemp = x * x - y * y + x0;
                y = 2 * x * y + y0;
                x = xtemp;
                iteration = iteration + 1;
            }
            output[index] = iteration;
        }


        private static Context context;
        private static LightningContext lc;
        private static System.Action<Index, int, int, int, ArrayView<int>> mandelbrot_kernel;

        /// <summary>
        /// Compile the mandelbrot kernel in ILGPU-CPU or ILGPU-CUDA mode.
        /// </summary>
        /// <param name="withCUDA"></param>
        public static void CompileKernel(bool withCUDA)
        {
            context = new Context();
            if (withCUDA)
                lc = LightningContext.CreateCudaContext(context);
            else
                lc = LightningContext.CreateCPUContext(context);

            mandelbrot_kernel = lc.LoadAutoGroupedStreamKernel<
                Index, int, int, int, ArrayView<int>>(MandelbrotKernel);
        }


        /// <summary>
        /// Dispose lightning-context and cuda-context.
        /// </summary>
        public static void Dispose()
        {
            lc.Dispose();
            context.Dispose();
        }


        /// <summary>
        /// Calculate the mandelbrot set on the GPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcGPU(int[] buffer, int width, int height, int max_iterations)
        {
            int num_values = buffer.Length;
            var dev_out = lc.Allocate<int>(num_values);

            // Launch kernel
            mandelbrot_kernel(num_values, width, height, max_iterations, dev_out.View);
            lc.Synchronize();
            dev_out.CopyTo(buffer, 0, 0, num_values);

            dev_out.Dispose();
            return;
        }


        /// <summary>
        /// Calculate the mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPU(int[] buffer, int width, int height, int max_iterations)
        {
            float h_a = -2.0f;
            float h_b = 1.0f;
            float v_a = -1.0f;
            float v_b = 1.0f;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    int index = i + j * width;  // ILGPU-like index
                    int img_x = index % width;
                    int img_y = index / width;

                    float x0 = h_a + img_x * (h_b - h_a) / width;
                    float y0 = v_a + img_y * (v_b - v_a) / height;
                    float x = 0.0f;
                    float y = 0.0f;
                    int iteration = 0;
                    while ((x * x + y * y < 2 * 2) && (iteration < max_iterations))
                    {
                        float xtemp = x * x - y * y + x0;
                        y = 2 * x * y + y0;
                        x = xtemp;
                        iteration = iteration + 1;
                    }
                    buffer[index] = iteration;
                }
            }
        }


    }
}
