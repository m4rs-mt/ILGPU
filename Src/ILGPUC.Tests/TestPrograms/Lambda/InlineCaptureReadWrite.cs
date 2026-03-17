// Test program: InlineCaptureReadWrite
// Inline lambda captures two views. First populates source, then
// a second kernel reads from source and writes to dest.
// Computes:
//   source[i] = (i + 1) * 10
//   dest[i] = source[i] + i
// Expected output: 10 21 32 43 (one per line)

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

        using var sourceBuffer = stream.Allocate1D<int>(4);
        using var destBuffer = stream.Allocate1D<int>(4);

        // First kernel: populate source
        var sourceView = sourceBuffer.View;
        stream.Launch((Index1D)4, index =>
        {
            sourceView[index] = (index + 1) * 10;
        });
        stream.Synchronize();

        // Second kernel: read from source, write to dest
        var destView = destBuffer.View;
        stream.Launch((Index1D)4, index =>
        {
            destView[index] = sourceView[index] + index;
        });
        stream.Synchronize();

        var data = destBuffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
