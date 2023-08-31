// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnaryFloatOperations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class UnaryFloatOperations : TestBase
    {
        protected UnaryFloatOperations(
            ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        public static TheoryData<object, object, object, object, object> HalfData =>
            new TheoryData<object, object, object, object, object>
        {
            { Half.PositiveInfinity, false, false, true, false },
            { Half.NegativeInfinity, false, false, false, true },
            { 0.0f, true, false, false, false },
            { Half.MaxValue, true, false, false, false },
            { Half.MinValue, true, false, false, false },
            { Half.NaN, false, true, false, false },
        };

        internal static void IsPredicateF16Kernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            Half value)
        {
            data[index + 0] = Half.IsFinite(value) ? 1 : 0;
            data[index + 1] = Half.IsInfinity(value) ? 1 : 0;
            data[index + 2] = Half.IsPositiveInfinity(value) ? 1 : 0;
            data[index + 3] = Half.IsNegativeInfinity(value) ? 1 : 0;
            data[index + 4] = Half.IsNaN(value) ? 1 : 0;
        }

        [Theory]
        [MemberData(nameof(HalfData))]
        [KernelMethod(nameof(IsPredicateF16Kernel))]
        public void IsPredicateF16(
            Half value,
            bool isFinite,
            bool isNaN,
            bool isPositiveInfinity,
            bool isNegativeInfinity)
        {
            using var buffer = Accelerator.Allocate1D<int>(5);
            Execute<Index1D>(1, buffer.View, value);

            var expected = new int[]
            {
                isFinite ? 1 : 0,
                isPositiveInfinity | isNegativeInfinity ? 1 : 0,
                isPositiveInfinity ? 1 : 0,
                isNegativeInfinity ? 1 : 0,
                isNaN ? 1 : 0,
            };
            Verify(buffer.View, expected);
        }

        internal static void IsPredicateF32Kernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            float value)
        {
            data[index + 0] = float.IsFinite(value) ? 1 : 0;
            data[index + 1] = float.IsInfinity(value) ? 1 : 0;
            data[index + 2] = float.IsPositiveInfinity(value) ? 1 : 0;
            data[index + 3] = float.IsNegativeInfinity(value) ? 1 : 0;
            data[index + 4] = float.IsNaN(value) ? 1 : 0;
        }

        [Theory]
        [InlineData(float.PositiveInfinity, false, false, true, false)]
        [InlineData(float.NegativeInfinity, false, false, false, true)]
        [InlineData(0.0f, true, false, false, false)]
        [InlineData(float.MaxValue, true, false, false, false)]
        [InlineData(float.MinValue, true, false, false, false)]
        [InlineData(float.NaN, false, true, false, false)]
        [KernelMethod(nameof(IsPredicateF32Kernel))]
        public void IsPredicateF32(
            float value,
            bool isFinite,
            bool isNaN,
            bool isPositiveInfinity,
            bool isNegativeInfinity)
        {
            using var buffer = Accelerator.Allocate1D<int>(5);
            Execute<Index1D>(1, buffer.View, value);

            var expected = new int[]
            {
                isFinite ? 1 : 0,
                isPositiveInfinity | isNegativeInfinity ? 1 : 0,
                isPositiveInfinity ? 1 : 0,
                isNegativeInfinity ? 1 : 0,
                isNaN ? 1 : 0,
            };
            Verify(buffer.View, expected);
        }

        internal static void IsPredicateF64Kernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            double value)
        {
            data[index + 0] = double.IsFinite(value) ? 1 : 0;
            data[index + 1] = double.IsInfinity(value) ? 1 : 0;
            data[index + 2] = double.IsPositiveInfinity(value) ? 1 : 0;
            data[index + 3] = double.IsNegativeInfinity(value) ? 1 : 0;
            data[index + 4] = double.IsNaN(value) ? 1 : 0;
        }

        [Theory]
        [InlineData(double.PositiveInfinity, false, false, true, false)]
        [InlineData(double.NegativeInfinity, false, false, false, true)]
        [InlineData(0.0f, true, false, false, false)]
        [InlineData(double.MaxValue, true, false, false, false)]
        [InlineData(double.MinValue, true, false, false, false)]
        [InlineData(double.NaN, false, true, false, false)]
        [KernelMethod(nameof(IsPredicateF64Kernel))]
        public void IsPredicateF64(
            double value,
            bool isFinite,
            bool isNaN,
            bool isPositiveInfinity,
            bool isNegativeInfinity)
        {
            using var buffer = Accelerator.Allocate1D<int>(5);
            Execute<Index1D>(1, buffer.View, value);

            var expected = new int[]
            {
                isFinite ? 1 : 0,
                isPositiveInfinity | isNegativeInfinity ? 1 : 0,
                isPositiveInfinity ? 1 : 0,
                isNegativeInfinity ? 1 : 0,
                isNaN ? 1 : 0,
            };
            Verify(buffer.View, expected);
        }
    }
}
