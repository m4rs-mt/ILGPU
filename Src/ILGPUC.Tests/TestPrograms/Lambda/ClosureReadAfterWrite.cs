// Test program: ClosureReadAfterWrite
// Two lambdas share a captured variable (state).
// write(read()) doubles the state, then read() returns it.
// Expected output: 0 2 4 6 (one per line)
//   index=0: state=0, write(0) → state=0, read()=0
//   index=1: state=1, write(1) → state=2, read()=2
//   index=2: state=2, write(2) → state=4, read()=4
//   index=3: state=3, write(3) → state=6, read()=6

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ClosureReadAfterWriteKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int state = index;
        Func<int> read = () => state;
        Action<int> write = x => { state = x * 2; };
        write(read());
        data[index] = read();
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
        stream.Launch((Index1D)4, index => Kernels.ClosureReadAfterWriteKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
