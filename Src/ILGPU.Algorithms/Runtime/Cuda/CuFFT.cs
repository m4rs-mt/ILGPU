// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFT.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Wrapper over cuFFT to simplify integration with ILGPU.
    /// </summary>
    [CLSCompliant(false)]
    public sealed class CuFFT
    {
        /// <summary>
        /// Constructs a new CuFFT instance.
        /// </summary>
        public CuFFT()
            : this(default)
        { }

        /// <summary>
        /// Constructs a new CuFFT instance.
        /// </summary>
        public CuFFT(CuFFTAPIVersion? version)
        {
            API = CuFFTAPI.Create(version);
        }

        /// <summary>
        /// The underlying API wrapper.
        /// </summary>
        public CuFFTAPI API { get; }

        #region Basic Plans

        /// <summary>
        /// Creates a 1D plan.
        /// </summary>
        /// <param name="plan">Filled in with the created plan.</param>
        /// <param name="nx">The transform size.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult Plan1D(out CuFFTPlan plan, int nx, CuFFTType type, int batch)
        {
            var errorCode = API.Plan1D(out var planHandle, nx, type, batch);
            plan = errorCode == CuFFTResult.CUFFT_SUCCESS
                ? new CuFFTPlan(API, planHandle)
                : default;
            return errorCode;
        }

        /// <summary>
        /// Creates a 2D plan.
        /// </summary>
        /// <param name="plan">Filled in with the created plan.</param>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult Plan2D(out CuFFTPlan plan, int nx, int ny, CuFFTType type)
        {
            var errorCode = API.Plan2D(out var planHandle, nx, ny, type);
            plan = errorCode == CuFFTResult.CUFFT_SUCCESS
                ? new CuFFTPlan(API, planHandle)
                : default;
            return errorCode;
        }

        /// <summary>
        /// Creates a 3D plan.
        /// </summary>
        /// <param name="plan">Filled in with the created plan.</param>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="nz">The transform size in the z dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult Plan3D(
            out CuFFTPlan plan,
            int nx,
            int ny,
            int nz,
            CuFFTType type)
        {
            var errorCode = API.Plan3D(out var planHandle, nx, ny, nz, type);
            plan = errorCode == CuFFTResult.CUFFT_SUCCESS
                ? new CuFFTPlan(API, planHandle)
                : default;
            return errorCode;
        }

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
        public CuFFTResult PlanMany(
            out CuFFTPlan plan,
            int rank,
            int[] n,
            int[] inembed,
            int istride,
            int idist,
            int[] onembed,
            int ostride,
            int odist,
            CuFFTType type,
            int batch)
        {
            var errorCode = API.PlanMany(
                out var planHandle,
                rank,
                n,
                inembed,
                istride,
                idist,
                onembed,
                ostride,
                odist,
                type,
                batch);
            plan = errorCode == CuFFTResult.CUFFT_SUCCESS
                ? new CuFFTPlan(API, planHandle)
                : default;
            return errorCode;
        }

        #endregion

        #region Extensible Plans

        /// <summary>
        /// Creates an extensible plan.
        /// </summary>
        public CuFFTResult CreatePlan(out CuFFTPlan plan)
        {
            var errorCode = API.Create(out var planHandle);
            plan = errorCode == CuFFTResult.CUFFT_SUCCESS
                ? new CuFFTPlan(API, planHandle)
                : default;
            return errorCode;
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
        public CuFFTResult Estimate1D(
            int nx,
            CuFFTType type,
            int batch,
            UIntPtr[] workSize) =>
            API.Estimate1D(
                nx,
                type,
                batch,
                workSize);

        /// <summary>
        /// Estimates the work area for a 2D plan.
        /// </summary>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">Filled in with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult Estimate2D(
            int nx,
            int ny,
            CuFFTType type,
            UIntPtr[] workSize) =>
            API.Estimate2D(
                nx,
                ny,
                type,
                workSize);

        /// <summary>
        /// Estimates the work area for a 3D plan.
        /// </summary>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="nz">The transform size in the z dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">Filled in with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult Estimate3D(
            int nx,
            int ny,
            int nz,
            CuFFTType type,
            UIntPtr[] workSize) =>
            API.Estimate3D(
                nx,
                ny,
                nz,
                type,
                workSize);

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
        public CuFFTResult EstimateMany(
            int rank,
            int[] n,
            int[] inembed,
            int istride,
            int idist,
            int[] onembed,
            int ostride,
            int odist,
            CuFFTType type,
            int batch,
            UIntPtr[] workSize) =>
            API.EstimateMany(
                rank,
                n,
                inembed,
                istride,
                idist,
                onembed,
                ostride,
                odist,
                type,
                batch,
                workSize);

        #endregion
    }
}
