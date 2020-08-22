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
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage(
            "Microsoft.Design",
            "CA1062:Validate arguments of public methods",
            MessageId = "0",
            Justification = "Will be verified in the constructor of the base class")]
        internal CudaKernel(
            CudaAccelerator accelerator,
            PTXCompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
#if DEBUG
            var kernelLoaded = CurrentAPI.LoadModule(
                out modulePtr,
                kernel.PTXAssembly,
                out string errorLog);
            if (kernelLoaded != CudaError.CUDA_SUCCESS)
            {
                Debug.WriteLine("Kernel loading failed:");
                if (string.IsNullOrWhiteSpace(errorLog))
                    Debug.WriteLine(">> No error information available");
                else
                    Debug.WriteLine(errorLog);
            }
            CudaException.ThrowIfFailed(kernelLoaded);
#else
            CudaException.ThrowIfFailed(
                CurrentAPI.LoadModule(
                    out modulePtr,
                    kernel.PTXAssembly));
#endif
            CudaException.ThrowIfFailed(
                CurrentAPI.GetModuleFunction(
                    out functionPtr,
                    modulePtr,
                    PTXCompiledKernel.EntryName));
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
                CudaException.ThrowIfFailed(
                    CurrentAPI.DestroyModule(modulePtr));
                functionPtr = IntPtr.Zero;
                modulePtr = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
