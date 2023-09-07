// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Generates OpenCL source code out of IR values.
    /// </summary>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    public abstract partial class CLCodeGenerator :
        CLVariableAllocator,
        IBackendCodeGenerator<StringBuilder>
    {
        #region Nested Types

        /// <summary>
        /// Generation arguments for code-generator construction.
        /// </summary>
        public readonly struct GeneratorArgs
        {
            internal GeneratorArgs(
                CLBackend backend,
                CLTypeGenerator typeGenerator,
                SeparateViewEntryPoint entryPoint,
                in AllocaKindInformation sharedAllocations,
                in AllocaKindInformation dynamicSharedAllocations)
            {
                Backend = backend;
                TypeGenerator = typeGenerator;
                EntryPoint = entryPoint;
                KernelTypeGenerator = new CLKernelTypeGenerator(
                    typeGenerator,
                    entryPoint);

                SharedAllocations = sharedAllocations;
                DynamicSharedAllocations = dynamicSharedAllocations;
            }

            /// <summary>
            /// Returns the underlying backend.
            /// </summary>
            public CLBackend Backend { get; }

            /// <summary>
            /// Returns the associated type generator.
            /// </summary>
            public CLTypeGenerator TypeGenerator { get; }

            /// <summary>
            /// Returns the current entry point.
            /// </summary>
            public SeparateViewEntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns all shared allocations.
            /// </summary>
            public AllocaKindInformation SharedAllocations { get; }

            /// <summary>
            /// Returns all dynamic shared allocations.
            /// </summary>
            public AllocaKindInformation DynamicSharedAllocations { get; }

            /// <summary>
            /// Returns the current kernel-type generator.
            /// </summary>
            internal CLKernelTypeGenerator KernelTypeGenerator { get; }
        }

        /// <summary>
        /// Represents a parameter that is mapped to OpenCL.
        /// </summary>
        protected internal readonly struct MappedParameter
        {
            #region Instance

            /// <summary>
            /// Constructs a new mapped parameter.
            /// </summary>
            /// <param name="variable">The OpenCL variable.</param>
            /// <param name="clName">The name of the parameter in OpenCL code.</param>
            /// <param name="parameter">The source parameter.</param>
            public MappedParameter(
                Variable variable,
                string clName,
                Parameter parameter)
            {
                Variable = variable;
                CLName = clName;
                Parameter = parameter;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated OpenCL variable.
            /// </summary>
            public Variable Variable { get; }

            /// <summary>
            /// Returns the name of the parameter in OpenCL code.
            /// </summary>
            public string CLName { get; }

            /// <summary>
            /// Returns the source parameter.
            /// </summary>
            public Parameter Parameter { get; }

            #endregion
        }

        /// <summary>
        /// Represents a parameter logic to setup function parameters.
        /// </summary>
        protected interface IParametersSetupLogic
        {
            /// <summary>
            /// Gets the corresponding OpenCL type for the given parameter.
            /// </summary>
            /// <param name="parameter">The parameter.</param>
            /// <returns>The resulting OpenCL type representation.</returns>
            string GetParameterType(Parameter parameter);

            /// <summary>
            /// Handles an intrinsic parameter and returns the
            /// associated allocated variable (if any).
            /// </summary>
            /// <param name="parameterOffset">
            /// The current intrinsic parameter index.
            /// </param>
            /// <param name="parameter">The intrinsic parameter.</param>
            /// <returns>The allocated variable (if any).</returns>
            Variable? HandleIntrinsicParameter(int parameterOffset, Parameter parameter);
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns the OpenCL function name for the given function.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The resolved OpenCL function name.</returns>
        protected static string GetMethodName(Method method)
        {
            var handleName = method.Handle.Name;
            if (method.HasFlags(MethodFlags.External))
            {
                return handleName;
            }
            else
            {
                // Constructor names in MSIL start with a dot. This is a reserved
                // character that cannot be used in the OpenCL function name. Replace
                // the dot with an underscore, assuming that the rest of the name is
                // using valid characters. Appending the IR node identifier will ensure
                // that there are no conflicts.
                return handleName.StartsWith(".", System.StringComparison.Ordinal)
                    ? handleName.Substring(1) + "_" + method.Id
                    : handleName + "_" + method.Id;
            }
        }

        /// <summary>
        /// Returns the OpenCL parameter name for the given parameter.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The resolved OpenCL parameter name.</returns>
        protected static string GetParameterName(Parameter parameter) =>
            "_" + parameter.Name + "_" + parameter.Id.ToString();

        #endregion

        #region Instance

        private int labelCounter;
        private readonly Dictionary<BasicBlock, string> blockLookup =
            new Dictionary<BasicBlock, string>();
        private readonly string labelPrefix;

        private StringBuilder prefixBuilder = new StringBuilder();
        private StringBuilder suffixBuilder = new StringBuilder();

        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">All local allocas.</param>
        internal CLCodeGenerator(in GeneratorArgs args, Method method, Allocas allocas)
            : base(args.TypeGenerator)
        {
            Backend = args.Backend;
            Method = method;
            ImplementationProvider = Backend.IntrinsicProvider;
            Allocas = allocas;

            labelPrefix = "L_" + Method.Id.ToString();

            Builder = prefixBuilder;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated backend.
        /// </summary>
        public CLBackend Backend { get; }

        /// <summary>
        /// Returns the associated method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns all local allocas.
        /// </summary>
        public Allocas Allocas { get; }

        /// <summary>
        /// Returns the current intrinsic provider for code-generation purposes.
        /// </summary>
        public IntrinsicImplementationProvider<CLIntrinsic.Handler>
            ImplementationProvider
        { get; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        public StringBuilder Builder { get; private set; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        public StringBuilder VariableBuilder { get; } = new StringBuilder();

        #endregion

        #region IBackendCodeGenerator

        /// <summary>
        /// Generates a function declaration in PTX code.
        /// </summary>
        public abstract void GenerateHeader(StringBuilder builder);

        /// <summary>
        /// Generates PTX code.
        /// </summary>
        public abstract void GenerateCode();

        /// <summary>
        /// Generates PTX constant declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateConstants(StringBuilder builder)
        {
            // No constants to emit
        }

        /// <summary cref="IBackendCodeGenerator{TKernelBuilder}.Merge(TKernelBuilder)"/>
        public void Merge(StringBuilder builder) => builder.Append(Builder);

        #endregion

        #region General Code Generation

        /// <summary>
        /// Declares a new label.
        /// </summary>
        /// <returns>The declared label.</returns>
        private string DeclareLabel() => labelPrefix + labelCounter++;

        /// <summary>
        /// Marks the given label.
        /// </summary>
        /// <param name="label">The label to mark.</param>
        protected void MarkLabel(string label)
        {
            Builder.Append('\t');
            Builder.Append(label);
            Builder.AppendLine(": ;");
        }

        /// <summary>
        /// Generates parameter declarations by writing them to the
        /// target builder provided.
        /// </summary>
        /// <typeparam name="TSetupLogic">
        /// The dependent code-generator type to use.
        /// </typeparam>
        /// <param name="logic">The type generator to use.</param>
        /// <param name="targetBuilder">The target builder to use.</param>
        /// <param name="paramOffset">The intrinsic parameter offset.</param>
        protected void SetupParameters<TSetupLogic>(
            StringBuilder targetBuilder,
            ref TSetupLogic logic,
            int paramOffset)
            where TSetupLogic : struct, IParametersSetupLogic
        {
            bool attachComma = false;
            int offset = 0;

            foreach (var param in Method.Parameters)
            {
                Variable? variable;
                if (offset < paramOffset)
                {
                    variable = logic.HandleIntrinsicParameter(offset, param);
                    offset++;
                }
                else
                {
                    variable = Allocate(param);
                }

                if (variable == null)
                    continue;

                if (attachComma)
                    targetBuilder.AppendLine(",");

                targetBuilder.Append('\t');
                targetBuilder.Append(logic.GetParameterType(param));
                targetBuilder.Append(' ');
                targetBuilder.Append(variable.VariableName);

                attachComma = true;
            }
        }

        /// <summary>
        /// Setups a given allocation.
        /// </summary>
        /// <param name="allocaInfo">The single allocation to declare.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>The allocated variable.</returns>
        protected Variable DeclareAllocation(
            in AllocaInformation allocaInfo,
            MemoryAddressSpace addressSpace)
        {
            var addressSpacePrefix = CLInstructions.GetAddressSpacePrefix(addressSpace);
            var allocationVariable = AllocateType(allocaInfo.ElementType);

            // Declare alloca using element-type information
            AppendIndent();
            Builder.Append(addressSpacePrefix);
            Builder.Append(' ');
            Builder.Append(TypeGenerator[allocaInfo.ElementType]);
            Builder.Append(' ');
            Builder.Append(allocationVariable.VariableName);

            if (allocaInfo.IsArray)
            {
                Builder.Append('[');
                Builder.Append(allocaInfo.ArraySize);
                Builder.Append(']');
            }

            Builder.AppendLine(";");
            return allocationVariable;
        }

        /// <summary>
        /// Setups local or shared allocations.
        /// </summary>
        /// <param name="allocas">The allocations to setup.</param>
        /// <param name="addressSpace">The source address space.local).</param>
        protected void SetupAllocations(
            AllocaKindInformation allocas,
            MemoryAddressSpace addressSpace)
        {
            foreach (var allocaInfo in allocas)
            {
                var allocationVariable = DeclareAllocation(allocaInfo, addressSpace);
                var allocaVariable = Allocate(allocaInfo.Alloca);

                // Since allocas are basically pointers in the IR we have to
                // 'convert' the local allocations into generic pointers
                using var statement = BeginStatement(allocaVariable);
                statement.AppendOperation(CLInstructions.AddressOfOperation);
                statement.Append(allocationVariable);
                if (allocaInfo.IsArray)
                    statement.AppendIndexer("0");
            }
            Builder.AppendLine();
        }

        /// <summary>
        /// Binds shared memory allocations.
        /// </summary>
        /// <param name="allocas">All allocations to bind.</param>
        protected void BindSharedMemoryAllocation(in AllocaKindInformation allocas)
        {
            foreach (var allocaInfo in allocas)
            {
                Bind(
                    allocaInfo.Alloca,
                    GetSharedMemoryAllocationVariable(allocaInfo));
            }
        }

        /// <summary>
        /// Generates code for all basic blocks.
        /// </summary>
        protected void GenerateCodeInternal()
        {
            // Setup allocations
            SetupAllocations(Allocas.LocalAllocations, MemoryAddressSpace.Local);

            // Build branch targets
            var blocks = Method.Blocks;
            foreach (var block in blocks)
                blockLookup.Add(block, DeclareLabel());

            // Find all phi nodes, allocate target registers and setup internal mapping
            var phiMapping = new Dictionary<BasicBlock, List<Variable>>();
            var dominators = Method.Blocks.CreateDominators();
            var phiBindings = PhiBindings.Create(
                blocks,
                (block, phiValue) =>
                {
                    var variable = Allocate(phiValue);

                    var targetBlock = block;
                    foreach (var argument in phiValue)
                    {
                        targetBlock = argument.BasicBlock == null
                            ? dominators.Root
                            : dominators.GetImmediateCommonDominator(
                                targetBlock,
                                argument.BasicBlock);

                        if (targetBlock == dominators.Root)
                            break;
                    }

                    if (!phiMapping.TryGetValue(targetBlock, out var phiVariables))
                    {
                        phiVariables = new List<Variable>();
                        phiMapping.Add(targetBlock, phiVariables);
                    }
                    phiVariables.Add(variable);
                });
            var intermediatePhiVariables = new Dictionary<Value, Variable>(
                phiBindings.MaxNumIntermediatePhis);

            // Generate code
            foreach (var block in blocks)
            {
                // Mark block label
                MarkLabel(blockLookup[block]);

                // Declare phi variables (if any)
                if (phiMapping.TryGetValue(block, out var phisToDeclare))
                {
                    foreach (var phiVariable in phisToDeclare)
                        Declare(phiVariable);
                }

                foreach (var value in block)
                {
                    // Check for intrinsic implementation
                    if (ImplementationProvider.TryGetCodeGenerator(
                        value,
                        out var intrinsicCodeGenerator))
                    {
                        // Generate specialized code for this intrinsic node
                        intrinsicCodeGenerator(Backend, this, value);
                    }
                    else
                    {
                        // Emit value
                        this.GenerateCodeFor(value);
                    }
                }

                // Wire phi nodes
                if (phiBindings.TryGetBindings(block, out var bindings))
                {
                    // Assign all temporaries
                    foreach (var (phiValue, value) in bindings)
                    {
                        // Load the current phi target variable
                        var phiTargetVariable = Load(phiValue);

                        // Check for an intermediate phi value
                        if (bindings.IsIntermediate(phiValue))
                        {
                            if (!intermediatePhiVariables.TryGetValue(
                                phiValue,
                                out var intermediateVariable))
                            {
                                intermediateVariable = AllocateType(phiValue.Type);
                                intermediatePhiVariables.Add(
                                    phiValue,
                                    intermediateVariable);

                                // Move this phi value into a temporary variable for reuse
                                Declare(intermediateVariable);
                            }
                            Move(intermediateVariable, phiTargetVariable);
                        }

                        // Determine the source value from which we need to copy from
                        var sourceVariable = intermediatePhiVariables
                            .TryGetValue(value, out var tempVariable)
                            ? tempVariable
                            : Load(value);

                        // Move contents
                        Move(phiTargetVariable, sourceVariable);
                    }
                }

                // Build terminator
                this.GenerateCodeFor(block.Terminator.AsNotNull());
                Builder.AppendLine();
            }
        }

        #endregion
    }
}
