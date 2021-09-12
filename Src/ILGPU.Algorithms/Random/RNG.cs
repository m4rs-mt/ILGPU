// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright (c) 2017-2018 ILGPU Samples Project
//                                    www.ilgpu.net
//
// File: RNG.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random.Operations;
using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Represents a generic RNG object to compute random numbers.
    /// </summary>
    public abstract class RNG : AcceleratorObject
    {
        #region Static

        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="random">The parent RNG provider.</param>
        public static RNG<TRandomProvider> Create<TRandomProvider>(
            Accelerator accelerator,
            System.Random random)
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
            new RNG<TRandomProvider>(accelerator, random);

        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="random">The parent RNG provider.</param>
        /// <param name="maxNumParallelWarps">
        /// The maximum number of parallel warps.
        /// </param>
        public static RNG<TRandomProvider> Create<TRandomProvider>(
            Accelerator accelerator,
            System.Random random,
            int maxNumParallelWarps)
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
            new RNG<TRandomProvider>(
                accelerator,
                random,
                maxNumParallelWarps);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new RNG instance.
        /// </summary>
        /// <param name="accelerator">The parent accelerator object.</param>
        protected RNG(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Fills the given view with uniformly distributed positive integers.
        /// </summary>
        /// <param name="view">The view to fill.</param>
        public void FillUniform(ArrayView<int> view) =>
            FillUniform(Accelerator.DefaultStream, view);

        /// <summary>
        /// Fills the given view with uniformly distributed positive integers.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        public abstract void FillUniform(
            AcceleratorStream stream,
            ArrayView<int> view);

        /// <summary>
        /// Fills the given view with uniformly distributed positive longs.
        /// </summary>
        /// <param name="view">The view to fill.</param>
        public void FillUniform(ArrayView<long> view) =>
            FillUniform(Accelerator.DefaultStream, view);

        /// <summary>
        /// Fills the given view with uniformly distributed positive longs.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        public abstract void FillUniform(
            AcceleratorStream stream,
            ArrayView<long> view);

        /// <summary>
        /// Fills the given view with uniformly distributed floats in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="view">The view to fill.</param>
        public void FillUniform(ArrayView<float> view) =>
            FillUniform(Accelerator.DefaultStream, view);

        /// <summary>
        /// Fills the given view with uniformly distributed floats in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        public abstract void FillUniform(
            AcceleratorStream stream,
            ArrayView<float> view);

        /// <summary>
        /// Fills the given view with uniformly distributed doubles in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="view">The view to fill.</param>
        public void FillUniform(ArrayView<double> view) =>
            FillUniform(Accelerator.DefaultStream, view);

        /// <summary>
        /// Fills the given view with uniformly distributed doubles in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        public abstract void FillUniform(
            AcceleratorStream stream,
            ArrayView<double> view);

        #endregion
    }

    /// <summary>
    /// A generic RNG view that allows the parallel generation of random numbers within
    /// ILGPU kernels. The underlying implementation uses a single instance per Warp and
    /// uses local shift functions to simulate different RNGs per lane. The global state
    /// is stored within an underlying memory buffer.
    /// </summary>
    /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
    public readonly struct RNGView<TRandomProvider> : IRandomProvider
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        #region Instance

        /// <summary>
        /// A view to a single random provider per warp.
        /// </summary>
        private readonly ArrayView<TRandomProvider> randomProviders;

        /// <summary>
        /// The maximum number of parallel groups.
        /// </summary>
        private readonly int groupSize;

        /// <summary>
        /// Initializes the RNG view.
        /// </summary>
        /// <param name="providers">The random providers.</param>
        /// <param name="numParallelGroups">The maximum number of parallel groups.</param>
        internal RNGView(ArrayView<TRandomProvider> providers, int numParallelGroups)
        {
            randomProviders = providers;
            groupSize = numParallelGroups;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the current random number provider that is associated with this warp.
        /// </summary>
        /// <returns>A reference to the current random provider.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly ref TRandomProvider GetRandomProvider()
        {
            // Compute the global warp index
            int groupOffset = Grid.Index.ComputeLinearIndex(Grid.Dimension) % groupSize;
            int warpOffset = Group.LinearIndex;
            int warpIdx = groupOffset * Warp.WarpSize + warpOffset / Warp.WarpSize;

            // Access the underlying provider
            Trace.Assert(
                warpIdx < randomProviders.Length,
                "Current warp does not have a valid RNG provider");
            return ref randomProviders[warpIdx];
        }

        /// <summary>
        /// Generates a random value using the operation provided.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="TOperation">The operation implementation type.</typeparam>
        /// <returns>The next value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T Next<T, TOperation>()
            where T : struct
            where TOperation : struct, IRandomProviderOperation<T, TRandomProvider>
        {
            // Load provider from memory
            ref var providerRef = ref GetRandomProvider();
            var provider = providerRef;
            Warp.Barrier();

            // Shift the local period
            provider.ShiftPeriod(Warp.LaneIdx);

            // Apply the operation
            TOperation operation = default;
            T result = operation.Apply(ref provider);

            // Store the updated provider in the first warp
            if (Warp.IsFirstLane)
                providerRef = provider;
            Warp.Barrier();

            return result;
        }

        /// <inheritdoc cref="IRandomProvider.Next"/>
        public readonly int Next() =>
            Next<int, NextIntOperation<TRandomProvider>>();

        /// <inheritdoc cref="IRandomProvider.NextLong"/>
        public readonly long NextLong() =>
            Next<long, NextLongOperation<TRandomProvider>>();

        /// <inheritdoc cref="IRandomProvider.NextFloat"/>
        public readonly float NextFloat() =>
            Next<float, NextFloatOperation<TRandomProvider>>();

        /// <inheritdoc cref="IRandomProvider.NextDouble"/>
        public readonly double NextDouble() =>
            Next<double, NextDoubleOperation<TRandomProvider>>();

        #endregion
    }

    /// <summary>
    /// A generic RNG implementation that uses an underlying random provider (compatible
    /// with <see cref="XorShift32"/>, <see cref="XorShift64Star"/>,
    /// <see cref="XorShift128"/> and <see cref="XorShift128Plus"/>). It uses a single
    /// instance per warp and uses local shift functions to simulate different RNGs
    /// per lane. The global state is stored within an underlying memory buffer.
    /// </summary>
    /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public class RNG<TRandomProvider> : RNG
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        #region Static

        /// <summary>
        /// Computes the number of parallel warps on this accelerator.
        /// </summary>
        private static int GetMaxNumWarps(Accelerator accelerator) =>
            XMath.DivRoundUp(accelerator.MaxNumThreads, accelerator.WarpSize);

        #endregion

        #region Kernels

        /// <summary>
        /// The actual implementation of a generic RNG-based fill kernel.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="TOperation">The RNG operation.</typeparam>
        internal struct FillKernelBody<T, TOperation> : IGridStrideKernelBody
            where T : unmanaged
            where TOperation : struct, IRandomProviderOperation<T, TRandomProvider>
        {
            /// <summary>
            /// Constructs a new fill kernel.
            /// </summary>
            /// <param name="rngView">
            /// The RNG view that references the associated RNG instances per warp.
            /// </param>
            /// <param name="target">The target view to write to.</param>
            public FillKernelBody(RNGView<TRandomProvider> rngView, ArrayView<T> target)
            {
                RNGView = rngView;
                Target = target;
            }

            /// <summary>
            /// The underlying RNG view.
            /// </summary>
            public RNGView<TRandomProvider> RNGView { get; }

            /// <summary>
            /// The target view to write to.
            /// </summary>
            public ArrayView<T> Target { get; }

            /// <summary>
            /// Executes the intended RNG using the <see cref="RNGView"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(LongIndex1D linearIndex)
            {
                var nextValue = RNGView.Next<T, TOperation>();
                if (linearIndex < Target.Length)
                    Target[linearIndex] = nextValue;
            }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Finish() { }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Stores a single RNG instance per warp.
        /// </summary>
        private readonly MemoryBuffer1D<
            TRandomProvider,
            Stride1D.Dense> randomProvidersPerWarp;

        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="random">The parent RNG provider.</param>
        public RNG(Accelerator accelerator, System.Random random)
            : this(accelerator, random, GetMaxNumWarps(accelerator))
        { }

        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="random">The parent RNG provider.</param>
        /// <param name="maxNumParallelWarps">
        /// The maximum number of parallel warps.
        /// </param>
        public RNG(
            Accelerator accelerator,
            System.Random random,
            int maxNumParallelWarps)
            : base(accelerator)
        {
            if (maxNumParallelWarps < 1)
                throw new ArgumentOutOfRangeException(nameof(maxNumParallelWarps));

            // Initialize a single provider per warp
            int maxNumWarps = Math.Max(GetMaxNumWarps(accelerator), maxNumParallelWarps);
            TRandomProvider provider = default;
            var providers = new TRandomProvider[maxNumWarps];
            for (int i = 0; i < maxNumWarps; ++i)
                providers[i] = provider.CreateProvider(random);

            // Initialize all random providers
            randomProvidersPerWarp = accelerator.Allocate1D(providers);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the maximum number of parallel warps.
        /// </summary>
        public int MaxNumParallelWarps => (int)randomProvidersPerWarp.Length;

        /// <summary>
        /// Returns the length in bytes of the underlying memory buffer.
        /// </summary>
        public long LengthInBytes => randomProvidersPerWarp.LengthInBytes;

        #endregion

        #region Methods

        /// <summary>
        /// Gets a compatible RNG view via a desired number of threads.
        /// </summary>
        /// <param name="numThreads">The maximum number of parallel threads.</param>
        /// <returns>The RNG view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RNGView<TRandomProvider> GetViewViaThreads(int numThreads) =>
            GetView(XMath.DivRoundUp(numThreads, Accelerator.WarpSize));

        /// <summary>
        /// Gets a compatible RNG view via a desired number of warps.
        /// </summary>
        /// <param name="numWarps">The maximum number of parallel warps.</param>
        /// <returns>The RNG view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RNGView<TRandomProvider> GetView(int numWarps)
        {
            // Ensure that the number of warps is a multiple of the warp size.
            int numGroups = XMath.DivRoundUp(numWarps, Accelerator.WarpSize);
            numWarps = numGroups * Accelerator.WarpSize;
            Trace.Assert(
                numWarps > 0 && numWarps <= randomProvidersPerWarp.Length,
                "Invalid number of warps");
            var subView = randomProvidersPerWarp.View.SubView(0, numWarps);
            return new RNGView<TRandomProvider>(subView, numGroups);
        }

        /// <summary>
        /// Fills the given view with random values based on the
        /// <typeparamref name="TOperation"/>.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TOperation">The RNG operation type.</typeparam>
        /// <param name="stream">The current stream.</param>
        /// <param name="view">The target view.</param>
        private void Fill<T, TOperation>(AcceleratorStream stream, ArrayView<T> view)
            where T : unmanaged
            where TOperation : struct, IRandomProviderOperation<T, TRandomProvider>
        {
            var (gridSize, groupSize) = Accelerator.ComputeGridStrideLoopExtent(
                view.Length);
            int numWarps = XMath.DivRoundUp(gridSize * groupSize, Accelerator.WarpSize);
            Accelerator.LaunchGridStride(
                stream,
                view.Length,
                new FillKernelBody<T, TOperation>(
                    GetView(numWarps),
                    view));
        }

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{int})"/>
        public override void FillUniform(
            AcceleratorStream stream,
            ArrayView<int> view) =>
            Fill<int, NextIntOperation<TRandomProvider>>(stream, view);

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{long})"/>
        public override void FillUniform(
            AcceleratorStream stream,
            ArrayView<long> view) =>
            Fill<long, NextLongOperation<TRandomProvider>>(stream, view);

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{float})"/>
        public override void FillUniform(
            AcceleratorStream stream,
            ArrayView<float> view) =>
            Fill<float, NextFloatOperation<TRandomProvider>>(stream, view);

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{double})"/>
        public override void FillUniform(
            AcceleratorStream stream,
            ArrayView<double> view) =>
            Fill<double, NextDoubleOperation<TRandomProvider>>(stream, view);

        #endregion

        #region IDisposable

        /// <inheritdoc cref="AcceleratorObject.DisposeAcceleratorObject(bool)"/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
                randomProvidersPerWarp.Dispose();
        }

        #endregion
    }
}
