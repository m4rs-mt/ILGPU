// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
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

namespace FixedSizeBuffers
{
    class Program
    {
        // .Net arrays are Reference Types, so they are currently not supported
        // in ILGPU. However, ILGPU does support Fixed Size Buffers, which are
        // considered Value Types.
        public unsafe struct CustomFixedBufferStruct
        {
            public fixed double Block1[2];
            public fixed int Block2[3];

            public override string ToString() =>
                $"[({Block1[0]}, {Block1[1]}), ({Block2[0]}, {Block2[1]}, {Block2[2]})]";
        }

        static unsafe void MyKernel(
            Index1D index,
            ArrayView1D<CustomFixedBufferStruct, Stride1D.Dense> view)
        {
            view[index].Block1[0] = 11;
            view[index].Block1[1] = 22;
            view[index].Block2[0] = 33;
            view[index].Block2[1] = 44;
            view[index].Block2[2] = 55;
        }

        static void Main()
        {
            using var context = Context.CreateDefault();

            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                var kernel = accelerator.LoadAutoGroupedStreamKernel<
                    Index1D,
                    ArrayView1D<CustomFixedBufferStruct, Stride1D.Dense>>(
                        MyKernel);

                using var buffer = accelerator.Allocate1D<CustomFixedBufferStruct>(16);
                buffer.MemSetToZero();

                kernel((int)buffer.Length, buffer.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var result = buffer.GetAsArray1D();
                for (int i = 0, e = result.Length; i < e; ++i)
                    Console.WriteLine($"Result[{i}] = {result[i]}");
            }
        }
    }
}
