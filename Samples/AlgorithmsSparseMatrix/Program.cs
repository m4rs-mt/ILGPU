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
using ILGPU.Algorithms;
using ILGPU.Algorithms.MatrixOperations;
using ILGPU.Runtime;
using System;

#pragma warning disable CA5394 // Insecure RNG

namespace AlgorithmsSparseMatrix
{
    class Program
    {
        /// <summary>
        /// Converts the given dense matrix into its sparse form.
        /// </summary>
        static SparseMatrixView<float, Stride2D.General> Sparsify(
            Random random,
            Accelerator accelerator,
            int length)
        {
            // Setup a sparse 2D matrix
            var matrix = new float[length, length];

            // Fill sparse matrix
            for (int i = 0; i < matrix.GetLength(0); ++i)
            {
                for (int j = 0; j < matrix.GetLength(1); ++j)
                {
                    // Create a sparse matrix with 5% sparse elements in this sample
                    matrix[i, j] = random.NextSingle() > 0.05f
                        ? 0.0f
                        : random.NextSingle() * 10.0f;
                }
            }

            // Allocate basic matrix on the accelerator and transfer it to the device
            using var matrixBuffer = accelerator.Allocate2DDenseY<float>(matrix.GetExtent());
            matrixBuffer.View.CopyFromCPU(matrix);

            // Allocate a temp buffer (or use existing memory from somewhere else)
            using var tempBuffer = accelerator.Allocate1D<int>(1);

            // Initialize the basic shape converter and data converters
            var shapeConverter = accelerator.CreateSparseMatrixShapeConverter<
                float,
                FloatEpsPredicate<Stride2D.General>,
                Stride2D.General>(tempBuffer.View);
            var converter = accelerator.CreateSparseMatrixConverter<float, Stride2D.General>();

            // Get basic shape of the sparse matrix living on the device which contains all required
            // dimension information and the actual sparse lookup table for efficient processing
            // the matrix elements later on
            var numNeighborsBuffer = accelerator.Allocate1D<int>(matrix.GetLength(0));
            var shapeView = shapeConverter(
                accelerator.DefaultStream,
                matrixBuffer.View.AsGeneral(),
                new FloatEpsPredicate<Stride2D.General>(
                    matrixBuffer.View.AsGeneral(),
                    0.0f),
                numNeighborsBuffer.View,
                maxNumNeighbors =>
                {
                    // The maximum number of neighbors per row is available at this point and we just
                    // allocate a buffer here for demonstration purposes. In practice, this can be
                    // the creation of a subview from an existing buffer.
                    return accelerator.Allocate2DDenseY<int>(
                            (matrix.GetLength(0), maxNumNeighbors))
                        .View.AsGeneral();
                });

            // Allocate the actual sparse data buffer for our result
            var sparseMatrixBuffer = accelerator.Allocate2DDenseY<float>(
                (matrix.GetLength(0), shapeView.Neighbors.Extent.Y));

            // Convert data and fill our sparse matrix structure
            var sparseView = converter(accelerator.DefaultStream, matrixBuffer.View.AsGeneral(),
                shapeView, sparseMatrixBuffer.View.AsGeneral());

            // !!! Note that we *do not* dispose buffers here to keep them alive in the related
            // views for the sake of simplicity. Please always make sure to dispose buffers
            // properly in production code !!!

            // Sparse view now contains all required data elements
            return sparseView;
        }

        /// <summary>
        /// Multiplies the given dense matrix and the sparse matrix efficiently on the GPU,
        /// while transposing the sparse matrix on the fly.
        /// </summary>
        static void MultiplySparseTransposed(
            Accelerator accelerator,
            float[,] denseMatrix,
            SparseMatrixView<float, Stride2D.General> sparseView)
        {
            // As mentioned above, the integrated sparse-matrix processor allows multiplying
            // the dense matrix with the sparse one while transposing the latter one.
            // However, it also allows to specify which values we are interested in and
            // ignoring all other values. For this purpose, we can use specialized predicates,
            // of which several are already predefined and available. In this sample, we use
            // a predicate that operates on a dense matrix to test whether the values are above
            // a certain threshold. If yes, the corresponding matrix element in the result
            // matrix will be computed.

            var maskMatrix = new float[denseMatrix.GetLength(0), denseMatrix.GetLength(1)];
            for (int i = 0; i < maskMatrix.GetLength(0); ++i)
            {
                for (int j = 0; j < maskMatrix.GetLength(1); ++j)
                    maskMatrix[i, j] = 1.0f; // Use your own values to avoid computing result elements
            }

            // Allocate dense output matrix
            using var aMatrixBuffer = accelerator.Allocate2DDenseY(denseMatrix);
            using var pMatrixBuffer = accelerator.Allocate2DDenseY(maskMatrix);
            using var outBuffer = accelerator.Allocate2DDenseY<float>(denseMatrix.GetExtent());

            // Create a single-streamed sparse matrix processor to multiply our matrix instances
            // as efficiently as possible
            var processor = accelerator.CreateSparseTransposedMatrixMultiplierMasked<
                float,
                FloatEpsPredicate<Stride2D.General>,
                Stride2D.General,
                FloatMaskedSparseMatrixProcessor>();

            // Multiply a single masked sparse matrix
            processor(
                accelerator.DefaultStream,
                new FloatEpsPredicate<Stride2D.General>(pMatrixBuffer.View.AsGeneral(), 0.0f),
                aMatrixBuffer.View.AsGeneral(),
                sparseView,
                outBuffer.View.AsGeneral());

            // The outBuffer contains the multiplication result
        }

        static void Main()
        {
            // Get a new ILGPU context
            using var context =
                Context.Create(builder => builder.Default().EnableAlgorithms());

            // Create a new RNG on the CPU side
            var random = new Random();

            const int lengthA = 288;
            const int lengthB = 376;

            // For each available device...
            foreach (var device in context)
            {
                // Create the associated accelerator
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Create a new dense matrix on the CPU and let it be sparsified on the GPU
                var sparseMatrixView = Sparsify(random, accelerator, lengthB);

                // Now, use the sparse matrix and multiply it efficiently with the given dense one
                var denseMatrix = new float[lengthA, lengthB];
                for (int i = 0; i < denseMatrix.GetLength(0); ++i)
                {
                    for (int j = 0; j < denseMatrix.GetLength(1); ++j)
                        denseMatrix[i, j] = random.NextSingle();
                }

                // Note that this sample method demonstrates the use of a specialized operation:
                // A * B^T, where B is considered a huger sparse matrix
                MultiplySparseTransposed(accelerator, denseMatrix, sparseMatrixView);
            }
        }
    }
}

#pragma warning restore CA5394
