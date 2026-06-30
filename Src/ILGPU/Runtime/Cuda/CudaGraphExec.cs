// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaGraphExec.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// An executable CUDA graph produced by <see cref="CudaGraph.Instantiate"/>. A single
    /// <see cref="Launch(CudaStream)"/> replays the entire recorded operation sequence
    /// with one driver call, amortizing per-launch host overhead across a repeated,
    /// fixed-shape workload. Wraps a native <c>CUgraphExec</c>.
    /// </summary>
    public sealed class CudaGraphExec : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Wraps an existing native executable-graph handle. Ownership of the handle
        /// transfers to the new instance, which destroys it on disposal.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="graphExecPtr">The native executable-graph handle.</param>
        internal CudaGraphExec(Accelerator accelerator, IntPtr graphExecPtr)
            : base(accelerator)
        {
            GraphExecPtr = graphExecPtr;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying native executable graph (<c>CUgraphExec</c>).
        /// </summary>
        public IntPtr GraphExecPtr { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Launches this executable graph on the given stream. The launch is asynchronous
        /// with respect to the host and ordered on the stream like any other submission;
        /// synchronize the stream when the results are needed.
        /// </summary>
        /// <param name="stream">The stream to launch on.</param>
        public void Launch(CudaStream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.LaunchGraph(GraphExecPtr, stream.StreamPtr));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Destroys the underlying native executable graph.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (GraphExecPtr == IntPtr.Zero)
                return;

            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyGraphExec(GraphExecPtr));
            GraphExecPtr = IntPtr.Zero;
        }

        #endregion
    }
}
