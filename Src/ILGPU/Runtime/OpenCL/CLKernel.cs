// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using System;
using System.Diagnostics;
using System.Reflection;
using static ILGPU.Runtime.OpenCL.CLAPI;

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
        /// <param name="name">The name of the entry-point function.</param>
        /// <param name="source">The OpenCL source code.</param>
        /// <param name="version">The OpenCL C version.</param>
        /// <param name="programPtr">The created program pointer.</param>
        /// <param name="kernelPtr">The created kernel pointer.</param>
        /// <param name="errorLog">The error log (if any).</param>
        /// <returns>
        /// True, if the program and the kernel could be loaded successfully.
        /// </returns>
        public static CLError LoadKernel(
            CLAccelerator accelerator,
            string name,
            string source,
            CLCVersion version,
            out IntPtr programPtr,
            out IntPtr kernelPtr,
            out string errorLog)
        {
            errorLog = null;
            kernelPtr = IntPtr.Zero;
            var programError = CurrentAPI.CreateProgram(
                accelerator.NativePtr,
                source,
                out programPtr);
            if (programError != CLError.CL_SUCCESS)
                return programError;

            // Specify the OpenCL C version.
            string options = "-cl-std=" + version.ToString();

            var buildError = CurrentAPI.BuildProgram(
                programPtr,
                accelerator.DeviceId,
                options);

            if (buildError != CLError.CL_SUCCESS)
            {
                CLException.ThrowIfFailed(
                    CurrentAPI.GetProgramBuildLog(
                        programPtr,
                        accelerator.DeviceId,
                        out errorLog));
                CLException.ThrowIfFailed(
                    CurrentAPI.ReleaseProgram(programPtr));
                programPtr = IntPtr.Zero;
                return buildError;
            }

            return CurrentAPI.CreateKernel(
                programPtr,
                name,
                out kernelPtr);
        }

        /// <summary>
        /// Loads the binary representation of the given OpenCL kernel.
        /// </summary>
        /// <param name="program">The program pointer.</param>
        /// <returns>The binary representation of the underlying kernel.</returns>
        public static unsafe byte[] LoadBinaryRepresentation(IntPtr program)
        {
            IntPtr kernelSize;
            CLException.ThrowIfFailed(
                CurrentAPI.GetProgramInfo(
                    program,
                    CLProgramInfo.CL_PROGRAM_BINARY_SIZES,
                    new IntPtr(IntPtr.Size),
                    &kernelSize,
                    out var _));

            var programBinary = new byte[kernelSize.ToInt32()];
            fixed (byte* binPtr = &programBinary[0])
            {
                CLException.ThrowIfFailed(
                    CurrentAPI.GetProgramInfo(
                        program,
                        CLProgramInfo.CL_PROGRAM_BINARIES,
                        new IntPtr(IntPtr.Size),
                        &binPtr,
                        out var _));
            }

            return programBinary;
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
        /// Loads a compiled kernel into the given OpenCL context as kernel program.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="kernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        public CLKernel(
            CLAccelerator accelerator,
            CLCompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
            var errorCode = LoadKernel(
                accelerator,
                kernel.Name,
                kernel.Source,
                kernel.CVersion,
                out programPtr,
                out kernelPtr,
                out var errorLog);
            if (errorCode != CLError.CL_SUCCESS)
            {
                Trace.WriteLine("Kernel loading failed:");
                if (string.IsNullOrWhiteSpace(errorLog))
                    Trace.WriteLine(">> No error information available");
                else
                    Trace.WriteLine(errorLog);
            }

            CLException.ThrowIfFailed(errorCode);
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

        #region Methods

        /// <summary>
        /// Loads the binary representation of the underlying OpenCL kernel.
        /// </summary>
        /// <returns>The binary representation of the underlying kernel.</returns>
        public byte[] LoadBinaryRepresentation() => LoadBinaryRepresentation(ProgramPtr);

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this OpenCL kernel.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            // Free the kernel
            if (kernelPtr != IntPtr.Zero)
            {
                CLException.VerifyDisposed(
                    disposing,
                    CurrentAPI.ReleaseKernel(kernelPtr));
                kernelPtr = IntPtr.Zero;
            }

            // Free the surrounding program
            if (programPtr != IntPtr.Zero)
            {
                CLException.VerifyDisposed(
                    disposing,
                    CurrentAPI.ReleaseProgram(programPtr));
                programPtr = IntPtr.Zero;
            }
        }

        #endregion
    }
}
