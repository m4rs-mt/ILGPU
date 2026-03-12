// Test program: IfChain
// Kernel writes 10/20/30/40 depending on value==0/1/2/else. Value=2.
// Expected output: 30 30 30 30 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IfChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        if (value == 0)
            data[index] = 10;
        else if (value == 1)
            data[index] = 20;
        else if (value == 2)
            data[index] = 30;
        else
            data[index] = 40;
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
        stream.Launch((Index1D)4, index => Kernels.IfChainKernel(index, buffer.View, 2));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
