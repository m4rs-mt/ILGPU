// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.CodeGeneration;
using ILGPU.Runtime;
using System;

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1051 // Do not declare visible instance fields

namespace StructureOfArrays
{
    public struct MyPoint
    {
        public int X;
        public int Y;
    }

    public static partial class Program
    {
        [GeneratedStructureOfArrays(typeof(MyPoint), 4)]
        public partial struct MyPoint4
        { }

        static unsafe void MyKernel(Index1D index, ArrayView<MyPoint4> dataView)
        {
            dataView[index].X[0] = index;
            dataView[index].X[1] = index + 1;
            dataView[index].X[2] = index + 2;
            dataView[index].X[3] = index + 3;
            dataView[index].Y[0] = index + 4;
            dataView[index].Y[1] = index + 5;
            dataView[index].Y[2] = index + 6;
            dataView[index].Y[3] = index + 7;
        }

        static unsafe void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                var kernel = accelerator.LoadAutoGroupedStreamKernel<
                    Index1D, ArrayView<MyPoint4>>(MyKernel);
                using var buffer = accelerator.Allocate1D<MyPoint4>(1024);

                kernel((int)buffer.Length, buffer.View);

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                {
                    if (data[i].X[0] != i
                        || data[i].X[1] != i + 1
                        || data[i].X[2] != i + 2
                        || data[i].X[3] != i + 3
                        || data[i].Y[0] != i + 4
                        || data[i].Y[1] != i + 5
                        || data[i].Y[2] != i + 6
                        || data[i].Y[3] != i + 7)
                        Console.WriteLine($"Error at element location {i}");
                }
            }
        }
    }
}

#pragma warning restore CA1034 // Nested types should not be visible
#pragma warning restore CA1051 // Do not declare visible instance fields
