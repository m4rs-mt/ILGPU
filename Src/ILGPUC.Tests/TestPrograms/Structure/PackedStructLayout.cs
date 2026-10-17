// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PackedStructLayout.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PackedStructLayout
// Kernels read and write every field of structures with packing and explicit sizes
// that keep all fields at their natural alignments, in views and as by-value
// parameters. The host compares the bytes written by the device with the bytes of the
// same values computed on the host.
// Expected output: the host sizes, then one "<name> ok" line per kernel.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
struct Pack4Natural
{
    public int A;
    public short B;
    public short C;
    public float D;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Pack1Bytes
{
    public byte A;
    public sbyte B;
    public short C;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct Pack8
{
    public byte A;
    public long B;
    public int C;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Pack1Ints
{
    public int A;
    public int B;
}

struct WithPack1Ints
{
    public int Tag;
    public Pack1Ints Inner;
}

[StructLayout(LayoutKind.Sequential, Size = 24)]
struct Sized
{
    public int A;
    public short B;
}

struct WithSized
{
    public byte Tag;
    public Sized Inner;
    public short S;
}

static class Ops
{
    public static Pack4Natural Next(Pack4Natural v) =>
        new Pack4Natural { A = v.A * 3, B = (short)(v.B + 1), C = (short)(v.C - 2), D = v.D + 0.5f };

    public static Pack1Bytes Next(Pack1Bytes v) =>
        new Pack1Bytes { A = (byte)(v.A + 1), B = (sbyte)(v.B - 1), C = (short)(v.C * 2) };

    public static Pack8 Next(Pack8 v) =>
        new Pack8 { A = (byte)(v.A + 1), B = v.B * 3, C = v.C - 7 };

    public static WithPack1Ints Next(WithPack1Ints v) =>
        new WithPack1Ints { Tag = v.Tag + 1, Inner = new Pack1Ints { A = v.Inner.A * 3, B = v.Inner.B - 5 } };

    public static WithSized Next(WithSized v) =>
        new WithSized { Tag = (byte)(v.Tag + 1), Inner = new Sized { A = v.Inner.A * 3, B = (short)(v.Inner.B + 1) }, S = (short)(v.S * 2) };
}

static class Kernels
{
    public static void Pack4NaturalKernel(
        Index1D index, ArrayView1D<Pack4Natural, Stride1D.Dense> input, ArrayView1D<Pack4Natural, Stride1D.Dense> output) =>
        output[index] = Ops.Next(input[index]);

    public static void Pack1BytesKernel(
        Index1D index, ArrayView1D<Pack1Bytes, Stride1D.Dense> input, ArrayView1D<Pack1Bytes, Stride1D.Dense> output)
    {
        var value = input[index];
        output[index].A = (byte)(value.A + 1);
        output[index].B = (sbyte)(value.B - 1);
        output[index].C = (short)(value.C * 2);
    }

    public static void Pack8Kernel(
        Index1D index, ArrayView1D<Pack8, Stride1D.Dense> input, ArrayView1D<Pack8, Stride1D.Dense> output)
    {
        var value = input[index];
        output[index].A = (byte)(value.A + 1);
        output[index].B = value.B * 3;
        output[index].C = value.C - 7;
    }

    public static void WithPack1IntsKernel(
        Index1D index, ArrayView1D<WithPack1Ints, Stride1D.Dense> input, ArrayView1D<WithPack1Ints, Stride1D.Dense> output) =>
        output[index] = Ops.Next(input[index]);

    public static void WithSizedKernel(
        Index1D index, ArrayView1D<WithSized, Stride1D.Dense> input, ArrayView1D<WithSized, Stride1D.Dense> output)
    {
        var value = input[index];
        output[index].Tag = (byte)(value.Tag + 1);
        output[index].Inner.A = value.Inner.A * 3;
        output[index].Inner.B = (short)(value.Inner.B + 1);
        output[index].S = (short)(value.S * 2);
    }

    public static void FieldAddressKernel(
        Index1D index, ArrayView1D<Pack4Natural, Stride1D.Dense> data)
    {
        data[index].A *= 3;
        data[index].B += 1;
        data[index].C -= 2;
        data[index].D += 0.5f;
    }

    public static void ByValueKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output, Pack1Bytes bytes, Pack8 wide, WithSized sized)
    {
        output[index] = index + bytes.A + bytes.B + bytes.C + wide.A + wide.B + wide.C +
            sized.Tag + sized.Inner.A + sized.Inner.B + sized.S;
    }
}

static class Program
{
    static void Check<T>(string name, T[] expected, T[] actual)
        where T : unmanaged
    {
        var e = MemoryMarshal.AsBytes(expected.AsSpan());
        var a = MemoryMarshal.AsBytes(actual.AsSpan());
        Console.WriteLine(e.SequenceEqual(a)
            ? $"{name} ok"
            : $"{name} {Convert.ToHexString(e)} {Convert.ToHexString(a)}");
    }

    static void Run<T>(
        AcceleratorStream stream,
        string name,
        T[] values,
        Func<T, T> next,
        Action<ArrayView1D<T, Stride1D.Dense>, ArrayView1D<T, Stride1D.Dense>> launch)
        where T : unmanaged
    {
        using var input = stream.Allocate1D<T>(values.Length);
        input.View.CopyFromCPU(stream, values);
        using var output = stream.Allocate1D<T>(values.Length);
        output.View.CopyFromCPU(stream, values);
        launch(input.View, output.View);
        stream.Synchronize();
        Check(name, Array.ConvertAll(values, x => next(x)), output.GetAsArray1D());
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;
        const int N = 5;

        Console.WriteLine(
            $"{Unsafe.SizeOf<Pack4Natural>()} {Unsafe.SizeOf<Pack1Bytes>()} {Unsafe.SizeOf<Pack8>()} " +
            $"{Unsafe.SizeOf<WithPack1Ints>()} {Unsafe.SizeOf<WithSized>()}");

        var pack4 = new Pack4Natural[N];
        var pack1 = new Pack1Bytes[N];
        var pack8 = new Pack8[N];
        var withInts = new WithPack1Ints[N];
        var withSized = new WithSized[N];
        for (int i = 0; i < N; i++)
        {
            pack4[i] = new Pack4Natural { A = 0x01020304 + i, B = (short)(0x1122 + i), C = (short)(-7 - i), D = 1.25f * (i + 1) };
            pack1[i] = new Pack1Bytes { A = (byte)(0xA0 + i), B = (sbyte)(-3 - i), C = (short)(0x0506 + i) };
            pack8[i] = new Pack8 { A = (byte)(0xB0 + i), B = 0x0102030405060708L + i, C = 0x11223344 + i };
            withInts[i] = new WithPack1Ints { Tag = 0x0BADF00D + i, Inner = new Pack1Ints { A = 0x10203040 + i, B = 0x50607080 + i } };
            withSized[i] = new WithSized { Tag = (byte)(0xE0 + i), Inner = new Sized { A = 0x10203040 + i, B = (short)(0x5060 + i) }, S = (short)(0x1234 + i) };
        }

        Run(stream, "Pack4NaturalKernel", pack4, Ops.Next, (i, o) =>
        {
            stream.Launch((Index1D)N, index => Kernels.Pack4NaturalKernel(index, i, o));
        });
        Run(stream, "Pack1BytesKernel", pack1, Ops.Next, (i, o) =>
        {
            stream.Launch((Index1D)N, index => Kernels.Pack1BytesKernel(index, i, o));
        });
        Run(stream, "Pack8Kernel", pack8, Ops.Next, (i, o) =>
        {
            stream.Launch((Index1D)N, index => Kernels.Pack8Kernel(index, i, o));
        });
        Run(stream, "WithPack1IntsKernel", withInts, Ops.Next, (i, o) =>
        {
            stream.Launch((Index1D)N, index => Kernels.WithPack1IntsKernel(index, i, o));
        });
        Run(stream, "WithSizedKernel", withSized, Ops.Next, (i, o) =>
        {
            stream.Launch((Index1D)N, index => Kernels.WithSizedKernel(index, i, o));
        });

        using (var data = stream.Allocate1D<Pack4Natural>(N))
        {
            data.View.CopyFromCPU(stream, pack4);
            stream.Launch((Index1D)N, index => Kernels.FieldAddressKernel(index, data.View));
            stream.Synchronize();
            Check("FieldAddressKernel", Array.ConvertAll(pack4, Ops.Next), data.GetAsArray1D());
        }

        using (var data = stream.Allocate1D<long>(N))
        {
            var bytes = pack1[1];
            var wide = pack8[2];
            var sized = withSized[3];
            stream.Launch((Index1D)N, index => Kernels.ByValueKernel(index, data.View, bytes, wide, sized));
            stream.Synchronize();
            var expected = new long[N];
            for (int i = 0; i < N; i++)
            {
                expected[i] = i + bytes.A + bytes.B + bytes.C + wide.A + wide.B + wide.C +
                    sized.Tag + sized.Inner.A + sized.Inner.B + sized.S;
            }
            Check("ByValueKernel", expected, data.GetAsArray1D());
        }
    }
}
