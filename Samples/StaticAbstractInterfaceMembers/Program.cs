// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace StaticAbstractInterfaceMembers;

// Static abstract interface members allow writing generic kernels
// with different implementations for some functionality.

public interface ICalculatorOperation<T>
    where T : INumber<T>
{
    static abstract T Calculate(T left, T right);
}

public class AdditionOp : ICalculatorOperation<int>
{
    public static int Calculate(int left, int right) => left + right;
}

public struct MultiplyOp : ICalculatorOperation<float>
{
    public static float Calculate(float left, float right) => left * right;
}

static class Kernels
{
    public static void CalculatorKernel<T, TOp>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> input,
        ArrayView1D<T, Stride1D.Dense> output)
        where T : unmanaged, INumber<T>
        where TOp : ICalculatorOperation<T>
    {
        output[index] = TOp.Calculate(input[index], input[index]);
    }
}

static class Program
{
    static void UsingAbstractFunction<T, TOp>(AcceleratorStream stream)
        where T : unmanaged, INumber<T>
        where TOp : ICalculatorOperation<T>
    {
        var values =
            Enumerable.Range(0, 16)
            .Select(x => T.CreateChecked(x))
            .ToArray();

        using var inputBuffer = stream.Allocate1D(values);
        using var outputBuffer = stream.Allocate1D(values);
        outputBuffer.MemSetToZero();

        stream.Launch(
            (Index1D)inputBuffer.Length,
            index => Kernels.CalculatorKernel<T, TOp>(
                index, inputBuffer.View, outputBuffer.View));
        stream.Synchronize();

        var result = outputBuffer.GetAsArray1D();
        for (var i = 0; i < result.Length; i++)
            Console.WriteLine($"[{i}] = {result[i]}");
        Console.WriteLine();
    }

    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        UsingAbstractFunction<int, AdditionOp>(stream);
        UsingAbstractFunction<float, MultiplyOp>(stream);
    }
}
