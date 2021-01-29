// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: CuRand.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms;
using ILGPU.Algorithms.Random;
using ILGPU.Runtime.Cuda.API;
using System;
using System.Runtime.CompilerServices;
using static ILGPU.Algorithms.Random.RandomExtensions;

namespace ILGPU.Runtime.Cuda
{

    /// <summary>
    /// Wraps library calls to the external native Nvidia cuRand library.
    /// </summary>
    public sealed class CuRand : RNG
    {
        #region Kernels

        /// <summary>
        /// A body implementation that converts unsigned ints to positive signed ints.
        /// </summary>
        internal readonly struct RandToIntBody : IGridStrideKernelBody
        {
            public RandToIntBody(ArrayView<int> data)
            {
                Data = data;
            }

            /// <summary>
            /// The target data view.
            /// </summary>
            public ArrayView<int> Data { get; }

            /// <inheritdoc cref="IGridStrideKernelBody.Execute(LongIndex1)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Execute(LongIndex1 linearIndex)
            {
                if (linearIndex >= Data.Length)
                    return;

                Data[linearIndex] = ToInt((uint)Data[linearIndex]);
            }
        }

        /// <summary>
        /// A body implementation that converts unsigned longs to positive signed longs.
        /// </summary>
        internal readonly struct RandToLongBody : IGridStrideKernelBody
        {
            public RandToLongBody(ArrayView<long> data)
            {
                Data = data;
            }

            /// <summary>
            /// The target data view.
            /// </summary>
            public ArrayView<long> Data { get; }

            /// <inheritdoc cref="IGridStrideKernelBody.Execute(LongIndex1)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Execute(LongIndex1 linearIndex)
            {
                if (linearIndex >= Data.Length)
                    return;

                Data[linearIndex] = ToLong((ulong)Data[linearIndex]);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// The underlying associated stream.
        /// </summary>
        private CudaStream currentStream;

        /// <summary>
        /// Constructs a new cuRand wrapper.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        public CuRand(CudaAccelerator accelerator, CuRandRngType rngType)
            : this(accelerator, rngType, CuRandAPIVersion.V10)
        { }

        /// <summary>
        /// Constructs a new cuRand wrapper.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        /// <param name="apiVersion">The cuRand API version.</param>
        public CuRand(
            CudaAccelerator accelerator,
            CuRandRngType rngType,
            CuRandAPIVersion apiVersion)
            : base(accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));

            API = CuRandAPI.Create(accelerator.Context, apiVersion);

            CuRandException.ThrowIfFailed(
                API.CreateGenerator(out IntPtr nativePtr, rngType));
            GeneratorPtr = nativePtr;

            CuRandException.ThrowIfFailed(
                API.GetVersion(out int version));
            Version = version;

            Stream = accelerator.DefaultStream as CudaStream;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated cuRand API instance.
        /// </summary>
        internal CuRandAPI API { get; }

        /// <summary>
        /// Returns the native cuRand generator pointer.
        /// </summary>
        public IntPtr GeneratorPtr { get; private set; }

        /// <summary>
        /// Returns the current library version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Sets the underlying native cuRand seed.
        /// </summary>
        public long Seed
        {
            set => CuRandException.ThrowIfFailed(
                API.SetSeed(GeneratorPtr, value));
        }

        /// <summary>
        /// Gets or sets the associated accelerator stream.
        /// </summary>
        public CudaStream Stream
        {
            get => currentStream;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                CuRandException.ThrowIfFailed(
                    API.SetStream(GeneratorPtr, value.StreamPtr));
                currentStream = value;
            }
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateOrKeepStream(AcceleratorStream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (currentStream == stream)
                return;
            Stream = (CudaStream)stream;
        }

        /// <summary>
        /// Generates random seeds using the native cuRand API.
        /// </summary>
        public void GenerateRandomSeeds() =>
            CuRandException.ThrowIfFailed(
                API.GenerateSeeds(GeneratorPtr));

        /// <summary>
        /// Fills the given view with uniformly distributed unsigned integers.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        [CLSCompliant(false)]
        public unsafe void FillUniform(
            AcceleratorStream stream,
            ArrayView<uint> view)
        {
            UpdateOrKeepStream(stream);
            CuRandException.ThrowIfFailed(
                API.GenerateUInt(
                    GeneratorPtr,
                    new IntPtr(view.LoadEffectiveAddress()),
                    new IntPtr(view.Length)));
        }

        /// <summary>
        /// Fills the given view with uniformly distributed unsigned longs.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        [CLSCompliant(false)]
        public unsafe void FillUniform(
            AcceleratorStream stream,
            ArrayView<ulong> view)
        {
            UpdateOrKeepStream(stream);
            CuRandException.ThrowIfFailed(
                API.GenerateULong(
                    GeneratorPtr,
                    new IntPtr(view.LoadEffectiveAddress()),
                    new IntPtr(view.Length)));
        }

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{int})"/>
        public override void FillUniform(
            AcceleratorStream stream,
            ArrayView<int> view)
        {
            // Fill view with arbitrary unsigned integers
            FillUniform(stream, view.Cast<uint>());

            // Convert into positive singed integers to match the API specification
            Accelerator.LaunchGridStride(
                stream,
                view.Length,
                new RandToIntBody(view));
        }

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{long})"/>
        public override void FillUniform(
            AcceleratorStream stream,
            ArrayView<long> view)
        {
            // Fill view with arbitrary unsigned longs
            FillUniform(stream, view.Cast<uint>());

            // Convert into positive singed longs to match the API specification
            Accelerator.LaunchGridStride(
                stream,
                view.Length,
                new RandToLongBody(view));
        }

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{float})"/>
        public override unsafe void FillUniform(
            AcceleratorStream stream,
            ArrayView<float> view)
        {
            UpdateOrKeepStream(stream);
            CuRandException.ThrowIfFailed(
                API.GenerateUniformFloat(
                    GeneratorPtr,
                    new IntPtr(view.LoadEffectiveAddress()),
                    new IntPtr(view.Length)));
        }

        /// <inheritdoc cref="RNG.FillUniform(AcceleratorStream, ArrayView{double})"/>
        public override unsafe void FillUniform(
            AcceleratorStream stream,
            ArrayView<double> view)
        {
            UpdateOrKeepStream(stream);
            CuRandException.ThrowIfFailed(
                API.GenerateUniformDouble(
                    GeneratorPtr,
                    new IntPtr(view.LoadEffectiveAddress()),
                    new IntPtr(view.Length)));
        }

        /// <summary>
        /// Fills the given view with normally distributed floats in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        /// <param name="mean">The normal distribution mean.</param>
        /// <param name="stddev">The normal distribution standard deviation.</param>
        public unsafe void FillNormal(
            AcceleratorStream stream,
            ArrayView<float> view,
            float mean,
            float stddev)
        {
            UpdateOrKeepStream(stream);
            CuRandException.ThrowIfFailed(
                API.GenerateNormalFloat(
                    GeneratorPtr,
                    new IntPtr(view.LoadEffectiveAddress()),
                    new IntPtr(view.Length),
                    mean,
                    stddev));
        }

        /// <summary>
        /// Fills the given view with normally distributed doubles in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="view">The view to fill.</param>
        /// <param name="mean">The normal distribution mean.</param>
        /// <param name="stddev">The normal distribution standard deviation.</param>
        public unsafe void FillNormal(
            AcceleratorStream stream,
            ArrayView<double> view,
            double mean,
            double stddev)
        {
            UpdateOrKeepStream(stream);
            CuRandException.ThrowIfFailed(
                API.GenerateNormalDouble(
                    GeneratorPtr,
                    new IntPtr(view.LoadEffectiveAddress()),
                    new IntPtr(view.Length),
                    mean,
                    stddev));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc cref="AcceleratorObject.DisposeAcceleratorObject(bool)"/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            var statusCode = API.DestoryGenerator(GeneratorPtr);
            if (disposing)
                CuRandException.ThrowIfFailed(statusCode);
            GeneratorPtr = IntPtr.Zero;
        }

        #endregion
    }
}
