// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a function generator for helper device functions.
    /// </summary>
    sealed class CLFunctionGenerator : CLCodeGenerator
    {
        #region Instance

        /// <summary>
        /// Creates a new OpenCL function generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="allocas">All local allocas.</param>
        public CLFunctionGenerator(
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
            // TODO: declare function and parameters
            builder.AppendLine(";");
        }

        /// <summary>
        /// Generates OpenCL code.
        /// </summary>
        public override void GenerateCode()
        {
            if (Method.HasFlags(MethodFlags.External))
                return;

            // TODO: declare function and parameters
            GenerateCodeInternal();
        }

        #endregion
    }
}
