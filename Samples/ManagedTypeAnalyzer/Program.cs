// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
namespace ManagedTypeAnalyzer;

class Program
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

    static int AnotherFunction()
    {
        return new RefType().Hello;
    }

    static void Kernel(Index1D index, ArrayView<int> input, ArrayView<int> output)
    {
        // This is disallowed, since MyRefType is a reference type
        var refType = new RefType();
        output[index] = input[index] + refType.Hello;

        // Allocating arrays of unmanaged types is fine
        ValueType[] array = [new ValueType()];
        int[] ints = [0, 1, 2];

        // But arrays of reference types are still disallowed
        RefType[] other =
        [
            new RefType(),
        ];

        // Any functions that may be called are also analyzed
        int result = AnotherFunction();
    }

    static void Main(string[] args)
    {
        using var context = Context.CreateDefault();
        var device = context.GetPreferredDevice(false);
        using var accelerator = device.CreateAccelerator(context);

        using var input = accelerator.Allocate1D<int>(1024);
        using var output = accelerator.Allocate1D<int>(1024);

        var kernel =
            accelerator
                .LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(
                    Kernel);

        kernel(input.IntExtent, input.View, output.View);

        accelerator.Synchronize();
    }
}
