// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Complex.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.ManagedType;

class Complex
{
    class RefTypeEmpty
    {
    }

    struct Unmanaged
    {
        private int a;
        private int b;
    }

    struct Managed
    {
        private int a;
        private RefTypeEmpty r;
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
        var unmanaged = new Unmanaged();
        var managed = new Managed();
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