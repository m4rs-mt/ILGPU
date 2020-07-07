using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class WarpOperations : TestBase
    {
        protected WarpOperations(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void WarpDimensionKernel(
            Index1 index,
            ArrayView<int> length,
            ArrayView<int> idx)
        {
            length[index] = Warp.WarpSize;
            idx[index] = Warp.LaneIdx;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [KernelMethod(nameof(WarpDimensionKernel))]
        public void WarpDimension(int warpMultiplier)
        {
            var length = Accelerator.WarpSize * warpMultiplier;
            using var lengthBuffer = Accelerator.Allocate<int>(length);
            using var idxBuffer = Accelerator.Allocate<int>(length);
            Execute(length, lengthBuffer.View, idxBuffer.View);

            var expectedLength = Enumerable.Repeat(
                Accelerator.WarpSize, length).ToArray();
            Verify(lengthBuffer, expectedLength);

            var expectedIndices = new int[length];
            for (int i = 0; i < length; ++i)
                expectedIndices[i] = i % Accelerator.WarpSize;
            Verify(idxBuffer, expectedIndices);
        }

        internal static void WarpBarrierKernel(ArrayView<int> data)
        {
            var idx = Grid.GlobalIndex.X;
            Warp.Barrier();
            data[idx] = idx;
        }

        [Theory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(WarpBarrierKernel))]
        public void WarpBarrier(int length)
        {
            var warpSize = Accelerator.WarpSize;
            using var buffer = Accelerator.Allocate<int>(length * warpSize);
            var extent = new KernelConfig(
                length,
                warpSize);
            Execute(extent, buffer.View);

            var expected = Enumerable.Range(0, length * warpSize).ToArray();
            Verify(buffer, expected);
        }

        internal static void WarpBroadcastKernel(ArrayView<int> data)
        {
            var idx = Grid.GlobalIndex.X;
            data[idx] = Warp.Broadcast(Group.IdxX, Warp.WarpSize - 1);
        }

        [Theory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(WarpBroadcastKernel))]
        public void WarpBroadcast(int length)
        {
            var warpSize = Accelerator.WarpSize;
            using var buffer = Accelerator.Allocate<int>(length * warpSize);
            var extent = new KernelConfig(
                length,
                warpSize);
            Execute(extent, buffer.View);

            var expected = Enumerable.Repeat(warpSize - 1, length * warpSize).ToArray();
            Verify(buffer, expected);
        }

        internal static void WarpShuffleKernel(
            Index1 index,
            ArrayView<int> data)
        {
            var targetIdx = Warp.WarpSize - 1;
            data[index] = Warp.Shuffle(Warp.LaneIdx, targetIdx);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [KernelMethod(nameof(WarpShuffleKernel))]
        public void WarpShuffle(int warpMultiplier)
        {
            var length = Accelerator.WarpSize * warpMultiplier;
            using var dataBuffer = Accelerator.Allocate<int>(length);
            Execute(length, dataBuffer.View);

            var expected = Enumerable.Repeat(
                Accelerator.WarpSize - 1, length).ToArray();
            Verify(dataBuffer, expected);
        }

        internal static void WarpShuffleDownKernel(
            Index1 index,
            ArrayView<int> data,
            int shiftAmount)
        {
            data[index] = Warp.ShuffleDown(Warp.LaneIdx, shiftAmount);
        }

        [Theory]
        [InlineData(1)]
        [KernelMethod(nameof(WarpShuffleDownKernel))]
        public void WarpShuffleDown(int warpMultiplier)
        {
            for (
                int shiftAmount = 0;
                shiftAmount < Math.Min(4, Accelerator.WarpSize);
                ++shiftAmount)
            {
                var length = Accelerator.WarpSize * warpMultiplier;
                using var dataBuffer = Accelerator.Allocate<int>(length);
                Execute(length, dataBuffer.View, shiftAmount);

                var expected = new int[length];
                for (int i = 0; i < warpMultiplier; ++i)
                {
                    var baseIdx = i * Accelerator.WarpSize;
                    for (int j = 0; j < Accelerator.WarpSize - shiftAmount; ++j)
                        expected[baseIdx + j] = j + shiftAmount;

                    // Do no test the remaining values as they are undefined
                    // for (
                    //     int j = Accelerator.WarpSize - shiftAmount;
                    //     j < Accelerator.WarpSize;
                    //     ++j)
                    //     expected[baseIdx + j] = j;
                }

                Verify(dataBuffer, expected, 0, Accelerator.WarpSize - shiftAmount);
            }
        }

        internal static void WarpShuffleUpKernel(
            Index1 index,
            ArrayView<int> data,
            int shiftAmount)
        {
            data[index] = Warp.ShuffleUp(Warp.LaneIdx, shiftAmount);
        }

        [Theory]
        [InlineData(1)]
        [KernelMethod(nameof(WarpShuffleUpKernel))]
        public void WarpShuffleUp(int warpMultiplier)
        {
            for (
                int shiftAmount = 0;
                shiftAmount < Math.Min(4, Accelerator.WarpSize);
                ++shiftAmount)
            {
                var length = Accelerator.WarpSize * warpMultiplier;
                using var dataBuffer = Accelerator.Allocate<int>(length);
                Execute(length, dataBuffer.View, shiftAmount);

                var expected = new int[length];
                for (int i = 0; i < warpMultiplier; ++i)
                {
                    var baseIdx = i * Accelerator.WarpSize;
                    for (int j = shiftAmount; j < Accelerator.WarpSize; ++j)
                        expected[baseIdx + j] = j - shiftAmount;

                    // Do no test the remaining values as they are undefined
                    // for (int j = 0; j < shiftAmount; ++j)
                    //     expected[baseIdx + j] = j;
                }

                Verify(dataBuffer, expected, shiftAmount);
            }
        }

        internal static void WarpShuffleXorKernel(
            Index1 index,
            ArrayView<int> data)
        {
            var value = Warp.LaneIdx;
            for (int laneMask = Warp.WarpSize / 2; laneMask > 0; laneMask >>= 1)
            {
                var shuffled = Warp.ShuffleXor(value, laneMask);
                value += shuffled;
            }
            data[index] = value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [KernelMethod(nameof(WarpShuffleXorKernel))]
        public void WarpShuffleXor(int warpMultiplier)
        {
            var length = Accelerator.WarpSize * warpMultiplier;
            using var dataBuffer = Accelerator.Allocate<int>(length);
            Execute(length, dataBuffer.View);

            var expected = Enumerable.Repeat(
                (Accelerator.WarpSize * (Accelerator.WarpSize - 1)) / 2,
                length).ToArray();

            Verify(dataBuffer, expected);
        }
    }
}
