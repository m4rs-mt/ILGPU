// Test program: GroupBarrier
// Kernel writes index, barriers, then increments.
// Expected output: 1 2 3 4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupBarrierKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X;
        Group.Barrier();
        data[index] = data[index] + 1;
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
        stream.Launch((Index1D)4, index => Kernels.GroupBarrierKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
