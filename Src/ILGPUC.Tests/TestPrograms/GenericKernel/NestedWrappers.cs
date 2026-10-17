// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedWrappers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: generic kernels launched from generic code.
// - Scale<T> is launched inside Level3<T>, reached through Level1<T> and Level2<T>
//   with int and float, and from a method of the generic class Runner<T> with long.
// - SwapPair<T> takes Pair<T>, launched inside PairRun<T> with int.
// - Scale<int> and Scale<short> are also launched directly with closed types, so
//   one generic kernel has several closed launch sites plus a dispatch stub.
// Expected output:
//   2 4 6 8 | 3 6 9 12 | 30 60 90 120 | 2 1 4 3 | 5 10 15 20 | 7 14 21 28

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

public struct Pair<T> where T : unmanaged
{
    public T A;
    public T B;
}

static class Kernels
{
    public static void Scale<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        T factor)
        where T : unmanaged, INumber<T>
    {
        data[index] = data[index] * factor;
    }

    public static void SwapPair<T>(
        Index1D index,
        ArrayView1D<Pair<T>, Stride1D.Dense> data)
        where T : unmanaged
    {
        var pair = data[index];
        data[index] = new Pair<T> { A = pair.B, B = pair.A };
    }
}

sealed class Runner<T> where T : unmanaged, INumber<T>
{
    public void Run(AcceleratorStream stream, T[] values, T factor)
    {
        using var buffer = stream.Allocate1D(values);
        var view = buffer.View;
        stream.Launch((Index1D)values.Length, i => Kernels.Scale(i, view, factor));
        stream.Synchronize();
        Program.Print(buffer.GetAsArray1D());
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        Level1(stream, new int[] { 1, 2, 3, 4 }, 2);
        Level1(stream, new float[] { 1, 2, 3, 4 }, 3f);
        new Runner<long>().Run(stream, new long[] { 10, 20, 30, 40 }, 3L);
        PairRun<int>(stream);

        using var ints = stream.Allocate1D(new int[] { 1, 2, 3, 4 });
        var intView = ints.View;
        stream.Launch((Index1D)4, i => Kernels.Scale(i, intView, 5));
        stream.Synchronize();
        Print(ints.GetAsArray1D());

        using var shorts = stream.Allocate1D(new short[] { 1, 2, 3, 4 });
        var shortView = shorts.View;
        stream.Launch((Index1D)4, i => Kernels.Scale(i, shortView, (short)7));
        stream.Synchronize();
        Print(shorts.GetAsArray1D());
    }

    static void Level1<T>(AcceleratorStream stream, T[] values, T factor)
        where T : unmanaged, INumber<T> =>
        Level2(stream, values, factor);

    static void Level2<T>(AcceleratorStream stream, T[] values, T factor)
        where T : unmanaged, INumber<T> =>
        Level3(stream, values, factor);

    static void Level3<T>(AcceleratorStream stream, T[] values, T factor)
        where T : unmanaged, INumber<T>
    {
        using var buffer = stream.Allocate1D(values);
        var view = buffer.View;
        stream.Launch((Index1D)values.Length, i => Kernels.Scale(i, view, factor));
        stream.Synchronize();
        Print(buffer.GetAsArray1D());
    }

    static void PairRun<T>(AcceleratorStream stream) where T : unmanaged, INumber<T>
    {
        var pairs = new Pair<T>[]
        {
            new() { A = T.One, B = T.One + T.One },
            new() { A = T.One + T.One + T.One, B = T.One + T.One + T.One + T.One },
        };
        using var buffer = stream.Allocate1D(pairs);
        var view = buffer.View;
        stream.Launch((Index1D)pairs.Length, i => Kernels.SwapPair(i, view));
        stream.Synchronize();
        foreach (var pair in buffer.GetAsArray1D())
        {
            Console.WriteLine(pair.A);
            Console.WriteLine(pair.B);
        }
    }

    public static void Print<T>(T[] values)
    {
        foreach (var value in values)
            Console.WriteLine(value);
    }
}
