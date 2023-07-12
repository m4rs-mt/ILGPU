// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MatrixTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.MatrixOperations;
using ILGPU.Runtime;
using ILGPU.Tests;
using System;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CA1814 // Jagged arrays
#pragma warning disable CA5394 // Insecure RNG

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class MatrixTests : TestBase
    {
        protected MatrixTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        #region MemberData

        public static TheoryData<object, object, object, object, object> DimensionsData =>
            new TheoryData<object, object, object, object, object>
            {
                { 39, 42, 17, 2918291, 0.8f },
                { 39, 42, 17, 2918291, 0.5f },
                { 39, 42, 17, 2918291, 0.05f },

                { 42, 42, 31, 2918292, 0.8f },
                { 42, 42, 31, 2918292, 0.5f },
                { 42, 42, 31, 2918292, 0.05f },

                { 376, 382, 288, 4132107, 0.8f },
                { 376, 382, 288, 4132107, 0.5f },
                { 376, 382, 288, 4132107, 0.05f },

                { 829, 277, 928, 31821912, 0.8f },
                { 829, 277, 928, 31821912, 0.5f },
                { 829, 277, 928, 31821912, 0.05f },

                { 829, 829, 1121, 31821913, 0.8f },
                { 829, 829, 1121, 31821913, 0.5f },
                { 829, 829, 1121, 31821913, 0.05f },
            };

        #endregion

        #region Helpers

        /// <summary>
        /// Multiplies two dense matrices and returns the resultant matrix in 2D.
        /// </summary>
        /// <param name="left">A dense MxK matrix</param>
        /// <param name="right">A dense KxN matrix</param>
        /// <returns>A dense MxN matrix</returns>
        private static float[,] MultiplyMatrix2D(float[,] left, float[,] right)
        {
            var leftRows = left.GetLength(0);
            var leftColumns = left.GetLength(1);
            var rightRows = right.GetLength(0);
            var rightColumns = right.GetLength(1);

            if (leftColumns != rightRows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(right),
                    $"Cannot multiply {leftRows}x{leftColumns} matrix by " +
                    $"{rightColumns}x{rightRows} matrix");
            }

            var result = new float[leftRows, rightColumns];
            for (var x = 0; x < leftRows; x++)
            {
                for (var y = 0; y < rightColumns; y++)
                {
                    for (var z = 0; z < leftColumns; z++)
                        result[x, y] += left[x, z] * right[z, y];
                }
            }

            return result;
        }

        /// <summary>
        /// Compute the transpose of a matrix in 2D.
        /// </summary>
        /// <param name="matrix">A MxN matrix</param>
        /// <returns>The transpose of A, a NxM matrix</returns>
        private static float[,] TransposeMatrix2D(float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            var transposed = new float[columns, rows];
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++)
                    transposed[j, i] = matrix[i, j];
            }

            return transposed;
        }

        /// <summary>
        /// Compares two matrices for equality.
        /// </summary>
        /// <param name="left">A MxN matrix (the actual matrix we got)</param>
        /// <param name="right">A MxN matrix (the matrix we expected) </param>
        /// <returns>True if the matrices are equal</returns>
        private static void AssertMatrixEqual2D(
            float[,] left,
            float[,] right,
            float eps = 0.0015f)
        {
            var leftRows = left.GetLength(0);
            var leftColumns = left.GetLength(1);
            var rightRows = right.GetLength(0);
            var rightColumns = right.GetLength(1);

            if (leftRows != rightRows || leftColumns != rightColumns)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(right),
                    $"Matrix dimensions {leftRows}x{leftColumns} and " +
                    $"{rightRows}x{rightColumns} do not match");
            }

            for (var i = 0; i < leftRows; i++)
            {
                for (var j = 0; j < leftColumns; j++)
                {
                    Assert.True(
                        Math.Abs(left[i, j] - right[i, j]) < eps,
                        "Matrix result not equal");
                }
            }
        }

        #endregion

        [SkippableTheory()]
        [MemberData(nameof(DimensionsData))]
        public void MatrixMultiplySparse(
            int length,
            int length2,
            int baseLength,
            int seed,
            float sparsityProbability)
        {
            // CPU-performance related checks to avoid extremely long test runs
            Skip.If(
                Accelerator.AcceleratorType == AcceleratorType.CPU &&
                (length > 512 || length2 > 512 || baseLength > 384));

            using var stream = Accelerator.CreateStream();

            // Our starting dense matrix
            var denseMatrix = new float[baseLength, length];

            // Setup sparse 2D matrix
            var sparseMatrix = new float[length2, length];

            var random = new System.Random(seed);
            for (int i = 0; i < denseMatrix.GetLength(0); ++i)
            {
                for (int j = 0; j < denseMatrix.GetLength(1); ++j)
                    denseMatrix[i, j] = (float)random.NextDouble();
            }

            for (int i = 0; i < sparseMatrix.GetLength(0); ++i)
            {
                for (int j = 0; j < sparseMatrix.GetLength(1); ++j)
                {
                    sparseMatrix[i, j] = random.NextDouble() > sparsityProbability
                        ? 0.0f
                        : (float)random.NextDouble() * 8.0f;
                }
            }

            // Setup the output mask
            var sparseMaskMatrix = new float[baseLength, length2];
            for (int i = 0; i < sparseMaskMatrix.GetLength(0); ++i)
            {
                for (int j = 0; j < sparseMaskMatrix.GetLength(1); ++j)
                    sparseMaskMatrix[i, j] = 1.0f;
            }

            // Initialize our sparse matrix
            using var matrixBuffer = Accelerator.Allocate2DDenseY<float>(
                sparseMatrix.GetExtent());
            matrixBuffer.View.CopyFromCPU(stream, sparseMatrix);

            // Allocate a temp buffer (or use existing memory from somewhere else)
            using var tempBuffer = Accelerator.Allocate1D<int>(1);

            // Initialize the basic shape converter and data converters
            var shapeConverter = Accelerator.CreateSparseMatrixShapeConverter<
                float,
                FloatEpsPredicate<Stride2D.General>,
                Stride2D.General>(tempBuffer.View);
            var converter = Accelerator.CreateSparseMatrixConverter<
                float,
                Stride2D.General>();

            // Get basic shape of the sparse matrix living on the device
            using var numNeighborsBuffer = Accelerator.Allocate1D<int>(
                sparseMatrix.GetLength(0));
            var shapeView = shapeConverter(
                stream,
                matrixBuffer.View.AsGeneral(),
                new(matrixBuffer.View.AsGeneral(), 0.0f),
                numNeighborsBuffer.View,
                maxNumNeighbors =>
                    // Allocate a shape-view buffer to store the neighbor lists
                    Accelerator.Allocate2DDenseY<int>(
                            (sparseMatrix.GetLength(0), maxNumNeighbors))
                        .View.AsGeneral());

            // Allocate the actual sparse data buffer
            using var dataBuffer = Accelerator.Allocate2DDenseY<float>(
                (sparseMatrix.GetLength(0), shapeView.Neighbors.Extent.Y));

            // Convert data and fill our sparse matrix structure
            var sparseView = converter(
                stream,
                matrixBuffer.View.AsGeneral(),
                shapeView,
                dataBuffer.View.AsGeneral());

            // Allocate dense output matrix
            using var aMatrixBuffer =
                Accelerator.Allocate2DDenseY<float>(denseMatrix.GetExtent());
            using var pMatrixBuffer =
                Accelerator.Allocate2DDenseY<float>(sparseMaskMatrix.GetExtent());
            using var outBuffer =
                Accelerator.Allocate2DDenseY<float>(
                    (denseMatrix.GetLength(0),
                        sparseMatrix.GetLength(0)));
            aMatrixBuffer.View.CopyFromCPU(stream, denseMatrix);
            pMatrixBuffer.View.CopyFromCPU(stream, sparseMaskMatrix);

            // Create a single-streamed sparse matrix buffer
            var processor = Accelerator.CreateSparseTransposedMatrixMultiplierMasked<
                float,
                FloatEpsPredicate<Stride2D.General>,
                Stride2D.General,
                FloatMaskedSparseMatrixProcessor>();

            // Multiply a single masked sparse matrix
            processor(
                stream,
                new(pMatrixBuffer.View.AsGeneral(), 0.0f),
                aMatrixBuffer.View.AsGeneral(),
                sparseView,
                outBuffer.View.AsGeneral());

            // Compute reference values
            var transposed = TransposeMatrix2D(sparseMatrix);
            var expected = MultiplyMatrix2D(denseMatrix, transposed);

            // Load our resulting matrix and test for equality
            var tArr = outBuffer.View.AsDenseY().GetAsArray2D(stream);
            AssertMatrixEqual2D(tArr, expected);
        }
    }
}
