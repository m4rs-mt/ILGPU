// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertIntOperations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract partial class ConvertIntOperations : TestBase
    {
        protected ConvertIntOperations(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void TruncateIntKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> input,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            var i = (int)input[index];
            output[i] = i;
        }

        [Fact]
        [KernelMethod(nameof(TruncateIntKernel))]
        public void TruncateIntConversion()
        {
            // NB: High 32-bits are discarded.
            const int Length = 64;
            var inputValues = Enumerable.Range(1, Length)
                .Select(x => Length - x + 0x5_0000_0000)
                .ToArray();
            var expected = Enumerable.Range(0, Length).ToArray();

            using var input = Accelerator.Allocate1D<long>(inputValues);
            using var output = Accelerator.Allocate1D<int>(input.Length);
            Execute(Length, input.View, output.View);
            Verify(output.View, expected);
        }

        internal static void TruncateSmallerKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> input,
            ArrayView1D<short, Stride1D.Dense> output)
        {
            var i = (int)input[index];
            output[(short)i] = (short)i;
        }

        [Fact]
        [KernelMethod(nameof(TruncateSmallerKernel))]
        public void TruncateSmallerConversion()
        {
            const int Length = 64;
            var inputValues = Enumerable.Range(1, Length)
                .Select(x => Length - x + 0x5_4321_0000)
                .ToArray();
            var expected = Enumerable.Range(0, Length)
                .Select(x => (short)x)
                .ToArray();

            using var input = Accelerator.Allocate1D<long>(inputValues);
            using var output = Accelerator.Allocate1D<short>(input.Length);
            Execute(Length, input.View, output.View);
            Verify(output.View, expected);
        }

        internal static void PromoteLarger_ByteToInt_Kernel(
            Index1D index,
            byte threshold,
            ArrayView1D<byte, Stride1D.Dense> input,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            var lhs = input[index] > threshold ? input[index] : default;
            output[index] = lhs;
        }

        [Fact]
        [KernelMethod(nameof(PromoteLarger_ByteToInt_Kernel))]
        public void PromoteLarger_ByteToInt_Conversion()
        {
            const int Start = 200;
            const int Length = 64;
            const byte Threshold = Start + 32;
            var inputValues = Enumerable.Range(Start, Length)
                .Select(x => (byte)x)
                .ToArray();
            var expected = Enumerable.Range(Start, Length)
                .Select(x => (byte)x)
                .Select(x => x > Threshold ? x : default)
                .Select(x => (int)x)
                .ToArray();

            using var input = Accelerator.Allocate1D<byte>(inputValues);
            using var output = Accelerator.Allocate1D<int>(input.Length);
            Execute(Length, Threshold, input.View, output.View);
            Verify(output.View, expected);
        }

        internal static void PromoteLarger_UShortToInt_Kernel(
            Index1D index,
            ushort threshold,
            ArrayView1D<ushort, Stride1D.Dense> input,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            var lhs = input[index] > threshold ? input[index] : default;
            output[index] = lhs;
        }

        [Fact]
        [KernelMethod(nameof(PromoteLarger_UShortToInt_Kernel))]
        public void PromoteLarger_UShortToInt_Conversion()
        {
            const int Start = 60_000;
            const int Length = 64;
            const ushort Threshold = Start + 32;
            var inputValues = Enumerable.Range(Start, Length)
                .Select(x => (ushort)x)
                .ToArray();
            var expected = Enumerable.Range(Start, Length)
                .Select(x => (ushort)x)
                .Select(x => x > Threshold ? x : default)
                .Select(x => (int)x)
                .ToArray();

            using var input = Accelerator.Allocate1D<ushort>(inputValues);
            using var output = Accelerator.Allocate1D<int>(input.Length);
            Execute(Length, Threshold, input.View, output.View);
            Verify(output.View, expected);
        }

        internal static void ImplicitCastAdditionKernel(
            Index1D index,
            ArrayView1D<uint, Stride1D.Dense> input,
            ArrayView1D<uint, Stride1D.Dense> output)
        {
            output[index] += (byte)input[index];
        }

        [Fact]
        [KernelMethod(nameof(ImplicitCastAdditionKernel))]
        public void ImplicitCastAddition()
        {
            const int length = 32;
            using var input = Accelerator.Allocate1D<uint>(length);
            using var output = Accelerator.Allocate1D<uint>(length);
            Initialize(input.View, (uint)byte.MaxValue);
            Initialize(output.View, (uint)0);
            Execute(length, input.View, output.View);

            uint result;
            unchecked
            {
                result = byte.MaxValue;
            }
            var reference = Enumerable.Repeat(result, length).ToArray();
            Verify(output.View, reference);
        }
    }
}
