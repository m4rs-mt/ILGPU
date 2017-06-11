// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaKernel.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda kernel that can be directly launched on a gpu.
    /// </summary>
    public sealed class CudaKernel : Kernel
    {
        #region Instance

        /// <summary>
        /// Holds the pointer to the native Cuda module in memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IntPtr modulePtr;

        /// <summary>
        /// Holds the pointer to the native Cuda function in memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IntPtr functionPtr;

        /// <summary>
        /// Loads a compiled kernel into the given Cuda context as kernel program.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="kernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Will be verified in the constructor of the base class")]
        internal CudaKernel(
            CudaAccelerator accelerator,
            CompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
            CudaException.ThrowIfFailed(CudaNativeMethods.cuModuleLoadData(out modulePtr, kernel.GetBuffer()));
            CudaException.ThrowIfFailed(CudaNativeMethods.cuModuleGetFunction(out functionPtr, modulePtr, kernel.EntryName));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Cuda module ptr.
        /// </summary>
        public IntPtr ModulePtr => modulePtr;

        /// <summary>
        /// Returns the Cuda function ptr.
        /// </summary>
        public IntPtr FunctionPtr => functionPtr;

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (modulePtr != IntPtr.Zero)
            {
                CudaException.ThrowIfFailed(CudaNativeMethods.cuModuleUnload(modulePtr));
                functionPtr = IntPtr.Zero;
                modulePtr = IntPtr.Zero;
            }
        }

        #endregion
    }
}
