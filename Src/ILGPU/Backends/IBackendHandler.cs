// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IBackendHandler.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;

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
        void FinishedCodeGeneration(IRContext context, Method entryPoint);

        /// <summary>
        /// Initialized the kernel context via imports of the required
        /// kernel function.
        /// </summary>
        /// <param name="kernelContext">The custom backend kernel context.</param>
        /// <param name="kernelMethod">The kernel function.</param>
        void InitializedKernelContext(IRContext kernelContext, Method kernelMethod);

        /// <summary>
        /// Performed final kernel optimization steps.
        /// </summary>
        /// <param name="kernelContext">The custom backend kernel context.</param>
        /// <param name="kernelMethod">The kernel function.</param>
        void OptimizedKernelContext(IRContext kernelContext, Method kernelMethod);
    }
}
