// Test program: IfAndOr
// Kernel writes 1 if both a>0 and b>0, 2 if either a>0 or b>0, else 0.
// Expected output: 1 1 1 1 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IfAndOrKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a,
        int b)
    {
        if (a > 0 && b > 0)
            data[index] = 1;
        else if (a > 0 || b > 0)
            data[index] = 2;
        else
            data[index] = 0;
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
        stream.Launch((Index1D)4, index => Kernels.IfAndOrKernel(index, buffer.View, 3, 5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
