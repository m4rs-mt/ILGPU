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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        d[i] = Warp.Reduce(d[i], (x, y) => x + x); // DoubledKernel

    public static void CapturedAddKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d)
    {
        int offset = d[0];
        d[i] = Warp.Reduce(d[i], (x, y) => x + y + offset); // CapturedAddKernel
    }

    public static void StoringAddKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d)
    {
        var view = d;
        d[i] = Warp.Reduce(d[i], (x, y) => // StoringAddKernel
        {
            view[0] = x;
            return x + y;
        });
    }

    public static void AddOneKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x + y + 1); // AddOneKernel

    public static void MulAddKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x * 3 + y);

    public static void SubKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x - y);

    public static void SubGroupReduceKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Group.Reduce(d[i], 0, (x, y) => x - y); // SubGroupReduceKernel

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

    public static void SubScanKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.InclusiveScan(d[i], (x, y) => x - y); // SubScanKernel

    public static void SubGroupScanKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Group.ExclusiveScan(d[i], 0, (x, y) => x - y); // SubGroupScanKernel

    public static void StructureKernel(
        Index1D i,
        ArrayView1D<Pair, Stride1D.Dense> d) =>
        d[i] = Warp.AllReduce( // StructureKernel
            d[i],
            (x, y) => new Pair(x.X + y.X, x.Y + y.Y));

    public static void PointerShuffleKernel(
        Index1D i,
        ArrayView1D<PointerPair, Stride1D.Dense> d) =>
        d[i] = Warp.Shuffle(d[i], 1); // PointerShuffleKernel

    public static void PointerBroadcastKernel(
        Index1D i,
        ArrayView1D<PointerPair, Stride1D.Dense> d) =>
        d[i] = Group.Broadcast(d[i], 1); // PointerBroadcastKernel

    public static void LonePointerShuffleKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Warp.Shuffle(d[i], 1); // LonePointerShuffleKernel

    public static void LonePointerShuffleDownKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Warp.ShuffleDown(d[i], 1); // LonePointerShuffleDownKernel

    public static void LonePointerShuffleUpKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Warp.ShuffleUp(d[i], 1); // LonePointerShuffleUpKernel

    public static void LonePointerShuffleXorKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Warp.ShuffleXor(d[i], 1); // LonePointerShuffleXorKernel

    public static void LonePointerBroadcastKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Warp.Broadcast(d[i]); // LonePointerBroadcastKernel

    public static void LonePointerGroupBroadcastKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Group.Broadcast(d[i], 1); // LonePointerGroupBroadcastKernel

    public static void LonePointerReduceKernel(
        Index1D i,
        ArrayView1D<LonePointer, Stride1D.Dense> d) =>
        d[i] = Warp.Reduce(d[i], (x, y) => x); // LonePointerReduceKernel

    public struct Pair(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    public unsafe struct PointerPair(int* pointer, int value)
    {
        public int* Pointer = pointer;
        public int Value = value;
    }

    /// <summary>
    /// A structure whose only field is a pointer, which has the type of the
    /// pointer in the IR.
    /// </summary>
    public unsafe struct LonePointer(int* pointer)
    {
        public int* Pointer = pointer;
    }
}

/// <summary>
/// Verifies that a collective takes over the operation of its lambda only if the
/// lambda computes exactly that operation of its two operands, and that any other
/// lambda, and any value a collective cannot exchange, is rejected at compile time
/// with ILGPU1200 at the location of the collective instead of being reduced with
/// the wrong operation.
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
        nameof(CollectiveOperationKernels.AddOneKernel),
        BackendType.CPU,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.AddOneKernel),
        BackendType.Cuda,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.AddOneKernel),
        BackendType.Metal,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.SubGroupReduceKernel),
        BackendType.CPU,
        "Group.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.SubGroupReduceKernel),
        BackendType.Cuda,
        "Group.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.DoubledKernel),
        BackendType.CPU,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.CapturedAddKernel),
        BackendType.Cuda,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.StoringAddKernel),
        BackendType.Metal,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.SubScanKernel),
        BackendType.CPU,
        "Warp.InclusiveScan")]
    [InlineData(
        nameof(CollectiveOperationKernels.SubScanKernel),
        BackendType.OpenCL,
        "Warp.InclusiveScan")]
    [InlineData(
        nameof(CollectiveOperationKernels.SubGroupScanKernel),
        BackendType.Cuda,
        "Group.ExclusiveScan")]
    [InlineData(
        nameof(CollectiveOperationKernels.StructureKernel),
        BackendType.CPU,
        "Warp.AllReduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.StructureKernel),
        BackendType.Cuda,
        "Warp.AllReduce")]
    public void OtherOperation_IsRejected(
        string kernelName,
        BackendType backend,
        string collective)
    {
        var exception = Compile(kernelName, backend);
        Assert.StartsWith(
            $"The operation of {collective} is not supported.",
            exception.Message,
            StringComparison.Ordinal);
        AssertLocation(exception, kernelName);
    }

    [Fact]
    public void OtherOperation_IsRejectedWithTheSupportedOperations() =>
        Assert.Equal(
            "The operation of Warp.Reduce is not supported. It must return one " +
            "operation of its two operands: one of +, *, &, | and ^, or Math.Min " +
            "or Math.Max, as in " +
            "(a, b) => a + b. Custom operations, including operations on " +
            "structures, are not supported.",
            Compile(
                nameof(CollectiveOperationKernels.AddOneKernel),
                BackendType.Cuda).Message);

    [Theory]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerShuffleKernel),
        BackendType.CPU,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerShuffleKernel),
        BackendType.Cuda,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerShuffleKernel),
        BackendType.Metal,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerShuffleKernel),
        BackendType.OpenCL,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerBroadcastKernel),
        BackendType.CPU,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerBroadcastKernel),
        BackendType.Cuda,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerBroadcastKernel),
        BackendType.Metal,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerBroadcastKernel),
        BackendType.OpenCL,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleKernel),
        BackendType.CPU,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleKernel),
        BackendType.Cuda,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleKernel),
        BackendType.Metal,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleKernel),
        BackendType.OpenCL,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerShuffleKernel),
        BackendType.ROCm,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.PointerBroadcastKernel),
        BackendType.ROCm,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleKernel),
        BackendType.ROCm,
        "Warp.Shuffle")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleDownKernel),
        BackendType.CPU,
        "Warp.ShuffleDown")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleDownKernel),
        BackendType.Metal,
        "Warp.ShuffleDown")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleUpKernel),
        BackendType.Cuda,
        "Warp.ShuffleUp")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleUpKernel),
        BackendType.ROCm,
        "Warp.ShuffleUp")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleXorKernel),
        BackendType.CPU,
        "Warp.ShuffleXor")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerShuffleXorKernel),
        BackendType.OpenCL,
        "Warp.ShuffleXor")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerBroadcastKernel),
        BackendType.CPU,
        "Warp.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerBroadcastKernel),
        BackendType.Cuda,
        "Warp.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerBroadcastKernel),
        BackendType.Metal,
        "Warp.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerBroadcastKernel),
        BackendType.OpenCL,
        "Warp.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerGroupBroadcastKernel),
        BackendType.CPU,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerGroupBroadcastKernel),
        BackendType.Cuda,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerGroupBroadcastKernel),
        BackendType.Metal,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerGroupBroadcastKernel),
        BackendType.OpenCL,
        "Group.Broadcast")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerReduceKernel),
        BackendType.CPU,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerReduceKernel),
        BackendType.Cuda,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerReduceKernel),
        BackendType.ROCm,
        "Warp.Reduce")]
    [InlineData(
        nameof(CollectiveOperationKernels.LonePointerGroupBroadcastKernel),
        BackendType.ROCm,
        "Group.Broadcast")]
    public void OtherType_IsRejected(
        string kernelName,
        BackendType backend,
        string collective)
    {
        var exception = Compile(kernelName, backend);
        Assert.Equal(
            $"{collective} does not support values of type 'int*'. " +
            "Only primitive types and structures of primitive types can be " +
            "broadcast, shuffled, reduced or scanned.",
            exception.Message);
        AssertLocation(exception, kernelName);
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
        nameof(CollectiveOperationKernels.UnsignedShortMinKernel),
        BackendType.CPU)]
    [InlineData(
        nameof(CollectiveOperationKernels.UnsignedShortMinKernel),
        BackendType.Metal)]
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
