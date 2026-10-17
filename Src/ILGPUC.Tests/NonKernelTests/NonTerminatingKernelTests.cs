// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NonTerminatingKernelTests.cs
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
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that kernels that can never return are rejected with a named error,
/// whether they loop forever in IL or only after constant branches were folded.
/// </summary>
public sealed class NonTerminatingKernelTests
{
    private static class Kernels
    {
        public static void Forever(Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            while (true)
                data[index] += 1;
        }

        public static void ForeverForLargeTypes<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            int x = data[index];
            if (Interop.SizeOf<T>() == 4)
                goto L1;
            goto L2;
        L1:
            x += 1;
        L2:
            x *= 2;
            if (Interop.SizeOf<T>() == 8)
                goto L1;
            data[index] = x;
        }
    }

    public static TheoryData<MethodInfo> NonTerminatingKernels => new()
    {
        typeof(Kernels).GetMethod(nameof(Kernels.Forever))!,
        typeof(Kernels).GetMethod(nameof(Kernels.ForeverForLargeTypes))!
            .MakeGenericMethod(typeof(long)),
    };

    [Theory]
    [MemberData(nameof(NonTerminatingKernels))]
    public void NonTerminatingKernels_AreNamedErrors(MethodInfo kernel)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(kernel, BackendType.CPU));

        var messages = new List<string>();
        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        var text = string.Join("\n", messages);
        Assert.Contains("never returns", text, StringComparison.Ordinal);
        Assert.Contains(kernel.Name, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(short))]
    public void TerminatingInstantiations_Compile(Type type)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels)
            .GetMethod(nameof(Kernels.ForeverForLargeTypes))!
            .MakeGenericMethod(type);

        var code = helper.GenerateBackendCode(kernel, BackendType.CPU);

        Assert.False(string.IsNullOrEmpty(code.SourceCode));
    }
}
