// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace AdvancedViews;

struct ComposedStructure
{
    public static readonly int ElementCounterOffset =
        Interop.OffsetOf<ComposedStructure>(nameof(ElementCounter));

    public short SomeElement;
    public byte SomeOtherElement;
    public int ElementCounter;

    public ComposedStructure(
        short someElement,
        byte someOtherElement,
        int elementCounter)
    {
        SomeElement = someElement;
        SomeOtherElement = someOtherElement;
        ElementCounter = elementCounter;
    }
}

static class Kernels
{
    public static void MyKernel(
        Index1D index,
        ArrayView<int> elements,
        ArrayView<ComposedStructure> view,
        int comparisonValue)
    {
        var element = elements[index];
        if (element == comparisonValue)
        {
            // Cast the struct view to bytes, then to int at the field offset
            var byteView = view.Cast<byte>();
            int byteOffset = ComposedStructure.ElementCounterOffset;
            var intView = byteView.SubView(byteOffset).Cast<int>();
            Atomic.Add(ref intView[0], 1);
        }
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
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        const int Length = 1024;
        using var elementsBuffer = stream.Allocate1D<int>(Length);
        using var composedStructBuffer = stream.Allocate1D<ComposedStructure>(1);
        elementsBuffer.MemSetToZero();
        composedStructBuffer.MemSetToZero();

        stream.Launch(
            (Index1D)Length,
            index => Kernels.MyKernel(
                index, elementsBuffer.View, composedStructBuffer.View, 0));
        stream.Synchronize();

        var results = composedStructBuffer.GetAsArray1D();
        ComposedStructure composedResult = results[0];
        Console.WriteLine("Composed.SomeElement = " + composedResult.SomeElement);
        Console.WriteLine("Composed.SomeOtherElement = " + composedResult.SomeOtherElement);
        Console.WriteLine("Composed.ElementCounter = " + composedResult.ElementCounter);
    }
}
