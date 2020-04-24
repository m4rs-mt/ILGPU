// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
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
                SeparateViewEntryPoint entryPoint)
            {
                Backend = backend;
                TypeGenerator = typeGenerator;
                EntryPoint = entryPoint;
                KernelTypeGenerator = new CLKernelTypeGenerator(
                    typeGenerator,
                    entryPoint);
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
            Variable HandleIntrinsicParameter(int parameterOffset, Parameter parameter);
        }

        /// <summary>
        /// Represents a specialized phi binding allocator.
        /// </summary>
        private readonly struct PhiBindingAllocator : IPhiBindingAllocator
        {
            private readonly Dictionary<BasicBlock, List<Variable>> phiMapping;

            /// <summary>
            /// Constructs a new phi binding allocator.
            /// </summary>
            /// <param name="parent">The parent code generator.</param>
            /// <param name="cfg">The CFG to use.</param>
            public PhiBindingAllocator(CLCodeGenerator parent, CFG cfg)
            {
                phiMapping = new Dictionary<BasicBlock, List<Variable>>(cfg.Count);
                Parent = parent;
                CFG = cfg;
                Dominators = Dominators.Create(cfg);
            }

            /// <summary>
            /// Returns the parent code generator.
            /// </summary>
            public CLCodeGenerator Parent { get; }

            /// <summary>
            /// Returns the underlying CFG.
            /// </summary>
            public CFG CFG { get; }

            /// <summary>
            /// Returns the referenced dominators.
            /// </summary>
            public Dominators Dominators { get; }

            /// <summary cref="IPhiBindingAllocator.Process(CFG.Node, Phis)"/>
            public void Process(CFG.Node node, Phis phis) { }

            /// <summary cref="IPhiBindingAllocator.Allocate(CFG.Node, PhiValue)"/>
            public void Allocate(CFG.Node node, PhiValue phiValue)
            {
                var variable = Parent.Allocate(phiValue);

                var targetNode = node;
                foreach (var argument in phiValue)
                {
                    targetNode = argument.BasicBlock == null
                        ? CFG.EntryNode
                        : Dominators.GetImmediateCommonDominator(
                            targetNode,
                            CFG[argument.BasicBlock]);

                    if (targetNode == CFG.EntryNode)
                        break;
                }

                if (!phiMapping.TryGetValue(targetNode.Block, out var phiVariables))
                {
                    phiVariables = new List<Variable>();
                    phiMapping.Add(targetNode.Block, phiVariables);
                }
                phiVariables.Add(variable);
            }

            /// <summary>
            /// Tries to get phi variables to declare in the given block.
            /// </summary>
            /// <param name="block">The block.</param>
            /// <param name="phisToDeclare">The variables to declare (if any).</param>
            /// <returns>True, if there are some phi variables to declare.</returns>
            public bool TryGetPhis(BasicBlock block, out List<Variable> phisToDeclare) =>
                phiMapping.TryGetValue(block, out phisToDeclare);
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
            return method.HasFlags(MethodFlags.External)
                ? handleName
                : handleName + "_" + method.Id;
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

        private int labelCounter = 0;
        private readonly Dictionary<BasicBlock, string> blockLookup =
            new Dictionary<BasicBlock, string>();
        private readonly string labelPrefix;

        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="allocas">All local allocas.</param>
        internal CLCodeGenerator(in GeneratorArgs args, Scope scope, Allocas allocas)
            : base(args.TypeGenerator)
        {
            Backend = args.Backend;
            Scope = scope;
            ImplementationProvider = Backend.IntrinsicProvider;
            Allocas = allocas;

            labelPrefix = "L_" + Method.Id.ToString();

            Builder = new StringBuilder();
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
        public Method Method => Scope.Method;

        /// <summary>
        /// Returns the current function scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns all local allocas.
        /// </summary>
        public Allocas Allocas { get; }

        /// <summary>
        /// Returns the current intrinsic provider for code-generation purposes.
        /// </summary>
        public IntrinsicImplementationProvider<CLIntrinsic.Handler>
            ImplementationProvider { get; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        public StringBuilder Builder { get; }

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
        public void Merge(StringBuilder builder) =>
            builder.Append(Builder.ToString());

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
            where TSetupLogic : IParametersSetupLogic
        {
            bool attachComma = false;
            int offset = 0;

            foreach (var param in Method.Parameters)
            {
                Variable variable;
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
        /// Setups local or shared allocations.
        /// </summary>
        /// <param name="allocas">The allocations to setup.</param>
        /// <param name="addressSpace">The source address space.local).</param>
        private void SetupAllocations(
            AllocaKindInformation allocas,
            MemoryAddressSpace addressSpace)
        {
            var addressSpacePrefix = CLInstructions.GetAddressSpacePrefix(addressSpace);
            foreach (var allocaInfo in allocas)
            {
                var allocationVariable = AllocateType(allocaInfo.ElementType);
                var allocaVariable = Allocate(allocaInfo.Alloca);

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

                // Since allocas are basically pointers in the IR we have to
                // 'convert' the local allocations into generic pointers
                using (var statement = BeginStatement(allocaVariable))
                {
                    statement.AppendOperation(CLInstructions.AddressOfOperation);
                    statement.Append(allocationVariable);
                    if (allocaInfo.IsArray)
                        statement.AppendIndexer("0");
                }
            }
            Builder.AppendLine();
        }

        /// <summary>
        /// Generates code for all basic blocks.
        /// </summary>
        protected void GenerateCodeInternal()
        {
            // Setup allocations
            SetupAllocations(Allocas.LocalAllocations, MemoryAddressSpace.Local);
            SetupAllocations(Allocas.SharedAllocations, MemoryAddressSpace.Shared);

            if (Allocas.DynamicSharedAllocations.Length > 0)
            {
                throw new NotSupportedException(
                    ErrorMessages.NotSupportedDynamicSharedMemoryAllocations);
            }

            // Build branch targets
            foreach (var block in Scope)
                blockLookup.Add(block, DeclareLabel());

            // Find all phi nodes, allocate target registers and setup internal mapping
            var cfg = Scope.CreateCFG();
            var bindingAllocator = new PhiBindingAllocator(this, cfg);
            var phiBindings = PhiBindings.Create(cfg, bindingAllocator);

            // Generate code
            foreach (var block in Scope)
            {
                // Mark block label
                MarkLabel(blockLookup[block]);

                // Declare phi variables (if any)
                if (bindingAllocator.TryGetPhis(block, out var phisToDeclare))
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
                    foreach (var (value, phiValue) in bindings)
                    {
                        var phiTargetRegister = Load(phiValue);
                        var sourceRegister = Load(value);

                        Move(phiTargetRegister, sourceRegister);
                    }
                }

                // Build terminator
                this.GenerateCodeFor(block.Terminator);
                Builder.AppendLine();
            }
        }

        #endregion
    }
}
