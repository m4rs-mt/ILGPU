using ILGPU.Runtime;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;
using static ILGPU.Tests.EnumValues;

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
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
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<T>(1);
            Execute<Index1D, T, TArraySize>(
                buffer.IntExtent,
                buffer.AsContiguous(),
                value,
                0);

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }

        internal static void ArraySimpleDivergentKernel<T, TArraySize>(
            Index1D index,
            ArrayView<T> data,
            T c,
            int localIndex)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            if (index > 10)
            {
                var array = new T[arraySize.Length];
                for (int i = 0; i < arraySize.Length; ++i)
                    array[i] = c;
                data[index] = array[localIndex];
            }
            else
            {
                data[index] = c;
            }
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArraySimpleDivergentKernel))]
        public void ArraySimpleDivergent<T, TArraySize>(T value, TArraySize _)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate1D<T>(1);
            Execute<Index1D, T, TArraySize>(
                buffer.IntExtent,
                buffer.AsContiguous(),
                value,
                0);

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }

        internal static void ArrayCallKernel<T, TArraySize>(
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<T>(1);
            Execute<Index1D, T, TArraySize>(
                buffer.IntExtent,
                buffer.AsContiguous(),
                value,
                (int)buffer.Length / 2);

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }

        internal static void ArrayLengthKernel<T, TArraySize>(
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<int>(2);
            Execute<Index1D, T, TArraySize>(1, buffer.AsContiguous());

            var expected = new int[] { size.Length, size.Length };
            Verify(buffer.View, expected);
        }

        internal static void ArrayLongLengthKernel<T, TArraySize>(
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<long>(2);
            Execute<Index1D, T, TArraySize>(1, buffer.AsContiguous());

            var expected = new long[] { size.Length, size.Length };
            Verify(buffer.View, expected);
        }

        internal static void ArrayBoundsKernel<T, TArraySize>(
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<int>(2);
            Execute<Index1D, T, TArraySize>(1, buffer.AsContiguous());

            var expected = new int[] { 0, size.Length - 1 };
            Verify(buffer.View, expected);
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
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<int>(1);
            Execute<Index1D, T>(buffer.IntExtent, buffer.AsContiguous());

            var expected = new int[] { 0 };
            Verify(buffer.View, expected);
        }

        internal static void ArrayRefKernel<T, TArraySize>(
            Index1D index,
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
            using var buffer = Accelerator.Allocate1D<T>(1);
            Execute<Index1D, T, TArraySize>(
                buffer.IntExtent,
                buffer.AsContiguous(),
                value,
                (int)buffer.Length / 2);

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }

        /// <summary>
        /// Test structure for the <see cref="ArrayInStructure{T, TArraySize}
        /// (T, TArraySize)"/> test.
        /// </summary>
        struct ArrayInStruct<T>
            where T : unmanaged
        {
            public ArrayInStruct(T[] data)
            {
                Data = data;
            }

            public T[] Data { get; }

            public readonly ref T this[int index] => ref Data[index];
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T GetValue<T>(ArrayInStruct<T> data, int index)
            where T : unmanaged =>
            data[index];

        internal static void ArrayInStructureKernel<T, TArraySize>(
            Index1D index,
            ArrayView<T> data,
            T c,
            int localIndex)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[arraySize.Length];
            var nestedStruct = new ArrayInStruct<T>(array);
            for (int i = 0; i < arraySize.Length; ++i)
                nestedStruct[i] = c;
            data[index] = GetValue(nestedStruct, localIndex);
        }

        [Theory]
        [MemberData(nameof(ArraySimpleTestData))]
        [KernelMethod(nameof(ArrayInStructureKernel))]
        public void ArrayInStructure<T, TArraySize>(T value, TArraySize _)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate1D<T>(1);
            Execute<Index1D, T, TArraySize>(
                buffer.IntExtent,
                buffer.AsContiguous(),
                value,
                0);

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }

        public struct MyData<T>
            where T : unmanaged
        {
            public T[] First;
            public int Second;
            public int Third;
            public int Fourth;
            public long Fifth;
        }

        internal static void ArrayInMultiFieldStructureKernel<T>(
            Index1D index,
            ArrayView<long> view)
            where T : unmanaged
        {
            var d = new MyData<T>();
            d.Fifth = 42;
            view[index] = d.Fifth;
        }

        [Theory]
        [MemberData(nameof(MultiDimArraySimpleTestData))]
        [KernelMethod(nameof(ArrayInMultiFieldStructureKernel))]
        public void ArrayInMultiFieldStructure<T, TArraySize>(T value, TArraySize _)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate1D<long>(1);
            Execute<Index1D, T>(1, buffer.AsContiguous());

            var expected = new long[] { 42 };
            Verify(buffer.View, expected);
        }

        // -----------------------------------------------------------------------
        // MultiDim Arrays
        // -----------------------------------------------------------------------

        public static TheoryData<object, object> MultiDimArraySimpleTestData =>
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
        };

        internal static void MultiDimArraySimpleKernel<T, TArraySize>(
            Index1D index,
            ArrayView<T> dataX,
            ArrayView<T> dataY,
            ArrayView<T> dataZ,
            ArrayView<T> dataW,
            T c,
            int localIndex)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[
                arraySize.Length,
                arraySize.Length,
                arraySize.Length,
                arraySize.Length];
            for (int i = 0; i < array.GetLength(2); ++i)
                array[i, i, i, i] = c;

            dataX[index] = array[0, 0, 0, localIndex];
            dataY[index] = array[0, 0, localIndex, 0];
            dataZ[index] = array[0, localIndex, 0, 0];
            dataW[index] = array[localIndex, 0, 0, 0];
        }

        [Theory]
        [MemberData(nameof(MultiDimArraySimpleTestData))]
        [KernelMethod(nameof(MultiDimArraySimpleKernel))]
        public void MultiDimArraySimple<T, TArraySize>(T value, TArraySize arraySize)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer1 = Accelerator.Allocate1D<T>(1);
            using var buffer2 = Accelerator.Allocate1D<T>(1);
            using var buffer3 = Accelerator.Allocate1D<T>(1);
            using var buffer4 = Accelerator.Allocate1D<T>(1);

            for (int i = 0; i < arraySize.Length; ++i)
            {
                Execute<Index1D, T, TArraySize>(
                    buffer1.IntExtent,
                    buffer1.AsContiguous(),
                    buffer2.AsContiguous(),
                    buffer3.AsContiguous(),
                    buffer4.AsContiguous(),
                    value,
                    0);

                var expected = new T[] { value };
                Verify(buffer1.View, expected);
                Verify(buffer2.View, expected);
                Verify(buffer3.View, expected);
                Verify(buffer4.View, expected);
            }
        }

        internal static void MultiDimArrayLengthKernel<T, TArraySize>(
            Index1D index,
            ArrayView<int> data,
            ArrayView<long> longData)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[
                arraySize.Length,
                arraySize.Length,
                arraySize.Length,
                arraySize.Length];

            data[index] = array.Length;
            data[index + 1] = array.GetLength(3);

            longData[index] = array.LongLength;
            longData[index + 1] = array.GetLongLength(3);
        }

        [Theory]
        [MemberData(nameof(MultiDimArraySimpleTestData))]
        [KernelMethod(nameof(MultiDimArrayLengthKernel))]
        public void MultiDimArrayLength<T, TArraySize>(T _, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate1D<int>(2);
            using var buffer2 = Accelerator.Allocate1D<long>(2);
            Execute<Index1D, T, TArraySize>(
                1,
                buffer.AsContiguous(),
                buffer2.AsContiguous());

            var expected = new int[]
            {
                size.Length * size.Length * size.Length * size.Length,
                size.Length
            };
            Verify(buffer.View, expected);
            Verify(buffer2.View, expected.Select(t => (long)t).ToArray());
        }

        internal static void MultiDimArrayBoundsKernel<T, TArraySize>(
            Index1D index,
            ArrayView<int> data)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var array = new T[
                arraySize.Length,
                arraySize.Length,
                arraySize.Length,
                arraySize.Length];
            data[index] = array.GetLowerBound(3);
            data[index + 1] = array.GetUpperBound(3);
        }

        [Theory]
        [MemberData(nameof(MultiDimArraySimpleTestData))]
        [KernelMethod(nameof(ArrayBoundsKernel))]
        public void MultiDimArrayBounds<T, TArraySize>(T _, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            using var buffer = Accelerator.Allocate1D<int>(2);
            Execute<Index1D, T, TArraySize>(1, buffer.AsContiguous());

            var expected = new int[] { 0, size.Length - 1 };
            Verify(buffer.View, expected);
        }

        // -----------------------------------------------------------------------
        // Array view conversions
        // -----------------------------------------------------------------------

        public static TheoryData<object, object> ArrayViewConversionTestData =>
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
            { default(EmptyStruct), default(Length1) },
            { default(TestStruct), default(Length1) },
            { default(TestStruct<TestStruct<byte>>), default(Length1) },

            { byte.MaxValue, default(Length2) },
            { short.MinValue, default(Length2) },
            { int.MinValue, default(Length2) },
            { long.MinValue, default(Length2) },
            { default(EmptyStruct), default(Length2) },
            { default(TestStruct), default(Length2) },
            { default(TestStruct<TestStruct<byte>>), default(Length2) },
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetViewValues<T>(ArrayView<T> view, T value)
            where T : unmanaged
        {
            for (int i = 0; i < view.Length; ++i)
                view[i] = value;
        }

        internal static void ArrayAsContiguousViewKernel<T, TArraySize>(
            Index1D index,
            T value,
            ArrayView<T> view)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var data = new T[arraySize.Length];

            SetViewValues(data.AsContiguousArrayView(), value);

            view[index] = data[index];
        }

        [Theory]
        [MemberData(nameof(ArrayViewConversionTestData))]
        [KernelMethod(nameof(ArrayAsContiguousViewKernel))]
        public void ArrayAsContiguousView<T, TArraySize>(T value, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            int length = Math.Min(size.Length, 16);
            using var buffer = Accelerator.Allocate1D<T>(length);
            Execute<Index1D, T, TArraySize>(length, value, buffer.AsContiguous());

            var expected = Enumerable.Repeat(value, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayAsView2DKernel<T, TArraySize>(
            Index1D index,
            T value,
            ArrayView<T> view)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var data = new T[arraySize.Length, arraySize.Length];

            SetViewValues(data.AsArrayView().AsContiguous(), value);

            view[index] = data[index, index];
        }

        [Theory]
        [MemberData(nameof(ArrayViewConversionTestData))]
        [KernelMethod(nameof(ArrayAsView2DKernel))]
        public void ArrayAsView2D<T, TArraySize>(T value, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            int length = Math.Min(size.Length, 2);
            using var buffer = Accelerator.Allocate1D<T>(length);
            Execute<Index1D, T, TArraySize>(length, value, buffer.AsContiguous());

            var expected = Enumerable.Repeat(value, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayAsView3DKernel<T, TArraySize>(
            Index1D index,
            T value,
            ArrayView<T> view)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            TArraySize arraySize = default;
            var data = new T[arraySize.Length, arraySize.Length, arraySize.Length];

            SetViewValues(data.AsArrayView().AsContiguous(), value);

            view[index] = data[index, index, index];
        }

        [Theory]
        [MemberData(nameof(ArrayViewConversionTestData))]
        [KernelMethod(nameof(ArrayAsView3DKernel))]
        public void ArrayAsView3D<T, TArraySize>(T value, TArraySize size)
            where T : unmanaged
            where TArraySize : unmanaged, ILength
        {
            int length = Math.Min(size.Length, 2);
            using var buffer = Accelerator.Allocate1D<T>(length);
            Execute<Index1D, T, TArraySize>(length, value, buffer.AsContiguous());

            var expected = Enumerable.Repeat(value, length).ToArray();
            Verify(buffer.View, expected);
        }

        // -----------------------------------------------------------------------
        // Array constants
        // -----------------------------------------------------------------------

        private static readonly int[,,,] StaticData = new int[,,,]
        {
            { { { 0, 1, 2, 3 } } },
            { { { 3, 4, 5, 6 } } },
            { { { 7, 8, 9, 10 } } },
        };

        private static readonly ImmutableArray<int> StaticImmutableData =
            ImmutableArray.Create(
                1, 2, 3, 4);

        internal static void MultiDimStaticArrayKernel(
            Index1D index,
            ArrayView<int> data)
        {
            data[index] = StaticData[0, 0, 0, index + 3];
            data[index + 1] = StaticData[1, 0, 0, index + 3];
            data[index + 2] = StaticData[2, 0, 0, index + 3];
        }

        [Fact]
        [KernelMethod(nameof(MultiDimStaticArrayKernel))]
        public void MultiDimStaticArray()
        {
            using var buffer = Accelerator.Allocate1D<int>(3);
            Execute(1, buffer.AsContiguous());

            var expected = new int[] { 3, 6, 10 };
            Verify(buffer.View, expected);
        }

        internal static void StaticImmutableArrayKernel(
            Index1D index,
            ArrayView<int> data)
        {
            data[index] = StaticImmutableData[index + 3];
        }

        [Fact]
        [KernelMethod(nameof(StaticImmutableArrayKernel))]
        public void StaticImmutableArray()
        {
            using var buffer = Accelerator.Allocate1D<int>(1);
            Execute(1, buffer.AsContiguous());

            var expected = new int[] { 4 };
            Verify(buffer.View, expected);
        }

        internal static void StaticInlineArrayKernel(
            Index1D index,
            ArrayView<int> data)
        {
            var staticInlineArray = new int[] { 1, 2, 3, 4 };
            data[index] = staticInlineArray[index + 3];
        }

        [Fact]
        [KernelMethod(nameof(StaticInlineArrayKernel))]
        public void StaticInlineArray()
        {
            using var buffer = Accelerator.Allocate1D<int>(1);
            Execute(1, buffer.AsContiguous());

            var expected = new int[] { 4 };
            Verify(buffer.View, expected);
        }

        internal static void ConditionalArrayFoldingKernel(
            Index1D index,
            ArrayView<int> buffer)
        {
            int[] values = new[] { 0, 1 };

            if (index == values[0])
                buffer[index] = 42;
            else
                buffer[index] = 24;
        }

        [Fact]
        [KernelMethod(nameof(ConditionalArrayFoldingKernel))]
        public void ConditionalArrayFolding()
        {
            using var buffer = Accelerator.Allocate1D<int>(4);
            Execute(buffer.IntExtent, buffer.AsContiguous());

            var expected = new int[] { 42, 24, 24, 24 };
            Verify(buffer.View, expected);
        }

        internal static void ConditionalArrayPartialFoldingKernel(
            Index1D index,
            ArrayView<int> buffer,
            int constant)
        {
            int[] values = new[] { 0, 1 };

            if (index == values[1] & constant == values[0])
                buffer[index] = 42;
            else
                buffer[index] = 24;
        }

        [InlineData(0)]
        [InlineData(1)]
        [Theory]
        [KernelMethod(nameof(ConditionalArrayPartialFoldingKernel))]
        public void ConditionalArrayPartialFolding(int constantValue)
        {
            using var buffer = Accelerator.Allocate1D<int>(4);
            Execute(4, buffer.AsContiguous(), constantValue);

            var expected = new int[] { 24, 42, 24, 24 };
            expected[constantValue] = 24;
            Verify(buffer.View, expected);
        }
    }
}

#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
#pragma warning restore xUnit1026 // Theory methods should use all of their parameters
