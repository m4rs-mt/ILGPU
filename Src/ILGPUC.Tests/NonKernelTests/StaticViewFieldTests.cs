// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StaticViewFieldTests.cs
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
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that kernels cannot load views or pointers from static fields, whose
/// values are read when the kernel is compiled and do not refer to memory of the
/// device, while static fields without them are still inlined.
/// </summary>
public sealed class StaticViewFieldTests
{
    #region Kernels

    private struct ViewHolder
    {
        public int Offset;
        public ArrayView<int> View;
    }

    private struct NestedViewHolder
    {
        public int Scale;
        public ViewHolder Inner;
    }

    private unsafe struct PointerHolder
    {
        public int* Pointer;
    }

    private struct Values
    {
        public int A;
        public long B;
    }

    private struct NestedValues
    {
        public Values Inner;
        public Index2D Index;
        public DayOfWeek Day;
    }

    private struct NativeValues
    {
        public nint Signed;
        public nuint Unsigned;
    }

    private static class Kernels
    {
        public static readonly ArrayView<int> View = default;
        public static readonly ArrayView1D<int, Stride1D.General> StridedView =
            default;
        public static readonly ArrayView2D<int, Stride2D.General> View2D = default;
        public static readonly ArrayView3D<int, Stride3D.DenseXY> View3D = default;
        public static readonly ViewHolder Holder = new() { Offset = 1, View = default };
        public static readonly NestedViewHolder Nested = new()
        {
            Scale = 2,
            Inner = new() { Offset = 3, View = default },
        };
        public static readonly unsafe PointerHolder Pointers = new() { Pointer = null };
        public static readonly unsafe int* Pointer = null;
        public static readonly ArrayView<int>[] ViewArray = [default];
        public static readonly ImmutableArray<ArrayView<int>> ViewImmutableArray =
            [default];
        public static ArrayView<int> MutableView = default;

        public static readonly int Scalar = 42;
        public static readonly Values Constants = new() { A = 1, B = 2 };
        public static readonly NestedValues NestedConstants = new()
        {
            Inner = new() { A = 3, B = 4 },
            Index = new(5, 6),
            Day = DayOfWeek.Friday,
        };

        public static readonly nint NativeInt = 5;
        public static readonly nuint NativeUInt = 6;
        public static readonly NativeValues NativeConstants = new()
        {
            Signed = 7,
            Unsigned = 8,
        };

        public static void ReadsView(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = View[index];

        public static void ReadsStridedView(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = StridedView[index];

        public static void ReadsView2D(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = View2D[new Index2D(index, 0)];

        public static void ReadsView3D(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = View3D[new Index3D(index, 0, 0)];

        public static void ReadsHolder(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Holder.View[index + Holder.Offset];

        public static void ReadsNested(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Nested.Inner.View[index] * Nested.Scale;

        public static void ReadsNestedScalarOnly(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Nested.Scale;

        public static void ReadsHolderByReference(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Read(in Holder, index);

        private static int Read(in ViewHolder holder, int index) =>
            holder.View[index];

        public static unsafe void ReadsPointer(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Pointer[(int)index];

        public static unsafe void ReadsPointerHolder(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Pointers.Pointer[(int)index];

        public static void ReadsViewArray(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = ViewArray[0][index];

        public static void ReadsViewImmutableArray(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = ViewImmutableArray[0][index];

        public static void ReadsMutableView(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = MutableView[index];

        public static void ReadsConstants(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Scalar + Constants.A + (int)Constants.B +
                NestedConstants.Inner.A + NestedConstants.Index.Y +
                (int)NestedConstants.Day;

        public static void ReadsNativeIntegers(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = (int)NativeInt + (int)NativeUInt +
                (int)NativeConstants.Signed + (int)NativeConstants.Unsigned;

        public static void ReadsEmptyViews(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = ArrayView<int>.Empty.IntLength +
                ArrayView1D<int, Stride1D.Dense>.Empty.IntLength +
                ArrayView2D<int, Stride2D.DenseX>.Empty.IntExtent.Y;
    }

    #endregion

    #region Tests

    private static readonly CompilationProperties MutableStaticFields =
        new(StaticFieldMode: StaticFieldMode.MutableStaticFields);

    public static TheoryData<string, string, string> RejectedLoads => new()
    {
        { nameof(Kernels.ReadsView), "Kernels.View", "ArrayView<int>" },
        {
            nameof(Kernels.ReadsStridedView),
            "Kernels.StridedView",
            "ArrayView1D<int, General>"
        },
        {
            nameof(Kernels.ReadsView2D),
            "Kernels.View2D",
            "ArrayView2D<int, General>"
        },
        {
            nameof(Kernels.ReadsView3D),
            "Kernels.View3D",
            "ArrayView3D<int, DenseXY>"
        },
        { nameof(Kernels.ReadsHolder), "Kernels.Holder", "ViewHolder" },
        { nameof(Kernels.ReadsNested), "Kernels.Nested", "NestedViewHolder" },
        {
            nameof(Kernels.ReadsNestedScalarOnly),
            "Kernels.Nested",
            "NestedViewHolder"
        },
        {
            nameof(Kernels.ReadsHolderByReference),
            "Kernels.Holder",
            "ViewHolder"
        },
        { nameof(Kernels.ReadsPointer), "Kernels.Pointer", "int*" },
        {
            nameof(Kernels.ReadsPointerHolder),
            "Kernels.Pointers",
            "PointerHolder"
        },
        { nameof(Kernels.ReadsViewArray), "Kernels.ViewArray", "ArrayView<int>[]" },
        {
            nameof(Kernels.ReadsViewImmutableArray),
            "Kernels.ViewImmutableArray",
            "ImmutableArray<ArrayView<int>>"
        },
    };

    [Theory]
    [MemberData(nameof(RejectedLoads))]
    public void StaticViewFields_AreNamedErrors(
        string kernelName,
        string fieldName,
        string typeName) =>
        AssertRejected(
            Compile(kernelName, BackendType.CPU),
            fieldName,
            typeName);

    /// <summary>
    /// The original report: a static readonly view compiled to a load from a
    /// null pointer on all backends instead of an error.
    /// </summary>
    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.Metal)]
    public void StaticReadonlyView_IsRejectedOnAllBackends(BackendType backend) =>
        AssertRejected(
            Compile(nameof(Kernels.ReadsView), backend),
            "Kernels.View",
            "ArrayView<int>");

    [Fact]
    public void MutableStaticView_IsRejectedInMutableStaticFieldMode() =>
        AssertRejected(
            Compile(
                nameof(Kernels.ReadsMutableView),
                BackendType.CPU,
                MutableStaticFields),
            "Kernels.MutableView",
            "ArrayView<int>");

    [Fact]
    public void MutableStaticView_IsNotReadOnlyByDefault()
    {
        var exception = Assert.ThrowsAny<Exception>(() =>
            GenerateBackendCode(nameof(Kernels.ReadsMutableView), BackendType.CPU));
        Assert.Contains(
            "since it is not read only",
            GetMessages(exception),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The field is rejected by its type, before its value is read, which would
    /// run the static constructor of its class in the compiler.
    /// </summary>
    [Fact]
    public void StaticViewField_DoesNotRunStaticConstructor()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GenerateBackendCode(
                typeof(AllocatingKernels).GetMethod(
                    nameof(AllocatingKernels.ReadsAllocatedView))!,
                BackendType.CPU));
        AssertRejected(exception, "AllocatingKernels.View", "ArrayView<int>");
        Assert.False(InitializationTracker.AllocatingKernelsInitialized);
    }

    [Theory]
    [InlineData(BackendType.CPU, false)]
    [InlineData(BackendType.Cuda, false)]
    [InlineData(BackendType.Metal, false)]
    [InlineData(BackendType.CPU, true)]
    public void StaticFieldsWithoutViews_AreInlined(BackendType backend, bool mutable)
    {
        var result = GenerateBackendCode(
            nameof(Kernels.ReadsConstants),
            backend,
            mutable ? MutableStaticFields : null);

        // 42 + 1 + 2 + 3 + 6 + 5, folded from the inlined values
        Assert.Contains(
            StoreOf(backend, 59),
            result.SourceCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Native integers map to pointer-sized IR types, but hold no memory.
    /// </summary>
    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.Metal)]
    public void StaticNativeIntegers_AreInlined(BackendType backend)
    {
        var result = GenerateBackendCode(nameof(Kernels.ReadsNativeIntegers), backend);

        // 5 + 6 + 7 + 8, folded from the inlined values
        Assert.Contains(
            StoreOf(backend, 26),
            result.SourceCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The static empty views of ILGPU hold no memory and remain usable.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.Metal)]
    public void LibraryEmptyViews_AreInlined(BackendType backend)
    {
        var result = GenerateBackendCode(nameof(Kernels.ReadsEmptyViews), backend);
        Assert.Contains(
            StoreOf(backend, 0),
            result.SourceCode,
            StringComparison.Ordinal);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Records whether the static constructor of
    /// <see cref="AllocatingKernels"/> ran, outside of that class.
    /// </summary>
    private static class InitializationTracker
    {
        public static bool AllocatingKernelsInitialized;
    }

    /// <summary>
    /// A class whose static constructor must not run in the compiler.
    /// </summary>
    private static class AllocatingKernels
    {
        public static readonly ArrayView<int> View = Allocate();

        private static ArrayView<int> Allocate()
        {
            InitializationTracker.AllocatingKernelsInitialized = true;
            return default;
        }

        public static void ReadsAllocatedView(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = View[index];
    }

    /// <summary>
    /// The store of the given constant to <c>data[index]</c> in the source the
    /// given backend generates.
    /// </summary>
    private static string StoreOf(BackendType backend, int value) =>
        backend == BackendType.CPU
            ? $"ScatterFill(laneIdx, {value}, activeMask);"
            : $" + param_0_index) = {value};";

    private static CodeGenerationResult GenerateBackendCode(
        string kernelName,
        BackendType backend,
        CompilationProperties? properties = null)
    {
        using var helper = new CompilationHelper(backend);
        var kernel = typeof(Kernels).GetMethod(kernelName)
            ?? throw new InvalidOperationException(
                $"Kernel method '{kernelName}' not found.");
        return helper.GenerateBackendCode(kernel, backend, properties);
    }

    private static KernelCompilationException Compile(
        string kernelName,
        BackendType backend,
        CompilationProperties? properties = null) =>
        Assert.Throws<KernelCompilationException>(() =>
            GenerateBackendCode(kernelName, backend, properties));

    private static void AssertRejected(
        KernelCompilationException exception,
        string fieldName,
        string typeName)
    {
        Assert.Equal("ILGPU1214", exception.Id);
        foreach (var fragment in new[]
        {
            $"Static field '{fieldName}' of type '{typeName}' holds a view or a " +
                "pointer, which kernels cannot load",
            "Pass the view to the kernel as a parameter or capture it",
        })
        {
            Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
        }
    }

    private static string GetMessages(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join("\n", messages);
    }

    #endregion
}
