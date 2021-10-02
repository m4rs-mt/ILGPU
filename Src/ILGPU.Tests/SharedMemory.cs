// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class SharedMemory : TestBase
    {
        protected SharedMemory(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void SharedMemoryVariableKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            ref var sharedMemory = ref ILGPU.SharedMemory.Allocate<int>();
            if (Group.IsFirstThread)
                sharedMemory = 0;
            Group.Barrier();

            Atomic.Add(ref sharedMemory, 1);
            Group.Barrier();

            var idx = Grid.GlobalIndex.X;
            data[idx] = sharedMemory;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [KernelMethod(nameof(SharedMemoryVariableKernel))]
        public void SharedMemoryVariable(int groupMultiplier)
        {
            for (int i = 1; i < Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(i * groupMultiplier);
                var index = new KernelConfig(groupMultiplier, i);
                Execute(index, buffer.View);

                var expected = Enumerable.Repeat(i, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);
            }
        }

        internal static void SharedMemoryArrayKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var sharedMemory = ILGPU.SharedMemory.Allocate1D<int>(2);
            sharedMemory[Group.IdxX] = Group.IdxX;
            Group.Barrier();

            var idx = Grid.GlobalIndex.X;
            data[idx] = sharedMemory[(Group.IdxX + 1) % Group.DimX];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(32)]
        [KernelMethod(nameof(SharedMemoryArrayKernel))]
        public void SharedMemoryArray(int groupMultiplier)
        {
            using var buffer = Accelerator.Allocate1D<int>(2 * groupMultiplier);
            var index = new KernelConfig(groupMultiplier, 2);
            Execute(index, buffer.View);

            var expected = new int[buffer.Length];
            for (int i = 0; i < buffer.Length; i += 2)
            {
                expected[i] = 1;
                expected[i + 1] = 0;
            }

            Verify(buffer.View, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int AllocateSharedMemoryNested()
        {
            var sharedMemory = ILGPU.SharedMemory.Allocate1D<int>(1024);
            sharedMemory[Group.IdxX] = Group.IdxX;
            Group.Barrier();

            return sharedMemory[(Group.IdxX + 1) % Group.DimX];
        }

        internal static void SharedMemoryNestedKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var idx = Grid.GlobalIndex.X;
            data[idx] = AllocateSharedMemoryNested();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(32)]
        [KernelMethod(nameof(SharedMemoryNestedKernel))]
        public void SharedMemoryNested(int groupMultiplier)
        {
            using var buffer = Accelerator.Allocate1D<int>(2 * groupMultiplier);
            var index = new KernelConfig(groupMultiplier, 2);
            Execute(index, buffer.View);

            var expected = new int[buffer.Length];
            for (int i = 0; i < buffer.Length; i += 2)
            {
                expected[i] = 1;
                expected[i + 1] = 0;
            }

            Verify(buffer.View, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int DynamicSharedMemoryNested()
        {
            var dynamicMemory = ILGPU.SharedMemory.GetDynamic<byte>();
            dynamicMemory[Group.IdxX] = (byte)Group.IdxX;
            Group.Barrier();

            return dynamicMemory[(Group.IdxX + 1) % Group.DimX];
        }

        internal static void DynamicSharedMemoryKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var dynamicMemory = ILGPU.SharedMemory.GetDynamic<int>();
            dynamicMemory[Group.IdxX] = Group.IdxX;
            Group.Barrier();
            int value = dynamicMemory[(Group.IdxX + 1) % Group.DimX];

            var idx = Grid.GlobalIndex.X;
            data[idx] = value + DynamicSharedMemoryNested();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(32)]
        [KernelMethod(nameof(DynamicSharedMemoryKernel))]
        public void DynamicSharedMemory(int groupMultiplier)
        {
            using var buffer = Accelerator.Allocate1D<int>(2 * groupMultiplier);
            var config = SharedMemoryConfig.RequestDynamic<int>(1024);
            var index = new KernelConfig(groupMultiplier, 2, config);
            Execute(index, buffer.View);

            var expected = new int[buffer.Length];
            for (int i = 0; i < buffer.Length; i += 2)
            {
                expected[i] = 2;
                expected[i + 1] = 0;
            }

            Verify(buffer.View, expected);
        }

        private static void DynamicSharedMemoryLengthKernel<T>(
            ArrayView1D<long, Stride1D.Dense> output)
            where T : unmanaged
        {
            var dynamicMemory = ILGPU.SharedMemory.GetDynamic<T>();
            output[0] = dynamicMemory.Length;
            output[1] = dynamicMemory.IntLength;
            output[2] = dynamicMemory.LengthInBytes;
        }

        [Theory]
        [InlineData(default(byte), 11)]
        [InlineData(default(int), 13)]
        [InlineData(default(long), 15)]
        [InlineData(default(float), 17)]
        [InlineData(default(double), 19)]
        [KernelMethod(nameof(DynamicSharedMemoryLengthKernel))]
        [SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters",
            Justification = "Required to infer generic type argument")]
        public void DynamicSharedMemoryLength<T>(T elementType, int length)
            where T : unmanaged
        {
            var expectedSizeInBytes = length * Interop.SizeOf<T>();
            var expected = new long[] { length, length, expectedSizeInBytes };

            using var output = Accelerator.Allocate1D<long>(expected.Length);
            output.MemSetToZero();
            var config = SharedMemoryConfig.RequestDynamic<T>(length);
            var index = new KernelConfig(1, 2, config);
            Execute<KernelConfig, T>(index, output.View);
            Verify(output.View, expected);
        }

        internal static void ImplicitSharedMemoryKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = AllocateSharedMemoryNested();
        }

        [Fact]
        [KernelMethod(nameof(ImplicitSharedMemoryKernel))]
        public void ImplicitlyGroupedSharedMemory()
        {
            using var buffer = Accelerator.Allocate1D<int>(10);
            Assert.Throws<InternalCompilerException>(() =>
                Execute(buffer.Length, buffer.View));
        }

        internal static void MultiDimensionalSharedMemoryKernel2D(
            ArrayView1D<int, Stride1D.Dense> output)
        {
            var sharedMemory = ILGPU.SharedMemory.Allocate2D<int, Stride2D.DenseX>(
                new Index2D(5, 7),
                new Stride2D.DenseX(7));
            if (Group.IsFirstThread)
                sharedMemory[2, 6] = 42;
            Group.Barrier();
            output[Grid.GlobalIndex.X] = sharedMemory[2, 6];
        }

        [Fact]
        [KernelMethod(nameof(MultiDimensionalSharedMemoryKernel2D))]
        public void MultiDimensionalSharedMemory2D()
        {
            int groupSize = Accelerator.MaxNumThreadsPerGroup;
            using var buffer = Accelerator.Allocate1D<int>(groupSize);
            Execute(new KernelConfig(1, groupSize), buffer.View);

            var expected = Enumerable.Repeat(42, groupSize).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void MultiDimensionalSharedMemoryKernel3D(
            ArrayView1D<int, Stride1D.Dense> output)
        {
            var sharedMemory = ILGPU.SharedMemory.Allocate3D<int, Stride3D.DenseZY>(
                new Index3D(5, 7, 3),
                new Stride3D.DenseZY(5 * 7, 7));
            if (Group.IsFirstThread)
                sharedMemory[2, 6, 1] = 42;
            Group.Barrier();
            output[Grid.GlobalIndex.X] = sharedMemory[2, 6, 1];
        }

        [Fact]
        [KernelMethod(nameof(MultiDimensionalSharedMemoryKernel3D))]
        public void MultiDimensionalSharedMemory3D()
        {
            int groupSize = Accelerator.MaxNumThreadsPerGroup;
            using var buffer = Accelerator.Allocate1D<int>(groupSize);
            Execute(new KernelConfig(1, groupSize), buffer.View);

            var expected = Enumerable.Repeat(42, groupSize).ToArray();
            Verify(buffer.View, expected);
        }
    }
}
