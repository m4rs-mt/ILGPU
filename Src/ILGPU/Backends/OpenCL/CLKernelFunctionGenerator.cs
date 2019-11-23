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

using ILGPU.Backends.EntryPoints;
using ILGPU.IR.Analyses;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a function generator for main kernel functions.
    /// </summary>
    sealed class CLKernelFunctionGenerator : CLCodeGenerator
    {
        #region Constants

        /// <summary>
        /// The string format of a kernel-view parameter name.
        /// </summary>
        public const string KernelViewNameFormat = "view_{0}";

        #endregion

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
        {
            EntryPoint = args.EntryPoint;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated entry point.
        /// </summary>
        public SeparateViewEntryPoint EntryPoint { get; }

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
            // Emit kernel declaration and parameter definitions
            Builder.Append("kernel void ");
            Builder.Append(CLCompiledKernel.EntryName);
            Builder.AppendLine("(");

            // Note that we have to emit custom parameters for every view argument
            // since views have to be mapped by the driver to kernel arguments.
            var viewParameters = EntryPoint.ViewParameters;
            bool hasDefaultParameters = EntryPoint.Parameters.NumParameters > 0;
            for (int i = 0, e = viewParameters.Length; i < e; ++i)
            {
                // Emit a specialized pointer type
                var elementType = viewParameters[i].ElementType;
                Builder.Append("\t global ");
                Builder.Append(TypeGenerator[elementType]);
                Builder.Append(' ');
                Builder.Append(CLInstructions.DereferenceOperation);
                Builder.AppendFormat(KernelViewNameFormat, i.ToString());

                if (hasDefaultParameters || i + 1 < e)
                    Builder.AppendLine(",");
            }

            // Emit all parameter declarations
            GenerateParameters(Builder, 1);
            Builder.AppendLine(")");

            // Emit code that moves view arguments into their appropriate targets
            Builder.AppendLine("{");
            GenerateViewWiring();

            // Generate code
            GenerateCodeInternal();
            Builder.AppendLine("}");
        }

        /// <summary>
        /// Generates code that wires custom view parameters and all other data structures
        /// that are passed to a kernel.
        /// </summary>
        private void GenerateViewWiring()
        {
            var viewParams = EntryPoint.ViewParameters;
            for (int i = 0, e = viewParams.Length; i < e; ++i)
            {
                var viewParam = viewParams[i];
                // Load the associated parameter and generate a field-access chain
                var param = Method.Parameters[viewParam.ParameterIndex + 1];
                var variable = Load(param);

                // Wire the view pointer and the passed structure
                using (var statement = BeginStatement(
                    variable,
                    viewParam.AccessChain.Add(CLTypeGenerator.ViewPointerFieldIndex)))
                {
                    statement.AppendOperation(
                        string.Format(KernelViewNameFormat, i.ToString()));
                }
            }
        }

        #endregion
    }
}
