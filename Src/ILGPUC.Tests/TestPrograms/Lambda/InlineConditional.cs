// Test program: InlineConditional
// Inline lambda with conditional logic and captured scalar threshold.
// Computes: output[i] = (i > threshold) ? threshold : i
// threshold=2
// Expected output: 0 1 2 2 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(4);
        var view = buffer.View;
        int threshold = 2;

        stream.Launch((Index1D)4, index =>
        {
            view[index] = index > threshold ? threshold : index;
        });
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
