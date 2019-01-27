// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Collections.Generic;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a function generator for helper device functions.
    /// </summary>
    sealed class PTXFunctionGenerator : PTXCodeGenerator
    {
        #region Static

        /// <summary>
        /// Uses this function generator to emit PTX header code.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        public static void GenerateHeader(
            in GeneratorArgs args,
            Scope scope)
        {
            var generator = new PTXFunctionGenerator(args, scope);
            generator.GenerateHeader();
            args.StringBuilder.AppendLine(";");
        }

        /// <summary>
        /// Uses this function generator to emit PTX code.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="allocas">Alloca information about the given scope.</param>
        /// <param name="constantOffset">The constant offset inside in the PTX code.</param>
        public static void Generate(
            in GeneratorArgs args,
            Scope scope,
            Allocas allocas,
            ref int constantOffset)
        {
            var generator = new PTXFunctionGenerator(args, scope);
            generator.GenerateCode(allocas, ref constantOffset);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Creates a new PTX function generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        private PTXFunctionGenerator(in GeneratorArgs args, Scope scope)
            : base(args, scope)
        { }

        #endregion

        #region Methods

        private List<MappedParameter> GenerateHeader()
        {
            var isExternal = Method.HasFlags(MethodFlags.External);

            Builder.AppendLine();
            if (isExternal)
                Builder.Append(".extern ");
            else
                Builder.Append(".visible ");
            Builder.Append(".func ");
            var returnType = Method.ReturnType;
            if (!returnType.IsVoidType)
            {
                Builder.Append("(");
                AppendParamDeclaration(returnType, returnParamName);
                Builder.Append(") ");
            }
            Builder.Append(GetMethodName(Method));
            Builder.AppendLine("(");

            var setupLogic = new EmptyParameterSetupLogic();
            var parameters = SetupParameters(ref setupLogic, 0);
            Builder.AppendLine();
            Builder.AppendLine(")");

            return parameters;
        }

        /// <summary>
        /// Generates PTX code.
        /// </summary>
        private void GenerateCode(Allocas allocas, ref int constantOffset)
        {
            if (Method.HasFlags(MethodFlags.External))
                return;

            var parameters = GenerateHeader();
            Builder.AppendLine("{");

            var allocations = SetupLocalAllocations(allocas);
            var registerOffset = Builder.Length;

            // Build param bindings and local memory variables
            PrepareCodeGeneration();
            BindAllocations(allocations);
            BindParameters(parameters);

            GenerateCode(registerOffset, ref constantOffset);
        }

        #endregion
    }
}
