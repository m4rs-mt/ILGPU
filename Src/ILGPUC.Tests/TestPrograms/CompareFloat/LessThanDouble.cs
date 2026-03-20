// Test program: CompareFloat LessThan<double>
// Kernel compares a < b element-wise.
// a: [1.0, 5.0, 3.0, 7.0], b: [2.0, 4.0, 6.0, 8.0] → result: [1, 0, 1, 1]
// Expected output: 1 0 1 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LessThanKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> a,
        ArrayView1D<double, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = (a[index] < b[index]) ? 1 : 0;
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

        double[] aData = [1.0, 5.0, 3.0, 7.0];
        double[] bData = [2.0, 4.0, 6.0, 8.0];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.LessThanKernel(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
