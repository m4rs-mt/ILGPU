// Test program: AccumulateClosure
// Lambda writes to a captured variable (sum) twice.
// sum += index; sum += index+1; data[index] = sum
// Expected output: 1 3 5 7 (one per line)
//   index=0: sum = 0+0+1 = 1
//   index=1: sum = 0+1+2 = 3
//   index=2: sum = 0+2+3 = 5
//   index=3: sum = 0+3+4 = 7

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AccumulateClosureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        Func<int, int> add = x => { sum += x; return sum; };
        add(index);
        add(index + 1);
        data[index] = sum;
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
        stream.Launch((Index1D)4, index => Kernels.AccumulateClosureKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
