// Test program: LocalArray IndexCompute
// Kernel allocates table[4], fills {100,200,300,400}, indexes by thread.
// Expected output: 100 200 300 400

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IndexComputeKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] table = new int[4];
        table[0] = 100; table[1] = 200; table[2] = 300; table[3] = 400;
        output[index] = table[index.X];
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

        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.IndexComputeKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
