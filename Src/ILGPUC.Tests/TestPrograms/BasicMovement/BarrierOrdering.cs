// Test program: BarrierOrdering
// Kernel writes index*2 to data, barriers, then stores data[index]+1 to result.
// Expected output: 1 3 5 7

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void BarrierOrderingKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        data[index] = index.X * 2;
        Group.Barrier();
        result[index] = data[index] + 1;
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

        using var dataBuf = stream.Allocate1D<int>(4);
        using var resultBuf = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.BarrierOrderingKernel(index, dataBuf.View, resultBuf.View));
        stream.Synchronize();

        var data = resultBuf.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
