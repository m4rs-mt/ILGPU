// Test program: WarpInclusiveScanAddRuntime
// Input read from a runtime buffer.
// 4 threads, input = [1, 2, 3, 4].
// InclusiveScan(+): lane 0 → 1, lane 1 → 3, lane 2 → 6, lane 3 → 10.
// Expected output: 1 3 6 10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpInclusiveScanAddRuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int result = Warp.InclusiveScan(value, (a, b) => a + b);
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

        int[] inputData = [1, 2, 3, 4];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.WarpInclusiveScanAddRuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
