// Test program: ReinterpretCast UInt32ToFloat
// Kernel reinterprets uint bits as float.
// 1065353216 = 0x3F800000 = 1.0f
// Expected output: 1 1 1 1

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UInt32ToFloatKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> input,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        uint val = input[index];
        output[index] = Unsafe.As<uint, float>(ref val);
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

        uint[] inputData = [1065353216u, 1065353216u, 1065353216u, 1065353216u];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<float>(4);
        stream.Launch((Index1D)4, index => Kernels.UInt32ToFloatKernel(index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((int)v);
    }
}
