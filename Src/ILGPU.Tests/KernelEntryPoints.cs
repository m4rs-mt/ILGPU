using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class KernelEntryPoints : TestBase
    {
        protected KernelEntryPoints(
            ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        internal static void Index1EntryPointKernel(Index1 index, ArrayView<int> output)
        {
            output[index] = index;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        [KernelMethod(nameof(Index1EntryPointKernel))]
        public void Index1EntryPoint(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Range(0, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void Index2EntryPointKernel(
            Index2 index,
            ArrayView<int> output,
            Index2 extent)
        {
            var linearIndex = index.ComputeLinearIndex(extent);
            output[linearIndex] = linearIndex;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        [KernelMethod(nameof(Index2EntryPointKernel))]
        public void Index2EntryPoint(int length)
        {
            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        internal static void Index3EntryPointKernel(
            Index3 index,
            ArrayView<int> output,
            Index3 extent)
        {
            var linearIndex = index.ComputeLinearIndex(extent);
            output[linearIndex] = linearIndex;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        [KernelMethod(nameof(Index3EntryPointKernel))]
        public void Index3EntryPoint(int length)
        {
            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        internal static void GroupedIndex1EntryPointKernel(
            ArrayView<int> output, int stride)
        {
            var idx = Grid.IdxX * stride + Group.IdxX;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        [KernelMethod(nameof(GroupedIndex1EntryPointKernel))]
        public void GroupedIndex1EntryPoint(int length)
        {
            for (int i = 1; i < Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                var extent = new KernelConfig(length, i);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                Execute(extent, buffer.View, i);

                var expected = new int[extent.Size];
                for (int j = 0; j < length; ++j)
                {
                    for (int k = 0; k < i; ++k)
                    {
                        var idx = j * i + k;
                        expected[idx] = idx;
                    }
                }

                Verify(buffer, expected);
            }
        }

        internal static void GroupedIndex2EntryPointKernel(
            ArrayView<int> output, Index2 stride, Index2 extent)
        {
            var idx1 = Grid.Index.X * stride.X + Group.Index.X;
            var idx2 = Grid.Index.Y * stride.Y + Group.Index.Y;
            var idx = idx2 * extent.X + idx1;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(129)]
        [KernelMethod(nameof(GroupedIndex2EntryPointKernel))]
        public void GroupedIndex2EntryPoint(int length)
        {
            var end = (int)Math.Sqrt(Accelerator.MaxNumThreadsPerGroup);
            for (int i = 1; i <= end; i <<= 1)
            {
                var stride = new Index2(i, i);
                var extent = new KernelConfig(
                    new Index2(length, length),
                    stride);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                buffer.MemSetToZero(Accelerator.DefaultStream);
                Execute(extent, buffer.View, stride, extent.GridDim.XY);

                var expected = new int[extent.Size];
                for (int j = 0; j < length * length; ++j)
                {
                    var gridIdx = Index2.ReconstructIndex(j, extent.GridDim.XY);
                    for (int k = 0; k < i * i; ++k)
                    {
                        var groupIdx = Index2.ReconstructIndex(k, extent.GroupDim.XY);
                        var idx = (gridIdx * stride + groupIdx).ComputeLinearIndex(
                            extent.GridDim.XY);
                        expected[idx] = idx;
                    }
                }

                Verify(buffer, expected);
            }
        }

        internal static void GroupedIndex3EntryPointKernel(
            ArrayView<int> output, Index3 stride, Index3 extent)
        {
            var idx1 = Grid.Index.X * stride.X + Group.Index.X;
            var idx2 = Grid.Index.Y * stride.Y + Group.Index.Y;
            var idx3 = Grid.Index.Z * stride.Z + Group.Index.Z;
            var idx = ((idx3 * extent.Y) + idx2) * extent.X + idx1;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [KernelMethod(nameof(GroupedIndex3EntryPointKernel))]
        public void GroupedIndex3EntryPoint(int length)
        {
            var end = (int)Math.Pow(Accelerator.MaxNumThreadsPerGroup, 1.0 / 3.0);
            for (int i = 1; i <= end; i <<= 1)
            {
                var stride = new Index3(i, i, i);
                var extent = new KernelConfig(
                    new Index3(length, length, length),
                    stride);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                buffer.MemSetToZero(Accelerator.DefaultStream);
                Execute(extent, buffer.View, stride, extent.GridDim);

                var expected = new int[extent.Size];
                for (int j = 0; j < length * length * length; ++j)
                {
                    var gridIdx = Index3.ReconstructIndex(j, extent.GridDim);
                    for (int k = 0; k < i * i * i; ++k)
                    {
                        var groupIdx = Index3.ReconstructIndex(k, extent.GroupDim);
                        var idx = (gridIdx * stride + groupIdx).ComputeLinearIndex(
                            extent.GridDim);
                        expected[idx] = idx;
                    }
                }

                Verify(buffer, expected);
            }
        }

        internal class InstaceHost
        {
            [SuppressMessage(
                "Performance",
                "CA1822:Mark members as static",
                Justification = "For testing instance method")]
            public int InstanceOffset()
            {
                return 24;
            }

            private int NestedFunction(int value)
            {
                return value + InstanceOffset();
            }

            public void InstanceKernel(Index1 index, ArrayView<int> output)
            {
                output[index] = NestedFunction(index);
            }

            public void InstanceKernel(Index2 index, ArrayView<int> output, Index2 extent)
            {
                var linearIndex = index.ComputeLinearIndex(extent);
                output[linearIndex] = NestedFunction(linearIndex);
            }

            public void InstanceKernel(Index3 index, ArrayView<int> output, Index3 extent)
            {
                var linearIndex = index.ComputeLinearIndex(extent);
                output[linearIndex] = NestedFunction(linearIndex);
            }
        }

        internal static class CaptureHost
        {
            public static int CaptureProperty => 42;
            public static int CaptureField = 37;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        public void NonCapturingLambdaIndex1EntryPoint(int length)
        {
            Action<Index1, ArrayView<int>> kernel =
                (index, output) =>
                {
                    output[index] = index;
                };

            using var buffer = Accelerator.Allocate<int>(length);
            Execute(kernel.Method, new Index1((int)buffer.Length), buffer.View);

            var expected = Enumerable.Range(0, length).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        public void NonCapturingLambdaIndex2EntryPoint(int length)
        {
            Action<Index2, ArrayView<int>, Index2> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex;
                };

            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        public void NonCapturingLambdaIndex3EntryPoint(int length)
        {
            Action<Index3, ArrayView<int>, Index3> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex;
                };

            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        public void InstanceMethodIndex1EntryPoint(int length)
        {
            var instanceHost = new InstaceHost();
            Action<Index1, ArrayView<int>> kernel = instanceHost.InstanceKernel;

            using var buffer = Accelerator.Allocate<int>(length);
            Execute(kernel.Method, new Index1((int)buffer.Length), buffer.View);

            var expected =
                Enumerable.Range(instanceHost.InstanceOffset(), length).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        public void InstanceMethodIndex2EntryPoint(int length)
        {
            var instanceHost = new InstaceHost();
            Action<Index2, ArrayView<int>, Index2> kernel = instanceHost.InstanceKernel;

            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(instanceHost.InstanceOffset(), extent.Size).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        public void InstanceMethodIndex3EntryPoint(int length)
        {
            var instanceHost = new InstaceHost();
            Action<Index3, ArrayView<int>, Index3> kernel = instanceHost.InstanceKernel;

            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(instanceHost.InstanceOffset(), extent.Size).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        public void StaticPropertyCapturingLambdaIndex1EntryPoint(int length)
        {
            Action<Index1, ArrayView<int>> kernel =
                (index, output) =>
                {
                    output[index] = index + CaptureHost.CaptureProperty;
                };

            using var buffer = Accelerator.Allocate<int>(length);
            Execute(kernel.Method, new Index1((int)buffer.Length), buffer.View);

            var expected =
                Enumerable.Range(CaptureHost.CaptureProperty, length).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        public void StaticPropertyCapturingLambdaIndex2EntryPoint(int length)
        {
            Action<Index2, ArrayView<int>, Index2> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureProperty;
                };

            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(CaptureHost.CaptureProperty, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        public void StaticPropertyCapturingLambdaIndex3EntryPoint(int length)
        {
            Action<Index3, ArrayView<int>, Index3> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureProperty;
                };

            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(CaptureHost.CaptureProperty, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        public void LocalCapturingLambdaIndex1EntryPoint(int length)
        {
            var capturedVariable = 1;
            Action<Index1, ArrayView<int>> kernel =
                (index, output) =>
                {
                    output[index] = index + capturedVariable;
                };

            using var buffer = Accelerator.Allocate<int>(length);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, new Index1((int)buffer.Length), buffer.View));
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        public void LocalCapturingLambdaIndex2EntryPoint(int length)
        {
            var capturedVariable = 1;
            Action<Index2, ArrayView<int>, Index2> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex + capturedVariable;
                };

            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        public void LocalCapturingLambdaIndex3EntryPoint(int length)
        {
            var capturedVariable = 1;
            Action<Index3, ArrayView<int>, Index3> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex + capturedVariable;
                };

            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        public void StaticFieldCapturingLambdaIndex1EntryPoint(int length)
        {
            Action<Index1, ArrayView<int>> kernel =
                (index, output) =>
                {
                    output[index] = index + CaptureHost.CaptureField;
                };

            using var buffer = Accelerator.Allocate<int>(length);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, new Index1((int)buffer.Length), buffer.View));
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        public void StaticFieldCapturingLambdaIndex2EntryPoint(int length)
        {
            Action<Index2, ArrayView<int>, Index2> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureField;
                };

            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        public void StaticFieldCapturingLambdaIndex3EntryPoint(int length)
        {
            Action<Index3, ArrayView<int>, Index3> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = index.ComputeLinearIndex(extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureField;
                };

            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
        }
    }
}
