// Test program: CompareInt GreaterThan<long>
// Kernel compares a > b element-wise.
// a: [5, 1, 7, 3], b: [2, 4, 6, 8] → result: [1, 0, 1, 0]
// Expected output: 1 0 1 0

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GreaterThanKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> a,
        ArrayView1D<long, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = (a[index] > b[index]) ? 1 : 0;
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

        long[] aData = [5L, 1L, 7L, 3L];
        long[] bData = [2L, 4L, 6L, 8L];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.GreaterThanKernel(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
