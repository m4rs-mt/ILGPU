// Test program: CompareInt GreaterEqual<int>
// Kernel compares a >= b element-wise.
// a: [5, 4, 7, 8], b: [2, 4, 6, 8] → result: [1, 1, 1, 1]
// Expected output: 1 1 1 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GreaterEqualKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> a,
        ArrayView1D<int, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = (a[index] >= b[index]) ? 1 : 0;
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

        int[] aData = [5, 4, 7, 8];
        int[] bData = [2, 4, 6, 8];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.GreaterEqualKernel(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
