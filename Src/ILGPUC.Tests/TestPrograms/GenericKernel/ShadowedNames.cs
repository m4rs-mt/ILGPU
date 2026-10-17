// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ShadowedNames.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: generic kernels instantiated with a type nested in a generic type, a
// type whose namespace is shadowed at the launch site and a value tuple, in a file
// without 'using System'.
// Expected output: 3 | 5 | 7

using ILGPU;
using ILGPU.Runtime;

namespace Lib
{
    public struct Item
    {
        public int Value;
    }
}

namespace App.Lib
{
    public struct Item
    {
        public long Value;
    }
}

namespace App
{
    public struct Outer<T> where T : unmanaged
    {
        public struct Cell
        {
            public T Value;
        }
    }

    static class Kernels
    {
        public static void Store<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> data,
            T value)
            where T : unmanaged
        {
            data[index] = value;
        }
    }

    static class Program
    {
        static T Run<T>(AcceleratorStream stream, T value) where T : unmanaged
        {
            using var buffer = stream.Allocate1D<T>(1);
            var view = buffer.View;
            stream.Launch((Index1D)1, i => Kernels.Store(i, view, value));
            stream.Synchronize();
            return buffer.GetAsArray1D()[0];
        }

        static void Main()
        {
            using var context = Context.Create(b => b.Default());
            using var accelerator = context.GetPreferredDevice(preferCPU: true)
                .CreateAccelerator(context);
            var stream = accelerator.DefaultStream;

            var cell = Run(stream, new Outer<int>.Cell { Value = 3 });
            System.Console.WriteLine(cell.Value);
            var item = Run(stream, new global::Lib.Item { Value = 5 });
            System.Console.WriteLine(item.Value);
            var tuple = Run(stream, (a: 4, b: 3));
            System.Console.WriteLine(tuple.a + tuple.b);
        }
    }
}
