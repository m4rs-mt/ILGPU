// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeDisplayTests.cs
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
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Verifies that diagnostics name types in the C# spelling of kernel code
/// instead of the spelling of IR types.
/// </summary>
public sealed class TypeDisplayTests
{
    public struct Pair<T>(T first, T second)
    {
        public T First = first;
        public T Second = second;
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(Half), "Half")]
    [InlineData(typeof(decimal), "decimal")]
    [InlineData(typeof(Int128), "Int128")]
    [InlineData(typeof(nint), "nint")]
    [InlineData(typeof(int*), "int*")]
    [InlineData(typeof(void*), "void*")]
    [InlineData(typeof(float[]), "float[]")]
    [InlineData(typeof(double[,]), "double[,]")]
    [InlineData(typeof(Pair<float>), "Pair<float>")]
    [InlineData(typeof(ArrayView1D<uint, Stride1D.Dense>), "ArrayView1D<uint, Dense>")]
    public void ManagedType_IsFormattedInCSharp(Type type, string expected) =>
        Assert.Equal(expected, TypeDisplay.Format(type));

    [Fact]
    public void ReferenceType_IsFormattedInCSharp() =>
        Assert.Equal("ref int", TypeDisplay.Format(typeof(int).MakeByRefType()));

    /// <summary>
    /// Kernels with unsupported constructs. Defined here, outside <c>Kernels/</c>,
    /// so that the kernel registry does not pick them up.
    /// </summary>
    private static class ErrorKernels
    {
        public static void WriteStructure(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            Interop.Write("{0}", new Pair<int>(index, data[index]));
    }

    /// <summary>
    /// An unsupported argument of Interop.Write is named by its C# type.
    /// </summary>
    [Fact]
    public void WriteArgument_IsNamedByItsCSharpType()
    {
        var kernel = typeof(ErrorKernels).GetMethod(
            nameof(ErrorKernels.WriteStructure),
            BindingFlags.Public | BindingFlags.Static)!;
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(kernel, BackendType.CPU));

        var messages = new List<string>();
        for (
            var current = exception;
            current is not null;
            current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        var text = string.Join("\n", messages);
        Assert.Contains(
            "Not supported write format argument type 'Pair<int>",
            text,
            StringComparison.Ordinal);
    }
}
