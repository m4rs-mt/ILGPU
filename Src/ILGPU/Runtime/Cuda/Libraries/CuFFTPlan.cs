// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTPlan.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a cuFFT plan.
    /// </summary>
    public sealed partial class CuFFTPlan : DisposeBase
    {
        /// <summary>
        /// The underlying API wrapper.
        /// </summary>
        public CuFFTAPI API { get; }

        /// <summary>
        /// The native plan handle.
        /// </summary>
        public IntPtr PlanHandle { get; private set; }

        /// <summary>
        /// Constructs a new instance to wrap a cuFFT plan.
        /// </summary>
        public CuFFTPlan(CuFFTAPI api, IntPtr plan)
        {
            API = api;
            PlanHandle = plan;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CuFFTException.ThrowIfFailed(
                    API.Destroy(PlanHandle));
                PlanHandle = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Associates a CUDA stream with a cuFFT plan.
        /// </summary>
        public void SetStream(CudaStream cudaStream)
        {
            CuFFTException.ThrowIfFailed(
                API.SetStream(PlanHandle, cudaStream));
        }

        #region Caller Allocated Work Area Support

        /// <summary>
        /// Indicates whether to allocate work area.
        /// </summary>
        public void SetAutoAllocate(bool autoAllocate) =>
            CuFFTException.ThrowIfFailed(
                API.SetAutoAllocation(PlanHandle, autoAllocate ? 1 : 0));

        /// <summary>
        /// Overrides the work area associated with a plan.
        /// </summary>
        public void SetWorkArea(ArrayView<byte> workArea) =>
            CuFFTException.ThrowIfFailed(
                API.SetWorkArea(PlanHandle, workArea));

        #endregion

        #region Extensible Plans

        /// <summary>
        /// Configures a 1D plan.
        /// </summary>
        /// <param name="nx">The transform size.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult MakePlan1D(
            int nx,
            CuFFTType type,
            int batch,
            UIntPtr[] workSize) =>
            API.MakePlan1D(
                PlanHandle,
                nx,
                type,
                batch,
                workSize);

        /// <summary>
        /// Configures a 2D plan.
        /// </summary>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult MakePlan2D(
            int nx,
            int ny,
            CuFFTType type,
            UIntPtr[] workSize) =>
            API.MakePlan2D(
                PlanHandle,
                nx,
                ny,
                type,
                workSize);

        /// <summary>
        /// Configures a 3D plan.
        /// </summary>
        /// <param name="nx">The transform size in the x dimension.</param>
        /// <param name="ny">The transform size in the y dimension.</param>
        /// <param name="nz">The transform size in the z dimension.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult MakePlan3D(
            int nx,
            int ny,
            int nz,
            CuFFTType type,
            UIntPtr[] workSize) =>
            API.MakePlan3D(
                PlanHandle,
                nx,
                ny,
                nz,
                type,
                workSize);

        /// <summary>
        /// Configures a custom plan.
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
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult MakePlanMany(
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
            API.MakePlanMany(
                PlanHandle,
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

        /// <summary>
        /// Configures a custom plan.
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
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult MakePlanMany(
            int rank,
            long[] n,
            long[] inembed,
            long istride,
            long idist,
            long[] onembed,
            long ostride,
            long odist,
            CuFFTType type,
            long batch,
            UIntPtr[] workSize) =>
            API.MakePlanMany(
                PlanHandle,
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

        #region Refined Estimated Size Of Work Area

        /// <summary>
        /// Estimates the work area for a 1D plan.
        /// </summary>
        /// <param name="nx">The transform size.</param>
        /// <param name="type">The transform type.</param>
        /// <param name="batch">The number of transforms.</param>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult GetSize1D(
            int nx,
            CuFFTType type,
            int batch,
            UIntPtr[] workSize) =>
            API.GetSize1D(
                PlanHandle,
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
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult GetSize2D(
            int nx,
            int ny,
            CuFFTType type,
            UIntPtr[] workSize) =>
            API.GetSize2D(
                PlanHandle,
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
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult GetSize3D(
            int nx,
            int ny,
            int nz,
            CuFFTType type,
            UIntPtr[] workSize) =>
            API.GetSize3D(
                PlanHandle,
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
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult GetSizeMany(
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
            API.GetSizeMany(
                PlanHandle,
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
        /// <param name="workSize">The work size.</param>
        /// <returns>The error code.</returns>
        public CuFFTResult GetSizeMany(
            int rank,
            long[] n,
            long[] inembed,
            long istride,
            long idist,
            long[] onembed,
            long ostride,
            long odist,
            CuFFTType type,
            long batch,
            UIntPtr[] workSize) =>
            API.GetSizeMany(
                PlanHandle,
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

        /// <summary>
        /// Returns the work size.
        /// </summary>
        /// <param name="workSize">Populated with the estimated size in bytes.</param>
        public void GetSize(UIntPtr[] workSize) =>
            CuFFTException.ThrowIfFailed(
                API.GetSize(
                    PlanHandle,
                    workSize));

        #endregion
    }
}
