// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: BackendHandler.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a custom backend event handler.
    /// </summary>
    public interface IBackendHandler
    {
        /// <summary>
        /// Completed all frontend operations.
        /// </summary>
        /// <param name="context">The main context.</param>
        /// <param name="entryPoint">The kernel function.</param>
        /// <returns>True, if the backend handler updated the given context.</returns>
        bool FinishedCodeGeneration(
            IRContext context,
            TopLevelFunction entryPoint);

        /// <summary>
        /// Initialized the kernel context via imports of the required
        /// kernel function.
        /// </summary>
        /// <param name="kernelContext">The custom backend kernel context.</param>
        /// <param name="kernelFunction">The kernel function.</param>
        /// <returns>True, if the backend handler updated the given context.</returns>
        bool InitializedKernelContext(
            IRContext kernelContext,
            TopLevelFunction kernelFunction);

        /// <summary>
        /// Prepared the kernel context for further optimization steps.
        /// </summary>
        /// <param name="kernelContext">The custom backend kernel context.</param>
        /// <param name="kernelFunction">The kernel function.</param>
        /// <returns>True, if the backend handler updated the given context.</returns>
        bool PreparedKernelContext(
            IRContext kernelContext,
            TopLevelFunction kernelFunction);

        /// <summary>
        /// Performed final kernel optimization steps.
        /// </summary>
        /// <param name="kernelContext">The custom backend kernel context.</param>
        /// <param name="kernelFunction">The kernel function.</param>
        /// <returns>True, if the backend handler updated the given context.</returns>
        bool OptimizedKernelContext(
            IRContext kernelContext,
            TopLevelFunction kernelFunction);
    }
}
