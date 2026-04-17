// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.ManagedType;

class Arrays
{
    class RefType
    {
        public int Hello => 42;
    }

    struct ValueType
    {
        public int Hello;

        public ValueType()
        {
            Hello = 42;
        }
    }

    static void Kernel(Index1D index, ArrayView<int> input)
    {
        ValueType[] array = [new ValueType()];
        int[] ints = [0, 1, 2];

        RefType[] refs = [new RefType()];
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