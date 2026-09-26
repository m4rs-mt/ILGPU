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
using ILGPUC.IR;
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

    public static void UnsignedMaxKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => Math.Max(x, y));

    public static void UnsignedMinKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Group.Reduce(d[i], uint.MaxValue, (x, y) => Math.Min(x, y));

    public static void UnsignedLongMaxKernel(
        Index1D i,
        ArrayView1D<ulong, Stride1D.Dense> d) =>
        d[i] = Warp.AllReduce(d[i], (x, y) => Math.Max(x, y));

    public static void UnsignedShortMinKernel(
        Index1D i,
        ArrayView1D<ushort, Stride1D.Dense> d) =>
        d[i] = Group.InclusiveScan(d[i], ushort.MaxValue, (x, y) => Math.Min(x, y));

    public static void UnsignedMaxMethodGroupKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], Math.Max);

    public static void UnsignedMinMethodGroupKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Warp.InclusiveScan(d[i], XMath.Min);

    public static void UnsignedMaxScanKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Warp.ExclusiveScan(d[i], 0u, XMath.Max);

    public static void UnsignedMaxGroupScanKernel(
        Index1D i,
        ArrayView1D<uint, Stride1D.Dense> d) =>
        d[i] = Group.ExclusiveScan(d[i], 0u, Math.Max);
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
    private static BinaryArithmeticKind? Recognize(string kernelName) =>
        Recognize(kernelName, out _);

    /// <summary>
    /// Compiles the given kernel and returns what its lambda is recognized as,
    /// with the flags of the recognized operation.
    /// </summary>
    private static BinaryArithmeticKind? Recognize(
        string kernelName,
        out ArithmeticFlags flags)
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
        return BasicBlockBuilder.TryRecognizeOperation(lambda, out flags);
    }

    private static MethodInfo GetKernel(string kernelName) =>
        typeof(CollectiveOperationKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// A recognized operation keeps the unsigned flag of an unsigned minimum or
    /// maximum, the only operations that depend on the signedness of their
    /// operands, and no flags otherwise.
    /// </summary>
    [Theory]
    [InlineData(nameof(CollectiveOperationKernels.AddKernel), "Add", false)]
    [InlineData(nameof(CollectiveOperationKernels.SwappedAddKernel), "Add", false)]
    [InlineData(nameof(CollectiveOperationKernels.MaxKernel), "Max", false)]
    [InlineData(nameof(CollectiveOperationKernels.XorKernel), "Xor", false)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedAddKernel), "Add", false)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedXorKernel), "Xor", false)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedMaxKernel), "Max", true)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedMinKernel), "Min", true)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedLongMaxKernel), "Max", true)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedShortMinKernel), "Min", true)]
    public void Operation_IsRecognized(string kernelName, string kind, bool isUnsigned)
    {
        Assert.Equal(
            Enum.Parse<BinaryArithmeticKind>(kind),
            Recognize(kernelName, out var flags));
        Assert.Equal(
            isUnsigned ? ArithmeticFlags.Unsigned : ArithmeticFlags.None,
            flags);
    }

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
    /// unsigned operands, also of an unsigned minimum or maximum.
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
    [InlineData(nameof(CollectiveOperationKernels.UnsignedMinKernel), BackendType.CPU)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedMinKernel), BackendType.Cuda)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedMinKernel), BackendType.Metal)]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMinKernel),
        BackendType.OpenCL)]
    [InlineData(nameof(CollectiveOperationKernels.UnsignedMinKernel), BackendType.ROCm)]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedLongMaxKernel),
        BackendType.Metal)]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedLongMaxKernel),
        BackendType.OpenCL)]
    public void Operation_IsCompiled(string kernelName, BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        helper.GenerateBackendCode(GetKernel(kernelName), backend);
    }

    /// <summary>
    /// A collective whose operation is an unsigned minimum or maximum, as a lambda
    /// or as a method group of <c>Math</c> or of <c>XMath</c>, is recognized as
    /// that unsigned operation, and a signed one as the signed operation.
    /// </summary>
    [Theory]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxMethodGroupKernel),
        "@ilgpu.warp.reduce.Reduce.Max.unsigned(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMinMethodGroupKernel),
        "@ilgpu.warp.scan.Inclusive.Min.unsigned(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxScanKernel),
        "@ilgpu.warp.scan.Exclusive.Max.unsigned(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxGroupScanKernel),
        "@ilgpu.group.scan.Exclusive.Max.unsigned(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMinKernel),
        "@ilgpu.group.reduce.Reduce.Min.unsigned(")]
    [InlineData(
        nameof(CollectiveOperationKernels.MaxKernel),
        "@ilgpu.warp.reduce.Reduce.Max(")]
    public void UnsignedOperation_IsRecognized(string kernelName, string collective)
    {
        using var helper = new CompilationHelper();
        var ir = helper.GetNormalizedIR(
            GetKernel(kernelName),
            IRDumpPoint.AfterGlobalOpt);
        Assert.Contains(collective, ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unsigned minimum or maximum is computed with the unsigned instruction
    /// or overload of each backend, and a signed one with the signed one: the
    /// native SIMD-group and sub-group functions of Metal and OpenCL get unsigned
    /// operands, and the shuffle trees of Cuda and ROCm compare unsigned values.
    /// </summary>
    [Theory]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxKernel),
        BackendType.CPU,
        "CPUVectorIntrinsics.WarpReduceMax<uint>(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxKernel),
        BackendType.Metal,
        "simd_max((uint)(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxKernel),
        BackendType.OpenCL,
        "sub_group_reduce_max((uint)(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxKernel),
        BackendType.Cuda,
        "umax(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxKernel),
        BackendType.ROCm,
        "(unsigned int)")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMaxMethodGroupKernel),
        BackendType.Metal,
        "simd_max((uint)(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMinMethodGroupKernel),
        BackendType.OpenCL,
        "sub_group_scan_inclusive_min((uint)(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedMinMethodGroupKernel),
        BackendType.CPU,
        "CPUVectorIntrinsics.WarpScanInclusiveMin<uint>(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedLongMaxKernel),
        BackendType.Cuda,
        "ullmax(")]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedLongMaxKernel),
        BackendType.OpenCL,
        "sub_group_reduce_max((ulong)(")]
    public void UnsignedOperation_IsEmittedUnsigned(
        string kernelName,
        BackendType backend,
        string expected)
    {
        using var helper = new CompilationHelper(backend);
        var source = helper.GenerateBackendCode(GetKernel(kernelName), backend)
            .SourceCode;
        Assert.Contains(expected, source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BackendType.CPU, "<uint>")]
    [InlineData(BackendType.Metal, "(uint)")]
    [InlineData(BackendType.OpenCL, "(uint)")]
    [InlineData(BackendType.Cuda, "umax(")]
    [InlineData(BackendType.ROCm, "unsigned")]
    public void SignedOperation_IsEmittedSigned(BackendType backend, string unexpected)
    {
        using var helper = new CompilationHelper(backend);
        var source = helper.GenerateBackendCode(
            GetKernel(nameof(CollectiveOperationKernels.MaxKernel)),
            backend).SourceCode;
        Assert.DoesNotContain(unexpected, source, StringComparison.Ordinal);
    }
}
