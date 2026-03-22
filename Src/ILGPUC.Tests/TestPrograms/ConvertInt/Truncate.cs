// Test program: Truncate
// Kernel truncates long to int.
// Expected output: 42 42 42 42

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void TruncateKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, long value)
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
        stream.Launch((Index1D)4, index => Kernels.TruncateKernel(index, buffer.View, 42L));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
