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
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;
using static ILGPU.Algorithms.Random.RandomExtensions;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// An abstract cuRand API interface.
    /// </summary>
    public interface ICuRand : IDisposable
    {
        /// <summary>
        /// Returns the associated cuRand API instance.
        /// </summary>
        CuRandAPI API { get; }

        /// <summary>
        /// Returns the native cuRand generator pointer.
        /// </summary>
        IntPtr GeneratorPtr { get; }

        /// <summary>
        /// Returns the current library version.
        /// </summary>
        int Version { get; }
    }

    /// <summary>
    /// Utility class to instantiate and manage <see cref="ICuRand"/> instances.
    /// </summary>
    public static class CuRand
    {
        /// <summary>
        /// Constructs a new cuRand wrapper on the GPU using the V10 API.
        /// </summary>
        /// <param name="accelerator">The associated Cuda accelerator.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        public static GPUCuRand CreateGPU(
            CudaAccelerator accelerator,
            CuRandRngType rngType) =>
            CreateGPU(accelerator, rngType, CuRandAPIVersion.V10);

        /// <summary>
        /// Constructs a new cuRand wrapper on the GPU.
        /// </summary>
        /// <param name="accelerator">The associated Cuda accelerator.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        /// <param name="apiVersion">The cuRand API version.</param>
        public static GPUCuRand CreateGPU(
            CudaAccelerator accelerator,
            CuRandRngType rngType,
            CuRandAPIVersion apiVersion) =>
            new GPUCuRand(accelerator, rngType, apiVersion);

        /// <summary>
        /// Constructs a new cuRand wrapper on the CPU using the V10 API.
        /// </summary>
        /// <param name="context">The parent ILGPU context.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        public static CPUCuRand CreateCPU(
            Context context,
            CuRandRngType rngType) =>
            CreateCPU(context, rngType, CuRandAPIVersion.V10);

        /// <summary>
        /// Constructs a new cuRand wrapper on the GPU.
        /// </summary>
        /// <param name="context">The parent ILGPU context.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        /// <param name="apiVersion">The cuRand API version.</param>
        public static CPUCuRand CreateCPU(
            Context context,
            CuRandRngType rngType,
            CuRandAPIVersion apiVersion) =>
            new CPUCuRand(context, rngType, apiVersion);

        /// <summary>
        /// Sets the underlying native cuRand seed.
        /// </summary>
        public static void SetSeed<TICuRand>(this TICuRand rand, long seed)
            where TICuRand : ICuRand =>
            CuRandException.ThrowIfFailed(
                rand.API.SetSeed(rand.GeneratorPtr, seed));

        /// <summary>
        /// Generates random seeds using the native cuRand API.
        /// </summary>
        public static void GenerateRandomSeeds<TICuRand>(this TICuRand rand)
            where TICuRand : ICuRand =>
            CuRandException.ThrowIfFailed(
                rand.API.GenerateSeeds(rand.GeneratorPtr));
    }

    /// <summary>
    /// Wraps library calls to the external native Nvidia cuRand library.
    /// </summary>
    public sealed class GPUCuRand : RNG, ICuRand
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
        /// <param name="accelerator">The associated Cuda accelerator.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        /// <param name="apiVersion">The cuRand API version.</param>
        public GPUCuRand(
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
        public CuRandAPI API { get; }

        /// <summary>
        /// Returns the native cuRand generator pointer.
        /// </summary>
        public IntPtr GeneratorPtr { get; private set; }

        /// <summary>
        /// Returns the current library version.
        /// </summary>
        public int Version { get; }

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

        /// <summary>
        /// Updates the current stream or skips the assignment process if the stream has
        /// already been assigned.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
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


    /// <summary>
    /// Wraps library calls to the external native Nvidia cuRand library using the CPU
    /// cuRand implementation.
    /// </summary>
    public sealed class CPUCuRand : DisposeBase, ICuRand
    {
        #region Instance

        /// <summary>
        /// Constructs a new cuRand wrapper.
        /// </summary>
        /// <param name="context">The parent ILGPU context.</param>
        /// <param name="rngType">The cuRand RNG type.</param>
        /// <param name="apiVersion">The cuRand API version.</param>
        public CPUCuRand(
            Context context,
            CuRandRngType rngType,
            CuRandAPIVersion apiVersion)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            API = CuRandAPI.Create(context, apiVersion);

            CuRandException.ThrowIfFailed(
                API.CreateGeneratorHost(out IntPtr nativePtr, rngType));
            GeneratorPtr = nativePtr;

            CuRandException.ThrowIfFailed(
                API.GetVersion(out int version));
            Version = version;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated cuRand API instance.
        /// </summary>
        public CuRandAPI API { get; }

        /// <summary>
        /// Returns the native cuRand generator pointer.
        /// </summary>
        public IntPtr GeneratorPtr { get; private set; }

        /// <summary>
        /// Returns the current library version.
        /// </summary>
        public int Version { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Fills the given span with uniformly distributed unsigned integers.
        /// </summary>
        /// <param name="span">The span to fill.</param>
        [CLSCompliant(false)]
        public unsafe void FillUniform(Span<uint> span)
        {
            fixed (uint* ptr = span)
            {
                CuRandException.ThrowIfFailed(
                    API.GenerateUInt(
                        GeneratorPtr,
                        new IntPtr(ptr),
                        new IntPtr(span.Length)));
            }
        }

        /// <summary>
        /// Fills the given span with uniformly distributed unsigned longs.
        /// </summary>
        /// <param name="span">The span to fill.</param>
        [CLSCompliant(false)]
        public unsafe void FillUniform(Span<ulong> span)
        {
            fixed (ulong* ptr = span)
            {
                CuRandException.ThrowIfFailed(
                    API.GenerateULong(
                        GeneratorPtr,
                        new IntPtr(ptr),
                        new IntPtr(span.Length)));
            }
        }

        /// <summary>
        /// Fills the given span with uniformly distributed positive integers.
        /// </summary>
        /// <param name="span">The span to fill.</param>
        public unsafe void FillUniform(Span<int> span)
        {
            fixed (int *ptr = span)
            {
                FillUniform(new Span<uint>(ptr, span.Length));

                for (int i = 0, e = span.Length; i < e; ++i)
                    ptr[i] = ToInt((uint)ptr[i]);

            }
        }

        /// <summary>
        /// Fills the given span with uniformly distributed positive longs.
        /// </summary>
        /// <param name="span">The span to fill.</param>
        public unsafe void FillUniform(Span<long> span)
        {
            fixed (long *ptr = span)
            {
                FillUniform(new Span<ulong>(ptr, span.Length));

                for (int i = 0, e = span.Length; i < e; ++i)
                    ptr[i] = ToLong((ulong)ptr[i]);
            }
        }

        /// <summary>
        /// Fills the given span with uniformly distributed floats in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="span">The span to fill.</param>
        public unsafe void FillUniform(Span<float> span)
        {
            fixed (float* ptr = span)
            {
                CuRandException.ThrowIfFailed(
                    API.GenerateUniformFloat(
                        GeneratorPtr,
                        new IntPtr(ptr),
                        new IntPtr(span.Length)));
            }
        }

        /// <summary>
        /// Fills the given span with uniformly distributed normals in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="span">The span to fill.</param>
        public unsafe void FillUniform(Span<double> span)
        {
            fixed (double* ptr = span)
            {
                CuRandException.ThrowIfFailed(
                    API.GenerateUniformDouble(
                        GeneratorPtr,
                        new IntPtr(ptr),
                        new IntPtr(span.Length)));
            }
        }

        /// <summary>
        /// Fills the given span with normally distributed floats in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="span">The span to fill.</param>
        /// <param name="mean">The normal distribution mean.</param>
        /// <param name="stddev">The normal distribution standard deviation.</param>
        public unsafe void FillNormal(Span<float> span, float mean, float stddev)
        {
            fixed (float* ptr = span)
            {
                CuRandException.ThrowIfFailed(
                    API.GenerateNormalFloat(
                        GeneratorPtr,
                        new IntPtr(ptr),
                        new IntPtr(span.Length),
                        mean,
                        stddev));
            }
        }

        /// <summary>
        /// Fills the given span with normally distributed doubles in [0.0, ..., 1.0).
        /// </summary>
        /// <param name="span">The span to fill.</param>
        /// <param name="mean">The normal distribution mean.</param>
        /// <param name="stddev">The normal distribution standard deviation.</param>
        public unsafe void FillNormal(Span<double> span, double mean, double stddev)
        {
            fixed (double* ptr = span)
            {
                CuRandException.ThrowIfFailed(
                    API.GenerateNormalDouble(
                        GeneratorPtr,
                        new IntPtr(ptr),
                        new IntPtr(span.Length),
                        mean,
                        stddev));
            }
        }

        #endregion

        #region IDisposable

        /// <inheritdoc cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            var statusCode = API.DestoryGenerator(GeneratorPtr);
            if (disposing)
                CuRandException.ThrowIfFailed(statusCode);
            GeneratorPtr = IntPtr.Zero;
        }

        #endregion
    }
}
