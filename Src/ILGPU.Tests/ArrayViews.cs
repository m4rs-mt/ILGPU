using ILGPU.Runtime;
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

        internal static void ArrayViewValidKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            ArrayView<int> invalid = default;
            data[index] = (data.IsValid ? 1 : 0) + (!invalid.IsValid ? 1 : 0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewValidKernel))]
        public void ArrayViewValid(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(2, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLeaKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            data[index] = source[(int)index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLeaKernel))]
        public void ArrayViewLea(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate1D<int>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLeaIndexKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            data[index] = source[index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLeaIndexKernel))]
        public void ArrayViewLeaIndex(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate1D<int>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLongLeaIndexKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            LongIndex1D longIndex = index;
            data[longIndex] = source[longIndex];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLongLeaIndexKernel))]
        public void ArrayViewLongLeaIndex(long length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var expected = Enumerable.Range(0, (int)length).ToArray();
            using (var source = Accelerator.Allocate1D<int>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute((int)length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLengthKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = data.IntLength;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLengthKernel))]
        public void ArrayViewLength(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(length, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLongLengthKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data)
        {
            data[index] = data.Length;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLongLengthKernel))]
        public void ArrayViewLongLength(int length)
        {
            using var buffer = Accelerator.Allocate1D<long>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat((long)length, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayViewExtentKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = data.IntExtent.X;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewExtentKernel))]
        public void ArrayViewExtent(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat(length, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLongExtentKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data)
        {
            data[index] = data.Extent.X;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLongExtentKernel))]
        public void ArrayViewLongExtent(int length)
        {
            using var buffer = Accelerator.Allocate1D<long>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat((long)length, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLengthInBytesKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data)
        {
            data[index] = data.LengthInBytes;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLengthInBytesKernel))]
        public void ArrayViewLengthInBytes(int length)
        {
            using var buffer = Accelerator.Allocate1D<long>(length);
            Execute(length, buffer.View);

            var expected = Enumerable.Repeat((long)length * sizeof(long), length).
                ToArray();
            Verify(buffer.View, expected);
        }

        [Fact]
        public void ArrayViewZeroLengthSubView1D()
        {
            using var buffer = Accelerator.Allocate1D<int>(128);

            // Take a zero-length subview of a non-zero length view
            var subView1 = buffer.View.SubView(64, 0);
            Assert.Equal(0, subView1.Length);
            Assert.Equal(0, subView1.LengthInBytes);
            Assert.Equal(0, subView1.Extent.X);

            var subView2 = buffer.View.AsGeneral().SubView(64, 0);
            Assert.Equal(0, subView2.Length);
            Assert.Equal(0, subView2.LengthInBytes);
            Assert.Equal(0, subView2.Extent.X);

            // Take a zero-length subview of a zero length view
            var subView3 = subView1.SubView(0, 0);
            Assert.Equal(0, subView3.Length);
            Assert.Equal(0, subView3.LengthInBytes);
            Assert.Equal(0, subView3.Extent.X);

            var subView4 = subView2.SubView(0, 0);
            Assert.Equal(0, subView4.Length);
            Assert.Equal(0, subView4.LengthInBytes);
            Assert.Equal(0, subView4.Extent.X);

        }

        [Fact]
        public void ArrayViewZeroLengthSubView2D()
        {
            void Check<TStride>(ArrayView2D<int, TStride> view)
                where TStride : struct, IStride2D
            {
                var extents = new[]
                {
                    new Index2D(0, 2),
                    new Index2D(2, 0),
                };

                foreach (var extent in extents)
                {
                    // A subview with a zero in one extent dimension, of a view
                    // with non-zero extent in every dimension.
                    var subView = view.SubView((4, 4), extent);
                    Assert.Equal(0, subView.Length);
                    Assert.Equal(0, subView.LengthInBytes);
                    Assert.Equal(extent.X, subView.Extent.X);
                    Assert.Equal(extent.Y, subView.Extent.Y);

                    // A subview with a zero in one extent dimension, of a view
                    // with zero extent in the same dimension.
                    var subView2 = subView.SubView((0, 0), extent);
                    Assert.Equal(0, subView2.Length);
                    Assert.Equal(0, subView2.LengthInBytes);
                    Assert.Equal(extent.X, subView2.Extent.X);
                    Assert.Equal(extent.Y, subView2.Extent.Y);
                }
            }

            using var buff1 = Accelerator.Allocate2DDenseY<int>((10, 10));
            using var buff2 = Accelerator.Allocate2DDenseX<int>((10, 10));
            Check<Stride2D.DenseY>(buff1.View);
            Check<Stride2D.General>(buff1.View.AsGeneral());
            Check<Stride2D.DenseX>(buff2.View);
            Check<Stride2D.General>(buff2.View.AsGeneral());
        }

        [Fact]
        public void ArrayViewZeroLengthSubView3D()
        {
            void Check<TStride>(ArrayView3D<int, TStride> view)
                where TStride : struct, IStride3D
            {
                var extents = new[]
                {
                    new Index3D(0, 2, 2),
                    new Index3D(2, 0, 2),
                    new Index3D(2, 2, 0),
                };

                foreach (var extent in extents)
                {
                    // A subview with a zero in one extent dimension, of a view
                    // with non-zero extent in every dimension.
                    var subView = view.SubView((4, 4, 4), extent);
                    Assert.Equal(0, subView.Length);
                    Assert.Equal(0, subView.LengthInBytes);
                    Assert.Equal(extent.X, subView.Extent.X);
                    Assert.Equal(extent.Y, subView.Extent.Y);
                    Assert.Equal(extent.Z, subView.Extent.Z);

                    // A subview with a zero in one extent dimension, of a view
                    // with zero extent in the same dimension.
                    var subView2 = subView.SubView((0, 0, 0), extent);
                    Assert.Equal(0, subView2.Length);
                    Assert.Equal(0, subView2.LengthInBytes);
                    Assert.Equal(extent.X, subView2.Extent.X);
                    Assert.Equal(extent.Y, subView2.Extent.Y);
                    Assert.Equal(extent.Z, subView2.Extent.Z);
                }
            }

            using var buff1 = Accelerator.Allocate3DDenseXY<int>((10, 10, 10));
            using var buff2 = Accelerator.Allocate3DDenseZY<int>((10, 10, 10));
            Check<Stride3D.DenseXY>(buff1.View);
            Check<Stride3D.General>(buff1.View.AsGeneral());
            Check<Stride3D.DenseZY>(buff2.View);
            Check<Stride3D.General>(buff2.View.AsGeneral());
        }

        internal static void ArrayViewGetSubViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> length,
            ArrayView1D<int, Stride1D.Dense> source,
            int subViewOffset,
            int subViewLength)
        {
            var subView = source.SubView(subViewOffset, subViewLength);
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
            using var buffer = Accelerator.Allocate1D<int>(length);
            using var viewLength = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(length);
            var data = Enumerable.Range(0, length).ToArray();
            source.CopyFromCPU(Accelerator.DefaultStream, data);

            Execute(
                length,
                buffer.View,
                viewLength.View,
                source.View,
                subViewOffset,
                subViewLength);

            var expected = Enumerable.Repeat(subViewOffset, length).ToArray();
            Verify(buffer.View, expected);

            var expectedLength = Enumerable.Repeat(subViewLength, length).ToArray();
            Verify(viewLength.View, expectedLength);
        }

        internal static void ArrayViewGetSubViewImplicitLengthKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> length,
            ArrayView1D<int, Stride1D.Dense> source,
            int subViewOffset)
        {
            ArrayView<int> rawView = source;
            var subView = rawView.SubView(subViewOffset);
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
            using var viewLength = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(length);
            Execute(
                length,
                viewLength.View,
                source.View,
                subViewOffset);

            var expectedLength = Enumerable.Repeat(
                length - subViewOffset, length).ToArray();
            Verify(viewLength.View, expectedLength);
        }

        internal static void ArrayViewCastSmallerKernel(
            Index1D index,
            ArrayView1D<byte, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> length,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            var subView = source.Cast<int, byte>();
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
            using var buffer = Accelerator.Allocate1D<byte>(length);
            using var viewLength = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(length);
            var data = Enumerable.Repeat(-1, length).ToArray();
            source.CopyFromCPU(Accelerator.DefaultStream, data);

            Execute(
                length,
                buffer.View,
                viewLength.View,
                source.View);

            var expected = Enumerable.Repeat(byte.MaxValue, length).ToArray();
            Verify(buffer.View, expected);

            var expectedLength = Enumerable.Repeat(
                sizeof(int) / sizeof(byte) * length, length).ToArray();
            Verify(viewLength.View, expectedLength);
        }

        internal static void ArrayViewCastLargerKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> length,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            var subView = source.Cast<int, long>();
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
            using var buffer = Accelerator.Allocate1D<long>(length);
            using var viewLength = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(2);
            var data = new int[] { -1, -1 };
            source.CopyFromCPU(Accelerator.DefaultStream, data);

            Execute(
                length,
                buffer.View,
                viewLength.View,
                source.View);

            var expected = new long[length];
            for (int i = 0; i < length; ++i)
                expected[i] = -1L;
            Verify(buffer.View, expected);

            var expectedLength = Enumerable.Repeat(
                length / 2, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ArrayViewLinearViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            data[index] = source.AsContiguous()[index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewLinearViewKernel))]
        public void ArrayViewLinearView(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate1D<int>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal static void ArrayViewGetVariableViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            data[index] = source.VariableView(index).Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewGetVariableViewKernel))]
        public void ArrayViewGetVariableView(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate1D<int>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal static void VariableSubViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2,
            ArrayView1D<long, Stride1D.Dense> source)
        {
            var view = source.VariableView(index);
            data[index] = view.SubView<int>(0).Value;
            data2[index] = view.SubView<int>(sizeof(int)).Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [KernelMethod(nameof(VariableSubViewKernel))]
        public void VariableSubView(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            using var buffer2 = Accelerator.Allocate1D<int>(length);
            using (var source = Accelerator.Allocate1D<long>(length))
            {
                var expected = Enumerable.Repeat(
                    (long)int.MaxValue << 32 | ushort.MaxValue, length).ToArray();
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute(length, buffer.View, buffer2.View, source.View);
            }

            Verify(
                buffer.View,
                Enumerable.Repeat((int)ushort.MaxValue, length).ToArray());
            Verify(buffer2.View, Enumerable.Repeat(int.MaxValue, length).ToArray());
        }

        internal static void ArrayViewCastToGenericViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            ArrayView<int> otherSource = source;
            data[index] = otherSource[index];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewCastToGenericViewKernel))]
        public void ArrayViewCastToGenericView(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var expected = Enumerable.Range(0, length).ToArray();
            using (var source = Accelerator.Allocate1D<int>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, expected);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal struct Pair<T>
            where T : struct
        {
            public T First;
            public T Second;
        }

        internal static void ArrayViewGetSubVariableViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<Pair<int>, Stride1D.Dense> source)
        {
            var variableView = source.VariableView(index);
            var actualView = variableView.SubView<int>(sizeof(int));
            data[index] = actualView.Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(ArrayViewGetSubVariableViewKernel))]
        public void ArrayViewGetSubVariableView(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var sourceData = Enumerable.Range(0, length).Select(t =>
                new Pair<int>() { First = t, Second = t + 1 }).ToArray();
            var expected = Enumerable.Range(1, length).ToArray();
            using (var source = Accelerator.Allocate1D<Pair<int>>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, sourceData);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }

        internal static void ArrayViewMultidimensionalAccessKernel(
            Index1D index,
            ArrayView3D<int, Stride3D.DenseXY> data,
            ArrayView3D<int, Stride3D.DenseXY> source)
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
            var extent = new LongIndex3D(length);
            using var buffer = Accelerator.Allocate3DDenseXY<int>(extent);
            var expectedData = Enumerable.Range(0, (int)extent.Size).ToArray();
            using (var source = Accelerator.Allocate3DDenseXY<int>(extent))
            {
                source.AsContiguous().CopyFromCPU(
                    Accelerator.DefaultStream,
                    new ReadOnlySpan<int>(expectedData));
                Execute((int)extent.Size, buffer.View, source.View);
            }
            Verify(buffer.AsContiguous(), expectedData);
        }

        internal static void ArrayViewVectorizedIOKernel<T, T2>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> source,
            ArrayView1D<T, Stride1D.Dense> target)
            where T : unmanaged
            where T2 : unmanaged
        {
            // Use compile-time known offsets to test the internal alignment rules

            var nonVectorAlignedSource = source.SubView(1, 2);
            var nonVectorAlignedCastedSource = nonVectorAlignedSource.Cast<T, T2>();

            var nonVectorAlignedTarget = target.SubView(1, 2);
            var nonVectorAlignedTargetCasted = nonVectorAlignedTarget.Cast<T, T2>();

            // Load from source and write to target
            T2 data = nonVectorAlignedCastedSource[index];
            nonVectorAlignedTargetCasted[index] = data;

            // Perform the same operations with compile-time known offsets

            var vectorAlignedSource = source.SubView(2, 2);
            var vectorAlignedCastedSource = vectorAlignedSource.Cast<T, T2>();

            var vectorAlignedTarget = target.SubView(2, 2);
            var vectorAlignedTargetCasted = vectorAlignedTarget.Cast<T, T2>();

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
            using var source = Accelerator.Allocate1D<T>(Length);
            using var target = Accelerator.Allocate1D<T2>(Length);

            Execute<Index1D, T, T2>(1, source.View, target.View.Cast<T2, T>());

            // Note that we don't have to check the result in this case. If the execution
            // succeeds, we already know that the vectorized IO access worked as intended
        }

        internal static void ArrayViewAlignmentKernel<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> data,
            ArrayView1D<long, Stride1D.Dense> prefixLength,
            ArrayView1D<long, Stride1D.Dense> mainLength,
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

            using var data = Accelerator.Allocate1D<T>(Length);

            using var prefixLengthData = Accelerator.Allocate1D<long>(NumThreads);
            using var mainLengthData = Accelerator.Allocate1D<long>(NumThreads);

            data.MemSetToZero();
            prefixLengthData.MemSetToZero();
            mainLengthData.MemSetToZero();
            Accelerator.Synchronize();

            Execute<Index1D, T>(
                NumThreads,
                data.View,
                prefixLengthData.View,
                mainLengthData.View,
                alignmentInBytes,
                value);

            var prefixLengths = prefixLengthData.GetAsArray1D();
            var mainLengths = mainLengthData.GetAsArray1D();

            // Check whether the prefix and main lengths are the same for all threads
            long prefixLength = prefixLengths[0];
            Verify(
                prefixLengthData.View,
                Enumerable.Repeat(prefixLength, NumThreads).ToArray());
            long mainLength = mainLengths[0];
            Verify(
                mainLengthData.View,
                Enumerable.Repeat(mainLength, NumThreads).ToArray());

            // Verify the alignment information on CPU and Cuda platforms
            if (Accelerator.AcceleratorType == Runtime.AcceleratorType.CPU ||
                Accelerator.AcceleratorType == Runtime.AcceleratorType.Cuda)
            {
                var (prefixView, mainView) = data.View.AlignTo(alignmentInBytes);

                // The prefix view address should be the same (CPU-code test)
                Assert.Equal(
                    prefixView.LoadEffectiveAddressAsPtr(),
                    data.NativePtr);

                // Determine the main view length using the raw pointers
                var mainViewPtr = mainView.LoadEffectiveAddressAsPtr();
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

            Verify(data.View, expected);
        }

        [Theory]
        [InlineData(4, 4, 2)]
        [InlineData(6, 6, 2)]
        [InlineData(8, 8, 2)]
        public void ArrayViewConversions(int length1, int length2, int length3)
        {
            long linearLength = length1 * length2 * length3;
            using var buffer = Accelerator.Allocate1D<int>(linearLength);

            // Verify 1D views
            var oneDView = new ArrayView1D<int, Stride1D.Dense>(
                buffer.View,
                buffer.Extent,
                default);
            Assert.Equal(linearLength, oneDView.Length);

            var oneDView2 = oneDView.To1DView();
            Assert.Equal(linearLength, oneDView2.Length);

            var oneDGeneral = oneDView2.AsGeneral(new Stride1D.General(2));
            Assert.Equal(linearLength * 2 - 1, oneDGeneral.Length);

            var oneDView3 = oneDGeneral.To1DView();
            Assert.Equal(linearLength * 2 - 1, oneDView3.Length);

            // Verify 2D views
            var twoDView = oneDView.As2DDenseXView(new LongIndex2D(length1, length2));
            Assert.Equal(length1 * length2, twoDView.Length);
            Assert.Equal(length1, twoDView.Extent.X);
            Assert.Equal(length2, twoDView.Extent.Y);
            Assert.Equal(1, twoDView.Stride.XStride);
            Assert.Equal(length1, twoDView.Stride.YStride);
            Assert.Equal(
                twoDView.Stride.StrideExtent.Size,
                length1);
            var twoDViewTransposed = twoDView.AsTransposed();
            Assert.Equal(
                twoDViewTransposed.Extent,
                new LongIndex2D(twoDView.Extent.Y, twoDView.Extent.X));
            Assert.Equal(twoDViewTransposed.Length, twoDView.Length);
            Assert.Equal(
                twoDViewTransposed.AsTransposed().Extent,
                twoDView.Extent);
            Assert.Equal(
                twoDViewTransposed.AsTransposed().Length,
                twoDView.Length);

            var oneD2DView = twoDView.To1DView();
            Assert.Equal(length1 * length2, oneD2DView.Extent.Size);
            Assert.Equal(length1 * length2, oneD2DView.Length);

            var twoDViewY = oneD2DView.As2DDenseYView(new LongIndex2D(length1, length2));
            Assert.Equal(length1 * length2, twoDViewY.Length);
            Assert.Equal(length1, twoDViewY.Extent.X);
            Assert.Equal(length2, twoDViewY.Extent.Y);
            Assert.Equal(1, twoDViewY.Stride.YStride);
            Assert.Equal(length2, twoDViewY.Stride.XStride);
            var twoDViewYTransposed = twoDViewY.AsTransposed();
            Assert.Equal(twoDViewYTransposed.Length, twoDViewY.Length);
            Assert.Equal(
                twoDViewYTransposed.Extent,
                new LongIndex2D(twoDViewY.Extent.Y, twoDViewY.Extent.X));
            Assert.Equal(
                twoDViewYTransposed.AsTransposed().Length,
                twoDViewY.Length);
            Assert.Equal(
                twoDViewYTransposed.AsTransposed().Extent,
                twoDView.Extent);

            // Verify 3D views
            var threeDView = oneDView.As3DDenseXYView(
                new LongIndex3D(length1, length2, length3));
            Assert.Equal(length1 * length2 * length3, threeDView.Length);
            Assert.Equal(length1, threeDView.Extent.X);
            Assert.Equal(length2, threeDView.Extent.Y);
            Assert.Equal(length3, threeDView.Extent.Z);
            Assert.Equal(1, threeDView.Stride.XStride);
            Assert.Equal(length1, threeDView.Stride.YStride);
            Assert.Equal(length1 * length2, threeDView.Stride.ZStride);
            var threeDViewTransposed = threeDView.AsTransposed();
            Assert.Equal(threeDViewTransposed.Length, threeDView.Length);
            Assert.Equal(
                threeDViewTransposed.Extent,
                new LongIndex3D(
                    threeDView.Extent.Z,
                    threeDView.Extent.Y,
                    threeDView.Extent.X));
            Assert.Equal(
                threeDViewTransposed.AsTransposed().Length,
                threeDView.Length);
            Assert.Equal(
                threeDViewTransposed.AsTransposed().Extent,
                threeDView.Extent);

            var oneD3DView = threeDView.To1DView();
            Assert.Equal(length1 * length2 * length3, oneD3DView.Extent.Size);
            Assert.Equal(1, oneD3DView.Stride.XStride);

            var threeDViewZ = oneD3DView.As3DDenseZYView(
                new LongIndex3D(length3, length2, length1));
            Assert.Equal(length1 * length2 * length3, threeDViewZ.Length);
            Assert.Equal(length3, threeDViewZ.Extent.X);
            Assert.Equal(length2, threeDViewZ.Extent.Y);
            Assert.Equal(length1, threeDViewZ.Extent.Z);
            Assert.Equal(1, threeDViewZ.Stride.ZStride);
            Assert.Equal(length2, threeDViewZ.Stride.YStride);
            Assert.Equal(length2 * length1, threeDViewZ.Stride.XStride);
        }
    }
}
