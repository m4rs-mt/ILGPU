// Test program: IntToFloat
// Kernel converts int to float.
// Expected output: 7 7 7 7

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IntToFloatKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data, int value)
    {
        data[index] = (float)value;
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

        using var buffer = stream.Allocate1D<float>(4);
        stream.Launch((Index1D)4, index => Kernels.IntToFloatKernel(index, buffer.View, 7));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((int)v);
    }
}
