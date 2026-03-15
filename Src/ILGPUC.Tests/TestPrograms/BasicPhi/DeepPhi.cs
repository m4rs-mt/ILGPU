// Test program: DeepPhi
// Kernel with deep nesting requiring multiple phi nodes.
// value=250: > 100, > 200, !> 300 → result = 3
// Expected output: 3 3 3 3

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DeepPhiKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result;
        if (value > 100)
        {
            if (value > 200)
            {
                if (value > 300)
                    result = 4;
                else
                    result = 3;
            }
            else
            {
                result = 2;
            }
        }
        else
        {
            result = 1;
        }

        data[index] = result;
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
        stream.Launch((Index1D)4, index => Kernels.DeepPhiKernel(index, buffer.View, 250));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
