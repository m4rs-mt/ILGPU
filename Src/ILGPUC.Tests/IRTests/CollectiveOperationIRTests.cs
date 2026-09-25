// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CollectiveOperationIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Tests.Framework;
using System;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Kernels for <see cref="CollectiveOperationIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class CollectiveOperationKernels
{
    public static void AddKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x + y);

    public static void SwappedAddKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => y + x);

    public static void MaxKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => Math.Max(x, y));

    public static void XorKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x ^ y);

    public static void BlockBodiedAddKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) =>
        {
            int sum = x + y;
            return sum;
        });

    public static void UnsignedAddKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x + y);

    public static void UnsignedXorKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Group.Reduce(d[i], 0u, (x, y) => x ^ y);

    public static void DoubledKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x + x);

    public static void CapturedAddKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d)
    {
        int offset = d[0];
        d[i] = Warp.Reduce(d[i], (x, y) => x + y + offset);
    }

    public static void StoringAddKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d)
    {
        var view = d;
        d[i] = Warp.Reduce(d[i], (x, y) =>
        {
            view[0] = x;
            return x + y;
        });
    }

    public static void AddOneKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x + y + 1);

    public static void MulAddKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x * 3 + y);

    public static void SubKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x - y);

    public static void SubGroupReduceKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Group.Reduce(d[i], 0, (x, y) => x - y);
}

/// <summary>
/// Verifies that a collective takes over the operation of its lambda only if the
/// lambda computes exactly that operation of its two operands, and that any other
/// lambda is rejected at compile time instead of being reduced with the wrong
/// operation.
/// </summary>
public sealed class CollectiveOperationIRTests
{
    /// <summary>
    /// Compiles the given kernel and returns what its lambda is recognized as.
    /// </summary>
    private static BinaryArithmeticKind? Recognize(string kernelName)
    {
        using var helper = new CompilationHelper();
        var module = helper.CompileToModule(GetKernel(kernelName));
        Method? lambda = null;
        foreach (var method in module.Methods)
        {
            if (method.Source?.Name.StartsWith($"<{kernelName}>") == true)
            {
                Assert.Null(lambda);
                lambda = method;
            }
        }
        Assert.NotNull(lambda);
        return BasicBlockBuilder.TryRecognizeOperation(lambda);
    }

    private static MethodInfo GetKernel(string kernelName) =>
        typeof(CollectiveOperationKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;

    [Theory]
    [InlineData(nameof(CollectiveOperationKernels.AddKernel), "Add")]
    [InlineData(nameof(CollectiveOperationKernels.SwappedAddKernel), "Add")]
    [InlineData(nameof(CollectiveOperationKernels.MaxKernel), "Max")]
    [InlineData(nameof(CollectiveOperationKernels.XorKernel), "Xor")]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedAddKernel), "Add")]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedXorKernel), "Xor")]
    public void Operation_IsRecognized(string kernelName, string kind) =>
        Assert.Equal(Enum.Parse<BinaryArithmeticKind>(kind), Recognize(kernelName));

    [Theory]
    [InlineData(nameof(CollectiveOperationKernels.AddOneKernel))]
    [InlineData(nameof(CollectiveOperationKernels.MulAddKernel))]
    [InlineData(nameof(CollectiveOperationKernels.SubKernel))]
    [InlineData(nameof(CollectiveOperationKernels.DoubledKernel))]
    [InlineData(nameof(CollectiveOperationKernels.CapturedAddKernel))]
    [InlineData(nameof(CollectiveOperationKernels.StoringAddKernel))]
    public void OtherOperation_IsNotRecognized(string kernelName) =>
        Assert.Null(Recognize(kernelName));

    [Theory]
    [InlineData(nameof(CollectiveOperationKernels.AddOneKernel), BackendType.CPU)]
    [InlineData(nameof(CollectiveOperationKernels.AddOneKernel), BackendType.Cuda)]
    [InlineData(nameof(CollectiveOperationKernels.AddOneKernel), BackendType.Metal)]
    [InlineData(
        nameof(CollectiveOperationKernels.SubGroupReduceKernel),
        BackendType.Cuda)]
    public void OtherOperation_IsRejected(string kernelName, BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        var exception = Assert.ThrowsAny<Exception>(
            () => helper.GenerateBackendCode(GetKernel(kernelName), backend));
        Assert.IsType<NotSupportedException>(exception.GetBaseException());
    }

    /// <summary>
    /// A lambda that returns the operation of its two operands compiles, also
    /// with a block body, whose Debug IL keeps the result in a local, and with
    /// unsigned operands.
    /// </summary>
    [Theory]
    [InlineData(nameof(CollectiveOperationKernels.AddKernel), BackendType.Cuda)]
    [InlineData(
        nameof(CollectiveOperationKernels.BlockBodiedAddKernel),
        BackendType.CPU)]
    [InlineData(
        nameof(CollectiveOperationKernels.BlockBodiedAddKernel),
        BackendType.Cuda)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedAddKernel), BackendType.Cuda)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedXorKernel), BackendType.CPU)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedXorKernel), BackendType.Metal)]
    public void Operation_IsCompiled(string kernelName, BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        helper.GenerateBackendCode(GetKernel(kernelName), backend);
    }
}
