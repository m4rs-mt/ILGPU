// Test program: SwitchStore
// Kernel stores index-dependent values based on switch case. selector=1 → index + 100.
// Expected output: 100 101 102 103

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SwitchStoreKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int selector)
    {
        switch (selector)
        {
            case 0:
                data[index] = index.X * 2;
                break;
            case 1:
                data[index] = index.X + 100;
                break;
            case 2:
                data[index] = index.X * index.X;
                break;
            default:
                data[index] = 0;
                break;
        }
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
        stream.Launch((Index1D)4, index => Kernels.SwitchStoreKernel(index, buffer.View, 1));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
