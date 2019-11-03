// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a function generator for main kernel functions.
    /// </summary>
    sealed class CLKernelFunctionGenerator : CLCodeGenerator
    {
        #region Instance

        /// <summary>
        /// Creates a new OpenCL function generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="allocas">All local allocas.</param>
        public CLKernelFunctionGenerator(
            in GeneratorArgs args,
            Scope scope,
            Allocas allocas)
            : base(args, scope, allocas)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a function declaration in OpenCL code.
        /// </summary>
        public override void GenerateHeader(StringBuilder builder)
        {
            // We do not need to generate a header for a kernel function.
        }

        /// <summary>
        /// Generates OpenCL code.
        /// </summary>
        public override void GenerateCode()
        {
            // TODO: declare kernel and parameters
            GenerateCodeInternal();
        }

        #endregion
    }
}
