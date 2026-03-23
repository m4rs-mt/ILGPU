// Test program: WarpAllReduceMaxRuntime
// Input read from a runtime buffer. Uses XMath.Max as a method group.
// 4 threads, input = [3, 1, 4, 2], AllReduce(max) → max = 4 for all lanes.
// Expected output: 4 4 4 4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpAllReduceMaxRuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int result = Warp.AllReduce(value, XMath.Max);
        output[index] = result;
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

        int[] inputData = [3, 1, 4, 2];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.WarpAllReduceMaxRuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
