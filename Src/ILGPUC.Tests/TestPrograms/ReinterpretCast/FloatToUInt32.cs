// Test program: ReinterpretCast FloatToUInt32
// Kernel reinterprets float bits as uint.
// 1.0f = 0x3F800000 = 1065353216
// Expected output: 1065353216 1065353216 1065353216 1065353216

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void FloatToUInt32Kernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> input,
        ArrayView1D<uint, Stride1D.Dense> output)
    {
        float val = input[index];
        output[index] = Unsafe.As<float, uint>(ref val);
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

        float[] inputData = [1.0f, 1.0f, 1.0f, 1.0f];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<uint>(4);
        stream.Launch((Index1D)4, index => Kernels.FloatToUInt32Kernel(index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
