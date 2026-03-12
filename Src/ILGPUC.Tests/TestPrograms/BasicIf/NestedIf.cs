// Test program: NestedIf
// Kernel writes 3 if value>20, 2 if value>10, else 1.
// Expected output: 3 3 3 3 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NestedIfKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        if (value > 10)
        {
            if (value > 20)
                data[index] = 3;
            else
                data[index] = 2;
        }
        else
        {
            data[index] = 1;
        }
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
        stream.Launch((Index1D)4, index => Kernels.NestedIfKernel(index, buffer.View, 25));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
