// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoadDiscovery.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.ManagedType;

class LoadDiscovery
{
    class RefType
    {
        public int Hello => 42;
    }

    // Separate kernels so we can observe multiple analyses
    static void Kernel1(Index1D index, ArrayView<int> input)
    {
        int result = new RefType().Hello;
    }

    static void Kernel2(Index1D index, ArrayView<int> input)
    {
        int result = new RefType().Hello;
    }

    static void Kernel3(Index1D index, ArrayView<int> input)
    {
        int result = new RefType().Hello;
    }

    static void Kernel4(Index1D index, ArrayView<int> input)
    {
        int result = new RefType().Hello;
    }

    static void Kernel5(Index1D index, ArrayView<int> input)
    {
        int result = new RefType().Hello;
    }

    static void Kernel6(Index1D index, ArrayView<int> input)
    {
        int result = new RefType().Hello;
    }

    static void Run()
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        // Also make sure kernels loaded twice aren't analyzed twice
        var kernel1 = accelerator.LoadStreamKernel<Index1D, ArrayView<int>>(Kernel1);
        var twice = accelerator.LoadStreamKernel<Index1D, ArrayView<int>>(Kernel1);

        var kernel2 =
            accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(Kernel2);
        var kernel3 =
            accelerator.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<int>>(
                Kernel3, 32);

        var kernel4 = accelerator.LoadKernel<Index1D, ArrayView<int>>(Kernel4);
        var kernel5 = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<int>>(Kernel5);
        var kernel6 =
            accelerator.LoadImplicitlyGroupedKernel<Index1D, ArrayView<int>>(Kernel6, 32);
    }
}