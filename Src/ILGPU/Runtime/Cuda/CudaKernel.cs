// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.PTX;
using System;
using System.Diagnostics;
using System.Reflection;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda kernel that can be directly launched on a GPU.
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
        internal CudaKernel(
            CudaAccelerator accelerator,
            PTXCompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
            var kernelLoaded = CurrentAPI.LoadModule(
                out modulePtr,
                kernel.PTXAssembly,
                out string errorLog);
            if (kernelLoaded != CudaError.CUDA_SUCCESS)
            {
                Trace.WriteLine("PTX Kernel loading failed:");
                if (string.IsNullOrWhiteSpace(errorLog))
                    Trace.WriteLine(">> No error information available");
                else
                    Trace.WriteLine(errorLog);
            }
            CudaException.ThrowIfFailed(kernelLoaded);

            CudaException.ThrowIfFailed(
                CurrentAPI.GetModuleFunction(
                    out functionPtr,
                    modulePtr,
                    kernel.Name));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Cuda module pointer.
        /// </summary>
        public IntPtr ModulePtr => modulePtr;

        /// <summary>
        /// Returns the Cuda function pointer.
        /// </summary>
        public IntPtr FunctionPtr => functionPtr;

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this Cuda kernel.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyModule(modulePtr));
            functionPtr = IntPtr.Zero;
            modulePtr = IntPtr.Zero;
        }

        #endregion
    }
}
