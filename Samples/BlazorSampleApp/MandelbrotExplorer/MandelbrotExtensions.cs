// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2022-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: MandelbrotExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorSampleApp.Components;
using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Half = ILGPU.Half;


namespace BlazorSampleApp.MandelbrotExplorer
{
    public static class MandelbrotExtensions
    {

        /// <summary>
        /// This "kernel" function will compile to IL code which ILGPU will ingest and convert to GPU compute shader code.
        ///
        /// </summary>
        /// <param name="index"></param>
        /// <param name="displayParams"></param> displayPort int[] {width, height }
        /// <param name="viewAreaParams"></param>  displayView float[] {h_a, h_b, v_a, v_b}
        /// <param name="maxIterations"></param>
        /// <param name="output"></param>
        public static void MandelbrotKernel(
          Index1D index,
          ArrayView1D<int, Stride1D.Dense> displayParams, ArrayView1D<float, Stride1D.Dense> viewAreaParams, int maxIterations,
          ArrayView<int> output)
        {

            if (index >= output.Length)
                return;

            int img_x = index % displayParams[0];
            int img_y = index / displayParams[0];

            float x0 = viewAreaParams[0] + img_x * (viewAreaParams[1] - viewAreaParams[0]) / displayParams[0];
            float y0 = viewAreaParams[2] + img_y * (viewAreaParams[3] - viewAreaParams[2]) / displayParams[1];
            float x = 0.0f;
            float y = 0.0f;
            int iteration = 0;
            while ((x * x + y * y < 2 * 2) && (iteration < maxIterations))
            {
                float xTemp = x * x - y * y + x0;
                y = 2 * x * y + y0;
                x = xTemp;
                iteration += 1;
            }
            output[index] = iteration;
        }



        /// <summary>
        /// Calculate the Mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalcCPUSingleThreadFP8E4M3(int[] buffer, int[] display, float[] view, int max_iterations)
        {

            FP8E4M3 two = FP8E4M3.One + FP8E4M3.One;
            FP8E4M3 four = two * two;

            for (int i=0; i < display[0]; i++ )
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    FP8E4M3 x0 = (FP8E4M3)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    FP8E4M3 y0 = (FP8E4M3)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    FP8E4M3 x = FP8E4M3.Zero;
                    FP8E4M3 y = FP8E4M3.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        FP8E4M3 xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }

                    buffer[index] = iteration;
                }
            }
        }


        /// <summary>
        /// Calculate the Mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalcCPUSingleThreadFP8E5M2(int[] buffer, int[] display, float[] view, int max_iterations)
        {

            FP8E5M2 two = FP8E5M2.One + FP8E5M2.One;
            FP8E5M2 four = two * two;

            for (int i=0; i < display[0]; i++ )
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    FP8E5M2 x0 = (FP8E5M2)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    FP8E5M2 y0 = (FP8E5M2)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    FP8E5M2 x = FP8E5M2.Zero;
                    FP8E5M2 y = FP8E5M2.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        FP8E5M2 xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }

                    buffer[index] = iteration;

                }
            }
        }


        /// <summary>
        /// Calculate the Mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalcCPUSingleThreadBF16(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            BF16 two = BF16.One + BF16.One;
            BF16 four = two * two;

            for (int i=0; i < display[0]; i++ )
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    BF16 x0 = (BF16)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    BF16 y0 = (BF16)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    BF16 x = BF16.Zero;
                    BF16 y = BF16.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        BF16 xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }

                    buffer[index] = iteration;

                }
            }
        }

        /// <summary>
        /// Calculate the Mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalcCPUSingleThreadHalf(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            Half two = Half.One + Half.One;
            Half four = two * two;

            for (int i=0; i < display[0]; i++ )
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    Half x0 = (Half)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    Half y0 = (Half)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    Half x = Half.Zero;
                    Half y = Half.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        Half xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }

                    buffer[index] = iteration;

                }
            }
        }


        /// <summary>
        /// Calculate the Mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUSingleThreadFloat(int[] buffer, int[] display, float[] view, int max_iterations)
        {

            for (int i=0; i < display[0]; i++ )
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    float x0 = view[0] + img_x * (view[1] - view[0]) / display[0];
                    float y0 = view[2] + img_y * (view[3] - view[2]) / display[1];
                    float x = 0.0f;
                    float y = 0.0f;
                    int iteration = 0;
                    while ((x * x + y * y < 2 * 2) && (iteration < max_iterations))
                    {
                        float xtemp = x * x - y * y + x0;
                        y = 2 * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            }
        }


        /// <summary>
        /// Calculate the Mandelbrot set single threaded on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUSingleThreadDouble(int[] buffer, int[] display, float[] view, int max_iterations)
        {

            for (int i=0; i < display[0]; i++ )
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    double x0 = view[0] + img_x * (view[1] - view[0]) / display[0];
                    double y0 = view[2] + img_y * (view[3] - view[2]) / display[1];
                    double x = 0.0f;
                    double y = 0.0f;
                    int iteration = 0;
                    while ((x * x + y * y < 2 * 2) && (iteration < max_iterations))
                    {
                        double xtemp = x * x - y * y + x0;
                        y = 2 * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            }
        }

        /// <summary>
        /// Calculate the Mandelbrot set using multiple parallel threads on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUParallelForFP8E4M3(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            int icnt = display[0];

            FP8E4M3 two = FP8E4M3.One + FP8E4M3.One;
            FP8E4M3 four = two + two;


            Parallel.For( 0, icnt, i =>
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    FP8E4M3 x0 = (FP8E4M3)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    FP8E4M3 y0 = (FP8E4M3)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    FP8E4M3 x = FP8E4M3.Zero;
                    FP8E4M3 y = FP8E4M3.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        FP8E4M3 xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            });
        }


        /// <summary>
        /// Calculate the Mandelbrot set using multiple parallel threads on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUParallelForFP8E5M2(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            int icnt = display[0];

            FP8E5M2 two = FP8E5M2.One + FP8E5M2.One;
            FP8E5M2 four = two + two;


            Parallel.For( 0, icnt, i =>
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    FP8E5M2 x0 = (FP8E5M2)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    FP8E5M2 y0 = (FP8E5M2)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    FP8E5M2 x = FP8E5M2.Zero;
                    FP8E5M2 y = FP8E5M2.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        FP8E5M2 xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            });
        }

        /// <summary>
        /// Calculate the Mandelbrot set using multiple parallel threads on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUParallelForBF16(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            int icnt = display[0];

            BF16 two = BF16.One + BF16.One;
            BF16 four = two + two;


            Parallel.For( 0, icnt, i =>
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    BF16 x0 = (BF16)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    BF16 y0 = (BF16)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    BF16 x = BF16.Zero;
                    BF16 y = BF16.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        BF16 xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            });
        }

        /// <summary>
        /// Calculate the Mandelbrot set using multiple parallel threads on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUParallelForHalf(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            int icnt = display[0];

            Half two = Half.One + Half.One;
            Half four = two + two;


            Parallel.For( 0, icnt, i =>
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    Half x0 = (Half)(view[0] + img_x * (view[1] - view[0]) / display[0]);
                    Half y0 = (Half)(view[2] + img_y * (view[3] - view[2]) / display[1]);
                    Half x = Half.Zero;
                    Half y = Half.Zero;
                    int iteration = 0;
                    while ((x * x + y * y < four) && (iteration < max_iterations))
                    {
                        Half xtemp = x * x - y * y + x0;
                        y = two * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            });
        }



        /// <summary>
        /// Calculate the Mandelbrot set using multiple parallel threads on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUParallelForFloat(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            int icnt = display[0];

            Parallel.For( 0, icnt, i =>
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    float x0 = view[0] + img_x * (view[1] - view[0]) / display[0];
                    float y0 = view[2] + img_y * (view[3] - view[2]) / display[1];
                    float x = 0.0f;
                    float y = 0.0f;
                    int iteration = 0;
                    while ((x * x + y * y < 2 * 2) && (iteration < max_iterations))
                    {
                        float xtemp = x * x - y * y + x0;
                        y = 2 * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            });
        }


        /// <summary>
        /// Calculate the Mandelbrot set using multiple parallel threads on the CPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="max_iterations"></param>
        public static void CalcCPUParallelForDouble(int[] buffer, int[] display, float[] view, int max_iterations)
        {
            int icnt = display[0];

            Parallel.For( 0, icnt, i =>
            {
                for (int j = 0; j < display[1]; j++)
                {
                    int index = i + j * display[0];  // ILGPU-like index
                    int img_x = index % display[0];
                    int img_y = index / display[0];

                    double x0 = view[0] + img_x * (view[1] - view[0]) / display[0];
                    double y0 = view[2] + img_y * (view[3] - view[2]) / display[1];
                    double x = 0.0f;
                    double y = 0.0f;
                    int iteration = 0;
                    while ((x * x + y * y < 2 * 2) && (iteration < max_iterations))
                    {
                        double xtemp = x * x - y * y + x0;
                        y = 2 * x * y + y0;
                        x = xtemp;
                        iteration += 1;
                    }
                    buffer[index] = iteration;
                }
            });
        }


        /// <summary>
        /// This creates and passes an array to webgl for rendering to the canvas using "2D" webgl interface
        ///
        /// There are two possibilities for showing our result:
        ///
        /// First is the "direct" draw approach where we pass a color map and create an ImageData object
        /// in JavaScript, copying each pixels color to the image data object.
        ///
        /// Second is we generate a compressed PNG image in memory and tell the webgl context
        /// to download the compressed PNG image as a file like any other web page process.
        ///
        /// While not implemented this approach would reduce the server bandwidth consumed
        /// per render by 80% or more as the resulting color map is large when uncompressed.
        /// </summary>
        /// <param name="basicCanvas"></param>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="iterations"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static async Task Draw(BasicCanvas basicCanvas, int[] data, int width, int height, int iterations, Color color)
        {

            byte[] result = new byte[width * height * 4];

            for (int i = 0; i < width * height; i++)
            {
                Color fillColor = color;


                if (data[i] >= iterations)
                {
                    fillColor = color;
                }
                else
                {

                    int red = data[i] * 30 % 256;
                    int green = data[i] * 20 % 256;
                    int blue = data[i] * 50 % 256;

                    fillColor = Color.FromArgb(255, red, green, blue);

                }


                result[i * 4] = fillColor.R;
                result[i * 4 + 1] = fillColor.G;
                result[i * 4 + 2] = fillColor.B;
                result[i * 4 + 3] = fillColor.A;

            }

            await basicCanvas.CreateImageDataCopyByteArray("Mandelbrot", width, height, result);
            await basicCanvas.PutImageData("Mandelbrot", 0, 0);
        }
    }
}
