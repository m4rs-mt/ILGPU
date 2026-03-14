// Test program: Goto
// Kernel skips assignment when value > 5. value=3 → result = 3*2 = 6.
// Expected output: 6 6 6 6

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GotoKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result = 0;
        if (value > 5)
            goto done;
        result = value * 2;
    done:
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
        stream.Launch((Index1D)4, index => Kernels.GotoKernel(index, buffer.View, 3));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
