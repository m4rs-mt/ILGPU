// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
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
    }
}
