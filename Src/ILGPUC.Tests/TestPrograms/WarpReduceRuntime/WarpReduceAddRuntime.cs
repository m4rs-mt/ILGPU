// Test program: WarpReduceAddRuntime
// Input read from a runtime buffer.
// 4 threads, input = [1, 2, 3, 4], Reduce(+) → lane 0 gets 10.
// Lane 0 writes the reduced sum; other lanes write -1 as a sentinel.
// Expected output: 10 -1 -1 -1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpReduceAddRuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int result = Warp.Reduce(value, (a, b) => a + b);
        output[index] = Warp.IsFirstLane ? result : -1;
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

        int[] inputData = [1, 2, 3, 4];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.WarpReduceAddRuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
