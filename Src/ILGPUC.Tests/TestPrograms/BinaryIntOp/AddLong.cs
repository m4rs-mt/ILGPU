// Test program: BinaryIntOp Add<long>
// Kernel adds two arrays element-wise.
// Input a: [1, 2, 3, 4], b: [10, 20, 30, 40]
// Expected output: 11 22 33 44

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AddKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] + b[index];
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

        long[] aData = [1, 2, 3, 4];
        long[] bData = [10, 20, 30, 40];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<long>(4);
        stream.Launch((Index1D)4, index => UNKNOWN(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
