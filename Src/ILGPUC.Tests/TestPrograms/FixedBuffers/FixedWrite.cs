// Test program: FixedBuffers FixedWrite
// Kernel writes value and multiples into a fixed-size buffer.
// value=5 → Data = [5, 10, 15, 20], sum = 50
// Expected output: 50 50 50 50

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct FixedBuffer4
{
    public fixed int Data[4];
}

static class Kernels
{
    public static void FixedWriteKernel(
        Index1D index,
        ArrayView1D<FixedBuffer4, Stride1D.Dense> output,
        int value)
    {
        unsafe
        {
            FixedBuffer4 buf;
            buf.Data[0] = value;
            buf.Data[1] = value * 2;
            buf.Data[2] = value * 3;
            buf.Data[3] = value * 4;
            output[index] = buf;
        }
    }
}

static class ReadKernels
{
    public static void SumKernel(
        Index1D index,
        ArrayView1D<FixedBuffer4, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        unsafe
        {
            FixedBuffer4 buf = input[index];
            output[index] = buf.Data[0] + buf.Data[1] + buf.Data[2] + buf.Data[3];
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

        using var buffers = stream.Allocate1D<FixedBuffer4>(4);
        using var sums = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.FixedWriteKernel(index, buffers.View, 5));
        stream.Synchronize();
        stream.Launch((Index1D)4, index => ReadKernels.SumKernel(index, buffers.View, sums.View));
        stream.Synchronize();

        var data = sums.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
