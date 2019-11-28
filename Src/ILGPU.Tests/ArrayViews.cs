using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class ArrayViews : TestBase
    {
        protected ArrayViews(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void ArrayViewValidKernel(Index index, ArrayView<int> data)
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
            Index index,
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
            Index index,
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

        internal static void ArrayViewLengthKernel(
            Index index,
            ArrayView<int> data)
        {
            data[index] = data.Length;
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

        internal static void ArrayViewExtentKernel(
            Index index,
            ArrayView<int> data)
        {
            data[index] = data.Extent.X;
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

        internal static void ArrayViewLengthInBytesKernel(
            Index index,
            ArrayView<int> data)
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
            using var buffer = Accelerator.Allocate<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(length * sizeof(int), length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ArrayViewGetSubViewKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> length,
            ArrayView<int> source,
            int subViewOffset,
            int subViewLength)
        {
            var subView = source.GetSubView(subViewOffset, subViewLength);
            data[index] = subView[0];
            length[index] = subView.Length;
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
        public void ArrayViewGetSubView(int length, int subViewOffset, int subViewLength)
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
            Index index,
            ArrayView<int> length,
            ArrayView<int> source,
            int subViewOffset)
        {
            var subView = source.GetSubView(subViewOffset);
            length[index] = subView.Length;
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

            var expectedLength = Enumerable.Repeat(length - subViewOffset, length).ToArray();
            Verify(viewLength, expectedLength);
        }

        internal static void ArrayViewCastSmallerKernel(
            Index index,
            ArrayView<byte> data,
            ArrayView<int> length,
            ArrayView<int> source)
        {
            var subView = source.Cast<byte>();
            data[index] = subView[0];
            length[index] = subView.Length;
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
            Index index,
            ArrayView<long> data,
            ArrayView<int> length,
            ArrayView<int> source)
        {
            var subView = source.Cast<long>();
            data[index] = subView[0];
            length[index] = subView.Length;
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
            Index index,
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
            Index index,
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

        internal static void ArrayViewCastToGenericViewKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            ArrayView<int, Index> otherKernel = source;
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
            Index index,
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
            var sourceData = Enumerable.Range(0, length).Select(t => new Pair<int>() { First = t, Second = t + 1 }).ToArray();
            var expected = Enumerable.Range(1, length).ToArray();
            using (var source = Accelerator.Allocate<Pair<int>>(length))
            {
                source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, length);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer, expected);
        }
    }
}
