// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using System;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a compiled kernel in OpenCL source form.
    /// </summary>
    public sealed class CLCompiledKernel : CompiledKernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new compiled kernel in OpenCL source form.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="info">Detailed kernel information.</param>
        /// <param name="source">The source code.</param>
        /// <param name="version">The OpenCL C version.</param>
        public CLCompiledKernel(
            Context context,
            SeparateViewEntryPoint entryPoint,
            KernelInfo info,
            string source,
            CLCVersion version)
            : base(context, entryPoint, info)
        {
            Source = source;
            CVersion = version;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the OpenCL source code.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Returns the used OpenCL C version.
        /// </summary>
        public CLCVersion CVersion { get; }

        /// <summary>
        /// Returns the internally used entry point.
        /// </summary>
        internal new SeparateViewEntryPoint EntryPoint =>
            base.EntryPoint as SeparateViewEntryPoint;

        #endregion
    }
}
