// Test program: SharedMemoryArray
// Kernel allocates shared memory array, writes index, barriers, reads neighbor.
// Expected output depends on runtime grouping (groupDim).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SharedMemoryArrayKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int groupIdx = Group.Index;
        int groupDim = Group.Dimension;

        if (groupIdx < 64)
            shared[groupIdx] = index.X;
        Group.Barrier();

        if (groupIdx < 64)
        {
            int next = (groupIdx + 1) % groupDim;
            if (next < 64)
                data[index] = shared[next];
            else
                data[index] = shared[groupIdx];
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
        stream.Launch((Index1D)4, index => Kernels.SharedMemoryArrayKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
