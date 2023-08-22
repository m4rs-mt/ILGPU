// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
#if NET7_0_OR_GREATER
using ILGPU.Algorithms.Optimization;
using ILGPU.Algorithms.Optimization.Optimizers;
using ILGPU.Algorithms.Random;
using ILGPU.Algorithms.Vectors;
#endif
using ILGPU.Runtime;
using System;

namespace AlgorithmsOptimization
{
#if NET7_0_OR_GREATER

    /// <summary>
    /// Represents a distance function to a uniformly defined n-D point.
    /// </summary>
    public readonly struct DistanceObjective : IOptimizationFunction<Float32x2, float, float>
    {
        private readonly float uniformDistanceValue;

        public DistanceObjective(float uniformValue)
        {
            uniformDistanceValue = uniformValue;
        }

        /// <summary>
        /// Computes the distance to our uniform target vector.
        /// </summary>
        public float Evaluate(LongIndex1D index, Index1D dimension, SingleVectorView<Float32x2> positionView)
        {
            float result = 0;
            for (Index1D i = 0; i < dimension; ++i)
            {
                var vec = positionView[i];
                var dist = vec - new Float32x2(uniformDistanceValue, uniformDistanceValue);
                result += dist.X * dist.X + dist.Y * dist.Y;
            }
            return result / dimension;
        }

        /// <summary>
        /// Minimize our objective.
        /// </summary>
        public bool CurrentIsBetter(float current, float proposed) => current < proposed;
    }

    /// <summary>
    /// The Himmbelblau function from the Wikipedia optimization test functions page:
    /// https://en.wikipedia.org/wiki/Test_functions_for_optimization
    /// </summary>
    public readonly struct HimmelblauObjective : IOptimizationFunction<Float32x2, float, float>
    {
        public float Evaluate(LongIndex1D index, Index1D dimension, SingleVectorView<Float32x2> positionView)
        {
            var firstVector = positionView[0];

            float first = (firstVector.X * firstVector.X + firstVector.X - 11);
            float second = (firstVector.X + firstVector.Y * firstVector.Y - 7);
            return first * first + second * second;
        }

        /// <summary>
        /// Minimize our objective.
        /// </summary>
        public bool CurrentIsBetter(float current, float proposed) => current < proposed;
    }

    class Program
    {
        /// <summary>
        /// Optimizes our distance function.
        /// </summary>
        static void OptimizeDistance(
            Random random,
            AcceleratorStream stream,
            OptimizationEngine<Float32x2, float, float> optimizationEngine)
        {
            // Setup lower and upper bounds for our problems
            var lowerBounds = new float[] {-10.0f, -10.0f};
            var upperBounds = new float[] {10.0f, 10.0f};

            // Transfer bounds to the optimization engine
            optimizationEngine.LoadBounds(stream, lowerBounds, upperBounds);

            // Load specific PSO parameters (the default ones in this case)
            optimizationEngine.LoadParameters(stream, PSO.DefaultFloatParameters);

            // Setup our vectorized objective function
            var objective = new DistanceObjective(5.0f);

            // Create a specialized optimizer taking our objective into account
            using var optimizer =
                optimizationEngine.CreateOptimizer(stream, random, objective);

            // Begin the optimization process by passing a (potentially) known best position
            // and an initial results value (here, float.MaxValue indicating that the currently
            // known result is extremely far away from the intended solution).
            var bestPosition = new float[] {0.0f, 0.0f};
            var run = optimizer.BeginOptimization(stream, bestPosition,
                bestResult: float.MaxValue);

            // If you just want to perform a full optimization run without explicit control over
            // all intermediate steps use optimizer.Optimize, as shown in the OptimizeHimmelblau
            // method below.

            // Perform 128 steps or use optimizer.Optimize and pass the max number of steps
            for (int i = 0; i < 128; ++i)
                run.Step();

            // Finish the optimization run in CPU land. Please note that this call does *not*
            // synchronize the optimizer with the GPU, and thus the results may not be valid
            // at this point.
            var result = run.FinishToCPUAsync();

            // Use stream.Synchronize() (as shown below) to make sure all results are accessible
            // from CPU land
            stream.Synchronize();

            Console.WriteLine("Distance objective: " + result.Result);
            Console.WriteLine("Best distance position: " +
                              string.Join(", ", result.ResultVector.ToArray()));

            // In order to (re-)use the result on the GPU use run.Finish() which gives
            // you direct access to the result views pointing to the right locations in
            // GPU memory containing the results. As before, the result is associated with
            // the given stream and may be invalid when accessed from another stream.
        }

        /// <summary>
        /// Optimizes the Himmelblau function.
        /// </summary>
        static void OptimizeHimmelblau(
            Random random,
            AcceleratorStream stream,
            OptimizationEngine<Float32x2, float, float> optimizationEngine)
        {
            // Setup lower and upper bounds for our problems
            var lowerBounds = new float[] {-5.0f, -5.0f};
            var upperBounds = new float[] {5.0f, 5.0f};

            // Transfer bounds to the optimization engine
            optimizationEngine.LoadBounds(stream, lowerBounds, upperBounds);

            // Load specific PSO parameters (the default ones in this case)
            optimizationEngine.LoadParameters(stream, PSO.DefaultFloatParameters);

            // Setup our vectorized objective function
            var objective = new HimmelblauObjective();

            // Create a specialized optimizer taking our objective into account
            using var optimizer =
                optimizationEngine.CreateOptimizer(stream, random, objective);

            // Begin the optimization process by passing a (potentially) known best position
            // and an initial results value (here, float.MaxValue indicating that the currently
            // known result is extremely far away from the intended solution).
            var bestPosition = new float[] {0.0f, 0.0f};

            var result = optimizer.OptimizeToCPUAsync(
                stream,
                bestPosition,
                bestResult: float.MaxValue,
                1024);

            // Use stream.Synchronize() (as shown below) to make sure all results are accessible
            // from CPU land
            stream.Synchronize();

            Console.WriteLine("Himmelblau objective: " + result.Result);
            Console.WriteLine("Best Himmelblau position: " +
                              string.Join(", ", result.ResultVector.ToArray()));

            // In order to (re-)use the result on the GPU use result.Optimize(....) which
            // you direct access to the result views pointing to the right locations in
            // GPU memory containing the results. As before, the result is associated with
            // the given stream and may be invalid when accessed from another stream.
        }

        static void Main()
        {
            // Create default context and enable algorithms library
            using var context =
                Context.Create(builder => builder.Default().EnableAlgorithms());

            // Create a new RNG on the CPU side
            var random = new Random();

            // For each available device...
            foreach (var device in context)
            {
                // Create the associated accelerator
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Create a new processing stream and create a new instance of the parallelized
                // particle swarm optimizer with 10240 particles and support for 2D problems.
                // A single instance of the optimization engine can be used with different objective
                // functions to reuse the allocated buffers associated with this instance.
                using var stream = accelerator.CreateStream();

                // Use a vectorized version to process 2 floats at once, while operating on float
                // types and evaluating our objective function yielding floats, as well. RNG-wise
                // this sample uses the XorShift64Star RNG.
                using var pso = new PSO<Float32x2, float, float, XorShift64Star>(
                    accelerator,
                    maxNumParticles: 10240,
                    dimension: 2);

                // Optimize our distance objective
                OptimizeDistance(random, stream, pso);

                // Optimize the Himmelblau function
                OptimizeHimmelblau(random, stream, pso);
            }
        }
    }

#else

    class Program
    {
        static void Main()
        {
            Console.WriteLine("Cannot use optimization API on frameworks prior to .Net7.0");
        }
    }

#endif
}
