using System;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class ReinterpretCasts : TestBase
    {
        public const uint SignBitFloat = 1U << 31;
        public const ulong SignBitDouble = 1UL << 63;

        public ReinterpretCasts(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void ReinterpretFloatsKernel(
            Index index,
            ArrayView<float> data,
            ArrayView<double> data2,
            float value,
            double value2)
        {
            var floatVal = Interop.FloatAsInt(value);
            floatVal &= ~SignBitFloat;
            data[index] = Interop.IntAsFloat(floatVal);

            var doubleVal = Interop.FloatAsInt(value2);
            doubleVal &= ~SignBitDouble;
            data2[index] = Interop.IntAsFloat(doubleVal);
        }

        [Theory]
        [InlineData(0.0f, 0.0)]
        [InlineData(1.0f, 1.0)]
        [InlineData(-1.0f, -1.0)]
        [InlineData(3.0f, 3.0)]
        [InlineData(-3.0f, -3.0)]
        [InlineData(float.MaxValue, double.MaxValue)]
        [InlineData(float.MinValue, double.MinValue)]
        [InlineData(float.NaN, double.NaN)]
        [KernelMethod(nameof(ReinterpretFloatsKernel))]
        public void ReinterpretFloats(float value, double value2)
        {
            using var buffer = Accelerator.Allocate<float>(1);
            using var buffer2 = Accelerator.Allocate<double>(1);
            Execute(buffer.Length, buffer.View, buffer2.View, value, value2);

            var expected = new float[] { Math.Abs(value) };
            var expected2 = new double[] { Math.Abs(value2) };
            Verify(buffer, expected);
            Verify(buffer2, expected2);
        }
    }
}
