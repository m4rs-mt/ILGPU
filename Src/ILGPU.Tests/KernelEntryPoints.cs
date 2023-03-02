// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelEntryPoints.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime;
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

        /// <summary cref="IDisposable.Dispose"/>
        protected override void Dispose(bool disposing)
        {
            // Clear the internal caches in case they will not be cleared
            if (!CleanTests)
                TestContext.ClearCaches();
            base.Dispose(disposing);
        }

        internal static void Index1EntryPointKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            output[index] = index;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(1025)]
        [KernelMethod(nameof(Index1EntryPointKernel))]
        public void Index1EntryPoint(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Range(0, length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void Index2EntryPointKernel(
            Index2D index,
            ArrayView1D<int, Stride1D.Dense> output,
            Index2D extent)
        {
            var linearIndex = Stride2D.DenseX.ComputeElementIndex(index, extent);
            output[linearIndex] = linearIndex;
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(513)]
        [KernelMethod(nameof(Index2EntryPointKernel))]
        public void Index2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            var extent = new Index2D(length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void Index3EntryPointKernel(
            Index3D index,
            ArrayView1D<int, Stride1D.Dense> output,
            Index3D extent)
        {
            var linearIndex = Stride3D.DenseXY.ComputeElementIndex(index, extent);
            output[linearIndex] = linearIndex;
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(257)]
        [KernelMethod(nameof(Index3EntryPointKernel))]
        public void Index3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            var extent = new Index3D(length, length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void GroupedIndex1EntryPointKernel(
            ArrayView1D<int, Stride1D.Dense> output,
            int stride)
        {
            var idx = Grid.IdxX * stride + Group.IdxX;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(1025)]
        [KernelMethod(nameof(GroupedIndex1EntryPointKernel))]
        public void GroupedIndex1EntryPoint(int length)
        {
            for (int i = 1; i < Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                var extent = new KernelConfig(length, i);
                using var buffer = Accelerator.Allocate1D<int>(extent.Size);
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

                Verify(buffer.View, expected);
            }
        }

        internal static void GroupedIndex2EntryPointKernel(
            ArrayView1D<int, Stride1D.Dense> output,
            Index2D stride,
            Index2D extent)
        {
            var idx1 = Grid.Index.X * stride.X + Group.Index.X;
            var idx2 = Grid.Index.Y * stride.Y + Group.Index.Y;
            var idx = idx2 * extent.X + idx1;
            output[idx] = idx;
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(129)]
        [KernelMethod(nameof(GroupedIndex2EntryPointKernel))]
        public void GroupedIndex2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            var end = (int)Math.Sqrt(Accelerator.MaxNumThreadsPerGroup);
            for (int i = 1; i <= end; i <<= 1)
            {
                var stride = new Index2D(i, i);
                var extent = new KernelConfig(
                    new Index2D(length, length),
                    stride);
                using var buffer = Accelerator.Allocate1D<int>(extent.Size);
                buffer.MemSetToZero(Accelerator.DefaultStream);
                Execute(extent, buffer.View, stride, extent.GridDim.XY);

                var expected = new int[extent.Size];
                for (int j = 0; j < length * length; ++j)
                {
                    var gridIdx = Stride2D.DenseX.ReconstructFromElementIndex(
                        j,
                        extent.GridDim.XY);
                    for (int k = 0; k < i * i; ++k)
                    {
                        var groupIdx = Stride2D.DenseX.ReconstructFromElementIndex(
                            k,
                            extent.GroupDim.XY);
                        var idx = Stride2D.DenseX.ComputeElementIndex(
                            gridIdx * stride + groupIdx,
                            extent.GridDim.XY);
                        expected[idx] = idx;
                    }
                }

                Verify(buffer.View, expected);
            }
        }

        internal static void GroupedIndex3EntryPointKernel(
            ArrayView1D<int, Stride1D.Dense> output,
            Index3D stride, Index3D extent)
        {
            var idx1 = Grid.Index.X * stride.X + Group.Index.X;
            var idx2 = Grid.Index.Y * stride.Y + Group.Index.Y;
            var idx3 = Grid.Index.Z * stride.Z + Group.Index.Z;
            var idx = ((idx3 * extent.Y) + idx2) * extent.X + idx1;
            output[idx] = idx;
        }

        [SkippableTheory]
        [InlineData(33)]
        [KernelMethod(nameof(GroupedIndex3EntryPointKernel))]
        public void GroupedIndex3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            var end = (int)Math.Pow(Accelerator.MaxNumThreadsPerGroup, 1.0 / 3.0);
            for (int i = 1; i <= end; i <<= 1)
            {
                var stride = new Index3D(i, i, i);
                var extent = new KernelConfig(
                    new Index3D(length, length, length),
                    stride);
                using var buffer = Accelerator.Allocate1D<int>(extent.Size);
                buffer.MemSetToZero(Accelerator.DefaultStream);
                Execute(extent, buffer.View, stride, extent.GridDim);

                var expected = new int[extent.Size];
                for (int j = 0; j < length * length * length; ++j)
                {
                    var gridIdx = Stride3D.DenseXY.ReconstructFromElementIndex(
                        j,
                        extent.GridDim);
                    for (int k = 0; k < i * i * i; ++k)
                    {
                        var groupIdx = Stride3D.DenseXY.ReconstructFromElementIndex(
                            k,
                            extent.GroupDim);
                        var idx = Stride3D.DenseXY.ComputeElementIndex(
                            gridIdx * stride + groupIdx,
                            extent.GridDim);
                        expected[idx] = idx;
                    }
                }

                Verify(buffer.View, expected);
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

            public void InstanceKernel(
                Index1D index,
                ArrayView1D<int, Stride1D.Dense> output)
            {
                output[index] = NestedFunction(index);
            }

            public void InstanceKernel(
                Index2D index,
                ArrayView1D<int, Stride1D.Dense> output,
                Index2D extent)
            {
                var linearIndex = Stride2D.DenseX.ComputeElementIndex(index, extent);
                output[linearIndex] = NestedFunction(linearIndex);
            }

            public void InstanceKernel(
                Index3D index,
                ArrayView1D<int, Stride1D.Dense> output,
                Index3D extent)
            {
                var linearIndex = Stride3D.DenseXY.ComputeElementIndex(index, extent);
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
        [InlineData(1025)]
        public void NonCapturingLambdaIndex1EntryPoint(int length)
        {
            Action<Index1D, ArrayView1D<int, Stride1D.Dense>> kernel =
                (index, output) =>
                {
                    output[index] = index;
                };

            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(kernel.Method, new Index1D((int)buffer.Length), buffer.View);

            var expected = Enumerable.Range(0, length).ToArray();
            Verify(buffer.View, expected);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(513)]
        public void NonCapturingLambdaIndex2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            Action<Index2D, ArrayView1D<int, Stride1D.Dense>, Index2D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride2D.DenseX.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex;
                };

            var extent = new Index2D(length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(257)]
        public void NonCapturingLambdaIndex3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            Action<Index3D, ArrayView1D<int, Stride1D.Dense>, Index3D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride3D.DenseXY.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex;
                };

            var extent = new Index3D(length, length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(1025)]
        public void InstanceMethodIndex1EntryPoint(int length)
        {
            var instanceHost = new InstaceHost();
            Action<Index1D, ArrayView1D<int, Stride1D.Dense>> kernel =
                instanceHost.InstanceKernel;

            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(kernel.Method, new Index1D((int)buffer.Length), buffer.View);

            var expected =
                Enumerable.Range(instanceHost.InstanceOffset(), length).ToArray();
            Verify(buffer.View, expected);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(513)]
        public void InstanceMethodIndex2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            var instanceHost = new InstaceHost();
            Action<Index2D, ArrayView1D<int, Stride1D.Dense>, Index2D> kernel =
                instanceHost.InstanceKernel;

            var extent = new Index2D(length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(instanceHost.InstanceOffset(), extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(257)]
        public void InstanceMethodIndex3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            var instanceHost = new InstaceHost();
            Action<Index3D, ArrayView1D<int, Stride1D.Dense>, Index3D> kernel =
                instanceHost.InstanceKernel;

            var extent = new Index3D(length, length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(instanceHost.InstanceOffset(), extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(1025)]
        public void StaticPropertyCapturingLambdaIndex1EntryPoint(int length)
        {
            Action<Index1D, ArrayView1D<int, Stride1D.Dense>> kernel =
                (index, output) =>
                {
                    output[index] = index + CaptureHost.CaptureProperty;
                };

            using var buffer = Accelerator.Allocate1D<int>(length);
            Execute(kernel.Method, new Index1D((int)buffer.Length), buffer.View);

            var expected =
                Enumerable.Range(CaptureHost.CaptureProperty, length).ToArray();
            Verify(buffer.View, expected);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(513)]
        public void StaticPropertyCapturingLambdaIndex2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            Action<Index2D, ArrayView1D<int, Stride1D.Dense>, Index2D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride2D.DenseX.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureProperty;
                };

            var extent = new Index2D(length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(CaptureHost.CaptureProperty, extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(257)]
        public void StaticPropertyCapturingLambdaIndex3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            Action<Index3D, ArrayView1D<int, Stride1D.Dense>, Index3D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride3D.DenseXY.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureProperty;
                };

            var extent = new Index3D(length, length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Execute(kernel.Method, extent, buffer.View, extent);

            var expected =
                Enumerable.Range(CaptureHost.CaptureProperty, extent.Size).ToArray();
            Verify(buffer.View, expected);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(1025)]
        public void LocalCapturingLambdaIndex1EntryPoint(int length)
        {
            var capturedVariable = 1;
            Action<Index1D, ArrayView1D<int, Stride1D.Dense>> kernel =
                (index, output) =>
                {
                    output[index] = index + capturedVariable;
                };

            using var buffer = Accelerator.Allocate1D<int>(length);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, new Index1D((int)buffer.Length), buffer.View));
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(513)]
        public void LocalCapturingLambdaIndex2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            var capturedVariable = 1;
            Action<Index2D, ArrayView1D<int, Stride1D.Dense>, Index2D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride2D.DenseX.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex + capturedVariable;
                };

            var extent = new Index2D(length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(257)]
        public void LocalCapturingLambdaIndex3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            var capturedVariable = 1;
            Action<Index3D, ArrayView1D<int, Stride1D.Dense>, Index3D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride3D.DenseXY.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex + capturedVariable;
                };

            var extent = new Index3D(length, length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            Assert.Throws<NotSupportedException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
        }

        private static void VerifyStaticFieldCapturingLambdaException(
            InternalCompilerException e)
        {
            // We would normally expect that a lambda that captures a static field would
            // throw a NotSupportedException. However, in DEBUG build configuration, the
            // the Method Builder is never marked as completed, and when running in
            // VisualStudio, a second exception is thrown from a failed Debug.Assert.
            // Deal with both expectations, so that unit tests pass in DEBUG mode when
            // running in VisualStudio.
#if DEBUG
            if (e.InnerException is not NotSupportedException)
            {
                Assert.Equal(
                    "Microsoft.VisualStudio.TestPlatform.TestHost.DebugAssertException",
                    e.InnerException?.GetType().FullName);
                return;
            }
#endif
            Assert.IsType<NotSupportedException>(e.InnerException);
        }

        [Theory]
        [InlineData(33)]
        [InlineData(1025)]
        public void StaticFieldCapturingLambdaIndex1EntryPoint(int length)
        {
            Action<Index1D, ArrayView1D<int, Stride1D.Dense>> kernel =
                (index, output) =>
                {
                    output[index] = index + CaptureHost.CaptureField;
                };

            using var buffer = Accelerator.Allocate1D<int>(length);
            var e = Assert.Throws<InternalCompilerException>(() =>
                Execute(kernel.Method, new Index1D((int)buffer.Length), buffer.View));
            VerifyStaticFieldCapturingLambdaException(e);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(513)]
        public void StaticFieldCapturingLambdaIndex2EntryPoint(int length)
        {
            Skip.If(length > Accelerator.MaxGroupSize.Y);

            Action<Index2D, ArrayView1D<int, Stride1D.Dense>, Index2D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride2D.DenseX.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureField;
                };

            var extent = new Index2D(length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            var e = Assert.Throws<InternalCompilerException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
            VerifyStaticFieldCapturingLambdaException(e);
        }

        [SkippableTheory]
        [InlineData(33)]
        [InlineData(257)]
        public void StaticFieldCapturingLambdaIndex3EntryPoint(int length)
        {
            Skip.If(
                length > Accelerator.MaxGroupSize.Y ||
                length > Accelerator.MaxGroupSize.Z);

            Action<Index3D, ArrayView1D<int, Stride1D.Dense>, Index3D> kernel =
                (index, output, extent) =>
                {
                    var linearIndex = Stride3D.DenseXY.ComputeElementIndex(index, extent);
                    output[linearIndex] = linearIndex + CaptureHost.CaptureField;
                };

            var extent = new Index3D(length, length, length);
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            var e = Assert.Throws<InternalCompilerException>(() =>
                Execute(kernel.Method, extent, buffer.View, extent));
            VerifyStaticFieldCapturingLambdaException(e);
        }

        [KernelName("My @ CustomKernel.Name12345 [1211]")]
        internal static void NamedEntryPointKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            output[index] = index;
        }

        [Fact]
        [KernelMethod(nameof(NamedEntryPointKernel))]
        public void NamedEntryPoint()
        {
            const int Length = 32;

            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Range(0, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal struct UnsupportedKernelParam
        {
            public int[] Data;
        }

        [Fact]
        public void UnsupportedEntryPointParameter()
        {
            Action<
                Index1D,
                ArrayView1D<int, Stride1D.Dense>,
                UnsupportedKernelParam> kernel =
                (index, output, param) =>
                {
                    output[index] = param.Data[index];
                };

            var extent = new Index1D(32);
            var param = new UnsupportedKernelParam() { Data = new int[extent.Size] };
            using var buffer = Accelerator.Allocate1D<int>(extent.Size);
            var e = Assert.Throws<ArgumentException>(() =>
                Execute(kernel.Method, extent, buffer.View, param));
            Assert.IsType<NotSupportedException>(e.InnerException);
        }
    }
}
