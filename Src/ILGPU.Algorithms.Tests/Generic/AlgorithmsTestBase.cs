// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: AlgorithmsTestBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Tests;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class AlgorithmsTestBase : TestBase
    {
        protected AlgorithmsTestBase(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        /// <summary>
        /// Compares two numbers for equality, within a defined tolerance.
        /// </summary>
        private class HalfPrecisionComparer
            : EqualityComparer<Half>
        {
            public readonly float Margin;

            public HalfPrecisionComparer(uint decimalPlaces) =>
                Margin = MathF.Pow(10, -decimalPlaces);

            public override bool Equals(Half x, Half y)
            {
                if ((Half.IsNaN(x) && Half.IsNaN(y)) ||
                    (Half.IsPositiveInfinity(x) && Half.IsPositiveInfinity(y)) ||
                    (Half.IsNegativeInfinity(x) && Half.IsNegativeInfinity(y)))
                {
                    return true;
                }
                else if ((Half.IsPositiveInfinity(x) && Half.IsNegativeInfinity(y)) ||
                    (Half.IsNegativeInfinity(x) && Half.IsPositiveInfinity(y)))
                {
                    return false;
                }

                return (float)Half.Abs(x - y) < Margin;
            }

            public override int GetHashCode(Half obj) =>
                obj.GetHashCode();
        }

        /// <summary>
        /// Compares two numbers for equality, within a defined tolerance.
        /// </summary>
        private class FloatPrecisionComparer
            : EqualityComparer<float>
        {
            public readonly float Margin;

            public FloatPrecisionComparer(uint decimalPlaces) =>
                Margin = MathF.Pow(10, -decimalPlaces);

            public override bool Equals(float x, float y)
            {
                if ((float.IsNaN(x) && float.IsNaN(y)) ||
                    (float.IsPositiveInfinity(x) && float.IsPositiveInfinity(y)) ||
                    (float.IsNegativeInfinity(x) && float.IsNegativeInfinity(y)))
                {
                    return true;
                }
                else if ((float.IsPositiveInfinity(x) && float.IsNegativeInfinity(y)) ||
                    (float.IsNegativeInfinity(x) && float.IsPositiveInfinity(y)))
                {
                    return false;
                }

                return Math.Abs(x - y) < Margin;
            }

            public override int GetHashCode(float obj) =>
                obj.GetHashCode();
        }

        /// <summary>
        /// Compares two numbers for equality, within a defined tolerance.
        /// </summary>
        private class DoublePrecisionComparer
            : EqualityComparer<double>
        {
            public readonly double Margin;

            public DoublePrecisionComparer(uint decimalPlaces) =>
                Margin = Math.Pow(10, -decimalPlaces);

            public override bool Equals(double x, double y)
            {
                if ((double.IsNaN(x) && double.IsNaN(y)) ||
                    (double.IsPositiveInfinity(x) && double.IsPositiveInfinity(y)) ||
                    (double.IsNegativeInfinity(x) && double.IsNegativeInfinity(y)))
                {
                    return true;
                }
                else if ((double.IsPositiveInfinity(x) && double.IsNegativeInfinity(y)) ||
                    (double.IsNegativeInfinity(x) && double.IsPositiveInfinity(y)))
                {
                    return false;
                }

                return Math.Abs(x - y) < Margin;
            }

            public override int GetHashCode(double obj) =>
                obj.GetHashCode();
        }

        /// <summary>
        /// Compares two numbers for equality, within a defined tolerance.
        /// </summary>
        private class HalfRelativeErrorComparer
            : EqualityComparer<Half>
        {
            public readonly float RelativeError;

            public HalfRelativeErrorComparer(float relativeError) =>
                RelativeError = relativeError;

            public override bool Equals(Half x, Half y)
            {
                if ((Half.IsNaN(x) && Half.IsNaN(y)) ||
                    (Half.IsPositiveInfinity(x) && Half.IsPositiveInfinity(y)) ||
                    (Half.IsNegativeInfinity(x) && Half.IsNegativeInfinity(y)))
                {
                    return true;
                }
                else if ((Half.IsPositiveInfinity(x) && Half.IsNegativeInfinity(y)) ||
                    (Half.IsNegativeInfinity(x) && Half.IsPositiveInfinity(y)))
                {
                    return false;
                }

                var diff = Half.Abs(x - y);
                if (diff == Half.Zero)
                    return true;
                if (x != Half.Zero)
                    return (float)Half.Abs(diff / x) < RelativeError;
                return false;
            }

            public override int GetHashCode(Half obj) =>
                obj.GetHashCode();
        }

        /// <summary>
        /// Compares two numbers for equality, within a defined tolerance.
        /// </summary>
        private class FloatRelativeErrorComparer
            : EqualityComparer<float>
        {
            public readonly float RelativeError;

            public FloatRelativeErrorComparer(float relativeError) =>
                RelativeError = relativeError;

            public override bool Equals(float x, float y)
            {
                if ((float.IsNaN(x) && float.IsNaN(y)) ||
                    (float.IsPositiveInfinity(x) && float.IsPositiveInfinity(y)) ||
                    (float.IsNegativeInfinity(x) && float.IsNegativeInfinity(y)))
                {
                    return true;
                }
                else if ((float.IsPositiveInfinity(x) && float.IsNegativeInfinity(y)) ||
                    (float.IsNegativeInfinity(x) && float.IsPositiveInfinity(y)))
                {
                    return false;
                }

                var diff = Math.Abs(x - y);

                if (diff == 0)
                    return true;

                if (x != 0)
                    return Math.Abs(diff / x) < RelativeError;

                return false;
            }

            public override int GetHashCode(float obj) =>
                obj.GetHashCode();
        }

        /// <summary>
        /// Compares two numbers for equality, within a defined tolerance.
        /// </summary>
        private class DoubleRelativeErrorComparer
            : EqualityComparer<double>
        {
            public readonly double RelativeError;

            public DoubleRelativeErrorComparer(double relativeError) =>
                RelativeError = relativeError;

            public override bool Equals(double x, double y)
            {
                if ((double.IsNaN(x) && double.IsNaN(y)) ||
                    (double.IsPositiveInfinity(x) && double.IsPositiveInfinity(y)) ||
                    (double.IsNegativeInfinity(x) && double.IsNegativeInfinity(y)))
                {
                    return true;
                }
                else if ((double.IsPositiveInfinity(x) && double.IsNegativeInfinity(y)) ||
                    (double.IsNegativeInfinity(x) && double.IsPositiveInfinity(y)))
                {
                    return false;
                }

                var diff = Math.Abs(x - y);

                if (diff == 0)
                    return true;

                if (x != 0)
                    return Math.Abs(diff / x) < RelativeError;

                return false;
            }

            public override int GetHashCode(double obj) =>
                obj.GetHashCode();
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="decimalPlaces">The acceptable error margin.</param>
        public void VerifyWithinPrecision(
            ArrayView<Half> buffer,
            Half[] expected,
            uint decimalPlaces)
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);

            var comparer = new HalfPrecisionComparer(decimalPlaces);
            for (int i = 0, e = data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i], comparer);
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="decimalPlaces">The acceptable error margin.</param>
        public void VerifyWithinPrecision(
            ArrayView<float> buffer,
            float[] expected,
            uint decimalPlaces)
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);

            var comparer = new FloatPrecisionComparer(decimalPlaces);
            for (int i = 0, e = data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i], comparer);
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="decimalPlaces">The acceptable error margin.</param>
        public void VerifyWithinPrecision(
            ArrayView<double> buffer,
            double[] expected,
            uint decimalPlaces)
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);

            var comparer = new DoublePrecisionComparer(decimalPlaces);
            for (int i = 0, e = data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i], comparer);
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="relativeError">The acceptable error margin.</param>
        public void VerifyWithinRelativeError(
            ArrayView<Half> buffer,
            Half[] expected,
            double relativeError)
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);

            var comparer = new HalfRelativeErrorComparer((float)relativeError);
            for (int i = 0, e = data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i], comparer);
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="relativeError">The acceptable error margin.</param>
        public void VerifyWithinRelativeError(
            ArrayView<float> buffer,
            float[] expected,
            double relativeError)
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);

            var comparer = new FloatRelativeErrorComparer((float)relativeError);
            for (int i = 0, e = data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i], comparer);
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="relativeError">The acceptable error margin.</param>
        public void VerifyWithinRelativeError(
            ArrayView<double> buffer,
            double[] expected,
            double relativeError)
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);

            var comparer = new DoubleRelativeErrorComparer(relativeError);
            for (int i = 0, e = data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i], comparer);
        }
    }
}
