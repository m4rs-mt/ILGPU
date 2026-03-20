// Test program: CompareFloat NotEqual<float>
// Kernel compares a != b element-wise.
// a: [1.0, 2.0, 3.0, 4.0], b: [1.0, 5.0, 3.0, 8.0] → result: [0, 1, 0, 1]
// Expected output: 0 1 0 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NotEqualKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = (a[index] != b[index]) ? 1 : 0;
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

        float[] aData = [1.0f, 2.0f, 3.0f, 4.0f];
        float[] bData = [1.0f, 5.0f, 3.0f, 8.0f];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.NotEqualKernel(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
