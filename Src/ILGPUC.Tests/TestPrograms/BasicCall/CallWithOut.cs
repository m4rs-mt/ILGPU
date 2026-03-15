// Test program: CallWithOut
// Kernel calls helper with out parameter: ComputeOut(10, 20, out result) → result = 30.
// Expected output: 30 30 30 30

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static void ComputeOut(int a, int b, out int result)
    {
        result = a + b;
    }

    public static void CallWithOutKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        ComputeOut(a, b, out int result);
        data[index] = result;
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
        stream.Launch((Index1D)4, index => Kernels.CallWithOutKernel(index, buffer.View, 10, 20));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
