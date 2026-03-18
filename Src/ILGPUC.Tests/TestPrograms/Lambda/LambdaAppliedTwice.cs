// Test program: LambdaAppliedTwice
// Lambda (shift = x + offset) is called twice per element: shift(x) + shift(x+1), offset=5.
// Expected output: 11 13 15 17 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LambdaAppliedTwiceKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int offset)
    {
        int off = offset;
        Func<int, int> shift = x => x + off;
        data[index] = shift(index.X) + shift(index.X + 1);
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
        stream.Launch((Index1D)4, index => Kernels.LambdaAppliedTwiceKernel(index, buffer.View, 5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
