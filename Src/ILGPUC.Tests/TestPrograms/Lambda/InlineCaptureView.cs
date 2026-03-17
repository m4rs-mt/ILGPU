// Test program: InlineCaptureView
// Inline lambda captures an ArrayView and writes thread index to it.
// Expected output: 0 1 2 3 (one per line)

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
        stream.Launch((Index1D)4, index => { view[index] = index; });
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
