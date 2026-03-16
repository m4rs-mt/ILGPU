// Test program: AtomicMin
// 4 threads atomically min with data[0] (init=100), value=5.
// Expected output: 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicMinKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Min(ref data[0], value);
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

        int[] init = [100];
        using var buffer = stream.Allocate1D(init);
        stream.Launch((Index1D)4, index => Kernels.AtomicMinKernel(index, buffer.View, 5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine(data[0]);
    }
}
