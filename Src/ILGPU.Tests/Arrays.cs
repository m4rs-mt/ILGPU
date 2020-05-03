using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;
using static ILGPU.Tests.EnumValues;

#pragma warning disable xUnit1026 // Theory methods should use all of their parameters

namespace ILGPU.Tests
{
    public abstract class Arrays : TestBase
    {
        protected Arrays(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        public static TheoryData<object, object> ArraySimpleTestData =>
            new TheoryData<object, object>
        {
            { sbyte.MinValue, default(Length1) },
            { byte.MaxValue, default(Length1) },
            { short.MinValue, default(Length1) },
            { ushort.MaxValue, default(Length1) },
            { int.MinValue, default(Length1) },
            { uint.MaxValue, default(Length1) },
            { long.MinValue, default(Length1) },
            { ulong.MaxValue, default(Length1) },
            { float.Epsilon, default(Length1) },
            { double.Epsilon, default(Length1) },
            { default(BasicEnum1), default(Length1) },
            { default(EmptyStruct), default(Length1) },
            { default(TestStruct), default(Length1) },
            { default(TestStruct<TestStruct<byte>>), default(Length1) },
            { default(
                TestStruct<BasicEnum4, TestStruct<short, EmptyStruct>>),
              default(Length1) },

            { byte.MaxValue, default(Length2) },
            { short.MinValue, default(Length2) },
            { int.MinValue, default(Length2) },
            { long.MinValue, default(Length2) },
            { default(BasicEnum1), default(Length2) },
            { default(EmptyStruct), default(Length2) },
            { default(TestStruct), default(Length2) },
            { default(TestStruct<TestStruct<byte>>), default(Length2) },

            { byte.MaxValue, default(Length31) },
            { short.MinValue, default(Length31) },
            { int.MinValue, default(Length31) },
            { long.MinValue, default(Length31) },
            { default(EmptyStruct), default(Length31) },
            { default(TestStruct<TestStruct<ushort>>), default(Length31) },

            { byte.MaxValue, default(Length32) },
            { short.MinValue, default(Length32) },
            { int.MinValue, default(Length32) },
            { long.MinValue, default(Length32) },
            { default(EmptyStruct), default(Length32) },
            { default(TestStruct<TestStruct<float>>), default(Length32) },

            { byte.MaxValue, default(Length33) },
            { short.MinValue, default(Length33) },
            { int.MinValue, default(Length33) },
            { long.MinValue, default(Length33) },
            { default(EmptyStruct), default(Length33) },
            { default(TestStruct<TestStruct<int>>), default(Length33) },

            { byte.MaxValue, default(Length65) },
            { short.MinValue, default(Length65) },
            { int.MinValue, default(Length65) },
            { long.MinValue, default(Length65) },
            { default(EmptyStruct), default(Length65) },
            { default(TestStruct<TestStruct<short>>), default(Length65) },

            { byte.MaxValue, default(Length127) },
            { short.MinValue, default(Length127) },
            { int.MinValue, default(Length127) },
            { long.MinValue, default(Length127) },
            { default(EmptyStruct), default(Length127) },
            { default(TestStruct<TestStruct<long>>), default(Length127) },
        };

        internal static void ArraySimpleKernel<T, TArraySize>(
            Index1 index,
            ArrayView<T> data,
            T c,
            int localIndex)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            for (int i = 0; i < arraySize.Length; ++i)
                array[i] = c;
            data[index] = array[localIndex];
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArraySimpleKernel))]
        public void ArraySimple<T, TArraySize>(T value, TArraySize _)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate<T>(1);
            Execute<Index1, T, TArraySize>(
                buffer.Length,
                buffer.View,
                value,
                0);

            var expected = new T[] { value };
            Verify(buffer, expected);
        }

        internal static void ArrayCallKernel<T, TArraySize>(
            Index1 index,
            ArrayView<T> data,
            T c,
            int localIndex)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            SetArrayValues<T, TArraySize>(array, c);
            data[index] = GetArrayValue(array, localIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetArrayValues<T, TArraySize>(T[] array, T c)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            for (int i = 0; i < arraySize.Length; ++i)
                array[i] = c;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T GetArrayValue<T>(T[] array, int localIndex)
            where T : unmanaged => array[localIndex];

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArrayCallKernel))]
        public void ArrayCall<T, TArraySize>(T value, TArraySize _)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate<T>(1);
            Execute<Index1, T, TArraySize>(
                buffer.Length,
                buffer.View,
                value,
                buffer.Length / 2);

            var expected = new T[] { value };
            Verify(buffer, expected);
        }

        internal static void ArrayLengthKernel<T, TArraySize>(
            Index1 index,
            ArrayView<int> data)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            data[index] = array.Length;
            data[index + 1] = array.GetLength(0);
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArrayLengthKernel))]
        public void ArrayLength<T, TArraySize>(T _, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate<int>(2);
            Execute<Index1, T, TArraySize>(1, buffer.View);

            var expected = new int[] { size.Length, size.Length };
            Verify(buffer, expected);
        }

        internal static void ArrayLongLengthKernel<T, TArraySize>(
            Index1 index,
            ArrayView<long> data)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            data[index] = array.LongLength;
            data[index + 1] = array.GetLongLength(0);
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArrayLongLengthKernel))]
        public void ArrayLongLength<T, TArraySize>(T _, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate<long>(2);
            Execute<Index1, T, TArraySize>(1, buffer.View);

            var expected = new long[] { size.Length, size.Length };
            Verify(buffer, expected);
        }

        internal static void ArrayBoundsKernel<T, TArraySize>(
            Index1 index,
            ArrayView<int> data)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            data[index] = array.GetLowerBound(0);
            data[index + 1] = array.GetUpperBound(0);
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArrayBoundsKernel))]
        public void ArrayBounds<T, TArraySize>(T _, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate<int>(2);
            Execute<Index1, T, TArraySize>(1, buffer.View);

            var expected = new int[] { 0, size.Length - 1 };
            Verify(buffer, expected);
        }

        public static TheoryData<object> ArrayEmptyTestData => new TheoryData<object>
        {
            { sbyte.MinValue },
            { byte.MaxValue },
            { short.MinValue },
            { ushort.MaxValue },
            { int.MinValue },
            { uint.MaxValue },
            { long.MinValue },
            { ulong.MaxValue },
            { float.Epsilon },
            { double.Epsilon },
            { default(BasicEnum1) },
            { default(EmptyStruct) },
            { default(TestStruct) },
            { default(TestStruct<TestStruct<byte>>) },
            { default(
                TestStruct<BasicEnum4, TestStruct<short, EmptyStruct>>)
            },
        };

        [SuppressMessage(
            "Performance",
            "CA1825:Avoid zero-length array allocations.",
            Justification = "Required for test cases")]
        internal static void ArrayEmptyKernel<T>(
            Index1 index,
            ArrayView<int> data)
            where T : unmanaged
        {
            var array = Array.Empty<T>();
            var otherArray = new T[0];
            data[index] = array.Length + otherArray.Length;
        }

        [Theory]
        [MemberData(nameof(ArrayEmptyTestData))]
        [KernelMethod(nameof(ArrayEmptyKernel))]
        public void ArrayEmpty<T>(T value)
            where T : unmanaged
        {
            using var buffer = Accelerator.Allocate<int>(1);
            Execute<Index1, T>(buffer.Length, buffer.View);

            var expected = new int[] { 0 };
            Verify(buffer, expected);
        }

        internal static void ArrayRefKernel<T, TArraySize>(
            Index1 index,
            ArrayView<T> data,
            T c,
            int localIndex)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            SetArrayValue(ref array[localIndex], c);
            data[index] = GetArrayValue(array, localIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetArrayValue<T>(ref T value, T c)
            where T : unmanaged
        {
            value = c;
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArrayRefKernel))]
        public void ArrayRef<T, TArraySize>(T value, TArraySize _)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate<T>(1);
            Execute<Index1, T, TArraySize>(
                buffer.Length,
                buffer.View,
                value,
                buffer.Length / 2);

            var expected = new T[] { value };
            Verify(buffer, expected);
        }
    }
}

#pragma warning restore xUnit1026 // Theory methods should use all of their parameters
