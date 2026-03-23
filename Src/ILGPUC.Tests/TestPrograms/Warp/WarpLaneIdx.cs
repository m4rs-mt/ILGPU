// Test program: WarpLaneIdx
// Kernel stores Warp.LaneIndex into data.
// Expected output depends on CPU runtime warp configuration.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpLaneIdxKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Warp.LaneIndex;
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
        stream.Launch((Index1D)4, index => Kernels.WarpLaneIdxKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
