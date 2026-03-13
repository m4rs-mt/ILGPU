// Test program: ForLoop
// Kernel sums 1..5 into each element. Sum = 15.
// Expected output: 15 15 15 15

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ForLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int n)
    {
        int sum = 0;
        for (int i = 1; i <= n; i++)
        {
            sum += i;
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
        stream.Launch((Index1D)4, index => Kernels.ForLoopKernel(index, buffer.View, 5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
