using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace ProfilingMarkers;

static class Kernels
{
    public static void ProfiledKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int result = 0;
        for (var i = index; i < input.Length; i++)
            result += input[i];

        output[index] = result;
    }
}

static class Program
{
    static void Main()
    {
        // Enable profiling via the context builder
        using var context = Context.Create(builder => builder.Default().Profiling());

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
        Console.WriteLine($"Using {accelerator}");

        // Use a dedicated stream for profiling
        using var stream = accelerator.CreateStream();

        var input = Enumerable.Range(0, 4096).ToArray();
        using var inputBuffer = stream.Allocate1D(input);
        using var outputBuffer = stream.Allocate1D<int>(input.Length);

        // Add a profiling marker before the kernel
        using var startMarker = stream.AddProfilingMarker();

        // Launch the kernel
        stream.Launch(
            (Index1D)input.Length,
            index => Kernels.ProfiledKernel(index, inputBuffer.View, outputBuffer.View));

        // Add a profiling marker after the kernel
        using var endMarker = stream.AddProfilingMarker();

        // Measure elapsed time between markers (implicitly synchronizes)
        TimeSpan elapsedTime = endMarker.MeasureFrom(startMarker);

        Console.WriteLine(
            $"Elapsed time: {(int)elapsedTime.TotalMilliseconds}ms");
    }
}
