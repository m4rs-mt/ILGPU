// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a compiled kernel in OpenCL source form.
    /// </summary>
    public sealed class CLCompiledKernel : CompiledKernel
    {
        #region Constants

        /// <summary>
        /// The entry name of the kernel function.
        /// </summary>
        public const string EntryName = "ILGPUKernel";

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new compiled kernel in OpenCL source form.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="source">The source code.</param>
        internal CLCompiledKernel(
            Context context,
            EntryPoint entryPoint,
            string source)
            : base(context, entryPoint)
        {
            Source = source;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the OpenCL source code.
        /// </summary>
        public string Source { get; }

        #endregion
    }
}
