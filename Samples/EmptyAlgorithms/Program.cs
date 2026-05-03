// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;

namespace EmptyAlgorithms;

static class Program
{
    static void Main()
    {
        // In the new API, no special EnableAlgorithms() call is needed.
        // Algorithm operations (scan, reduce, radix sort, initialize, sequence,
        // transform) are built into ILGPU directly via:
        //   using ILGPU.ScanReduce;
        //   using ILGPU.RadixSort;
        //   using ILGPU.Initialization;
        using var context = Context.CreateDefault();
    }
}
