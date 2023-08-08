// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMetaOptimizerTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Optimization.CPU;
using ILGPU.Algorithms.Random;
using System;
using System.Threading.Tasks;
using Xunit;

#if NET7_0_OR_GREATER

#pragma warning disable CA1034 // Do not nest types
#pragma warning disable CA1819 // Properties should not return arrays

namespace ILGPU.Algorithms.Tests.CPU
{
    /// <summary>
    /// Contains tests to verify the functionality of the CPU-specialized
    /// <see cref="MetaOptimizer{T,TEvalType}"/> class.
    /// </summary>
    public class CPUMetaOptimizerTests
    {
        #region CPU Functions

        public interface IOptimizerTestFunction :
            OptimizationTests.IPredefineTestFunction,
            ICPUOptimizationFunction<float, float>
        { }

        public readonly record struct TestBreakFunction(float Goal) :
            ICPUOptimizationBreakFunction<float>
        {
            public bool Break(float evalType, int iteration) =>
                Math.Abs(evalType - Goal) < 1e-3f || iteration > 1000;
        }

        /// <summary>
        /// Represents the Himmelblau function:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct HimmelblauFunction : IOptimizerTestFunction
        {
            public float Evaluate(ReadOnlySpan<float> position) =>
                OptimizationTests.HimmelblauFunction.Evaluate(
                    position[0],
                    position[1]);

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;

            public float Result =>
                new OptimizationTests.HimmelblauFunction().Result;
            public float[] LowerBounds =>
                new OptimizationTests.HimmelblauFunction().LowerBounds;
            public float[] UpperBounds =>
                new OptimizationTests.HimmelblauFunction().UpperBounds;
        }

        /// <summary>
        /// Represents the Easom function:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct EasomFunction : IOptimizerTestFunction
        {
            public float Evaluate(ReadOnlySpan<float> position) =>
                OptimizationTests.EasomFunction.Evaluate(
                    position[0],
                    position[1]);

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;

            public float Result =>
                new OptimizationTests.EasomFunction().Result;
            public float[] LowerBounds =>
                new OptimizationTests.EasomFunction().LowerBounds;
            public float[] UpperBounds =>
                new OptimizationTests.EasomFunction().UpperBounds;
        }
        /// <summary>
        /// Represents the Shaffer function N4:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct ShafferFunction4 : IOptimizerTestFunction
        {
            public float Evaluate(ReadOnlySpan<float> position) =>
                OptimizationTests.ShafferFunction4.Evaluate(
                    position[0],
                    position[1]);

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;

            public float Result =>
                new OptimizationTests.ShafferFunction4().Result;
            public float[] LowerBounds =>
                new OptimizationTests.ShafferFunction4().LowerBounds;
            public float[] UpperBounds =>
                new OptimizationTests.ShafferFunction4().UpperBounds;
        }

        /// <summary>
        /// Represents the Rosenbrock function constrained to a disk
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct RosenbrockDisk : IOptimizerTestFunction
        {
            public float Evaluate(ReadOnlySpan<float> position) =>
                OptimizationTests.RosenbrockDisk.Evaluate(
                    position[0],
                    position[1]);

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;

            public float Result =>
                new OptimizationTests.RosenbrockDisk().Result;
            public float[] LowerBounds =>
                new OptimizationTests.RosenbrockDisk().LowerBounds;
            public float[] UpperBounds =>
                new OptimizationTests.RosenbrockDisk().UpperBounds;
        }

        /// <summary>
        /// Represents the Gomez and Levy function:
        /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
        /// </summary>
        public readonly record struct GomezAndLevyFunction : IOptimizerTestFunction
        {
            public float Evaluate(ReadOnlySpan<float> position) =>
                OptimizationTests.GomezAndLevyFunction.Evaluate(
                    position[0],
                    position[1]);

            public bool CurrentIsBetter(float current, float proposed) =>
                current < proposed;

            public float Result =>
                new OptimizationTests.GomezAndLevyFunction().Result;
            public float[] LowerBounds =>
                new OptimizationTests.GomezAndLevyFunction().LowerBounds;
            public float[] UpperBounds =>
                new OptimizationTests.GomezAndLevyFunction().UpperBounds;
        }

        #endregion

        #region MemberData

        public static TheoryData<
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
                object>
        {
            { new HimmelblauFunction(), 8192, 0.5f, 0.5f, 0.5f },
            { new EasomFunction(), 81920, 0.5f, 0.5f, 0.5f },
            { new ShafferFunction4(), 8192, 0.5f, 0.5f, 0.5f },
            { new RosenbrockDisk(), 8192, 0.5f, 0.5f, 0.5f },
            { new GomezAndLevyFunction(), 81920, 0.5f, 0.5f, 0.5f },
        };

        #endregion

        [Theory]
        [MemberData(nameof(TestData))]
        public void MetaOptimizationScalar<TObjective>(
            TObjective objective,
            int numParticles,
            float stepSizeDefensive,
            float stepSizeOffensive,
            float stepSizeOffensiveSOG)
            where TObjective : struct, IOptimizerTestFunction
        {
            int numDimensions = objective.LowerBounds.Length;
            var random = new System.Random(13377331);

            using var optimizer = MetaOptimizer.CreateScalar<
                float,
                float,
                RandomRanges.RandomRangeFloatProvider<XorShift64Star>>(
                random,
                numParticles,
                numDimensions,
                maxNumParallelThreads: 1);

            optimizer.LowerBounds = objective.LowerBounds;
            optimizer.UpperBounds = objective.UpperBounds;

            optimizer.DefensiveStepSize = stepSizeDefensive;
            optimizer.OffensiveStepSize = stepSizeOffensive;
            optimizer.OffensiveSOGStepSize = stepSizeOffensiveSOG;

            var breakFunction = new TestBreakFunction(objective.Result);
            var result = optimizer.Optimize(
                objective,
                breakFunction,
                float.MaxValue);

            // The actually achievable result is 1e-6. However, as the RNG gives us
            // non-deterministic results due to parallel processing, we limit ourselves
            // to 1e-3 to make sure that the result lies roughly in the same ballpark
            // what we were expecting
            Assert.True(Math.Abs(result.Result - objective.Result) < 1e-3f);
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void MetaOptimizationVectorized<TObjective>(
            TObjective objective,
            int numParticles,
            float stepSizeDefensive,
            float stepSizeOffensive,
            float stepSizeOffensiveSOG)
            where TObjective : struct, IOptimizerTestFunction
        {
            int numDimensions = objective.LowerBounds.Length;
            var random = new System.Random(13377331);

            using var optimizer = MetaOptimizer.CreateVectorized<
                float,
                float,
                RandomRanges.RandomRangeFloatProvider<XorShift64Star>>(
                random,
                numParticles,
                numDimensions,
                maxNumParallelThreads: 1);

            optimizer.LowerBounds = objective.LowerBounds;
            optimizer.UpperBounds = objective.UpperBounds;

            optimizer.DefensiveStepSize = stepSizeDefensive;
            optimizer.OffensiveStepSize = stepSizeOffensive;
            optimizer.OffensiveSOGStepSize = stepSizeOffensiveSOG;

            var breakFunction = new TestBreakFunction(objective.Result);
            var result = optimizer.Optimize(
                objective,
                breakFunction,
                float.MaxValue);

            // The actually achievable result is 1e-6. However, as the RNG gives us
            // non-deterministic results due to parallel processing, we limit ourselves
            // to 1e-3 to make sure that the result lies roughly in the same ballpark
            // what we were expecting
            Assert.True(
                Math.Abs(result.Result - objective.Result) < 1e-3f,
                $"Expected {objective.Result}, but found {result.Result}");
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void MetaOptimizationScalarRaw<TObjective>(
            TObjective objective,
            int numParticles,
            float stepSizeDefensive,
            float stepSizeOffensive,
            float stepSizeOffensiveSOG)
            where TObjective : struct, IOptimizerTestFunction
        {
            int numDimensions = objective.LowerBounds.Length;
            var random = new System.Random(13377331);

            using var optimizer = MetaOptimizer.CreateScalar<
                float,
                float,
                RandomRanges.RandomRangeFloatProvider<XorShift64Star>>(
                random,
                numParticles,
                numDimensions,
                maxNumParallelThreads: 1);

            optimizer.LowerBounds = objective.LowerBounds;
            optimizer.UpperBounds = objective.UpperBounds;

            optimizer.DefensiveStepSize = stepSizeDefensive;
            optimizer.OffensiveStepSize = stepSizeOffensive;
            optimizer.OffensiveSOGStepSize = stepSizeOffensiveSOG;

            void EvaluatePosition(
                Memory<float> allPositions,
                Memory<float> evaluations,
                int _,
                int numPaddedDimensions,
                int __,
                Stride2D.DenseY positionStride,
                ParallelOptions options)
            {
                for (int i = 0; i < numParticles; ++i)
                {
                    int offset = positionStride.ComputeElementIndex((i, 0));
                    int endOffset = positionStride.ComputeElementIndex(
                        (i, numPaddedDimensions));
                    var position = allPositions.Slice(offset, endOffset - offset);
                    var result = objective.Evaluate(position.Span);
                    evaluations.Span[i] = result;
                }
            }

            var breakFunction = new TestBreakFunction(objective.Result);
            var result = optimizer.OptimizeRaw(
                EvaluatePosition,
                breakFunction.Break,
                objective.CurrentIsBetter,
                float.MaxValue);

            // The actually achievable result is 1e-6. However, as the RNG gives us
            // non-deterministic results due to parallel processing, we limit ourselves
            // to 1e-3 to make sure that the result lies roughly in the same ballpark
            // what we were expecting
            Assert.True(
                Math.Abs(result.Result - objective.Result) < 1e-3f,
                $"Expected {objective.Result}, but found {result.Result}");
        }
    }
}

#pragma warning restore CA1819
#pragma warning restore CA1034

#endif
