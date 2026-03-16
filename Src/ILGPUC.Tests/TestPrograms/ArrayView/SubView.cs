// Test program: SubView
// Kernel reads from a sub-view with offset.
// Input: [10, 20, 30, 40], offset=2 → sub=[30, 40]
// Expected output: 30 40 -1 -1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SubViewKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result,
        int offset)
    {
        var sub = data.SubView(offset, data.IntLength - offset);
        if (index.X < sub.IntLength)
            result[index] = sub[index];
        else
            result[index] = -1;
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

        int[] inputData = [10, 20, 30, 40];
        using var data = stream.Allocate1D(inputData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.SubViewKernel(index, data.View, result.View, 2));
        stream.Synchronize();

        var output = result.GetAsArray1D();
        foreach (var v in output)
            Console.WriteLine(v);
    }
}
