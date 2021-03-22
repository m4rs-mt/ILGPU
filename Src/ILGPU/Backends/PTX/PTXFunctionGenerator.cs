// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Collections.Generic;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a function generator for helper device functions.
    /// </summary>
    sealed class PTXFunctionGenerator : PTXCodeGenerator
    {
        #region Instance

        /// <summary>
        /// Creates a new PTX function generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">All local allocas.</param>
        public PTXFunctionGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
            : base(args, method, allocas)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a PTX compatible list of mapped parameters.
        /// </summary>
        /// <param name="targetBuilder">
        /// The target builder to append the information to.
        /// </param>
        private List<MappedParameter> GenerateHeaderDeclaration(
            StringBuilder targetBuilder)
        {
            var isExternal = Method.HasFlags(MethodFlags.External);

            targetBuilder.AppendLine();
            if (isExternal)
                targetBuilder.Append(".extern ");
            else
                targetBuilder.Append(".visible ");
            targetBuilder.Append(".func ");
            var returnType = Method.ReturnType;
            if (!returnType.IsVoidType)
            {
                targetBuilder.Append('(');
                AppendParamDeclaration(targetBuilder, returnType, ReturnParamName);
                targetBuilder.Append(") ");
            }
            targetBuilder.Append(GetMethodName(Method));
            targetBuilder.AppendLine("(");

            var setupLogic = new EmptyParameterSetupLogic();
            var parameters = SetupParameters(targetBuilder, ref setupLogic, 0);
            targetBuilder.AppendLine();
            targetBuilder.AppendLine(")");

            return parameters;
        }

        /// <summary>
        /// Generates a function declaration in PTX code.
        /// </summary>
        public override void GenerateHeader(StringBuilder builder)
        {
            GenerateHeaderDeclaration(builder);
            builder.AppendLine(";");
        }

        /// <summary>
        /// Generates PTX code.
        /// </summary>
        public override void GenerateCode()
        {
            if (Method.HasFlags(MethodFlags.External))
                return;

            var parameters = GenerateHeaderDeclaration(Builder);
            Builder.AppendLine("{");

            var allocations = SetupAllocations();
            var registerOffset = Builder.Length;

            // Build param bindings and local memory variables
            PrepareCodeGeneration();
            BindAllocations(allocations);
            BindParameters(parameters);

            GenerateCodeInternal(registerOffset);
        }

        #endregion
    }
}
