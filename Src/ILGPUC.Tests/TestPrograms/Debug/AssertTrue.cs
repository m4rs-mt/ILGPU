// Test program: AssertTrue
// Kernel asserts true and writes constant.
// Expected output: 42 42 42 42

using System;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AssertTrueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Debug.Assert(true);
        data[index] = 42;
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
        stream.Launch((Index1D)4, index => Kernels.AssertTrueKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
