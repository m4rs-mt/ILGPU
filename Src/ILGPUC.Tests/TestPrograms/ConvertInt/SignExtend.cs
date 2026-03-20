// Test program: SignExtend
// Kernel sign-extends sbyte to int.
// Expected output: -10 -10 -10 -10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SignExtendKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, sbyte value)
    {
        data[index] = (int)value;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.SignExtendKernel(index, buffer.View, (sbyte)(-10)));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
