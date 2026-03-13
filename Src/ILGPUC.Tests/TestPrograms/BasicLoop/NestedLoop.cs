// Test program: NestedLoop
// Kernel computes sum of i*4+j for i,j in 0..3. Sum = 120.
// Expected output: 120 120 120 120

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NestedLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                sum += i * 4 + j;
            }
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
        stream.Launch((Index1D)4, index => Kernels.NestedLoopKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
