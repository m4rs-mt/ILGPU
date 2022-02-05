// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicMovement.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162
#pragma warning disable CA1508 // Avoid dead conditional code

namespace ILGPU.Tests
{
    public abstract class BasicMovement : TestBase
    {
        private const int MaxLength = 32;

        protected BasicMovement(ITestOutputHelper output, TestContext textContext)
            : base(output, textContext)
        { }

        public int Length => Math.Max(
            Math.Min(Accelerator.MaxNumThreadsPerGroup, MaxLength),
            2);

        internal static void MoveLoadsBarrierKernel(
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<Int2, Stride1D.Dense> target,
            int c)
        {
            var index = Grid.GlobalIndex.X;
            var value1 = source[index];
            var result = new Int2(Group.BarrierAnd(c > 0) ? 0 : 1, c);
            var value2 = source[index + 1];
            target[index] = result + new Int2(value1, value2);
        }

        [Fact]
        [KernelMethod(nameof(MoveLoadsBarrierKernel))]
        public void MoveLoadsBarrier()
        {
            using var source = Accelerator.Allocate1D<int>(Length + 1);
            using var target = Accelerator.Allocate1D<Int2>(Length);
            Initialize(source.View, 23);

            KernelConfig config = (1, Length);
            Execute(config, source.View, target.View, 13);

            var expected = Enumerable.Repeat(new Int2(23, 36), Length).ToArray();
            Verify(target.View, expected);
        }

        internal static void MoveLoadsStoresKernel(
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<Int2, Stride1D.Dense> target)
        {
            var index = Grid.GlobalIndex.X;
            var value1 = source[index];
            var value2 = target[index].Y;
            target[index] = new Int2(42, 42);
            var value3 = target[index].X;
            target[index] = new Int2(value1 + value2, value3);
        }

        [Fact]
        [KernelMethod(nameof(MoveLoadsStoresKernel))]
        public void MoveLoadsStores()
        {
            using var source = Accelerator.Allocate1D<int>(Length + 1);
            using var target = Accelerator.Allocate1D<Int2>(Length);
            Initialize(source.View, 23);
            Initialize(target.View, new Int2(1, 2));

            KernelConfig config = (1, Length);
            Execute(config, source.View, target.View);

            var expected = Enumerable.Repeat(new Int2(25, 42), Length).ToArray();
            Verify(target.View, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteToTarget(
            int index,
            ArrayView1D<Int2, Stride1D.Dense> target) =>
            target[index] = new Int2(42, 42);

        internal static void MoveLoadsStoresCallKernel(
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<Int2, Stride1D.Dense> target)
        {
            var index = Grid.GlobalIndex.X;
            var value1 = source[index];
            var value2 = target[index].Y;
            WriteToTarget(index, target);
            var value3 = target[index].X;
            target[index] = new Int2(value1 + value2, value3);
        }

        [Fact]
        [KernelMethod(nameof(MoveLoadsStoresCallKernel))]
        public void MoveLoadsStoresCall()
        {
            using var source = Accelerator.Allocate1D<int>(Length + 1);
            using var target = Accelerator.Allocate1D<Int2>(Length);
            Initialize(source.View, 23);
            Initialize(target.View, new Int2(1, 2));

            KernelConfig config = (1, Length);
            Execute(config, source.View, target.View);

            var expected = Enumerable.Repeat(new Int2(25, 42), Length).ToArray();
            Verify(target.View, expected);
        }

        internal static void MoveLoadsStoresAddressSpacesKernel(
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<Int2, Stride1D.Dense> target)
        {
            var shared = ILGPU.SharedMemory.Allocate1D<int>(MaxLength);
            var local = LocalMemory.Allocate1D<int>(MaxLength);
            var index = Grid.GlobalIndex.X;
            var value1 = source[index];
            shared[index] = 42;
            local[index] = shared[index];
            var value2 = source[index + 1];
            target[index] = new Int2(value1 + local[index], value2);
        }

        [Fact]
        [KernelMethod(nameof(MoveLoadsStoresAddressSpacesKernel))]
        public void MoveLoadsStoresAddressSpaces()
        {
            using var source = Accelerator.Allocate1D<int>(Length + 1);
            using var target = Accelerator.Allocate1D<Int2>(Length);
            Initialize(source.View, 23);
            Initialize(target.View, new Int2(1, 2));

            KernelConfig config = (1, Length);
            Execute(config, source.View, target.View);

            var expected = Enumerable.Repeat(new Int2(65, 23), Length).ToArray();
            Verify(target.View, expected);
        }

        internal static void MoveLoadsStoresAddressSpacesBarrierKernel(
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<Int2, Stride1D.Dense> target)
        {
            var shared = ILGPU.SharedMemory.Allocate1D<int>(MaxLength);
            var local = LocalMemory.Allocate1D<int>(MaxLength);
            var index = Grid.GlobalIndex.X;
            var value1 = source[index];
            shared[index] = Group.IdxX;
            Group.Barrier();
            local[index] = shared[Group.DimX - 2];
            Group.Barrier();
            var value2 = target[index];
            target[index] = new Int2(value1 + local[index], value2.X + value2.Y);
        }

        [Fact]
        [KernelMethod(nameof(MoveLoadsStoresAddressSpacesBarrierKernel))]
        public void MoveLoadsStoresAddressSpacesBarrier()
        {
            using var source = Accelerator.Allocate1D<int>(Length + 1);
            using var target = Accelerator.Allocate1D<Int2>(Length);
            Initialize(source.View, 23);
            Initialize(target.View, new Int2(1, 2));

            KernelConfig config = (1, Length);
            Execute(config, source.View, target.View);

            var expected = Enumerable
                .Repeat(new Int2(23 + Length - 2, 3), Length)
                .ToArray();
            Verify(target.View, expected);
        }

    }
}

#pragma warning restore CA1508 // Avoid dead conditional code
#pragma warning restore CS0162
