// Test program: InlineExpressionBody
// Inline lambda with expression body (no braces).
// Computes: output[i] = i + offset
// offset=100
// Expected output: 100 101 102 103 (one per line)

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
        int offset = 100;

        stream.Launch((Index1D)4, index => view[index] = index + offset);
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
