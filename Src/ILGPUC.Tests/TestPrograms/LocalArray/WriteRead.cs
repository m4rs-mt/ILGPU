// Test program: LocalArray WriteRead
// Kernel allocates local int[4], fills with constants, copies to output.
// Expected output: 10 20 30 40

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WriteReadKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[4];
        local[0] = 10; local[1] = 20; local[2] = 30; local[3] = 40;
        output[index] = local[index];
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
        stream.Launch((Index1D)4, index => Kernels.WriteReadKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
