// Test program: AtomicMax
// 4 threads atomically max with data[0], value=99.
// Expected output: 99

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicMaxKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Max(ref data[0], value);
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
        stream.Launch((Index1D)4, index => Kernels.AtomicMaxKernel(index, buffer.View, 99));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine(data[0]);
    }
}
