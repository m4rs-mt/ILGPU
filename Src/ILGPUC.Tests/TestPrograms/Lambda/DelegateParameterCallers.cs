// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateParameterCallers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DelegateParameter (callers in a second file, see
// DelegateParameter.cs)

using ILGPU;
using ILGPU.Runtime;

static class BitOps
{
    public static void Invert(AcceleratorStream stream, ArrayView<int> view) =>
        Host.UnaryOp(stream, view, v => ~v);
}

static class MathOps
{
    static int Twice(int value) => value * 2;

    public static void Double(AcceleratorStream stream, ArrayView<int> view) =>
        Host.UnaryOp(stream, view, Twice);
}
