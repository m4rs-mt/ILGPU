// Test program: IntEnum
// Kernel casts enum to int.
// Expected output: 200 200 200 200

using System;
using ILGPU;
using ILGPU.Runtime;

enum IntEnum { Foo = 100, Bar = 200 }

static class Kernels
{
    public static void IntEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        IntEnum e = IntEnum.Bar;
        data[index] = (int)e;
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
        stream.Launch((Index1D)4, index => Kernels.IntEnumKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
