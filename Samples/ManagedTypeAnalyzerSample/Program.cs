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
using System;

namespace SimpleMath
{
    record ManagedType(int X);

    static class Program
    {
        static void ManagedTypeKernel(Index1D index, ArrayView<int> dataView)
        {
            // Analyzer should produce an error here
            ManagedType type = new ManagedType(dataView[index]);
            dataView[index] = type.X;
        }

        static void Main()
        {
            using var context = Context.CreateDefault();
            var device = context.GetPreferredDevice(false);
            using var accelerator = device.CreateAccelerator(context);

            var kernel =
                accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(
                    ManagedTypeKernel);

            using var buffer = accelerator.Allocate1D<int>(128);

            kernel((int)buffer.Length, buffer.View);
        }
    }
}
