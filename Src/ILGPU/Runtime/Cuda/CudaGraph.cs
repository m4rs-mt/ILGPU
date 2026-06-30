// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaGraph.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// A CUDA graph: the sequence of operations recorded between
    /// <see cref="CudaStream.BeginCapture(CudaStreamCaptureMode)"/> and
    /// <see cref="CudaStream.EndCapture"/>. A graph is a recorded template; call
    /// <see cref="Instantiate"/> to produce an executable graph that can be launched
    /// (and re-launched) with a single driver call. Wraps a native <c>CUgraph</c>.
    /// </summary>
    public sealed class CudaGraph : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Wraps an existing native graph handle. Ownership of the handle transfers to
        /// the new instance, which destroys it on disposal.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="graphPtr">The native graph handle.</param>
        internal CudaGraph(Accelerator accelerator, IntPtr graphPtr)
            : base(accelerator)
        {
            GraphPtr = graphPtr;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying native CUDA graph (<c>CUgraph</c>).
        /// </summary>
        public IntPtr GraphPtr { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Instantiates this graph into an executable graph. The returned
        /// <see cref="CudaGraphExec"/> can be launched repeatedly; this graph may be
        /// disposed independently once instantiation has completed.
        /// </summary>
        /// <returns>A new executable graph.</returns>
        public CudaGraphExec Instantiate()
        {
            using var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.InstantiateGraph(out var execPtr, GraphPtr, 0L));
            return new CudaGraphExec(Accelerator, execPtr);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Destroys the underlying native graph.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (GraphPtr == IntPtr.Zero)
                return;

            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyGraph(GraphPtr));
            GraphPtr = IntPtr.Zero;
        }

        #endregion
    }
}
