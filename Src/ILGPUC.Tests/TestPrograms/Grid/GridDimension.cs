// Test program: GridDimension
// Kernel stores Grid.Dimension into data.
// With LoadAutoGroupedStreamKernel(4, ...) on CPU, grid dim depends on runtime.
// Expected output depends on CPU runtime grouping strategy.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GridDimensionKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> data)
    {
        data[index] = Grid.Dimension;
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

        using var buffer = stream.Allocate1D<long>(4);
        stream.Launch((Index1D)4, index => Kernels.GridDimensionKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
