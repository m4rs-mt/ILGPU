// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: OptimizationTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#if NET7_0_OR_GREATER
using ILGPU.Algorithms.Optimization;
using ILGPU.Algorithms.Optimization.Optimizers;
using ILGPU.Algorithms.Random;
using ILGPU.Algorithms.Vectors;
#endif

using ILGPU.Runtime;
using ILGPU.Tests;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CA1034
#pragma warning disable CA1819
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable xUnit1026

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class OptimizationTests : TestBase
    {
        protected OptimizationTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

#if NET7_0_OR_GREATER

        #region Objectives

        public interface IPredefineTestFunction
        {
            float Result { get; }
            float[] LowerBounds { get; }
            float[] UpperBounds { get; }
        }

        public readonly record struct DistanceF32x2(float Constant) :
            IOptimizationFunction<Float32x2, float, float>
        {
            public float Evaluate(
                LongIndex1D index,
                Index1D dimension,
                SingleVectorView<Float32x2> positionView)
            {
                float result = 0;
                for (Index1D i = 0; i < dimension; ++i)
                {
                    var vec = positionView[i];
                    var dist = vec - Float32x2.FromScalar(Constant);
                    result += dist.X * dist.X + dist.Y * dist.Y;
                }
                return result / dimension;
            }

            public bool CurrentIsBetter(float current, float proposed) =>
                current <= proposed;
        }

        /// <summary>
        /// Represents the Himmelblau function:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct HimmelblauFunction :
            IOptimizationFunction<Float32x2, float, float>,
            IPredefineTestFunction
        {
            private static readonly float[] GlobalLowerBounds = new float[]
            {
                -5.0f, -5.0f
            };

            private static readonly float[] GlobalUpperBounds = new float[]
            {
                5.0f, 5.0f
            };

            /// <summary>
            /// The optimal result.
            /// </summary>
            public const float GlobalResult = 0.0f;

            /// <summary>
            /// Evaluates the Himmelblau function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Evaluate(float x, float y)
            {
                float first = (x * x + y - 11);
                float second = (x + y * y - 7);
                return first * first + second * second;
            }

            public float Result => GlobalResult;
            public float[] LowerBounds => GlobalLowerBounds;
            public float[] UpperBounds => GlobalUpperBounds;

            public float Evaluate(
                LongIndex1D index,
                Index1D dimension,
                SingleVectorView<Float32x2> positionView)
            {
                var first = positionView[0];
                return Evaluate(first.X, first.Y);
            }

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;
        }

        /// <summary>
        /// Represents the Easom function:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct EasomFunction :
            IOptimizationFunction<Float32x2, float, float>,
            IPredefineTestFunction
        {
            private static readonly float[] GlobalLowerBounds = new float[]
            {
                -100.0f, -100.0f
            };

            private static readonly float[] GlobalUpperBounds = new float[]
            {
                100.0f, 100.0f
            };

            /// <summary>
            /// The optimal result.
            /// </summary>
            public const float GlobalResult = -1.0f;

            public float Result => GlobalResult;
            public float[] LowerBounds => GlobalLowerBounds;
            public float[] UpperBounds => GlobalUpperBounds;

            /// <summary>
            /// Evaluates the Easom function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Evaluate(float x, float y)
            {
                float xPart = x - XMath.PI;
                float yPart = y - XMath.PI;
                return -XMath.Cos(x) * XMath.Cos(y) *
                    XMath.Exp(-(xPart * xPart + yPart * yPart));
            }
            public float Evaluate(
                LongIndex1D index,
                Index1D dimension,
                SingleVectorView<Float32x2> positionView)
            {
                var first = positionView[0];
                return Evaluate(first.X, first.Y);
            }

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;
        }

        /// <summary>
        /// Represents the Shaffer function N4:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct ShafferFunction4 :
            IOptimizationFunction<Float32x2, float, float>,
            IPredefineTestFunction
        {
            private static readonly float[] GlobalLowerBounds = new float[]
            {
                -100.0f, -100.0f
            };

            private static readonly float[] GlobalUpperBounds = new float[]
            {
                100.0f, 100.0f
            };

            /// <summary>
            /// The optimal result.
            /// </summary>
            public const float GlobalResult = 0.292579f;

            public float Result => GlobalResult;
            public float[] LowerBounds => GlobalLowerBounds;
            public float[] UpperBounds => GlobalUpperBounds;

            /// <summary>
            /// Evaluates the Shaffer function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Evaluate(float x, float y)
            {
                float cos = XMath.Cos(XMath.Sin(XMath.Abs(x * x - y * y)));
                float nominator = cos * cos - 0.5f;
                float denominator = 1 + 0.001f * (x * x + y * y);
                return 0.5f + nominator / (denominator * denominator);
            }
            public float Evaluate(
                LongIndex1D index,
                Index1D dimension,
                SingleVectorView<Float32x2> positionView)
            {
                var first = positionView[0];
                return Evaluate(first.X, first.Y);
            }

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;
        }

        /// <summary>
        /// Represents the Rosenbrock function constrained to a disk
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct RosenbrockDisk :
            IOptimizationFunction<Float32x2, float, float>,
            IPredefineTestFunction
        {
            private static readonly float[] GlobalLowerBounds = new float[]
            {
                -1.5f, -1.5f
            };

            private static readonly float[] GlobalUpperBounds = new float[]
            {
                1.5f, 1.5f
            };

            /// <summary>
            /// The optimal result.
            /// </summary>
            public const float GlobalResult = 0.0f;

            public float Result => GlobalResult;
            public float[] LowerBounds => GlobalLowerBounds;
            public float[] UpperBounds => GlobalUpperBounds;

            /// <summary>
            /// Evaluates the constrained Rosenbrock function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Evaluate(float x, float y)
            {
                float xMin = 1.0f - x;
                float x2 = x * x;
                float result = xMin * xMin + 100.0f * (y - x2) * (y - x2);
                if (x * x + y * y <= 2.0f)
                    return result;
                return float.MaxValue;
            }

            public float Evaluate(
                LongIndex1D index,
                Index1D dimension,
                SingleVectorView<Float32x2> positionView)
            {
                var first = positionView[0];
                return Evaluate(first.X, first.Y);
            }

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;
        }

        /// <summary>
        /// Represents the Gomez and Levy function:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct GomezAndLevyFunction :
            IOptimizationFunction<Float32x2, float, float>,
            IPredefineTestFunction
        {
            private static readonly float[] GlobalLowerBounds = new float[]
            {
                -1.0f, -1.0f
            };

            private static readonly float[] GlobalUpperBounds = new float[]
            {
                0.75f, 1.0f
            };

            /// <summary>
            /// The optimal result.
            /// </summary>
            public const float GlobalResult = -1.031628453f;

            public float Result => GlobalResult;
            public float[] LowerBounds => GlobalLowerBounds;
            public float[] UpperBounds => GlobalUpperBounds;

            /// <summary>
            /// Evaluates the constrained Gomez and Levy function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Evaluate(float x, float y)
            {
                float x2 = x * x;
                float x4 = x2 * x2;
                float y2 = y * y;
                float y4 = y2 * y2;
                float result = 4.0f * x2 + 2.1f * x4 + 1.0f / 3.0f * x4 * x2 +
                    x * y - 4.0f * y2 + 4.0f * y4;
                float sin = XMath.Sin(2.0f * XMath.PI * y);
                float conditionValue = -XMath.Sin(4.0f * XMath.PI * x) + 2.0f * sin * sin;
                return conditionValue < 1.5f ? result : float.MaxValue;
            }

            public float Evaluate(
                LongIndex1D index,
                Index1D dimension,
                SingleVectorView<Float32x2> positionView)
            {
                var first = positionView[0];
                return Evaluate(first.X, first.Y);
            }

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;
        }

        #endregion

        #region MemberData

        public record OptimizerConfig<TElementType>(
            int NumIterations,
            int NumParticles,
            int NumDimensions,
            TElementType[] BestPositions,
            TElementType[] Parameters);

        public static TheoryData<
            object,
            object,
            object,
            object,
            object,
            object,
            object,
            object,
            object> TestData =>
            new TheoryData<
                object,
                object,
                object,
                object,
                object,
                object,
                object,
                object,
                object>
        {
            {
                new OptimizerConfig<float>(
                    32,
                    64,
                    4,
                    new float[] { 0.0f, 0.0f, 0.0f, 0.0f},
                    new float[] { PSO.Omega, PSO.PhiG, PSO.PhiP }),
                new DistanceF32x2(1.3f),
                0.0f,
                2.0f,
                float.MaxValue,
                4e-4f,
                0.0001f,
                default(Float32x2),
                default(XorShift64Star)
            },
            {
                new OptimizerConfig<float>(
                    512,
                    2048,
                    6,
                    new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f},
                    new float[] { PSO.Omega, PSO.PhiG, PSO.PhiP }),
                new DistanceF32x2(4.7f),
                0.0f,
                20.0f,
                float.MaxValue,
                1e-5f,
                1e-6f,
                default(Float32x2),
                default(XorShift128Plus)
            },
        };

        #endregion

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        public void ParticleSwarmOptimization<
            TFunc,
            TNumericType,
            TElementType,
            TEvalType,
            TRandom>(
            object optimizerConfigObj,
            TFunc function,
            TElementType lower,
            TElementType upper,
            TEvalType best,
            TEvalType expected,
            TEvalType delta,
            TNumericType _,
            TRandom __)
            where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
            where TElementType : unmanaged, INumber<TElementType>
            where TEvalType : unmanaged, INumber<TEvalType>
            where TRandom : unmanaged, IRandomProvider<TRandom>
            where TFunc : struct,
                IOptimizationFunction<TNumericType, TElementType, TEvalType>
        {
            var optimizerConfig = (OptimizerConfig<TElementType>)optimizerConfigObj;

            // Skip larger problems on the CPU
            Skip.If(
                Accelerator.AcceleratorType == AcceleratorType.CPU &&
                optimizerConfig.NumIterations * optimizerConfig.NumParticles > 2048);

            const int Seed = 24404699;
            using var pso = new PSO<
                TNumericType,
                TElementType,
                TEvalType,
                TRandom>(
                Accelerator,
                optimizerConfig.NumParticles,
                optimizerConfig.NumDimensions);
            using var stream = Accelerator.CreateStream();

            var random = new System.Random(Seed);
            using var optimizer = pso.CreateOptimizer(stream, random, function);

            // Load config
            var lowerRange = Enumerable.Repeat(lower, pso.Dimension).ToArray();
            var upperRange = Enumerable.Repeat(upper, pso.Dimension).ToArray();
            pso.LoadBounds(stream, lowerRange, upperRange);
            pso.LoadParameters(stream, optimizerConfig.Parameters);

            // Optimize and wait for results
            var result = optimizer.OptimizeToCPUAsync(
                stream,
                optimizerConfig.BestPositions,
                best,
                optimizerConfig.NumIterations);
            stream.Synchronize();

            // Check result
            Assert.True(
                result.Result - delta <= expected,
                "Invalid result");
        }
#endif
    }
}

#pragma warning restore xUnit1026
#pragma warning restore CA1861 // Avoid constant arrays as arguments
#pragma warning restore CA1819
#pragma warning restore CA1034
