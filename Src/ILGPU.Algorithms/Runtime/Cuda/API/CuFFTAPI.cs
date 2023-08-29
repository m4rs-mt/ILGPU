// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

// disable: max_line_length

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the cuFFT API.
    /// </summary>
    public abstract partial class CuFFTAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The cuFFT version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static CuFFTAPI Create(CuFFTAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
                ?? throw new DllNotFoundException(nameof(CuFFTAPI))
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static CuFFTAPI CreateLatest()
        {
            Exception? firstException = null;
#if NET5_0_OR_GREATER
            var versions = Enum.GetValues<CuFFTAPIVersion>();
#else
            var versions = (CuFFTAPIVersion[])Enum.GetValues(typeof(CuFFTAPIVersion));
#endif
            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = versions[i];
                var api = CreateInternal(version);
                if (api is null)
                    continue;

                try
                {
                    var status = api.GetVersion(out _);
                    if (status == CuFFTResult.CUFFT_SUCCESS)
                        return api;
                }
                catch (Exception ex) when (
                    ex is DllNotFoundException ||
                    ex is EntryPointNotFoundException)
                {
                    firstException ??= ex;
                }
            }

            throw firstException ?? new DllNotFoundException(nameof(CuFFTAPI));
        }

        #endregion

        #region Basic Plans

        /// <summary>
        /// Creates a custom plan.
        /// </summary>
        /// <param name="plan">Filled in with the created plan.</param>
        /// <param name="rank">The transform.</param>
        /// <param name="n">The transform dimensions.</param>
        /// <param name="inembed">The storage dimensions of the input data.</param>
        /// <param name="istride">The stride of the input data.</param>
        /// <param name="idist">The distance of the input data.</param>
        /// <param name="onembed">The storage dimensions of the output data.</param>
        /// <param name="ostride">The stride of the output data.</param>
        /// <param name="odist">The distance of the output data.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult PlanMany(
            out IntPtr plan,
            int rank,
            ReadOnlySpan<int> n,
            ReadOnlySpan<int> inembed,
            int istride,
            int idist,
            Span<int> onembed,
            int ostride,
            int odist,
            CuFFTType type,
            int batch)
        {
            fixed (int* nPtr = n)
            fixed (int* inembedPtr = inembed)
            fixed (int* onembedPtr = onembed)
            {
                var errorCode = PlanMany(
                    out var planHandle,
                    rank,
                    nPtr,
                    inembedPtr,
                    istride,
                    idist,
                    onembedPtr,
                    ostride,
                    odist,
                    type,
                    batch);
                plan = errorCode == CuFFTResult.CUFFT_SUCCESS
                    ? planHandle
                    : default;
                return errorCode;
            }
        }

        #endregion

        #region Estimated Size Of Work Area

        /// <summary>
        /// Estimates the work area for a 1D plan.
        /// </summary>
        /// <param name="nx">The transform size.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">Filled in with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult Estimate1D(
            int nx,
            CuFFTType type,
            int batch,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return Estimate1D(
                    nx,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Estimates the work area for a 2D plan.
        /// </summary>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">Filled in with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult Estimate2D(
            int nx,
            int ny,
            CuFFTType type,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return Estimate2D(
                    nx,
                    ny,
                    type,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Estimates the work area for a 3D plan.
        /// </summary>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="nz">The transform size in the z dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">Filled in with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult Estimate3D(
            int nx,
            int ny,
            int nz,
            CuFFTType type,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return Estimate3D(
                    nx,
                    ny,
                    nz,
                    type,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Estimates the work area for a custom plan.
        /// </summary>
        /// <param name="rank">The transform.</param>
        /// <param name="n">The transform dimensions.</param>
        /// <param name="inembed">The storage dimensions of the input data.</param>
        /// <param name="istride">The stride of the input data.</param>
        /// <param name="idist">The distance of the input data.</param>
        /// <param name="onembed">The storage dimensions of the output data.</param>
        /// <param name="ostride">The stride of the output data.</param>
        /// <param name="odist">The distance of the output data.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">Filled in with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult EstimateMany(
            int rank,
            ReadOnlySpan<int> n,
            ReadOnlySpan<int> inembed,
            int istride,
            int idist,
            Span<int> onembed,
            int ostride,
            int odist,
            CuFFTType type,
            int batch,
            Span<UIntPtr> workSize)
        {
            fixed (int* nPtr = n)
            fixed (int* inembedPtr = inembed)
            fixed (int* onembedPtr = onembed)
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return EstimateMany(
                    rank,
                    nPtr,
                    inembedPtr,
                    istride,
                    idist,
                    onembedPtr,
                    ostride,
                    odist,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Associates a CUDA stream with a cuFFT plan.
        /// </summary>
        public CuFFTResult SetStream(IntPtr plan, CudaStream cudaStream) =>
            SetStream(
                plan,
                cudaStream?.StreamPtr ?? IntPtr.Zero);

        #endregion

        #region Caller Allocated Work Area Support

        /// <summary>
        /// Indicates whether to allocate work area.
        /// </summary>
        public CuFFTResult SetAutoAllocate(IntPtr plan, bool autoAllocate) =>
            SetAutoAllocation(plan, autoAllocate ? 1 : 0);

        /// <summary>
        /// Overrides the work area associated with a plan.
        /// </summary>
        public CuFFTResult SetWorkArea(IntPtr plan, ArrayView<byte> workArea) =>
            SetWorkArea(
                plan,
                workArea.LoadEffectiveAddressAsPtr());

        #endregion

        #region Extensible Plans

        /// <summary>
        /// Configures a 1D plan.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="nx">The transform size.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult MakePlan1D(
            IntPtr plan,
            int nx,
            CuFFTType type,
            int batch,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return MakePlan1D(
                    plan,
                    nx,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Configures a 2D plan.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult MakePlan2D(
            IntPtr plan,
            int nx,
            int ny,
            CuFFTType type,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return MakePlan2D(
                    plan,
                    nx,
                    ny,
                    type,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Configures a 3D plan.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="nz">The transform size in the z dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult MakePlan3D(
            IntPtr plan,
            int nx,
            int ny,
            int nz,
            CuFFTType type,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return MakePlan3D(
                    plan,
                    nx,
                    ny,
                    nz,
                    type,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Configures a custom plan.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="rank">The transform.</param>
        /// <param name="n">The transform dimensions.</param>
        /// <param name="inembed">The storage dimensions of the input data.</param>
        /// <param name="istride">The stride of the input data.</param>
        /// <param name="idist">The distance of the input data.</param>
        /// <param name="onembed">The storage dimensions of the output data.</param>
        /// <param name="ostride">The stride of the output data.</param>
        /// <param name="odist">The distance of the output data.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult MakePlanMany(
            IntPtr plan,
            int rank,
            ReadOnlySpan<int> n,
            ReadOnlySpan<int> inembed,
            int istride,
            int idist,
            Span<int> onembed,
            int ostride,
            int odist,
            CuFFTType type,
            int batch,
            Span<UIntPtr> workSize)
        {
            fixed (int* nPtr = n)
            fixed (int* inembedPtr = inembed)
            fixed (int* onembedPtr = onembed)
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return MakePlanMany(
                    plan,
                    rank,
                    nPtr,
                    inembedPtr,
                    istride,
                    idist,
                    onembedPtr,
                    ostride,
                    odist,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Configures a custom plan.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="rank">The transform.</param>
        /// <param name="n">The transform dimensions.</param>
        /// <param name="inembed">The storage dimensions of the input data.</param>
        /// <param name="istride">The stride of the input data.</param>
        /// <param name="idist">The distance of the input data.</param>
        /// <param name="onembed">The storage dimensions of the output data.</param>
        /// <param name="ostride">The stride of the output data.</param>
        /// <param name="odist">The distance of the output data.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult MakePlanMany(
            IntPtr plan,
            int rank,
            ReadOnlySpan<long> n,
            ReadOnlySpan<long> inembed,
            long istride,
            long idist,
            Span<long> onembed,
            long ostride,
            long odist,
            CuFFTType type,
            long batch,
            Span<UIntPtr> workSize)
        {
            fixed (long* nPtr = n)
            fixed (long* inembedPtr = inembed)
            fixed (long* onembedPtr = onembed)
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return MakePlanMany64(
                    plan,
                    rank,
                    nPtr,
                    inembedPtr,
                    istride,
                    idist,
                    onembedPtr,
                    ostride,
                    odist,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        #endregion

        #region Refined Estimated Size Of Work Area

        /// <summary>
        /// Provides a more accurate estimate of the work area for a 1D plan than
        /// <see cref="Estimate1D(int, CuFFTType, int, Span{UIntPtr})"/>.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="nx">The transform size.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult GetSize1D(
            IntPtr plan,
            int nx,
            CuFFTType type,
            int batch,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return GetSize1D(
                    plan,
                    nx,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Provides a more accurate estimate of the work area for a 2D plan than
        /// <see cref="Estimate2D(int, int, CuFFTType, Span{UIntPtr})"/>.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult GetSize2D(
            IntPtr plan,
            int nx,
            int ny,
            CuFFTType type,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return GetSize2D(
                    plan,
                    nx,
                    ny,
                    type,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Provides a more accurate estimate of the work area for a 3D plan than
        /// <see cref="Estimate3D(int, int, int, CuFFTType, Span{UIntPtr})"/>.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="nz">The transform size in the z dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult GetSize3D(
            IntPtr plan,
            int nx,
            int ny,
            int nz,
            CuFFTType type,
            Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return GetSize3D(
                    plan,
                    nx,
                    ny,
                    nz,
                    type,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Provides a more accurate estimate of the work area for a custom plan than
        /// <see cref="EstimateMany(int, ReadOnlySpan{int}, ReadOnlySpan{int}, int, int, Span{int}, int, int, CuFFTType, int, Span{UIntPtr})"/>.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="rank">The transform.</param>
        /// <param name="n">The transform dimensions.</param>
        /// <param name="inembed">The storage dimensions of the input data.</param>
        /// <param name="istride">The stride of the input data.</param>
        /// <param name="idist">The distance of the input data.</param>
        /// <param name="onembed">The storage dimensions of the output data.</param>
        /// <param name="ostride">The stride of the output data.</param>
        /// <param name="odist">The distance of the output data.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult GetSizeMany(
            IntPtr plan,
            int rank,
            ReadOnlySpan<int> n,
            ReadOnlySpan<int> inembed,
            int istride,
            int idist,
            Span<int> onembed,
            int ostride,
            int odist,
            CuFFTType type,
            int batch,
            Span<UIntPtr> workSize)
        {
            fixed (int* nPtr = n)
            fixed (int* inembedPtr = inembed)
            fixed (int* onembedPtr = onembed)
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return GetSizeMany(
                    plan,
                    rank,
                    nPtr,
                    inembedPtr,
                    istride,
                    idist,
                    onembedPtr,
                    ostride,
                    odist,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Estimates the work area for a custom plan.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="rank">The transform.</param>
        /// <param name="n">The transform dimensions.</param>
        /// <param name="inembed">The storage dimensions of the input data.</param>
        /// <param name="istride">The stride of the input data.</param>
        /// <param name="idist">The distance of the input data.</param>
        /// <param name="onembed">The storage dimensions of the output data.</param>
        /// <param name="ostride">The stride of the output data.</param>
        /// <param name="odist">The distance of the output data.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public unsafe CuFFTResult GetSizeMany(
            IntPtr plan,
            int rank,
            ReadOnlySpan<long> n,
            ReadOnlySpan<long> inembed,
            long istride,
            long idist,
            Span<long> onembed,
            long ostride,
            long odist,
            CuFFTType type,
            long batch,
            Span<UIntPtr> workSize)
        {
            fixed (long* nPtr = n)
            fixed (long* inembedPtr = inembed)
            fixed (long* onembedPtr = onembed)
            fixed (UIntPtr* workSizePtr = workSize)
            {
                return GetSizeMany64(
                    plan,
                    rank,
                    nPtr,
                    inembedPtr,
                    istride,
                    idist,
                    onembedPtr,
                    ostride,
                    odist,
                    type,
                    batch,
                    workSizePtr);
            }
        }

        /// <summary>
        /// Returns the work size.
        /// </summary>
        /// <param name="plan">The plan handle.</param>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        public unsafe CuFFTResult GetSize(IntPtr plan, Span<UIntPtr> workSize)
        {
            fixed (UIntPtr* workSizePtr = workSize)
                return GetSize(plan, workSizePtr);
        }

        #endregion
    }
}
