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
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that kernels cannot load views or pointers from static fields, whose
/// values are read when the kernel is compiled and do not refer to memory of the
/// device, nor strings and class instances, which do not exist on the device,
/// while static fields without them are still inlined.
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

    private sealed class Box
    {
        public int Value;
    }

    private interface IValue
    {
        int Value { get; }
    }

    private sealed class ValueBox : IValue
    {
        public int Value => 2;
    }

    private static class ObjectKernels
    {
        public static readonly string Name = "abc";
        public static readonly string? NullName = null;
        public static readonly Box Box = new() { Value = 2 };
        public static readonly Box? NullBox = null;
        public static readonly object Object = new Box() { Value = 2 };
        public static readonly object BoxedValue = 2;
        public static readonly IValue Interface = new ValueBox();
        private static readonly Box PropertyBox = new() { Value = 2 };

        public static Box Property => PropertyBox;

        public static Box AutoProperty { get; } = new() { Value = 3 };

        public static void ReadsName(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Name.Length;

        public static void ReadsNullName(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = NullName!.Length;

        public static void ReadsBox(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Box.Value;

        public static void ReadsNullBox(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = NullBox!.Value;

        public static void ReadsObject(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = ((Box)Object).Value;

        public static void ReadsBoxedValue(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = (int)BoxedValue;

        public static void ReadsInterface(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Interface.Value;

        public static void ReadsProperty(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Property.Value;

        public static void ReadsAutoProperty(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = AutoProperty.Value;
    }

    /// <summary>
    /// Helpers loading static fields that kernels cannot load, on paths that
    /// optimization removes or keeps.
    /// </summary>
    private static class HelperKernels
    {
        public static readonly ArrayView<int> View = default;
        public static readonly string Name = "abc";
        public static readonly Box Box = new() { Value = 2 };

        private static int Fail() => throw new InvalidOperationException(Name);

        private static int CheckedArg(int value) => value >= 0 ? value : Fail();

        private static int ReadView(int index) => View[index];

        private static int ReadName() => Name.Length;

        private static int ReadBox() => Box.Value;

        private static int PickName(int value) => value == 0 ? 1 : ReadName();

        private static int PickBox(int value) => value == 0 ? 1 : ReadBox();

        private static int PickView(int value) => value == 0 ? 1 : ReadView(value);

        // Recursive helpers are never inlined and tail-recursive helpers become
        // loops, so their calls to ReadView remain unless the recursion is folded
        private static int Sum(int value) =>
            value < 0 ? ReadView(0) : value == 0 ? 0 : value + Sum(value - 1);

        private static int Even(int value) =>
            value < 0 ? ReadView(0) : value == 0 ? 1 : 1 - Odd(value - 1);

        private static int Odd(int value) => value == 0 ? 0 : 1 - Even(value - 1);

        private static int Count(int value, int sum) =>
            value < 0 ? ReadView(0) : value == 0 ? sum : Count(value - 1, sum + value);

        private static int SumName(int value) =>
            value <= 0 ? Name.Length : 1 + SumName(value - 1);

        // The constant evaluator of the frontend does not see through the tuple,
        // so the helpers are generated and only the IR optimizer removes their
        // calls
        public static void PicksView(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = PickView((((int Value, int Index))(0, index.X)).Value);

        public static void ChecksConstant(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = CheckedArg((((int Value, int Index))(5, index.X)).Value);

        public static void PicksName(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = PickName((((int Value, int Index))(0, index.X)).Value);

        public static void PicksBox(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = PickBox((((int Value, int Index))(0, index.X)).Value);

        // The constant evaluator of the frontend folds Sum(5), so the helpers
        // are never generated
        public static void SumsConstant(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Sum(5);

        public static void PicksViewByIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = PickView(index);

        public static void SumsIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Sum(index);

        public static void ChecksEvenIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Even(index);

        public static void CountsIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = Count(index, 0);

        public static void ChecksIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = CheckedArg(index - 2);

        public static void PicksNameByIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = PickName(index);

        public static void PicksBoxByIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = PickBox(index);

        public static void SumsNameByIndex(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = SumName(index);

        public static void ReadsNameInLocalFunction(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = index.X == 0 ? 1 : Local();
            static int Local() => Name.Length;
        }

        public static void ReadsNameByReference(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            ref readonly string name = ref Name;
            data[index] = name.Length;
        }

        public static void ReadsGenericString(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = GenericHolder<string>.Value!.Length;

        public static void ReadsGenericInt(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            data[index] = GenericHolder<int>.Value + 3;
    }

    private static class GenericHolder<T>
    {
        public static readonly T? Value = default;
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

    public static TheoryData<string, string, string, string> RejectedObjects => new()
    {
        {
            nameof(ObjectKernels.ReadsName),
            "ObjectKernels.Name",
            "string",
            "data[index] = Name.Length"
        },
        {
            nameof(ObjectKernels.ReadsNullName),
            "ObjectKernels.NullName",
            "string",
            "data[index] = NullName!.Length"
        },
        {
            nameof(ObjectKernels.ReadsBox),
            "ObjectKernels.Box",
            "Box",
            "data[index] = Box.Value"
        },
        {
            nameof(ObjectKernels.ReadsNullBox),
            "ObjectKernels.NullBox",
            "Box",
            "data[index] = NullBox!.Value"
        },
        {
            nameof(ObjectKernels.ReadsObject),
            "ObjectKernels.Object",
            "object",
            "data[index] = ((Box)Object).Value"
        },
        {
            nameof(ObjectKernels.ReadsBoxedValue),
            "ObjectKernels.BoxedValue",
            "object",
            "data[index] = (int)BoxedValue"
        },
        {
            nameof(ObjectKernels.ReadsInterface),
            "ObjectKernels.Interface",
            "IValue",
            "data[index] = Interface.Value"
        },
        {
            nameof(ObjectKernels.ReadsProperty),
            "ObjectKernels.PropertyBox",
            "Box",
            "public static Box Property => PropertyBox;"
        },
        {
            nameof(ObjectKernels.ReadsAutoProperty),
            "ObjectKernels.AutoProperty",
            "Box",
            "data[index] = AutoProperty.Value"
        },
    };

    /// <summary>
    /// Static fields of strings, classes and interfaces failed with an unnamed
    /// error that the class type is not supported, naming <c>RuntimeType</c>
    /// for null fields, and with internal compiler errors for boxed values. The
    /// error points at the load of the field, or at the call of the accessor of
    /// an auto-implemented property, whose location is its declaration.
    /// </summary>
    [Theory]
    [MemberData(nameof(RejectedObjects))]
    public void StaticObjects_AreNamedErrors(
        string kernelName,
        string fieldName,
        string typeName,
        string sourceLine)
    {
        var exception = Assert.Throws<KernelCompilationException>(() =>
            GenerateBackendCode(
                kernelName,
                BackendType.CPU,
                kernels: typeof(ObjectKernels)));
        Assert.Equal("ILGPU1214", exception.Id);
        foreach (var fragment in new[]
        {
            $"Static field '{fieldName}' has the reference type '{typeName}', " +
                "which kernels cannot load, even if the field is null",
            "other objects, such as Type objects, do not exist on the device",
            "Pass the data to the kernel in an ArrayView or as a parameter instead",
        })
        {
            Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
        }
        AssertLocation(exception, sourceLine);
    }

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

    /// <summary>
    /// Helpers loading these fields failed the kernel right away, although
    /// optimization removed every call to them, as it removes the calls to
    /// helpers failing with other errors.
    /// </summary>
    [Theory]
    [InlineData(nameof(HelperKernels.PicksView), 1)]
    [InlineData(nameof(HelperKernels.ChecksConstant), 5)]
    [InlineData(nameof(HelperKernels.PicksName), 1)]
    [InlineData(nameof(HelperKernels.PicksBox), 1)]
    [InlineData(nameof(HelperKernels.SumsConstant), 15)]
    public void RemovedHelperCalls_Compile(string kernelName, int value)
    {
        var result = GenerateBackendCode(
            kernelName,
            BackendType.CPU,
            kernels: typeof(HelperKernels));
        Assert.Contains(
            StoreOf(BackendType.CPU, value),
            result.SourceCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A call to such a helper that survives optimization reports the original
    /// error at the load of the field in the helper. Calls to recursive and
    /// mutually recursive helpers, and the loops of tail-recursive helpers,
    /// survive optimization, so the error of the helper they call is reported,
    /// on Metal before its error for recursion.
    /// </summary>
    [Theory]
    [InlineData(nameof(HelperKernels.PicksViewByIndex), BackendType.CPU)]
    [InlineData(nameof(HelperKernels.SumsIndex), BackendType.CPU)]
    [InlineData(nameof(HelperKernels.SumsIndex), BackendType.Metal)]
    [InlineData(nameof(HelperKernels.ChecksEvenIndex), BackendType.CPU)]
    [InlineData(nameof(HelperKernels.ChecksEvenIndex), BackendType.Metal)]
    [InlineData(nameof(HelperKernels.CountsIndex), BackendType.CPU)]
    [InlineData(nameof(HelperKernels.CountsIndex), BackendType.Metal)]
    public void SurvivingHelperCalls_AreNamedErrors(
        string kernelName,
        BackendType backend)
    {
        var exception = Assert.Throws<KernelCompilationException>(() =>
            GenerateBackendCode(
                kernelName,
                backend,
                kernels: typeof(HelperKernels)));
        AssertRejected(exception, "HelperKernels.View", "ArrayView<int>");
        AssertLocation(
            exception,
            "private static int ReadView(int index) => View[index];");
    }

    public static TheoryData<string, string, string> SurvivingReferenceHelperCalls =>
        new()
    {
        {
            nameof(HelperKernels.ChecksIndex),
            "Static field 'HelperKernels.Name' has the reference type 'string'",
            "private static int Fail() => throw new InvalidOperationException(Name);"
        },
        {
            nameof(HelperKernels.PicksNameByIndex),
            "Static field 'HelperKernels.Name' has the reference type 'string'",
            "private static int ReadName() => Name.Length;"
        },
        {
            nameof(HelperKernels.PicksBoxByIndex),
            "Static field 'HelperKernels.Box' has the reference type 'Box'",
            "private static int ReadBox() => Box.Value;"
        },
        {
            nameof(HelperKernels.SumsNameByIndex),
            "Static field 'HelperKernels.Name' has the reference type 'string'",
            "value <= 0 ? Name.Length : 1 + SumName(value - 1);"
        },
        {
            nameof(HelperKernels.ReadsNameInLocalFunction),
            "Static field 'HelperKernels.Name' has the reference type 'string'",
            "static int Local() => Name.Length;"
        },
    };

    /// <summary>
    /// Helpers loading strings and class instances are reported like helpers
    /// loading views.
    /// </summary>
    [Theory]
    [MemberData(nameof(SurvivingReferenceHelperCalls))]
    public void SurvivingReferenceHelperCalls_AreNamedErrors(
        string kernelName,
        string message,
        string sourceLine)
    {
        var exception = Assert.Throws<KernelCompilationException>(() =>
            GenerateBackendCode(
                kernelName,
                BackendType.CPU,
                kernels: typeof(HelperKernels)));
        Assert.Equal("ILGPU1214", exception.Id);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        AssertLocation(exception, sourceLine);
    }

    [Fact]
    public void StaticObjectReference_IsNamedError()
    {
        var exception = Assert.Throws<KernelCompilationException>(() =>
            GenerateBackendCode(
                nameof(HelperKernels.ReadsNameByReference),
                BackendType.CPU,
                kernels: typeof(HelperKernels)));
        Assert.Equal("ILGPU1214", exception.Id);
        Assert.Contains(
            "Static field 'HelperKernels.Name' has the reference type 'string'",
            exception.Message,
            StringComparison.Ordinal);
        AssertLocation(exception, "ref readonly string name = ref Name;");
    }

    /// <summary>
    /// Fields of generic types are checked by the type of their instantiation.
    /// </summary>
    [Fact]
    public void GenericStaticFields_AreCheckedByInstantiation()
    {
        var exception = Assert.Throws<KernelCompilationException>(() =>
            GenerateBackendCode(
                nameof(HelperKernels.ReadsGenericString),
                BackendType.CPU,
                kernels: typeof(HelperKernels)));
        Assert.Equal("ILGPU1214", exception.Id);
        Assert.Contains(
            ".Value' has the reference type 'string'",
            exception.Message,
            StringComparison.Ordinal);
        AssertLocation(
            exception,
            "data[index] = GenericHolder<string>.Value!.Length;");

        var result = GenerateBackendCode(
            nameof(HelperKernels.ReadsGenericInt),
            BackendType.CPU,
            kernels: typeof(HelperKernels));
        Assert.Contains(
            StoreOf(BackendType.CPU, 3),
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
        CompilationProperties? properties = null,
        Type? kernels = null)
    {
        using var helper = new CompilationHelper(backend);
        var kernel = (kernels ?? typeof(Kernels)).GetMethod(kernelName)
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

    /// <summary>
    /// Asserts that the given error points at the given line of this file.
    /// </summary>
    private static void AssertLocation(
        KernelCompilationException exception,
        string sourceLine)
    {
        var location = Assert.IsAssignableFrom<FileLocation>(exception.Location);
        Assert.Equal(
            nameof(StaticViewFieldTests) + ".cs",
            Path.GetFileName(location.FileName));
        var line = File.ReadAllLines(location.FileName)[location.StartLine - 1];
        Assert.Contains(sourceLine, line, StringComparison.Ordinal);
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
