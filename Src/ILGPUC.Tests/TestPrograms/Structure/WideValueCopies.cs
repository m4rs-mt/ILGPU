// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WideValueCopies.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WideValueCopies
// Kernels store and copy Int128, UInt128 and decimal values without computing with
// them: through views, locals, structure fields, kernel parameters and helpers
// (generic and not), and in a structure mixing them with smaller fields.
// Expected output: one line per kernel, True if the bytes of the copied values
// match the bytes of the values on the host.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;

struct Holder
{
    public int Tag;
    public Int128 Value;
    public decimal Amount;
    public short Small;
    public UInt128 Unsigned;
}

static class Kernels
{
    static Int128 Pass(Int128 value) => value;

    static T PassGeneric<T>(T value) where T : struct => value;

    static Holder Retag(Holder holder, int tag)
    {
        var result = holder;
        result.Tag = tag;
        return result;
    }

    public static void CopyKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> source,
        ArrayView1D<Int128, Stride1D.Dense> target,
        ArrayView1D<UInt128, Stride1D.Dense> unsignedSource,
        ArrayView1D<UInt128, Stride1D.Dense> unsignedTarget,
        ArrayView1D<decimal, Stride1D.Dense> amountSource,
        ArrayView1D<decimal, Stride1D.Dense> amountTarget)
    {
        Int128 local = Pass(source[index]);
        target[index] = local;
        unsignedTarget[index] = PassGeneric(unsignedSource[(index + 1) % 4]);
        var amount = amountSource[index];
        amountTarget[index] = amount;
    }

    public static void BuildKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> values,
        ArrayView1D<UInt128, Stride1D.Dense> unsignedValues,
        ArrayView1D<decimal, Stride1D.Dense> amounts,
        ArrayView1D<Holder, Stride1D.Dense> target)
    {
        var holder = new Holder
        {
            Tag = index,
            Value = values[index],
            Amount = amounts[index],
            Small = (short)(index * 3),
            Unsigned = unsignedValues[index],
        };
        target[index] = PassGeneric(holder);
    }

    public static void StructKernel(
        Index1D index,
        ArrayView1D<Holder, Stride1D.Dense> source,
        ArrayView1D<Holder, Stride1D.Dense> target,
        ArrayView1D<Int128, Stride1D.Dense> values,
        Int128 parameter)
    {
        var holder = Retag(source[index], index + 10);
        target[index] = holder;
        values[index] = index == 0 ? parameter : holder.Value;
    }
}

static class Program
{
    static bool SameBytes<T>(T[] expected, T[] actual) where T : struct =>
        MemoryMarshal.AsBytes(expected.AsSpan())
            .SequenceEqual(MemoryMarshal.AsBytes(actual.AsSpan()));

    // Compares the bytes of each field, as the padding between them is not copied
    static bool SameFields(Holder[] expected, Holder[] actual) =>
        expected.Zip(actual).All(pair =>
            pair.First.Tag == pair.Second.Tag &&
            SameBytes([pair.First.Value], [pair.Second.Value]) &&
            SameBytes([pair.First.Amount], [pair.Second.Amount]) &&
            pair.First.Small == pair.Second.Small &&
            SameBytes([pair.First.Unsigned], [pair.Second.Unsigned]));

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        var values = new Int128[]
        {
            Int128.MinValue,
            new(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            -1,
            Int128.MaxValue,
        };
        var unsignedValues = new UInt128[]
        {
            UInt128.MaxValue,
            new(1, 2),
            0,
            new(0x8000000000000000, 0x7FFFFFFFFFFFFFFF),
        };
        var amounts = new decimal[]
        {
            1.5m,
            -79228162514264337593543950335m,
            0m,
            0.0001m,
        };
        var holders = Enumerable.Range(0, 4)
            .Select(i => new Holder
            {
                Tag = i,
                Value = values[i],
                Amount = amounts[i],
                Small = (short)(i * 3),
                Unsigned = unsignedValues[i],
            })
            .ToArray();

        using var valueSource = stream.Allocate1D(values);
        using var valueTarget = stream.Allocate1D<Int128>(4);
        using var unsignedSource = stream.Allocate1D(unsignedValues);
        using var unsignedTarget = stream.Allocate1D<UInt128>(4);
        using var amountSource = stream.Allocate1D(amounts);
        using var amountTarget = stream.Allocate1D<decimal>(4);
        using var built = stream.Allocate1D<Holder>(4);
        using var copied = stream.Allocate1D<Holder>(4);
        using var extracted = stream.Allocate1D<Int128>(4);

        var vs = valueSource.View;
        var vt = valueTarget.View;
        var us = unsignedSource.View;
        var ut = unsignedTarget.View;
        var @as = amountSource.View;
        var at = amountTarget.View;
        var bv = built.View;
        var cv = copied.View;
        var ev = extracted.View;
        var parameter = new Int128(0xA5A5A5A5A5A5A5A5, 0x5A5A5A5A5A5A5A5A);
        stream.Launch(
            (Index1D)4,
            index => Kernels.CopyKernel(index, vs, vt, us, ut, @as, at));
        stream.Launch(
            (Index1D)4,
            index => Kernels.BuildKernel(index, vs, us, @as, bv));
        stream.Launch(
            (Index1D)4,
            index => Kernels.StructKernel(index, bv, cv, ev, parameter));
        stream.Synchronize();

        var rotated = Enumerable.Range(0, 4)
            .Select(i => unsignedValues[(i + 1) % 4])
            .ToArray();
        var retagged = holders
            .Select((h, i) => { h.Tag = i + 10; return h; })
            .ToArray();
        var expectedExtracted = values.ToArray();
        expectedExtracted[0] = parameter;

        Console.WriteLine(
            SameBytes(values, valueTarget.GetAsArray1D()) &&
            SameBytes(rotated, unsignedTarget.GetAsArray1D()) &&
            SameBytes(amounts, amountTarget.GetAsArray1D()));
        Console.WriteLine(SameFields(holders, built.GetAsArray1D()));
        Console.WriteLine(
            SameFields(retagged, copied.GetAsArray1D()) &&
            SameBytes(expectedExtracted, extracted.GetAsArray1D()));
    }
}
