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
        /// <param name="version">The OpenCL C version.</param>
        /// <param name="programPtr">The created program pointer.</param>
        /// <param name="kernelPtr">The created kernel pointer.</param>
        /// <param name="errorLog">The error log (if any).</param>
        /// <returns>True, if the program and the kernel could be loaded successfully.</returns>
        internal static CLError LoadKernel(
            CLAccelerator accelerator,
            string source,
            CLCVersion version,
            out IntPtr programPtr,
            out IntPtr kernelPtr,
            out string errorLog)
        {
            errorLog = null;
            kernelPtr = IntPtr.Zero;
            var programError = CLAPI.CreateProgram(
                accelerator.ContextPtr,
                source,
                out programPtr);
            if (programError != CLError.CL_SUCCESS)
                return programError;

            // Specify the OpenCL C version.
            string options = "-cl-std=" + version.ToString();

            var buildError = CLAPI.BuildProgram(
                programPtr,
                accelerator.DeviceId,
                options);

            if (buildError != CLError.CL_SUCCESS)
            {
                CLException.ThrowIfFailed(
                    CLAPI.GetProgramBuildLog(
                        programPtr,
                        accelerator.DeviceId,
                        out errorLog));
                CLException.ThrowIfFailed(
                    CLAPI.ReleaseProgram(programPtr));
                programPtr = IntPtr.Zero;
                return buildError;
            }

            return CLAPI.CreateKernel(
                programPtr,
                CLCompiledKernel.EntryName,
                out kernelPtr);
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
        public CLKernel(
            CLAccelerator accelerator,
            CLCompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
#if DEBUG
            var errorCode = LoadKernel(
                accelerator,
                kernel.Source,
                kernel.CVersion,
                out programPtr,
                out kernelPtr,
                out var errorLog);
            if (errorCode != CLError.CL_SUCCESS)
            {
                Debug.WriteLine("Kernel loading failed:");
                if (string.IsNullOrWhiteSpace(errorLog))
                    Debug.WriteLine(">> No error information available");
                else
                    Debug.WriteLine(errorLog);
            }
            CLException.ThrowIfFailed(errorCode);
#else
            CLException.ThrowIfFailed(LoadKernel(
                accelerator,
                kernel.Source,
                kernel.CVersion,
                out programPtr,
                out kernelPtr,
                out var _));
#endif

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
            base.Dispose(disposing);
        }

        #endregion
    }
}
