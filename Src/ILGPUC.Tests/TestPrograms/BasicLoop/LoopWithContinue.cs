// Test program: LoopWithContinue
// Kernel sums only odd numbers 0..9 using continue. Sum = 25.
// Expected output: 25 25 25 25

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LoopWithContinueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
                continue;
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
        stream.Launch((Index1D)4, index => Kernels.LoopWithContinueKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
