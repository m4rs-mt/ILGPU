// Test program: Length
// Kernel stores array view length into result.
// Expected output: 8 8 8 8

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LengthKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        result[index] = data.Length;
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

        using var data = stream.Allocate1D<int>(8);
        using var result = stream.Allocate1D<long>(4);
        stream.Launch((Index1D)4, index => Kernels.LengthKernel(index, data.View, result.View));
        stream.Synchronize();

        var output = result.GetAsArray1D();
        foreach (var v in output)
            Console.WriteLine(v);
    }
}
