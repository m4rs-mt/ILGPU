// Test program: IntToByte
// Kernel truncates int to byte.
// 300 & 0xFF = 44
// Expected output: 44 44 44 44

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IntToByteKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        byte truncated = (byte)value;
        data[index] = (int)truncated;
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
        stream.Launch((Index1D)4, index => Kernels.IntToByteKernel(index, buffer.View, 300));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
