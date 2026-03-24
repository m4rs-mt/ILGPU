// Test program: WarpRadixSortDescendingInt32Runtime
// Input read from a runtime buffer so the optimizer cannot fold.
// Warp.RadixSort requires the full warp to participate, so the launch
// size adapts to the accelerator's warp size (8 on CPU, 32 on Metal).
// The 8 real values are padded with int.MinValue which sorts to the end
// in descending order.
// Expected output (first 8): 8 7 6 5 4 3 2 1

using System;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpRadixSortDescendingInt32RuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int sorted = Warp.RadixSort<int, DescendingInt32>(value);
        output[index] = sorted;
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

        int ws = Math.Max(accelerator.WarpSize, 8);
        int[] inputData = new int[ws];
        inputData[0] = 7; inputData[1] = 3; inputData[2] = 5; inputData[3] = 1;
        inputData[4] = 8; inputData[5] = 2; inputData[6] = 6; inputData[7] = 4;
        for (int i = 8; i < ws; i++)
            inputData[i] = int.MinValue;

        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(ws);
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortDescendingInt32RuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        for (int i = 0; i < 8; i++)
            Console.WriteLine(data[i]);
    }
}
