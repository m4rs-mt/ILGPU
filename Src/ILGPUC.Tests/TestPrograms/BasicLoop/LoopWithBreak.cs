// Test program: LoopWithBreak
// Kernel sums loop counter until sum exceeds 10, then breaks. Sum = 15.
// Expected output: 15 15 15 15

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LoopWithBreakKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += i;
            if (sum > 10)
                break;
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
        stream.Launch((Index1D)4, index => Kernels.LoopWithBreakKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
