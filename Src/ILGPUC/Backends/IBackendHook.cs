// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IBackendHook.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a custom backend hook.
    /// </summary>
    public interface IBackendHook
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
