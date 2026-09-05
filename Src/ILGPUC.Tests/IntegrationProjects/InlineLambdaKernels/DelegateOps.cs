// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateOps.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Runtime;

namespace InlineLambdaKernels;

static class Kernels
{
    public static void UnaryOpKernel(
        Index1D index, ArrayView<int> data, Func<int, int> op) =>
        data[index] = op(data[index]);
}

// Launches a kernel method with the delegate its callers pass (issue #463):
// one kernel is compiled for each lambda and static method passed to it.
static class DelegateOps
{
    public static void UnaryOp(
        AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
    {
        stream.Launch(view.IntExtent, index => Kernels.UnaryOpKernel(index, view, op));
    }

    public static int Square(int value) => value * value;
}
