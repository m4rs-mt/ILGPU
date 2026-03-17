// Test program: InlineCaptureMultipleScalars
// Inline lambda captures multiple scalar values (int, int).
// Computes: output[i] = i * a + b
// a=3, b=7
// Expected output: 7 10 13 16 (one per line)

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
        int a = 3;
        int b = 7;

        stream.Launch((Index1D)4, index =>
        {
            view[index] = index * a + b;
        });
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
