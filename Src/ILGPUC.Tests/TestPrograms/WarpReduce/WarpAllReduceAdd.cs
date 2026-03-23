// Test program: WarpAllReduceAdd
// Each thread contributes index+1, all-reduce with addition.
// For 4 threads: sum = 1+2+3+4 = 10, all lanes get 10.
// Expected output: 10 10 10 10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpAllReduceAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X + 1;
        int result = Warp.AllReduce(value, (a, b) => a + b);
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
        stream.Launch(
            (Index1D)4,
            index => Kernels.WarpAllReduceAddKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
