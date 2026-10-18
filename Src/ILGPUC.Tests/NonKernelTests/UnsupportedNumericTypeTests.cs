// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsupportedNumericTypeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Kernels that compute with Int128, UInt128 or decimal values, which kernels can
/// only store and copy, and kernels that store and copy them or use 64-bit
/// integers and generic math instead. Defined here, outside <c>Kernels/</c>, so
/// that the kernel registry does not pick them up. Each rejected operation is
/// marked with the name of its kernel.
/// </summary>
static class UnsupportedNumericTypeKernels
{
    public struct Wide(Int128 value, decimal amount, int count)
    {
        public Int128 Value = value;
        public decimal Amount = amount;
        public int Count = count;
    }

    public static void ImplicitConversionKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> data)
    {
        Int128 value = index.X; // ImplicitConversionKernel
        data[index] = value;
    }

    public static void ConstructorKernel(
        Index1D index,
        ArrayView1D<UInt128, Stride1D.Dense> data) =>
        data[index] = new UInt128(7, (ulong)index.X); // ConstructorKernel

    public static void ArithmeticKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> data) =>
        data[index] = data[index] * data[index]; // ArithmeticKernel

    public static void ComparisonKernel(
        Index1D index,
        ArrayView1D<UInt128, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output) =>
        output[index] = data[index] == data[0] ? 1 : 0; // ComparisonKernel

    public static void NarrowingKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> data,
        ArrayView1D<long, Stride1D.Dense> output) =>
        output[index] = (long)data[index]; // NarrowingKernel

    private static T Add<T>(T left, T right) where T : INumber<T> =>
        left + right; // GenericMathKernel

    public static void GenericMathKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> data) =>
        data[index] = Add(data[index], data[index]);

    public static void DecimalKernel(
        Index1D index,
        ArrayView1D<Wide, Stride1D.Dense> data)
    {
        var wide = data[index];
        wide.Amount += wide.Amount; // DecimalKernel
        data[index] = wide;
    }

    public static void HalfConversionKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        ArrayView1D<Int128, Stride1D.Dense> output) =>
        output[index] = (Int128)data[index]; // HalfConversionKernel

    public static void LibraryKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> output) =>
        output[index] = Math.BigMul((long)index.X, 3L); // LibraryKernel

    private static Int128 Pass(Int128 value) => value;

    private static T PassGeneric<T>(T value) where T : struct => value;

    /// <summary>
    /// Stores and copies Int128, UInt128 and decimal values through views,
    /// structure fields, locals and helpers.
    /// </summary>
    public static void StorageKernel(
        Index1D index,
        ArrayView1D<Int128, Stride1D.Dense> values,
        ArrayView<UInt128> unsignedValues,
        ArrayView1D<Wide, Stride1D.Dense> wides,
        Int128 parameter)
    {
        Int128 local = Pass(values[index]);
        var wide = new Wide(local, wides[index].Amount, index);
        wides[index] = PassGeneric(wide);
        values[index] = index == 0 ? parameter : default;
        unsignedValues[index] = unsignedValues[(index + 1) % 4];
    }

    /// <summary>
    /// Uses 64-bit integers, generic math and the generic conversions of the
    /// integer types, whose library implementations call Int128 and decimal
    /// members in branches for other type arguments.
    /// </summary>
    public static void SupportedKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> output,
        ArrayView1D<Half, Stride1D.Dense> halves)
    {
        long value = Add<long>(index.X, 2);
        value += int.CreateSaturating(value * long.MaxValue);
        value += long.CreateTruncating((ulong)value);
        value += ulong.CreateChecked(index.X) > 3 ? 1 : 0;
        value += (long)Half.ConvertToInteger<ulong>(halves[index]);
        output[index] = Math.BigMul((int)value, 3) + Math.Max(value, 4);
    }
}

/// <summary>
/// Verifies that operations on Int128, UInt128 and decimal values are rejected in
/// kernels with ILGPU1200 at the call in kernel code that computes with them, and
/// that storing and copying such values, and kernels using other integer types,
/// still compile.
/// </summary>
public sealed class UnsupportedNumericTypeTests
{
    private static MethodInfo GetKernel(string kernelName) =>
        typeof(UnsupportedNumericTypeKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// Compiles the given kernel, which must be rejected with ILGPU1200: the
    /// diagnostic itself is thrown, not an error wrapping it.
    /// </summary>
    private static KernelCompilationException Compile(
        string kernelName,
        BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        var exception = Assert.Throws<KernelCompilationException>(
            () => helper.GenerateBackendCode(GetKernel(kernelName), backend));
        Assert.Equal("ILGPU1200", exception.Id);
        return exception;
    }

    /// <summary>
    /// Asserts that the given diagnostic points at the line of this file marked
    /// with the given comment.
    /// </summary>
    private static void AssertLocation(
        KernelCompilationException exception,
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        var location = Assert.IsAssignableFrom<FileLocation>(exception.Location);
        Assert.Equal(
            Path.GetFileName(sourcePath),
            Path.GetFileName(location.FileName));
        var lines = File.ReadAllLines(sourcePath);
        int line = Array.FindIndex(
            lines,
            l => l.EndsWith("// " + marker, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{marker}' not found");
        Assert.Equal(line, location.StartLine);
    }

    [Theory]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.ImplicitConversionKernel),
        "Int128.op_Implicit",
        "Int128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.ConstructorKernel),
        "UInt128..ctor",
        "UInt128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.ArithmeticKernel),
        "Int128.op_Multiply",
        "Int128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.ComparisonKernel),
        "UInt128.op_Equality",
        "UInt128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.NarrowingKernel),
        "Int128.op_Explicit",
        "Int128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.GenericMathKernel),
        "Int128.op_Addition",
        "Int128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.DecimalKernel),
        "Decimal.op_Addition",
        "decimal")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.HalfConversionKernel),
        "Half.op_Explicit",
        "Int128")]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.LibraryKernel),
        "Math.BigMul",
        "Int128")]
    public void Operation_IsRejected(string kernelName, string method, string type)
    {
        foreach (var backend in new[] { BackendType.CPU, BackendType.Metal })
        {
            var exception = Compile(kernelName, backend);
            Assert.Equal(
                $"Kernel code cannot call '{method}', which computes with values " +
                $"of type '{type}'. Kernels can store and copy Int128, UInt128 " +
                "and decimal values, but cannot compute with them or convert " +
                "them; use long, ulong or double instead.",
                exception.Message);
            AssertLocation(exception, kernelName);
        }
    }

    [Theory]
    [InlineData(nameof(UnsupportedNumericTypeKernels.StorageKernel), BackendType.CPU)]
    [InlineData(nameof(UnsupportedNumericTypeKernels.StorageKernel), BackendType.Metal)]
    [InlineData(nameof(UnsupportedNumericTypeKernels.SupportedKernel), BackendType.CPU)]
    [InlineData(
        nameof(UnsupportedNumericTypeKernels.SupportedKernel),
        BackendType.Metal)]
    public void StorageAndOtherIntegerTypes_AreSupported(
        string kernelName,
        BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        var code = helper.GenerateBackendCode(GetKernel(kernelName), backend);
        Assert.False(string.IsNullOrEmpty(code.SourceCode));
    }
}
