// Test program: WhileLoop
// Kernel sums 0..9 into each element. Sum = 45.
// Expected output: 45 45 45 45

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WhileLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        int i = 0;
        while (i < 10)
        {
            sum += i;
            i++;
        }
        data[index] = sum;
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
        stream.Launch((Index1D)4, index => Kernels.WhileLoopKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
