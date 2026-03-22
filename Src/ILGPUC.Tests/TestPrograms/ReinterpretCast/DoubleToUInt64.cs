// Test program: ReinterpretCast DoubleToUInt64
// Kernel reinterprets double bits as ulong.
// 1.0 = 0x3FF0000000000000 = 4607182418800017408
// Expected output: 4607182418800017408 4607182418800017408 4607182418800017408 4607182418800017408

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DoubleToUInt64Kernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> input,
        ArrayView1D<ulong, Stride1D.Dense> output)
    {
        double val = input[index];
        output[index] = Unsafe.As<double, ulong>(ref val);
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

        double[] inputData = [1.0, 1.0, 1.0, 1.0];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<ulong>(4);
        stream.Launch((Index1D)4, index => Kernels.DoubleToUInt64Kernel(index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
