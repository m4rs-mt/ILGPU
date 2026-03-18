// Test program: BinaryIntOp And<long>
// Kernel bitwise-ANDs two arrays element-wise.
// Input a: [255, 240, 15, 85], b: [15, 15, 255, 170]
// Expected output: 15 0 15 0

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AndKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>, IBitwiseOperators<T, T, T>
    {
        result[index] = a[index] & b[index];
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

        long[] aData = [255, 240, 15, 85];
        long[] bData = [15, 15, 255, 170];
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
