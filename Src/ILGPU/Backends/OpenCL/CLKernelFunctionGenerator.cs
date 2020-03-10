// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;
using System.Diagnostics;
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

        #region Nested Types

        /// <summary>
        /// A specialized kernel setup logic for parameters.
        /// </summary>
        private struct KernelParameterSetupLogic : IParametersSetupLogic
        {
            /// <summary>
            /// Constructs a new specialized kernel setup logic.
            /// </summary>
            /// <param name="generator">The parent generator.</param>
            public KernelParameterSetupLogic(CLKernelFunctionGenerator generator)
            {
                Parent = generator;

                IndexVariable = null;
                LengthVariable = null;
            }

            /// <summary>
            /// Returns the main index variable.
            /// </summary>
            public Variable IndexVariable { get; private set; }

            /// <summary>
            /// Returns the length variable of implicitly grouped kernels.
            /// </summary>
            public Variable LengthVariable { get; private set; }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public CLKernelFunctionGenerator Parent { get; }

            /// <summary cref="CLCodeGenerator.IParametersSetupLogic.GetOrCreateType(TypeNode)"/>
            public string GetOrCreateType(TypeNode typeNode) =>
                Parent.TypeGenerator.GetKernelArgumentType(typeNode);

            /// <summary cref="CLCodeGenerator.IParametersSetupLogic.HandleIntrinsicParameter(int, Parameter)"/>
            public Variable HandleIntrinsicParameter(int parameterOffset, Parameter parameter)
            {
                if (!Parent.EntryPoint.IsExplicitlyGrouped)
                {
                    IndexVariable = Parent.Allocate(parameter);

                    // This is an implicitly grouped kernel that needs boundary information
                    // to avoid out-of-bounds dispatches (See also PTXKernelFunctionGenerator)
                    LengthVariable = Parent.AllocateType(parameter.ParameterType);
                }

                return LengthVariable;
            }
        }

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

            // Analyze and create required kernel interop types first
            foreach (var param in Method.Parameters)
                TypeGenerator.GetKernelArgumentType(param.Type);
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
            bool hasDefaultParameters = EntryPoint.Parameters.Count > 0;
            for (int i = 0, e = viewParameters.Length; i < e; ++i)
            {
                // Emit a specialized pointer type
                var elementType = viewParameters[i].ElementType;
                Builder.Append("\tglobal ");
                Builder.Append(TypeGenerator[elementType]);
                Builder.Append(CLInstructions.DereferenceOperation);
                Builder.Append(' ');
                Builder.AppendFormat(KernelViewNameFormat, i.ToString());

                if (hasDefaultParameters || i + 1 < e)
                    Builder.AppendLine(",");
            }

            // Emit all parameter declarations
            int paramOffset = EntryPoint.IsExplicitlyGrouped ? 0 : 1;
            var setupLogic = new KernelParameterSetupLogic(this);
            SetupParameters(
                Builder,
                ref setupLogic,
                paramOffset);
            Builder.AppendLine(")");

            // Emit code that moves view arguments into their appropriate targets
            Builder.AppendLine("{");
            PushIndent();
            var paramVariables = GenerateArgumentMapping(paramOffset);
            GenerateViewWiring(paramVariables, paramOffset);

            // Emit index computation
#if DEBUG
            Builder.AppendLine();
            Builder.AppendLine("\t// Kernel indices");
            Builder.AppendLine();
#endif
            SetupKernelIndex(setupLogic.IndexVariable, setupLogic.LengthVariable);

            // Generate code
            GenerateCodeInternal();
            PopIndent();
            Builder.AppendLine("}");
        }

        /// <summary>
        /// Generates a set of instructions to copy input information to its
        /// internal structure representation.
        /// </summary>
        /// <param name="source">The source variable to copy from.</param>
        /// <param name="target">The target variable to copy to.</param>
        /// <param name="typeNode">The current type.</param>
        /// <param name="accessChain">The current access chain.</param>
        private void GenerateArgumentMappingAssignment(
            Variable source,
            Variable target,
            TypeNode typeNode,
            ImmutableArray<int> accessChain)
        {
            if (TypeGenerator.RequiresKernelArgumentMapping(typeNode, out var structureType))
            {
                // We have to emit mapping code in this case
                for (int i = 0, e = structureType.NumFields; i < e; ++i)
                {
                    GenerateArgumentMappingAssignment(
                        source,
                        target,
                        structureType.Fields[i],
                        accessChain.Add(i));
                }
            }
            else if (typeNode is ViewType)
            {
                // Ignore views here -> will be mapped afterwards
            }
            else
            {
                // Copy the contents to target
                using (var statement = BeginStatement(
                    target,
                    accessChain))
                {
                    statement.Append(source);
                    statement.AppendField(accessChain);
                }
            }
        }

        /// <summary>
        /// Generates code that wires kernel-specific arguments into internal arguments.
        /// </summary>
        /// <param name="paramOffset">The parameter offset.</param>
        private Variable[] GenerateArgumentMapping(int paramOffset)
        {
#if DEBUG
            Builder.AppendLine("\t// Map parameters");
            Builder.AppendLine();
#endif
            var parameters = Method.Parameters;
            var oldVariables = new Variable[parameters.Count];

            for (int i = paramOffset, e = parameters.Count; i < e; ++i)
            {
                var param = parameters[i];
                var sourceVariable = Load(param);
                var targetVariable = AllocateType(param.Type);
                Declare(targetVariable);

                GenerateArgumentMappingAssignment(
                    sourceVariable,
                    targetVariable,
                    param.Type,
                    ImmutableArray<int>.Empty);

                oldVariables[i] = sourceVariable;
                Bind(param, targetVariable);
            }

            return oldVariables;
        }

        /// <summary>
        /// Generates code that wires custom view parameters and all other data structures
        /// that are passed to a kernel.
        /// </summary>
        /// <param name="oldVariables">All old variables that have the be bound.</param>
        /// <param name="paramOffset">The parameter offset.</param>
        private void GenerateViewWiring(Variable[] oldVariables, int paramOffset)
        {
#if DEBUG
            Builder.AppendLine();
            Builder.AppendLine("\t// Assign views");
            Builder.AppendLine();
#endif
            var viewParams = EntryPoint.ViewParameters;
            for (int i = 0, e = viewParams.Length; i < e; ++i)
            {
                var viewParam = viewParams[i];
                // Load the associated parameter and generate a field-access chain
                var param = Method.Parameters[viewParam.ParameterIndex + paramOffset];
                var sourceVariable = oldVariables[viewParam.ParameterIndex + paramOffset];
                var targetVariable = Load(param);

                // Wire the view pointer and the passed structure
                var pointerChain = viewParam.AccessChain.Add(CLTypeGenerator.ViewPointerFieldIndex);
                using (var statement = BeginStatement(targetVariable, pointerChain))
                {
                    statement.AppendOperation(
                        string.Format(KernelViewNameFormat, i.ToString()));
                    statement.AppendOperation(
                        CLInstructions.GetArithmeticOperation(
                            BinaryArithmeticKind.Add,
                            false,
                            out var _));

                    statement.Append(sourceVariable);
                    statement.AppendField(pointerChain);
                }

                // Wire the length and the passed structure
                var lengthChain = viewParam.AccessChain.Add(CLTypeGenerator.ViewLengthFieldIndex);
                using (var statement = BeginStatement(targetVariable, lengthChain))
                {
                    statement.Append(sourceVariable);
                    statement.AppendField(lengthChain);
                }
            }
        }

        /// <summary>
        /// Emits an implicit kernel index computation.
        /// </summary>
        /// <param name="indexVariable">The index variable to write to.</param>
        /// <param name="boundsVariable">The associated bounds variable.</param>
        /// <param name="accessChain">The access chain to use.</param>
        /// <param name="dimension">The parameter dimension.</param>
        private void EmitImplicitKernelIndex(
            Variable indexVariable,
            Variable boundsVariable,
            ImmutableArray<int> accessChain,
            int dimension)
        {
            // Assign global id
            using (var statement = BeginStatement(indexVariable, accessChain))
            {
                statement.AppendOperation(CLInstructions.GetGlobalId);
                statement.BeginArguments();
                statement.AppendCommand(dimension.ToString());
                statement.EndArguments();
            }

            // Access bounds check
            var tempCondition = AllocateType(BasicValueType.Int1);
            using (var statement = BeginStatement(tempCondition))
            {
                statement.Append(indexVariable);
                statement.AppendField(accessChain);

                statement.AppendOperation(
                    CLInstructions.GetCompareOperation(CompareKind.GreaterEqual));

                statement.Append(boundsVariable);
                statement.AppendField(accessChain);
            }

            // TODO: refactor if-block generation into a separate emitter
            // See also Visit(ConditionalBranch).
            AppendIndent();
            Builder.Append("if (");
            Builder.Append(tempCondition.ToString());
            Builder.AppendLine(")");
            PushIndent();
            using (var statement = BeginStatement(CLInstructions.ReturnStatement)) { }
            PopIndent();
        }

        /// <summary>
        /// Setups the current kernel indices.
        /// </summary>
        /// <param name="indexVariable">The main kernel index variable.</param>
        /// <param name="lengthVariable">The length variable of implicitly grouped kernels.</param>
        private void SetupKernelIndex(Variable indexVariable, Variable lengthVariable)
        {
            if (EntryPoint.IsExplicitlyGrouped)
                return;

            Debug.Assert(indexVariable != null, "Invalid index variable");
            Declare(indexVariable);
            for (int i = 0, e = (int)EntryPoint.IndexType; i < e; ++i)
            {
                EmitImplicitKernelIndex(
                    indexVariable,
                    lengthVariable,
                    ImmutableArray.Create(i),
                    i);
            }
        }

        #endregion
    }
}
