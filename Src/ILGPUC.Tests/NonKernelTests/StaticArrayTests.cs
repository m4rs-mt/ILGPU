// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StaticArrayTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests the errors for static arrays that kernels cannot use.
/// </summary>
public sealed class StaticArrayTests
{
    private struct Holder
    {
        public int[] Values;
    }

    private static class Kernels
    {
        public static readonly int[] Table = [1, 2, 3];
        public static readonly int[]? Missing = null;
        public static readonly int[] Huge = Enumerable.Range(0, 2000).ToArray();
        public static readonly Holder Nested = new() { Values = [1, 2] };
        public static readonly ImmutableArray<Func<int, int>> DelegateTable =
            [x => x + 1];
        public static readonly Func<int, int>[] DelegateArray = [x => x + 1];

        public static void ReadsTable(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Table[index];

        public static void ReadsMissing(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Missing![index];

        public static void ReadsHuge(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Huge[index];

        public static void ReadsNested(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Nested.Values[index];

        public static void ReadsDelegateTable(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = DelegateTable[0](data[index]);

        public static void ReadsDelegateArray(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = DelegateArray[0](data[index]);
    }

    private static readonly CompilationProperties Inline =
        new(ArrayMode: ArrayMode.InlineMutableStaticArrays);

    public static TheoryData<string, bool, string[]> Errors => new()
    {
        {
            nameof(Kernels.ReadsTable),
            false,
            ["Kernels.Table", "ImmutableArray", "InlineMutableStaticArrays"]
        },
        { nameof(Kernels.ReadsMissing), true, ["Kernels.Missing", "is null"] },
        { nameof(Kernels.ReadsHuge), true, ["Kernels.Huge", "2000 elements"] },
        { nameof(Kernels.ReadsNested), true, ["Int32[]", "inside a static value"] },
        {
            nameof(Kernels.ReadsDelegateTable),
            false,
            ["Kernels.DelegateTable", "holds a delegate"]
        },
        {
            nameof(Kernels.ReadsDelegateArray),
            true,
            ["Kernels.DelegateArray", "holds a delegate"]
        },
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void UnusableStaticArrays_AreNamedErrors(
        string kernelName,
        bool inline,
        string[] fragments)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(kernelName)!;

        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(
                kernel,
                BackendType.CPU,
                inline ? Inline : null));

        var messages = new List<string>();
        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        var text = string.Join("\n", messages);
        foreach (var fragment in fragments)
            Assert.Contains(fragment, text, StringComparison.Ordinal);
    }
}
