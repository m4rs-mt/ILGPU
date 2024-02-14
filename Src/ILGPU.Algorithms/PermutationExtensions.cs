// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PermutationExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.RadixSortOperations;
using ILGPU.Algorithms.Random;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    #region Delegates

    /// <summary>
    /// A permutation method that permutes all elements in a given buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The stride of all values.</typeparam>
    /// <typeparam name="TRandomProvider">The random number provider type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The elements to permute.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    /// <param name="random">The random number provider.</param>
    public delegate void Permute<T, TStride, TRandomProvider>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        ArrayView<int> temp,
        System.Random random)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TRandomProvider : struct, IRandomProvider<TRandomProvider>;

    /// <summary>
    /// A permutation method that permutes all elements in a given buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The stride of all values.</typeparam>
    /// <typeparam name="TRandomProvider">The random number provider.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The elements to permute.</param>
    /// <param name="random">The random number provider.</param>
    public delegate void BufferedPermute<T, TStride, TRandomProvider>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        System.Random random)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TRandomProvider : struct, IRandomProvider<TRandomProvider>;


    #endregion

    /// <summary>
    /// Represents a permutation provider for a permute operation.
    /// </summary>
    public sealed class PermutationProvider : AlgorithmObject
    {
        #region Instance

        [SuppressMessage(
           "Microsoft.Usage",
           "CA2213: Disposable fields should be disposed",
           Justification = "This is disposed in DisposeAccelerator")]
        private readonly MemoryBuffer1D<int, Stride1D.Dense> tempBuffer;

        internal PermutationProvider(Accelerator accelerator, int tempSize)
            : base(accelerator)
        {
            tempBuffer = accelerator.Allocate1D<int>(tempSize);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new permutation operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride of all values.</typeparam>
        /// <typeparam name="TRandomProvider">The random number provider.</typeparam>
        /// <returns></returns>
        public BufferedPermute<T, TStride, TRandomProvider> CreatePermutation<
            T,
            TStride,
            TRandomProvider>()
            where T : unmanaged
            where TStride : struct, IStride1D
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
        {
            var permute = Accelerator.CreatePermutation<T, TStride, TRandomProvider>();
            return (stream, input, random) => permute(
                stream,
                input,
                tempBuffer.View,
                random);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc cref="AcceleratorObject.DisposeAcceleratorObject(bool)"/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
                tempBuffer.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Contains extension methods for permutation operations.
    /// </summary>
    public static class PermutationExtensions
    {
        #region Permutation Helpers

        /// <summary>
        /// A pass delegate for the first pass.
        /// </summary>
        private delegate void Pass1KernelDelegate<T, TStride>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView1D<T, TStride> view,
            ArrayView<int> randomNumbers)
            where T : unmanaged
            where TStride : struct, IStride1D;

        /// <summary>
        /// A pass delegate for the second pass.
        /// </summary>
        private delegate void Pass2KernelDelegate<T, TInputStride, TOutputStride>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView1D<T, TInputStride> input,
            ArrayView1D<T, TOutputStride> tempOutput,
            ArrayView<int> randomNumbers,
            int blockSize)
            where T : unmanaged
            where TInputStride : struct, IStride1D
            where TOutputStride : struct, IStride1D;

        /// <summary>
        /// Computes the required number of temp-storage elements for a permutation
        /// operation and the given input data length.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of elements to permute.</param>
        /// <returns>
        /// The required number of temp-storage elements in 32 bit ints.
        /// </returns>
        public static Index1D ComputePermutationTempStorageSize<T>(
           this Accelerator accelerator,
           Index1D dataLength)
           where T : unmanaged
        {
            long numIntTElementsLong = Interop.ComputeRelativeSizeOf<int, T>(dataLength);
            IndexTypeExtensions.AssertIntIndexRange(numIntTElementsLong);
            int numIntTElements = (int)numIntTElementsLong;

            return dataLength + numIntTElements;
        }

        /// <summary>
        /// Computes the amount of shared memory in bytes that is needed to perform
        /// a group-wide permutation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="groupSize">The size of the group.</param>
        /// <returns>The size of shared memory in bytes.</returns>
        public static int ComputeGroupWidePermutationSharedMemorySize<T>(
            this Accelerator accelerator,
            int groupSize)
            where T : unmanaged
        {
            var arrayLength = Interop.SizeOf<T>() * groupSize;
            var radixLength = groupSize * sizeof(int);
            var keyLength = accelerator.WarpSize * sizeof(int);

            return
                arrayLength
                + arrayLength % sizeof(int)
                + radixLength
                + 2 * keyLength
                + sizeof(int);
        }

        /// <summary>
        /// Computes the amount of shared memory in bytes that is needed in the second
        /// kernel of the permutation algorithm.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="groupSize">The size of the group.</param>
        /// <returns>The size of shared memory in bytes.</returns>
        public static int ComputePermutationKernel2SharedMemorySize(
            this Accelerator accelerator,
            int groupSize) =>
                groupSize * sizeof(int)
                    + 2 * accelerator.WarpSize * sizeof(int)
                    + sizeof(int);

        #endregion

        #region Permutation Implementation

        private static readonly MethodInfo PermutationKernel1Method =
            typeof(PermutationExtensions).GetMethod(
               nameof(PermutationKernel1),
               BindingFlags.NonPublic | BindingFlags.Static)
            .ThrowIfNull();

        private static readonly MethodInfo PermutationKernel2Method =
            typeof(PermutationExtensions).GetMethod(
               nameof(PermutationKernel2),
               BindingFlags.NonPublic | BindingFlags.Static)
            .ThrowIfNull();

        /// <summary>
        /// The first permutation pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride of the input view.</typeparam>
        /// <param name="view">The input view to use.</param>
        /// <param name="randomNumbers">The provided random numbers to use.</param>
        internal static void PermutationKernel1<T, TStride>(
            ArrayView1D<T, TStride> view,
            ArrayView<int> randomNumbers)
            where T : unmanaged
            where TStride : struct, IStride1D
        {
            var sharedMem = ILGPU.SharedMemory.GetDynamic<byte>();
            var arrayLength = Interop.SizeOf<T>() * Group.DimX;
            var elements = sharedMem.SubView(0, arrayLength).Cast<T>();
            var radixArray = sharedMem.SubView(arrayLength);

            var value =
                (randomNumbers[Grid.GlobalLinearIndex] & 0x7ffffc00) + Group.IdxX;
            elements[Group.IdxX] = view[Grid.GlobalLinearIndex];
            Group.Barrier();
            value = GroupExtensions.RadixSort<int, AscendingInt32>(value, radixArray);
            Group.Barrier();
            view[Grid.GlobalLinearIndex] = elements[value & 0x000003ff];
        }

        /// <summary>
        /// The second permutation pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TInputStride">The stride of the input buffer.</typeparam>
        /// <typeparam name="TOutputStride">The stride of the output buffer.</typeparam>
        /// <param name="input">The input buffer to use.</param>
        /// <param name="output">The output buffer to use.</param>
        /// <param name="randomNumbers">The provided random numbers to use.</param>
        /// <param name="blockSize">The block size to use.</param>
        internal static void PermutationKernel2<T, TInputStride, TOutputStride>(
            ArrayView1D<T, TInputStride> input,
            ArrayView1D<T, TOutputStride> output,
            ArrayView<int> randomNumbers,
            int blockSize)
            where T : unmanaged
            where TInputStride : struct, IStride1D
            where TOutputStride : struct, IStride1D
        {
            var radixArray = ILGPU.SharedMemory.GetDynamic<byte>();
            var value =
                (randomNumbers[Grid.GlobalLinearIndex] & 0x7ffffc00) + Group.IdxX;

            value = GroupExtensions.RadixSort<int, AscendingInt32>(value, radixArray);
            Group.Barrier();

            for (int i = 0; i < blockSize; i++)
            {
                var targetIdx = (value & 0x000003ff) + Group.DimX * Grid.IdxX;
                var sourceIdx = targetIdx * blockSize + i;
                output[
                    i
                    + Group.IdxX * blockSize * Grid.DimX
                    + blockSize * Grid.IdxX] = input[sourceIdx];
            }
        }

        #endregion

        #region Permutation

        /// <summary>
        /// Creates a new permutation operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The stride of all values.</typeparam>
        /// <typeparam name="TRandomProvider">The random number provider.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created permutation handler.</returns>
        public static Permute<T, TStride, TRandomProvider> CreatePermutation<
            T,
            TStride,
            TRandomProvider>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
        {
            var pass1Kernel = accelerator.LoadKernel<Pass1KernelDelegate<T,
                TStride>>(PermutationKernel1Method.MakeGenericMethod(
                    typeof(T), typeof(TStride)));

            var pass2Kernel = accelerator.LoadKernel<Pass2KernelDelegate<
                T,
                TStride,
                Stride1D.Dense>>(PermutationKernel2Method.MakeGenericMethod(
                    typeof(T), typeof(TStride), typeof(Stride1D.Dense)));

            var pass1DenseKernel = accelerator.LoadKernel<Pass1KernelDelegate<T,
                Stride1D.Dense>>(PermutationKernel1Method.MakeGenericMethod(
                    typeof(T), typeof(Stride1D.Dense)));

            var pass2DenseKernel = accelerator.LoadKernel<Pass2KernelDelegate<
                T,
                Stride1D.Dense,
                TStride>>(PermutationKernel2Method.MakeGenericMethod(
                    typeof(T), typeof(Stride1D.Dense), typeof(TStride)));

            return (stream, input, tempView, random) =>
            {
                var groupSize = accelerator.MaxNumThreadsPerGroup;
                var groups = XMath.DivRoundUp(input.IntLength, groupSize);
                var warpsPerGroup = groupSize / accelerator.WarpSize;
                var kernel2Groups = groups < accelerator.WarpSize ?
                    groups :
                    warpsPerGroup * groups / groupSize;
                var blockSize = groups < accelerator.WarpSize ? 1 : accelerator.WarpSize;

                var SharedMemoryConfigKernel1 = SharedMemoryConfig.RequestDynamic<byte>(
                    ComputeGroupWidePermutationSharedMemorySize<T>(
                        accelerator,
                        groupSize));
                var SharedMemoryConfigKernel2 = SharedMemoryConfig.RequestDynamic<byte>(
                    ComputePermutationKernel2SharedMemorySize(
                        accelerator,
                        groupSize));

                VerifyArguments<T, TStride>(
                    accelerator,
                    input,
                    tempView,
                    out var randomNumbers,
                    out var tempOutput);

                using var rng = RNG.Create<TRandomProvider>(accelerator, random);
                var view = rng.GetView(rng.MaxNumParallelWarps);

                rng.FillUniform(stream, randomNumbers);
                pass1Kernel(
                    stream,
                    new KernelConfig(
                        groups,
                        groupSize,
                        SharedMemoryConfigKernel1),
                    input,
                    randomNumbers);
                pass2Kernel(
                    stream,
                    new KernelConfig(
                        kernel2Groups,
                        groupSize,
                        SharedMemoryConfigKernel2),
                    input,
                    tempOutput,
                    randomNumbers,
                    blockSize);

                rng.FillUniform(stream, randomNumbers);
                pass1DenseKernel(
                    stream,
                    new KernelConfig(
                        groups,
                        groupSize,
                        SharedMemoryConfigKernel1),
                    tempOutput,
                    randomNumbers);
                pass2DenseKernel(
                    stream,
                    new KernelConfig(
                        kernel2Groups,
                        groupSize,
                        SharedMemoryConfigKernel2),
                    tempOutput,
                    input,
                    randomNumbers,
                    blockSize);
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void VerifyArguments<T, TStride>(
            Accelerator accelerator,
            ArrayView1D<T, TStride> input,
            ArrayView<int> tempView,
            out ArrayView<int> randomNumbers,
            out ArrayView<T> tempOutputView)
            where T : unmanaged
            where TStride : struct, IStride1D
        {
            if (!input.IsValid)
                throw new ArgumentNullException(nameof(input));

            var viewManager = new TempViewManager(tempView, nameof(tempView));

            tempOutputView = viewManager.Allocate<T>(input.Length);
            randomNumbers = viewManager.Allocate<int>(input.Length);
        }

        /// <summary>
        /// Creates a specialized permutation provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="tempStorageSize">
        /// the number of 32 bit integers to use as a temp storage.
        /// </param>
        /// <returns>The created provider.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static PermutationProvider CreatePermutationProvider(
            this Accelerator accelerator,
            Index1D tempStorageSize) =>
            tempStorageSize < 1
            ? throw new ArgumentOutOfRangeException(nameof(tempStorageSize))
            : new PermutationProvider(accelerator, tempStorageSize);

        /// <summary>
        /// Allocates a temporary memory view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The expected maximum data length to permute.</param>
        /// <returns>The allocated temporary view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PermutationProvider CreatePermutationProvider<T>(
            this Accelerator accelerator,
            Index1D dataLength)
            where T : unmanaged
        {
            var tempSize = accelerator.ComputePermutationTempStorageSize<T>(
                dataLength);
            return CreatePermutationProvider(accelerator, tempSize);
        }

        #endregion
    }
}
