// Test program: ValueTuple TuplePass
// Kernel passes values through a helper that returns a tuple.
// Index 0..3: intOutput = [1, 2, 3, 4], floatOutput = [1, 2, 3, 4]
// Expected output (interleaved): 1 1 2 2 3 3 4 4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static (int, float) CreateTuple(int a, float b)
    {
        return (a + 1, b + 1.0f);
    }

    public static void TuplePassKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> intOutput,
        ArrayView1D<float, Stride1D.Dense> floatOutput)
    {
        var tuple = CreateTuple(index.X, (float)index.X);
        intOutput[index] = tuple.Item1;
        floatOutput[index] = tuple.Item2;
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

        using var intBuf = stream.Allocate1D<int>(4);
        using var floatBuf = stream.Allocate1D<float>(4);
        stream.Launch((Index1D)4, index => Kernels.TuplePassKernel(index, intBuf.View, floatBuf.View));
        stream.Synchronize();

        var intData = intBuf.GetAsArray1D();
        var floatData = floatBuf.GetAsArray1D();
        for (int i = 0; i < 4; i++)
        {
            Console.WriteLine(intData[i]);
            Console.WriteLine((int)floatData[i]);
        }
    }
}
