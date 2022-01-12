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
using System;
using System.IO;
using System.Reflection;

namespace AlgorithmsNvJpeg
{
    class Program
    {
        static unsafe void Main()
        {
            // Load the sample image.
            var executablePath = Assembly.GetExecutingAssembly().Location;
            var basePath = Path.GetDirectoryName(executablePath);
            var imagePath = Path.Combine(basePath, "Resources", "sample.jpg");
            var imageBytes = File.ReadAllBytes(imagePath);

            // Create Cuda accelerator.
            using var context = Context.CreateDefault();
            using var accelerator = context.CreateCudaAccelerator(0);

            // Create an NvJPEG library instance.
            var nvjpeg = new NvJpeg();
            using var nvjpegLib = nvjpeg.CreateSimple();

            // Retrieve information from the JPEG image.
            NvJpegException.ThrowIfFailed(
                nvjpegLib.GetImageInfo(
                    imageBytes,
                    out int numComponents,
                    out NvJpegChromaSubsampling subsampling,
                    out var widths,
                    out var heights));
            Console.WriteLine($"Image has {numComponents} channels.");

            for (var i = 0; i < numComponents; i++)
                Console.WriteLine($"Channel #{i}: {widths[i]} x {heights[i]}");

            switch (subsampling)
            {
                case NvJpegChromaSubsampling.NVJPEG_CSS_444:
                    Console.WriteLine("YUV 4:4:4 chroma subsampling");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_440:
                    Console.WriteLine("YUV 4:4:0 chroma subsampling");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_422:
                    Console.WriteLine("YUV 4:2:2 chroma subsampling");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_420:
                    Console.WriteLine("YUV 4:2:0 chroma subsampling");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_411:
                    Console.WriteLine("YUV 4:1:1 chroma subsampling");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_410:
                    Console.WriteLine("YUV 4:1:0 chroma subsampling");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_GRAY:
                    Console.WriteLine("Grayscale JPEG");
                    break;
                case NvJpegChromaSubsampling.NVJPEG_CSS_UNKNOWN:
                default:
                    Console.WriteLine("Unknown chroma subsampling");
                    break;
            }

            // Allocate output buffer for decoding the image into BGR format.
            // Uses 3 channels of equal size.
            var outputFormat = NvJpegOutputFormat.NVJPEG_OUTPUT_BGR;
            var outputWidth = widths[0];
            var outputHeight = heights[0];

            var outputImage = NvJpegImage.Create(
                accelerator,
                outputWidth,
                outputHeight,
                numComponents);

            // Decode the JPEG image.
            using var jpegState = nvjpegLib.CreateState();

            NvJpegException.ThrowIfFailed(
                nvjpegLib.Decode(
                    jpegState,
                    imageBytes,
                    outputFormat,
                    outputImage));
            accelerator.Synchronize();
        }
    }
}
