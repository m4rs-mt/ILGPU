using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class ArrayViews : TestBase
    {
        protected ArrayViews(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void ArrayViewValidKernel(Index1 index, ArrayView<int> data)
        {
            ArrayView<int> invalid = default;
            data[index] = (data.IsValid ? 1 : 0) + (!invalid.IsValid ? 1 : 0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewValidKernel))]
        public void ArrayViewValid(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(2, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewLeaKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            data[index] = source[(int)index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLeaKernel))]
        public void ArrayViewLea(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate<int>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal static void ArrayViewLeaIndexKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            data[index] = source[index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLeaIndexKernel))]
        public void ArrayViewLeaIndex(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate<int>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal static void ArrayViewLongLeaIndexKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            LongIndex1 longIndex = index;
            data[longIndex] = source[longIndex];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLongLeaIndexKernel))]
        public void ArrayViewLongLeaIndex(long length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Range(0, (int)length).ToArray();
            using (var source = Accelerator.Allocate<int>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute((int)length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal static void ArrayViewLengthKernel(
            Index1 index,
            ArrayView<int> data)
        {
            data[index] = data.IntLength;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLengthKernel))]
        public void ArrayViewLength(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(length, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewLongLengthKernel(
            Index1 index,
            ArrayView<long> data)
        {
            data[index] = data.Length;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLongLengthKernel))]
        public void ArrayViewLongLength(int length)
        {
            using var buffer = Accelerator.Allocate<long>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat((long)length, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewExtentKernel(
            Index1 index,
            ArrayView<int> data)
        {
            data[index] = data.IntExtent.X;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewExtentKernel))]
        public void ArrayViewExtent(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(length, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewLongExtentKernel(
            Index1 index,
            ArrayView<long> data)
        {
            data[index] = data.Extent.X;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLongExtentKernel))]
        public void ArrayViewLongExtent(int length)
        {
            using var buffer = Accelerator.Allocate<long>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat((long)length, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewLengthInBytesKernel(
            Index1 index,
            ArrayView<long> data)
        {
            data[index] = data.LengthInBytes;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLengthInBytesKernel))]
        public void ArrayViewLengthInBytes(int length)
        {
            using var buffer = Accelerator.Allocate<long>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat((long)length * sizeof(long), length).
                ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewGetSubViewKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> length,
            ArrayView<int> source,
            int subViewOffset,
            int subViewLength)
        {
            var subView = source.GetSubView(subViewOffset, subViewLength);
            data[index] = subView[0];
            length[index] = subView.IntLength;
        }

        [Theory]
        [InlineData(1, 0, 1)]
        [InlineData(17, 0, 2)]
        [InlineData(17, 12, 2)]
        [InlineData(17, 16, 1)]
        [InlineData(1025, 0, 129)]
        [InlineData(1025, 319, 371)]
        [InlineData(1025, 723, 129)]
        [InlineData(1025, 1024, 1)]
        [KernelMethod(nameof(ArrayViewGetSubViewKernel))]
        public void ArrayViewGetSubView(
            int length,
            int subViewOffset,
            int subViewLength)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            using var viewLength = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            var data = Enumerable.Range(0, length).ToArray();
            source.CopyFrom(Accelerator.DefaultStream, data, 0, 0, length);

            Execute(
                length,
                buffer.View,
                viewLength.View,
                source.View,
                subViewOffset,
                subViewLength);

            var expected = Enumerable.Repeat(subViewOffset, length).ToArray();
            Verify(buffer, expected);

            var expectedLength = Enumerable.Repeat(subViewLength, length).ToArray();
            Verify(viewLength, expectedLength);
        }

        internal static void ArrayViewGetSubViewImplicitLengthKernel(
            Index1 index,
            ArrayView<int> length,
            ArrayView<int> source,
            int subViewOffset)
        {
            var subView = source.GetSubView(subViewOffset);
            length[index] = subView.IntLength;
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(17, 0)]
        [InlineData(17, 12)]
        [InlineData(17, 16)]
        [InlineData(1025, 0)]
        [InlineData(1025, 319)]
        [InlineData(1025, 723)]
        [InlineData(1025, 1024)]
        [KernelMethod(nameof(ArrayViewGetSubViewImplicitLengthKernel))]
        public void ArrayViewGetSubViewImplicitLength(int length, int subViewOffset)
        {
            using var viewLength = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            Execute(
                length,
                viewLength.View,
                source.View,
                subViewOffset);

            var expectedLength = Enumerable.Repeat(
                length - subViewOffset, length).ToArray();
            Verify(viewLength, expectedLength);
        }

        internal static void ArrayViewCastSmallerKernel(
            Index1 index,
            ArrayView<byte> data,
            ArrayView<int> length,
            ArrayView<int> source)
        {
            var subView = source.Cast<byte>();
            data[index] = subView[0];
            length[index] = subView.IntLength;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(17)]
        [InlineData(64)]
        [InlineData(1025)]
        [KernelMethod(nameof(ArrayViewCastSmallerKernel))]
        public void ArrayViewCastSmaller(int length)
        {
            using var buffer = Accelerator.Allocate<byte>(length);
            using var viewLength = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            var data = new int[] { -1 };
            source.CopyFrom(Accelerator.DefaultStream, data, 0, 0, data.Length);

            Execute(
                length,
                buffer.View,
                viewLength.View,
                source.View);

            var expected = Enumerable.Repeat(byte.MaxValue, length).ToArray();
            Verify(buffer, expected);

            var expectedLength = Enumerable.Repeat(
                sizeof(int) / sizeof(byte) * length, length).ToArray();
            Verify(viewLength, expectedLength);
        }

        internal static void ArrayViewCastLargerKernel(
            Index1 index,
            ArrayView<long> data,
            ArrayView<int> length,
            ArrayView<int> source)
        {
            var subView = source.Cast<long>();
            data[index] = subView[0];
            length[index] = subView.IntLength;
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(17)]
        [InlineData(64)]
        [InlineData(1025)]
        [KernelMethod(nameof(ArrayViewCastLargerKernel))]
        public void ArrayViewCastLarger(int length)
        {
            using var buffer = Accelerator.Allocate<long>(length);
            using var viewLength = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(2);
            var data = new int[] { -1, -1 };
            source.CopyFrom(Accelerator.DefaultStream, data, 0, 0, data.Length);

            Execute(
                length,
                buffer.View,
                viewLength.View,
                source.View);

            var expected = new long[length];
            for (int i = 0; i < length; ++i)
                expected[i] = -1L;
            Verify(buffer, expected);

            var expectedLength = Enumerable.Repeat(
                length / 2, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewLinearViewKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            data[index] = source.AsLinearView()[index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewLinearViewKernel))]
        public void ArrayViewLinearView(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate<int>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal static void ArrayViewGetVariableViewKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            data[index] = source.GetVariableView(index).Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewGetVariableViewKernel))]
        public void ArrayViewGetVariableView(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate<int>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal static void VariableSubViewKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> data2,
            ArrayView<long> source)
        {
            var view = source.GetVariableView(index);
            data[index] = view.GetSubView<int>(0).Value;
            data2[index] = view.GetSubView<int>(sizeof(int)).Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [KernelMethod(nameof(VariableSubViewKernel))]
        public void VariableSubView(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            using var buffer2 = Accelerator.Allocate<int>(length);
            using (var source = Accelerator.Allocate<long>(length))
            {
                var expected = Enumerable.Repeat(
                    (long)int.MaxValue << 32 | ushort.MaxValue, length).ToArray();
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute(length, buffer.View, buffer2.View, source.View);
            }

            Verify(buffer, Enumerable.Repeat((int)ushort.MaxValue, length).ToArray());
            Verify(buffer2, Enumerable.Repeat(int.MaxValue, length).ToArray());
        }

        internal static void ArrayViewCastToGenericViewKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            ArrayView<int, Index1> otherKernel = source;
            data[index] = otherKernel[index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewCastToGenericViewKernel))]
        public void ArrayViewCastToGenericView(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate<int>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal struct Pair<T>
            where T : struct
        {
            public T First;
            public T Second;
        }

        internal static void ArrayViewGetSubVariableViewKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<Pair<int>> source)
        {
            var variableView = source.GetVariableView(index);
            var actualView = variableView.GetSubView<int>(sizeof(int));
            data[index] = actualView.Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(ArrayViewGetSubVariableViewKernel))]
        public void ArrayViewGetSubVariableView(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var sourceData = Enumerable.Range(0, length).Select(t =>
                new Pair<int>() { First = t, Second = t + 1 }).ToArray();
            var expected = Enumerable.Range(1, length).ToArray();
            using (var source = Accelerator.Allocate<Pair<int>>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }

        internal static void ArrayViewMultidimensionalAccessKernel(
            Index1 index,
            ArrayView<int, LongIndex3> data,
            ArrayView<int, LongIndex3> source)
        {
            var reconstructedIndex = data.Extent.ReconstructIndex(index);
            data[reconstructedIndex] = source[reconstructedIndex];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(127)]
        [KernelMethod(nameof(ArrayViewMultidimensionalAccessKernel))]
        public void ArrayViewMultidimensionalAccess(long length)
        {
            var extent = new LongIndex3(length);
            using var buffer = Accelerator.Allocate<int, LongIndex3>(extent);
            var expectedData = Enumerable.Range(0, (int)extent.Size).ToArray();
            using (var source = Accelerator.Allocate<int, LongIndex3>(extent))
            {
                source.CopyFrom(
                    Accelerator.DefaultStream,
                    expectedData,
                    0,
                    LongIndex3.Zero,
                    buffer.Length);
                Execute((int)extent.Size, buffer.View, source.View);
            }
            Verify(buffer, expectedData);
        }

        internal static void ArrayViewVectorizedIOKernel<T, T2>(
            Index1 index,
            ArrayView<T> source,
            ArrayView<T> target)
            where T : unmanaged
            where T2 : unmanaged
        {
            // Use compile-time known offsets to test the internal alignment rules

            var nonVectorAlignedSource = source.GetSubView(1, 2);
            var nonVectorAlignedCastedSource = nonVectorAlignedSource.Cast<T2>();

            var nonVectorAlignedTarget = target.GetSubView(1, 2);
            var nonVectorAlignedTargetCasted = nonVectorAlignedTarget.Cast<T2>();

            // Load from source and write to target
            T2 data = nonVectorAlignedCastedSource[index];
            nonVectorAlignedTargetCasted[index] = data;

            // Perform the same operations with compile-time known offsets

            var vectorAlignedSource = source.GetSubView(2, 2);
            var vectorAlignedCastedSource = vectorAlignedSource.Cast<T2>();

            var vectorAlignedTarget = target.GetSubView(2, 2);
            var vectorAlignedTargetCasted = vectorAlignedTarget.Cast<T2>();

            // Load from source and write to target
            T2 data2 = vectorAlignedCastedSource[index];
            vectorAlignedTargetCasted[index] = data2;
        }

        public static TheoryData<object, object> VectorizedIOData =>
            new TheoryData<object, object>
            {
                { default(int), default(PairStruct<int, int>) },
                { default(long), default(PairStruct<long, long>) },
                {
                    default(PairStruct<int, int>),
                    default(PairStruct<PairStruct<int, int>, PairStruct<int, int>>)
                },
                {
                    default(PairStruct<long, long>),
                    default(PairStruct<PairStruct<long, long>, PairStruct<long, long>>)
                },

                { default(float), default(PairStruct<float, float>) },
                { default(double), default(PairStruct<double, double>) },
                {
                    default(PairStruct<float, float>),
                    default(
                        PairStruct<
                            PairStruct<float, float>,
                            PairStruct<float, float>>)
                },
                {
                    default(PairStruct<double, double>),
                    default(
                        PairStruct<
                            PairStruct<double, double>,
                            PairStruct<double, double>>)
                },
            };

        [Theory]
        [MemberData(nameof(VectorizedIOData))]
        [KernelMethod(nameof(ArrayViewVectorizedIOKernel))]
        [SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters")]
        public void ArrayViewVectorizedIO<T, T2>(T sourceType, T2 targetType)
            where T : unmanaged
            where T2 : unmanaged
        {
            const int Length = 4;
            using var source = Accelerator.Allocate<T>(Length);
            using var target = Accelerator.Allocate<T2>(Length);

            Execute<Index1, T, T2>(1, source.View, target.View.Cast<T>());

            // Note that we don't have to check the result in this case. If the execution
            // succeeds, we already know that the vectorized IO access worked as intended
        }

        internal static void ArrayViewAlignmentKernel<T>(
            Index1 index,
            ArrayView<T> data,
            ArrayView<long> prefixLength,
            ArrayView<long> mainLength,
            int alignmentInBytes,
            T element)
            where T : unmanaged
        {
            var (prefix, main) = data.AlignTo(alignmentInBytes);

            prefixLength[index] = prefix.Length;
            mainLength[index] = main.Length;

            if (index < prefix.Length)
                prefix[index] = element;

            Trace.Assert(main.Length > 0);
            main[index] = element;
        }

        public static TheoryData<object, object> AlignToData =>
            new TheoryData<object, object>
            {
                { 8, int.MaxValue },
                { 16, int.MaxValue },
                { 32, int.MaxValue },
                { 64, int.MaxValue },
                { 128, int.MaxValue },
                { 256, int.MaxValue },
                { 512, int.MaxValue },

                { 16, long.MaxValue },
                { 32, long.MaxValue },
                { 64, long.MaxValue },
                { 128, long.MaxValue },
                { 256, long.MaxValue },
                { 512, long.MaxValue },

                { 16, PairStruct.MaxFloats },
                { 32, PairStruct.MaxFloats },
                { 64, PairStruct.MaxFloats },
                { 128, PairStruct.MaxFloats },
                { 256, PairStruct.MaxFloats },
                { 512, PairStruct.MaxFloats },

                { 32, PairStruct.MaxDoubles },
                { 64, PairStruct.MaxDoubles },
                { 128, PairStruct.MaxDoubles },
                { 256, PairStruct.MaxDoubles },
                { 512, PairStruct.MaxDoubles },
            };

        [Theory]
        [MemberData(nameof(AlignToData))]
        [KernelMethod(nameof(ArrayViewAlignmentKernel))]
        public unsafe void ArrayViewAlignment<T>(int alignmentInBytes, T value)
            where T : unmanaged
        {
            const int Length = 8192;
            const int NumThreads = 1024;

            using var data = Accelerator.Allocate<T>(Length);

            using var prefixLengthData = Accelerator.Allocate<long>(NumThreads);
            using var mainLengthData = Accelerator.Allocate<long>(NumThreads);

            data.MemSetToZero();
            prefixLengthData.MemSetToZero();
            mainLengthData.MemSetToZero();
            Accelerator.Synchronize();

            Execute<Index1, T>(
                NumThreads,
                data.View,
                prefixLengthData.View,
                mainLengthData.View,
                alignmentInBytes,
                value);

            var prefixLengths = prefixLengthData.GetAsArray();
            var mainLengths = mainLengthData.GetAsArray();

            // Check whether the prefix and main lengths are the same for all threads
            long prefixLength = prefixLengths[0];
            Verify(
                prefixLengthData,
                Enumerable.Repeat(prefixLength, NumThreads).ToArray());
            long mainLength = mainLengths[0];
            Verify(
                mainLengthData,
                Enumerable.Repeat(mainLength, NumThreads).ToArray());

            // Verify the alignment information on CPU and Cuda platforms
            if (Accelerator.AcceleratorType == Runtime.AcceleratorType.CPU ||
                Accelerator.AcceleratorType == Runtime.AcceleratorType.Cuda)
            {
                var (prefixView, mainView) = data.View.AlignTo(alignmentInBytes);

                // The prefix view address should be the same (CPU-code test)
                Assert.Equal(
                    new IntPtr(prefixView.LoadEffectiveAddress()),
                    data.NativePtr);

                // Determine the main view length using the raw pointers
                var mainViewPtr = mainView.LoadEffectiveAddress();
                long prefixViewLength = ((long)mainViewPtr - data.NativePtr.ToInt64()) /
                    Interop.SizeOf<T>();
                Assert.Equal(prefixLength, prefixViewLength);
                Assert.Equal(prefixLength, prefixView.Length);
                Assert.Equal(mainLength, mainView.Length);

                // Align the pointers explicitly and compare them
                Assert.Equal(0, (long)mainViewPtr & (alignmentInBytes - 1));
            }

            // Check the actual data content
            var expected = new T[Length];
            for (int i = 0; i < NumThreads; ++i)
            {
                if (i < prefixLength)
                    expected[i] = value;
                expected[prefixLength + i] = value;
            }

            Verify(data, expected);
        }
    }
}
