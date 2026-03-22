// Test program: ValueTuple TupleCreate
// Kernel creates tuples from index values.
// Index 0..3: intOutput = [0, 2, 4, 6], floatOutput = [0, 3, 6, 9]
// Expected output (interleaved): 0 0 2 3 4 6 6 9

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void TupleCreateKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> intOutput,
        ArrayView1D<float, Stride1D.Dense> floatOutput)
    {
        var tuple = (index.X * 2, index.X * 3.0f);
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
        stream.Launch((Index1D)4, index => Kernels.TupleCreateKernel(index, intBuf.View, floatBuf.View));
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
