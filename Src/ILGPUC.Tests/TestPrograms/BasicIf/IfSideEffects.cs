// Test program: IfSideEffects
// Kernel writes 1 if index > threshold, else 0. Threshold = 1, 4 elements.
// Expected output: 0 0 1 1 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IfSideEffectsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int threshold)
    {
        data[index] = index.X > threshold ? 1 : 0;
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
        stream.Launch((Index1D)4, index => Kernels.IfSideEffectsKernel(index, buffer.View, 1));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
