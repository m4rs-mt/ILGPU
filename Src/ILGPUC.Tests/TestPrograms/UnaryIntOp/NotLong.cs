// Test program: NotLong
// Kernel bitwise-NOTs longs.
// Input: [0L,1L,2L,3L], Expected output: -1 -2 -3 -4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NotKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        result[index] = ~input[index];
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

        var inputData = new long[] { 0L, 1L, 2L, 3L };
        using var inputBuf = stream.Allocate1D(inputData);
        using var resultBuf = stream.Allocate1D<long>(4);

        stream.Launch((Index1D)4, index => Kernels.NotKernel(index, inputBuf.View, resultBuf.View));
        stream.Synchronize();

        var data = resultBuf.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
