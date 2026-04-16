using System;
using System.Diagnostics;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace SimpleViews;

static class Program
{
    static void UnsafeAccess(ArrayView<int> view)
    {
        var doubleView = view.Cast<double>();
        Debug.Assert(doubleView.Length * sizeof(double) == view.Length * sizeof(int));
        doubleView[0] = double.NaN;

        var byteView = view.Cast<byte>();
        Debug.Assert(byteView.Length * sizeof(byte) == view.Length * sizeof(int));
        for (int i = 0; i < sizeof(double); ++i)
            Console.WriteLine($"DoubleAsByte[{i}] = {byteView[i]}");
    }

    static void SubViewAccess(ArrayView<int> view)
    {
        var subView = view.SubView(10);
        Debug.Assert(subView.Length == view.Length - 10);

        subView[0] = 42;
        Console.WriteLine($"Value of sub view at index 0: {subView[0]} = value of view at index 10: {view[10]}");

        var subView2 = view.SubView(10, 20);
        Debug.Assert(subView2.Length == 20);
        Console.WriteLine($"Value of sub view 2 at index 0: {subView2[0]} = value of view at index 10: {view[10]}");

        var subView3 = subView2.SubView(10, 2);
        subView3[1] = 23;
        Debug.Assert(subView3.Length == 2);
        Console.WriteLine($"Value of sub view 3 at index 1: {subView3[1]} = value of view at index 21: {view[21]}");
    }

    static void VariableViewAccess(ArrayView<int> view)
    {
        // Direct element access (VariableView was removed)
        view[view.Length - 1] = 13;
        Debug.Assert(view[view.Length - 1] == 13);
    }

    static void OffsetCopyAccess(ArrayView<int> view)
    {
        var replacementValues = Enumerable.Repeat(42, 256).ToArray();
        view.SubView(0, replacementValues.Length).CopyFromCPU(replacementValues);

        var nextReplacementValues = Enumerable.Repeat(97, 256).ToArray();
        view.SubView(256, nextReplacementValues.Length).CopyFromCPU(nextReplacementValues);

        var fromGPU = view.SubView(128, 256).GetAsArray();
        view.SubView(0, 128).CopyToCPU(fromGPU.AsSpan().Slice(128));
    }

    static void UnsafeVariableViewAccess(ArrayView<int> view)
    {
        // Use Cast<> to reinterpret memory as shorts, then write at offset
        var shortView = view.Cast<short>();
        shortView[1] = short.MaxValue; // offset 1 short = sizeof(short) bytes
        Debug.Assert(view[0] == short.MaxValue << 16);
    }

    static void Main()
    {
        using var context = Context.CreateDefault();

        using var accelerator = context.GetDevice<CPUDevice>(0)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;
        using var buffer = stream.Allocate1D<int>(1024);

        ArrayView<int> bufferView = buffer.View;

        UnsafeAccess(bufferView);
        SubViewAccess(bufferView);
        VariableViewAccess(bufferView);
        UnsafeVariableViewAccess(bufferView);
        OffsetCopyAccess(bufferView);

        Console.WriteLine("Done.");
    }
}
