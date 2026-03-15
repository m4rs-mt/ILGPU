// Test program: Index2D
// Kernel uses Index2D to compute linear index and store encoded position.
// Expected output: 0 1000 1 1001

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Index2DKernel(
        Index2D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int width)
    {
        int linear = index.X + index.Y * width;
        data[linear] = index.X + index.Y * 1000;
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
        stream.Launch(new Index2D(2, 2), index => Kernels.Index2DKernel(index, buffer.View, 2));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
