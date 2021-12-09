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

using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Cuda.API;
using System;
using System.Numerics;
using static ILGPU.Runtime.Cuda.CuFFTWConstants;

namespace AlgorithmsCuFFTW
{
    class Program
    {
        static void Main()
        {
            var input = new Complex[8];
            for (var i = 0; i < input.Length; i++)
                input[i] = new Complex(i + 1, 0);

            Console.WriteLine("Input Values:");
            for (var i = 0; i < input.Length; i++)
                Console.WriteLine($"  [{i}] = {input[i]}");

            var cufftw = new CuFFTW();
            DoForwardPlan(cufftw, input, out var output);

            var inverseInput = output;
            DoInversePlan(cufftw.API, inverseInput, out _);
        }

        // Use the CuFFTW wrapper to perform a forward transform.
        static void DoForwardPlan(CuFFTW cufftw, Complex[] input, out Complex[] output)
        {
            output = new Complex[input.Length];

            using var plan = cufftw.Plan1D(
                input.Length,
                input,
                output,
                FFTW_FORWARD,
                FFTW_ESTIMATE);

            plan.Execute();

            Console.WriteLine("Output Values:");
            for (var i = 0; i < output.Length; i++)
                Console.WriteLine($"  [{i}] = {output[i]}");
        }

        // Use the low-level CuFFTW API to perform an inverse transform.
        static void DoInversePlan(CuFFTWAPI api, Complex[] input, out Complex[] output)
        {
            output = new Complex[input.Length];

            var inversePlan = api.fftw_plan_dft_1d(
                input.Length,
                input,
                output,
                FFTW_BACKWARD,
                FFTW_ESTIMATE);
            api.fftw_execute(inversePlan);
            api.fftw_destroy_plan(inversePlan);

            // Scale the output to obtain the inverse.
            for (var i = 0; i < output.Length; i++)
                output[i] /= output.Length;

            Console.WriteLine("Inverse Values:");
            for (var i = 0; i < output.Length; i++)
                Console.WriteLine($"  [{i}] = {output[i].Real}");
        }
    }
}
