// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedOperandChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: BinaryIntOp shared operand chain
// Kernel hashes every element with 64 rounds of x = x * 31 + 7; x ^= x >> 3.
// Every round reads its intermediate value twice.
// Input: [0, 1, 12345, 4294967295]
// Expected output: the same hashes computed on the host

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SharedOperandChainKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> input,
        ArrayView1D<uint, Stride1D.Dense> output)
    {
        uint x = input[index];
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        x = x * 31 + 7; x ^= x >> 3;
        output[index] = x;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        uint[] inputData = [0, 1, 12345, 4294967295];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<uint>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.SharedOperandChainKernel(index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
