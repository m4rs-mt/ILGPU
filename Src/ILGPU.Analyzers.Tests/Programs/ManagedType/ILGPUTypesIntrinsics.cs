// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILGPUTypesIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Algorithms.Random;

namespace ILGPU.Analyzers.Tests.Programs.ManagedType;

class ILGPUTypesIntrinsics
{
    static void Kernel(Index1D index, ArrayView<int> input)
    {
        var a = input.SubView(0, 10);
        int b = a[index];
        int c = Warp.WarpIdx;
        Group.Barrier();

        // Strings should be allowed for this and debug.
        // We will be conservative in our analysis and ignore all strings.
        CudaAsm.Emit("");

        // Algorithms should also be allowed.
        // RNGView is just an example of a managed type in Algorithms.
        var rngView = new RNGView<XorShift128>();
        rngView.Next();
    }

    static void Run()
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        using var input = accelerator.Allocate1D<int>(1024);

        var kernel =
            accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(Kernel);

        kernel(input.IntExtent, input.View);

        accelerator.Synchronize();
    }
}