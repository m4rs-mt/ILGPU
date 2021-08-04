using ILGPU;
using ILGPU.Runtime;
using System;
using System.Linq;

namespace ProfilingMarkers
{
    class Program
    {
        static void ProfiledKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> input,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            int result = 0;
            for (var i = index; i < input.Length; i++)
                result += input[i];

            output[index] = result;
        }

        static void Main()
        {
            // Create default context and enable profiling.
            // For GPU accelerators, Profiling Markers provide better accuracy than
            // using the .NET Stopwatch class.
            using var context = Context.Create(builder => builder.Default().Profiling());

            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                using var stream = accelerator.CreateStream();
                var kernel = accelerator.LoadAutoGroupedKernel<
                    Index1D,
                    ArrayView1D<int, Stride1D.Dense>,
                    ArrayView1D<int, Stride1D.Dense>>(
                        ProfiledKernel);

                var input = Enumerable.Range(0, 4096).ToArray();
                using var inputBuffer = accelerator.Allocate1D(input);
                using var outputBuffer = accelerator.Allocate1D<int>(input.Length);

                // Add a profiling marker into the same stream as the kernel.
                using var startMarker = stream.AddProfilingMarker();

                // Launch the kernel to be profiled.
                kernel(
                    stream,
                    (int)inputBuffer.Length,
                    inputBuffer.View,
                    outputBuffer.View);

                // Enqueue another profiling marker after the kernel.
                using var endMarker = stream.AddProfilingMarker();

                // Measure the time between the markers, which measures the elasped time
                // of the kernel.
                // NB: Implicitly synchronizes the stream until the marker is processed.
                TimeSpan elapsedTime = endMarker.MeasureFrom(startMarker);

                // Alternatively, use:
                elapsedTime = endMarker - startMarker;

                Console.WriteLine(
                    $"Elapsed time: {(int)elapsedTime.TotalMilliseconds}ms");
            }
        }
    }
}
