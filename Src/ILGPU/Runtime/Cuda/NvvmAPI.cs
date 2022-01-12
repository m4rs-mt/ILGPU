// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvvmAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
#if NET5_0_OR_GREATER
using NativeLibrary = System.Runtime.InteropServices.NativeLibrary;
#else
using NativeLibrary = ILGPU.Util.NativeLibrary;
#endif

#pragma warning disable CA2216 // Disposable types should declare finalizer

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Wrapper for the NVVM API.
    /// </summary>
    public sealed class NvvmAPI : DisposeBase
    {
        #region Delegates

        [UnmanagedFunctionPointer(
            CallingConvention.Winapi,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        delegate string NvvmGetErrorString(NvvmResult result);

        delegate NvvmResult NvvmVersion(out int major, out int minor);

        delegate NvvmResult NvvmIRVersion(
            out int majorIR,
            out int minorIR,
            out int majorDbg,
            out int minorDbg);

        delegate NvvmResult NvvmCreateProgram(out IntPtr program);

        delegate NvvmResult NvvmDestroyProgram(ref IntPtr program);

        [UnmanagedFunctionPointer(
            CallingConvention.Winapi,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        delegate NvvmResult NvvmAddModuleToProgram(
            IntPtr program,
            IntPtr buffer,
            IntPtr size,
            string name);

        [UnmanagedFunctionPointer(
            CallingConvention.Winapi,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        delegate NvvmResult NvvmLazyAddModuleToProgram(
            IntPtr program,
            IntPtr buffer,
            IntPtr size,
            string name);

        delegate NvvmResult NvvmCompileProgram(
            IntPtr program,
            int numOptions,
            IntPtr options);

        delegate NvvmResult NvvmVerifyProgram(
            IntPtr program,
            int numOptions,
            IntPtr options);

        delegate NvvmResult NvvmGetCompiledResultSize(
            IntPtr program,
            out IntPtr bufferSize);
        delegate NvvmResult NvvmGetCompiledResult(
            IntPtr program,
            IntPtr buffer);

        delegate NvvmResult NvvmGetProgramLogSize(
            IntPtr program,
            out IntPtr bufferSize);
        delegate NvvmResult NvvmGetProgramLog(
            IntPtr program,
            IntPtr buffer);

        #endregion

        #region Static

        /// <summary>
        /// Creates a new instance of the NVVM API for the specified path.
        /// </summary>
        /// <param name="libNvvmPath">Path to NVVM library.</param>
        /// <returns>The NVVM API instance.</returns>
        public static NvvmAPI Create(string libNvvmPath) =>
            Create(libNvvmPath, string.Empty);

        /// <summary>
        /// Creates a new instance of the NVVM API for the specified path.
        /// </summary>
        /// <param name="libNvvmPath">Path to NVVM library.</param>
        /// <param name="libDevicePath">Path to LibDevice bitcode.</param>
        /// <returns>The NVVM API instance.</returns>
        public static NvvmAPI Create(string libNvvmPath, string libDevicePath) =>
            new NvvmAPI(libNvvmPath, libDevicePath);

        #endregion

        #region Instance

        /// <summary>
        /// The bytes of the loaded LibDevice bitcode.
        /// </summary>
        public ReadOnlySpan<byte> LibDeviceBytes => libDeviceBytes;

        /// <summary>
        /// The storage bytes of the loaded LibDevice bitcode. Exposed to the caller
        /// via <see cref="LibDeviceBytes"/> so that the contents cannot be modified.
        /// </summary>
        private readonly byte[] libDeviceBytes;

        /// <summary>
        /// Handle to NVVM module.
        /// </summary>
        private IntPtr libNvvmModule;

        private readonly NvvmGetErrorString nvvmGetErrorString;
        private readonly NvvmVersion nvvmVersion;
        private readonly NvvmIRVersion nvvmIRVersion;
        private readonly NvvmCreateProgram nvvmCreateProgram;
        private readonly NvvmDestroyProgram nvvmDestroyProgram;
        private readonly NvvmAddModuleToProgram nvvmAddModuleToProgram;
        private readonly NvvmLazyAddModuleToProgram nvvmLazyAddModuleToProgram;
        private readonly NvvmCompileProgram nvvmCompileProgram;
        private readonly NvvmVerifyProgram nvvmVerifyProgram;
        private readonly NvvmGetCompiledResultSize nvvmGetCompiledResultSize;
        private readonly NvvmGetCompiledResult nvvmGetCompiledResult;
        private readonly NvvmGetProgramLogSize nvvmGetProgramLogSize;
        private readonly NvvmGetProgramLog nvvmGetProgramLog;

        private NvvmAPI(string libNvvmPath, string libDevicePath)
        {
            libNvvmModule = NativeLibrary.Load(libNvvmPath);
            libDeviceBytes = !string.IsNullOrEmpty(libDevicePath)
                ? File.ReadAllBytes(libDevicePath)
                : Array.Empty<byte>();

            nvvmGetErrorString =
                Marshal.GetDelegateForFunctionPointer<NvvmGetErrorString>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmGetErrorString"));
            nvvmVersion =
                Marshal.GetDelegateForFunctionPointer<NvvmVersion>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmVersion"));
            nvvmIRVersion =
                Marshal.GetDelegateForFunctionPointer<NvvmIRVersion>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmIRVersion"));
            nvvmCreateProgram =
                Marshal.GetDelegateForFunctionPointer<NvvmCreateProgram>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmCreateProgram"));
            nvvmDestroyProgram =
                Marshal.GetDelegateForFunctionPointer<NvvmDestroyProgram>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmDestroyProgram"));
            nvvmAddModuleToProgram =
                Marshal.GetDelegateForFunctionPointer<NvvmAddModuleToProgram>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmAddModuleToProgram"));
            nvvmLazyAddModuleToProgram =
                Marshal.GetDelegateForFunctionPointer<NvvmLazyAddModuleToProgram>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmLazyAddModuleToProgram"));
            nvvmCompileProgram =
                Marshal.GetDelegateForFunctionPointer<NvvmCompileProgram>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmCompileProgram"));
            nvvmVerifyProgram =
                Marshal.GetDelegateForFunctionPointer<NvvmVerifyProgram>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmVerifyProgram"));
            nvvmGetCompiledResultSize =
                Marshal.GetDelegateForFunctionPointer<NvvmGetCompiledResultSize>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmGetCompiledResultSize"));
            nvvmGetCompiledResult =
                Marshal.GetDelegateForFunctionPointer<NvvmGetCompiledResult>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmGetCompiledResult"));
            nvvmGetProgramLogSize =
                Marshal.GetDelegateForFunctionPointer<NvvmGetProgramLogSize>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmGetProgramLogSize"));
            nvvmGetProgramLog =
                Marshal.GetDelegateForFunctionPointer<NvvmGetProgramLog>(
                    NativeLibrary.GetExport(
                        libNvvmModule,
                        "nvvmGetProgramLog"));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (libNvvmModule != IntPtr.Zero)
            {
                NativeLibrary.Free(libNvvmModule);
                libNvvmModule = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the error string for the NVVM error code.
        /// </summary>
        /// <param name="result">The error code.</param>
        /// <returns>The error string.</returns>
        public string GetErrorString(NvvmResult result) =>
            nvvmGetErrorString(result);

        /// <summary>
        /// Gets the NVVM version.
        /// </summary>
        /// <param name="major">Filled in with the major version.</param>
        /// <param name="minor">Filled in with the minor version.</param>
        /// <returns>The error code.</returns>
        public NvvmResult GetVersion(out int major, out int minor) =>
            nvvmVersion(out major, out minor);

        /// <summary>
        /// Gets the NVVM IR version.
        /// </summary>
        /// <param name="majorIR">Filled in with the major version</param>
        /// <param name="minorIR">Filled in with the minor version.</param>
        /// <param name="majorDbg">Filled in with the major version</param>
        /// <param name="minorDbg">Filled in with the minor version.</param>
        /// <returns>The error code.</returns>
        public NvvmResult GetIRVersion(
            out int majorIR,
            out int minorIR,
            out int majorDbg,
            out int minorDbg) =>
            nvvmIRVersion(out majorIR, out minorIR, out majorDbg, out minorDbg);

        /// <summary>
        /// Creates a new NVVM program.
        /// </summary>
        /// <param name="program">Filled in with the program handle.</param>
        /// <returns>The error code.</returns>
        public NvvmResult CreateProgram(out IntPtr program) =>
            nvvmCreateProgram(out program);

        /// <summary>
        /// Destroys a previously created NVVM program.
        /// </summary>
        /// <param name="program">The program to destroy. Filled in with NULL.</param>
        /// <returns>The error code.</returns>
        public NvvmResult DestroyProgram(ref IntPtr program) =>
            nvvmDestroyProgram(ref program);

        /// <summary>
        /// Add a module to the program.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="buffer">The buffer pointer.</param>
        /// <param name="size">The buffer size.</param>
        /// <param name="name">The module name.</param>
        /// <returns>The error code.</returns>
        public NvvmResult AddModuleToProgram(
            IntPtr program,
            IntPtr buffer,
            IntPtr size,
            string name) =>
            nvvmAddModuleToProgram(program, buffer, size, name);

        /// <summary>
        /// Add a lazy module to the program.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="buffer">The buffer pointer.</param>
        /// <param name="size">The buffer size.</param>
        /// <param name="name">The module name.</param>
        /// <returns>The error code.</returns>
        public NvvmResult LazyAddModuleToProgram(
            IntPtr program,
            IntPtr buffer,
            IntPtr size,
            string name) =>
            nvvmLazyAddModuleToProgram(program, buffer, size, name);

        /// <summary>
        /// Compiles the program.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="numOptions">The number of options.</param>
        /// <param name="options">The options.</param>
        /// <returns>The error code.</returns>
        public NvvmResult CompileProgram(
            IntPtr program,
            int numOptions,
            IntPtr options) =>
            nvvmCompileProgram(program, numOptions, options);

        /// <summary>
        /// Verifies the program.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="numOptions">The number of options.</param>
        /// <param name="options">The options.</param>
        /// <returns>The error code.</returns>
        public NvvmResult VerifyProgram(IntPtr program, int numOptions, IntPtr options) =>
            nvvmVerifyProgram(program, numOptions, options);

        /// <summary>
        /// Gets the compiled PTX result.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="result">Filled in with the PTX result.</param>
        /// <returns>The error code.</returns>
        public unsafe NvvmResult GetCompiledResult(IntPtr program, out string result)
        {
            var error = GetCompiledResultSize(program, out var bufferSize);
            if (error == NvvmResult.NVVM_SUCCESS)
            {
                var buffer = new byte[bufferSize.ToInt64()];
                fixed (byte* bufferPtr = buffer)
                {
                    error = GetCompiledResult(program, new IntPtr(bufferPtr));
                    if (error == NvvmResult.NVVM_SUCCESS)
                    {
                        // Remove the trailing null terminator.
                        result = Encoding.ASCII.GetString(buffer, 0, buffer.Length - 1);
                    }
                    else
                    {
                        result = default;
                    }
                }
            }
            else
            {
                result = default;
            }

            return error;
        }

        /// <summary>
        /// Gets the size of the compiled PTX result.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="bufferSize">Filled in with the buffer size.</param>
        /// <returns>The error code.</returns>
        public NvvmResult GetCompiledResultSize(IntPtr program, out IntPtr bufferSize) =>
            nvvmGetCompiledResultSize(program, out bufferSize);

        /// <summary>
        /// Gets the compiled PTX result.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="buffer">The buffer pointer.</param>
        /// <returns>The error code.</returns>
        public NvvmResult GetCompiledResult(IntPtr program, IntPtr buffer) =>
            nvvmGetCompiledResult(program, buffer);

        /// <summary>
        /// Gets the program log.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="result">Filled in with the program log.</param>
        /// <returns>The error code.</returns>
        public unsafe NvvmResult GetProgramLog(IntPtr program, out string result)
        {
            var error = GetProgramLogSize(program, out var bufferSize);
            if (error == NvvmResult.NVVM_SUCCESS)
            {
                var buffer = new byte[bufferSize.ToInt64()];
                fixed (byte* bufferPtr = buffer)
                {
                    error = GetProgramLog(program, new IntPtr(bufferPtr));
                    if (error == NvvmResult.NVVM_SUCCESS)
                    {
                        // Remove the trailing null terminator.
                        result = Encoding.ASCII.GetString(buffer, 0, buffer.Length - 1);
                    }
                    else
                    {
                        result = default;
                    }
                }
            }
            else
            {
                result = default;
            }

            return error;
        }

        /// <summary>
        /// Gets the size of the program log.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="bufferSize">Filled in with the buffer size.</param>
        /// <returns>The error code.</returns>
        public NvvmResult GetProgramLogSize(IntPtr program, out IntPtr bufferSize) =>
            nvvmGetProgramLogSize(program, out bufferSize);

        /// <summary>
        /// Gets the program log.
        /// </summary>
        /// <param name="program">The program.</param>
        /// <param name="buffer">The buffer pointer.</param>
        /// <returns>The error code.</returns>
        public NvvmResult GetProgramLog(IntPtr program, IntPtr buffer) =>
            nvvmGetProgramLog(program, buffer);

        #endregion
    }
}

#pragma warning restore CA2216 // Disposable types should declare finalizer
