// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLKernel.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using ILGPU.Runtime.OpenCL.API;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL kernel that can be directly launched on an OpenCL device.
    /// </summary>
    public sealed class CLKernel : Kernel
    {
        #region Static

        /// <summary>
        /// Loads the given OpenCL kernel.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="source">The OpenCL source code.</param>
        /// <param name="programPtr">The created program pointer.</param>
        /// <param name="kernelPtr">The created kernel pointer.</param>
        /// <returns>True, if the program and the kernel could be loaded successfully.</returns>
        internal static CLError LoadKernel(
            CLAccelerator accelerator,
            string source,
            out IntPtr programPtr,
            out IntPtr kernelPtr)
        {
            kernelPtr = IntPtr.Zero;
            var error = CLAPI.CreateProgram(
                accelerator.ContextPtr,
                source,
                out programPtr);
            if (error != CLError.CL_SUCCESS)
                return error;

            // TODO: OpenCL compiler options
            string options = string.Empty;

            error |= CLAPI.BuildProgram(
                programPtr,
                accelerator.DeviceId,
                options);

            error |= CLAPI.CreateKernel(
                programPtr,
                CLCompiledKernel.EntryName,
                out kernelPtr);

            if (error != CLError.CL_SUCCESS)
            {
                CLException.ThrowIfFailed(
                    CLAPI.ReleaseProgram(programPtr));
                programPtr = IntPtr.Zero;
            }
            return error;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Holds the pointer to the native OpenCL program in memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IntPtr programPtr;

        /// <summary>
        /// Holds the pointer to the native OpenCL kernel in memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IntPtr kernelPtr;

        /// <summary>
        /// Loads a compiled kernel into the given Cuda context as kernel program.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="kernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods",
            MessageId = "0", Justification = "Will be verified in the constructor of the base class")]
        internal CLKernel(
            CLAccelerator accelerator,
            CLCompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
            CLException.ThrowIfFailed(LoadKernel(
                accelerator,
                kernel.Source,
                out programPtr,
                out kernelPtr));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the OpenCL program ptr.
        /// </summary>
        public IntPtr ProgramPtr => programPtr;

        /// <summary>
        /// Returns the OpenCL kernel ptr.
        /// </summary>
        public IntPtr KernelPtr => kernelPtr;

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (kernelPtr != IntPtr.Zero)
            {
                CLException.ThrowIfFailed(
                    CLAPI.ReleaseKernel(kernelPtr));
                kernelPtr = IntPtr.Zero;
            }
            if (programPtr != IntPtr.Zero)
            {
                CLException.ThrowIfFailed(
                    CLAPI.ReleaseProgram(programPtr));
                programPtr = IntPtr.Zero;
            }
        }

        #endregion
    }
}
