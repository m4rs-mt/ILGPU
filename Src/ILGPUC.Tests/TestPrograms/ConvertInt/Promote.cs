// Test program: Promote
// Kernel promotes int to long.
// Expected output: 42 42 42 42

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PromoteKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> data, int value)
    {
        data[index] = (long)value;
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

        using var buffer = stream.Allocate1D<long>(4);
        stream.Launch((Index1D)4, index => Kernels.PromoteKernel(index, buffer.View, 42));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
