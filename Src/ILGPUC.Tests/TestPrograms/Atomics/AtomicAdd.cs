// Test program: AtomicAdd
// 4 threads atomically add 1 to data[0].
// Expected output: 4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicAddKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Add(ref data[0], value);
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

        using var buffer = stream.Allocate1D<int>(1);
        buffer.MemSetToZero();
        stream.Launch((Index1D)4, index => Kernels.AtomicAddKernel(index, buffer.View, 1));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine(data[0]);
    }
}
