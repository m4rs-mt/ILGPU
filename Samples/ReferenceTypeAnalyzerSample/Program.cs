// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ManagedTypeAnalyzerSample;

record ReferenceType(int X);

static class Program
{
    static void ReferenceTypeKernel(Index1D index, ArrayView<int> dataView)
    {
        // TODO: sample for arrays (also need tests in general)
        // TODO: should errors trace back to the point of creation?
        // Analyzer should produce an error here
        ReferenceType type = new ReferenceType(dataView[index]);
        dataView[index] = type.X;
    }

    static void Main()
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        var kernel =
            accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(
                ReferenceTypeKernel);

        using var buffer = accelerator.Allocate1D<int>(128);
        buffer.MemSetToZero();

        kernel((int)buffer.Length, buffer.View);

        accelerator.Synchronize();

        int[] a = new int[128];
        buffer.CopyToCPU(a);
    }
}
