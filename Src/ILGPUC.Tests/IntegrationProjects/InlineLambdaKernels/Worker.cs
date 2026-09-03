// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Worker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace InlineLambdaKernels;

sealed class Worker(AcceleratorStream stream)
{
    public void Run(ArrayView1D<int, Stride1D.Dense> target, int factor)
    {
        // Captures a parameter and a local; the stream is used outside only
        int bias = 1;
        stream.Launch(target.IntExtent, index =>
        {
            target[index] = index * factor - bias;
        });
    }
}
