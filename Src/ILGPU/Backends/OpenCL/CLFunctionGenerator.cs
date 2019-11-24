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
using ILGPU.IR.Types;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a function generator for helper device functions.
    /// </summary>
    sealed class CLFunctionGenerator : CLCodeGenerator
    {
        #region Nested Types

        /// <summary>
        /// A specialized function-type generator.
        /// </summary>
        private readonly struct FunctionTypeGenerator : IContextDependentTypeGenerator
        {
            /// <summary>
            /// Constructs a new specialized function-type generator.
            /// </summary>
            /// <param name="typeGenerator">The parent type generator.</param>
            public FunctionTypeGenerator(CLTypeGenerator typeGenerator)
            {
                TypeGenerator = typeGenerator;
            }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public CLTypeGenerator TypeGenerator { get; }

            /// <summary cref="CLCodeGenerator.IContextDependentTypeGenerator.GetOrCreateType(TypeNode)"/>
            public string GetOrCreateType(TypeNode typeNode) => TypeGenerator[typeNode];
        }

        #endregion

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
        /// Generates a header stub for the current method.
        /// </summary>
        /// <param name="builder">The target builder to use.</param>
        private void GenerateHeaderStub(StringBuilder builder)
        {
            Builder.Append(TypeGenerator[Method.ReturnType]);
            Builder.Append(' ');
            Builder.Append(GetMethodName(Method));
            Builder.AppendLine("(");
            GenerateParameters(
                new FunctionTypeGenerator(TypeGenerator),
                Builder,
                0);
            Builder.AppendLine(")");
        }

        /// <summary>
        /// Generates a function declaration in OpenCL code.
        /// </summary>
        public override void GenerateHeader(StringBuilder builder)
        {
            if (Method.HasFlags(MethodFlags.External))
                return;

            GenerateHeaderStub(builder);
            builder.AppendLine(";");
        }

        /// <summary>
        /// Generates OpenCL code.
        /// </summary>
        public override void GenerateCode()
        {
            if (Method.HasFlags(MethodFlags.External))
                return;

            // Declare function and parameters
            GenerateHeaderStub(Builder);

            // Generate code
            Builder.AppendLine("{");
            GenerateCodeInternal();
            Builder.AppendLine("}");
        }

        #endregion
    }
}
