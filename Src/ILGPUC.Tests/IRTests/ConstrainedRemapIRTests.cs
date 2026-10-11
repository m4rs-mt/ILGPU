// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstrainedRemapIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Tests.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Kernels for <see cref="ConstrainedRemapIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class ConstrainedRemapKernels
{
    static bool IsInteger<T>(T x) where T : INumberBase<T> => T.IsInteger(x);

    public static void ConstrainedRemapKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> floats,
        ArrayView1D<double, Stride1D.Dense> doubles,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] =
            (IsInteger(floats[index]) ? 1 : 0) | (IsInteger(doubles[index]) ? 2 : 0);
    }
}

/// <summary>
/// Constrained calls of generic math members are resolved to the members of the
/// type argument while the frontend generates code. Members with a remapping
/// that has no code generator (here float.IsInteger and double.IsInteger, which
/// map to <see cref="XMath.IsInteger(float)"/> and
/// <see cref="XMath.IsInteger(double)"/>) must call the remapping instead of
/// compiling the BCL implementation.
/// </summary>
public sealed class ConstrainedRemapIRTests
{
    [Fact]
    public void ConstrainedRemap_CallsRemapping()
    {
        var kernel = typeof(ConstrainedRemapKernels).GetMethod(
            nameof(ConstrainedRemapKernels.ConstrainedRemapKernel))!;

        using var recorder = AssertionRecorder.Start();
        using var helper = new CompilationHelper();
        var module = helper.CompileToModule(kernel, new CompilationProperties());
        Assert.Empty(recorder.Failures);

        var sources = new List<MethodBase>();
        foreach (var method in module.Methods)
        {
            if (method.Source is { } source)
                sources.Add(source);
        }
        Assert.DoesNotContain(
            sources,
            source => source.DeclaringType == typeof(float) ||
                source.DeclaringType == typeof(double));
        Assert.Equal(
            2,
            sources.Count(source =>
                source.DeclaringType == typeof(XMath) &&
                source.Name == nameof(XMath.IsInteger)));
    }
}
