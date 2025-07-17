// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PartialMethods.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Analyzers.Tests.Programs.ManagedType.PartialMethods;

class RefType
{
    public int Hello => 42;
}

partial class Foo
{
    public static partial int Bar(int i);
}

partial class Foo
{
    public static partial int Bar(int i)
    {
        RefType r = new RefType();
        return r.Hello + i;
    }
}

class PartialMethods
{
    static void Kernel(Index1D index, ArrayView<int> view)
    {
        view[index] = Foo.Bar(10);
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