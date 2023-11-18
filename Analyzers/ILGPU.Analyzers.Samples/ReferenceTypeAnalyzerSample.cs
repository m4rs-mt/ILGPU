// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReferenceTypeAnalyzerSample.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Analyzers.Samples;

record ReferenceType(int X);

static partial class ReferenceTypeAnalyzerSample
{
    // Works with partial methods
    static partial void ReferenceTypeKernel(Index1D index, ArrayView<int> dataView);
}

static partial class ReferenceTypeAnalyzerSample
{
    static partial void ReferenceTypeKernel(Index1D index, ArrayView<int> dataView)
    {
        // Analyzer should produce an error here
        ReferenceType type = new ReferenceType(dataView[index]);
        dataView[index] = type.X;
        
        // No error here
        int[] array1 = { 10 };
        
        // But error here
        ReferenceType[] array2 = { };
        
        // Also analyzes any called methods
        dataView[index] = AnExternalMethod();
    }

    static int AnExternalMethod()
    {
        return AnotherExternalMethod();
    }

    static int AnotherExternalMethod()
    {
        return new ReferenceType(10).X;
    }

    static void Main()
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        var kernel =
            accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(
                ReferenceTypeKernel);

        // Also works with lambdas
        var kernel2 = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(
            (index, dataView) =>
            {
                ReferenceType type = new ReferenceType(dataView[index]);
                dataView[index] = type.X;
            });
    }
}
