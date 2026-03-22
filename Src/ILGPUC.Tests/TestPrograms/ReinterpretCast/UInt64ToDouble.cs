// Test program: ReinterpretCast UInt64ToDouble
// Kernel reinterprets ulong bits as double.
// 4607182418800017408 = 0x3FF0000000000000 = 1.0
// Expected output: 1 1 1 1

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UInt64ToDoubleKernel(
        Index1D index,
        ArrayView1D<ulong, Stride1D.Dense> input,
        ArrayView1D<double, Stride1D.Dense> output)
    {
        ulong val = input[index];
        output[index] = Unsafe.As<ulong, double>(ref val);
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

        ulong[] inputData = [4607182418800017408uL, 4607182418800017408uL, 4607182418800017408uL, 4607182418800017408uL];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<double>(4);
        stream.Launch((Index1D)4, index => Kernels.UInt64ToDoubleKernel(index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((int)v);
    }
}
