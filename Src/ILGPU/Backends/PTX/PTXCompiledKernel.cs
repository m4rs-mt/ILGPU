﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using System;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a compiled kernel in PTX form.
    /// </summary>
    public sealed class PTXCompiledKernel : CompiledKernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new compiled kernel in PTX form.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="info">Detailed kernel information.</param>
        /// <param name="ptxAssembly">The assembly code.</param>
        internal PTXCompiledKernel(
            Context context,
            EntryPoint entryPoint,
            KernelInfo info,
            string ptxAssembly)
            : base(context, entryPoint, info)
        {
            PTXAssembly = ptxAssembly;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the PTX assembly code.
        /// </summary>
        public string PTXAssembly { get; }

        #endregion
    }
}
