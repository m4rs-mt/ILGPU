using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleStructures;

readonly struct CustomDataType
{
    public CustomDataType(int value)
    {
        First = value;
        Second = value * value;
    }

    public int First { get; }
    public int Second { get; }
}

static class Kernels
{
    public static void MyKernel(
        Index1D index,
        ArrayView1D<CustomDataType, Stride1D.Dense> dataView)
    {
        dataView[index] = new CustomDataType(index);
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Performing operations on {accelerator}");
        var stream = accelerator.DefaultStream;

        const int Length = 1024;
        using var buffer = stream.Allocate1D<CustomDataType>(Length);

        stream.Launch(
            (Index1D)Length,
            index => Kernels.MyKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].First != i || data[i].Second != i * i)
                Console.WriteLine($"Error at element {i}: First={data[i].First}, Second={data[i].Second}");
        }

        Console.WriteLine("Done.");
    }
}
