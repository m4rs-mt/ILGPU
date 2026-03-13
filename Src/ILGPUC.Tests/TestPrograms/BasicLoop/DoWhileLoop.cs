// Test program: DoWhileLoop
// Kernel counts down from value using do-while. Count = value.
// Expected output: 5 5 5 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DoWhileLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int count = 0;
        int v = value;
        do
        {
            count++;
            v--;
        } while (v > 0);
        data[index] = count;
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
        stream.Launch((Index1D)4, index => Kernels.DoWhileLoopKernel(index, buffer.View, 5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
