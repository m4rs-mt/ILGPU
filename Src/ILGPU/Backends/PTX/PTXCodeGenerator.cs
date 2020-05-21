// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CFG = ILGPU.IR.Analyses.CFG<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;
using CFGNode = ILGPU.IR.Analyses.CFG.Node<
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Generates PTX code out of IR values.
    /// </summary>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    public abstract partial class PTXCodeGenerator :
        PTXRegisterAllocator,
        IBackendCodeGenerator<StringBuilder>
    {
        #region Constants

        /// <summary>
        /// The supported PTX instruction sets (in descending order).
        /// </summary>
        [SuppressMessage(
            "Microsoft.Security",
            "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "The collection is immutable")]
        public static readonly IEnumerable<PTXInstructionSet> SupportedInstructionSets =
            ImmutableSortedSet.Create(
                Comparer<PTXInstructionSet>.Create((first, second) =>
                    second.CompareTo(first)),
                PTXInstructionSet.ISA_64,
                PTXInstructionSet.ISA_63,
                PTXInstructionSet.ISA_62,
                PTXInstructionSet.ISA_61,
                PTXInstructionSet.ISA_60);

        /// <summary>
        /// The name for the globally registered dynamic shared memory alloca (if any).
        /// </summary>
        protected const string DynamicSharedMemoryAllocationName = "__dyn_shared_alloca";

        #endregion

        #region Nested Types

        /// <summary>
        /// Generation arguments for code-generator construction.
        /// </summary>
        public readonly struct GeneratorArgs
        {
            internal GeneratorArgs(
                PTXBackend backend,
                EntryPoint entryPoint,
                PTXDebugInfoGenerator debugInfoGenerator,
                ContextFlags contextFlags)
            {
                Backend = backend;
                EntryPoint = entryPoint;
                DebugInfoGenerator = debugInfoGenerator;
                ContextFlags = contextFlags;
            }

            /// <summary>
            /// Returns the underlying backend.
            /// </summary>
            public PTXBackend Backend { get; }

            /// <summary>
            /// Returns the current backend.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns the current context flags.
            /// </summary>
            public ContextFlags ContextFlags { get; }

            /// <summary>
            /// Returns the debug-information code generator.
            /// </summary>
            public PTXDebugInfoGenerator DebugInfoGenerator { get; }
        }

        /// <summary>
        /// Represents a parameter that is mapped to PTX.
        /// </summary>
        protected internal readonly struct MappedParameter
        {
            #region Instance

            /// <summary>
            /// Constructs a new mapped parameter.
            /// </summary>
            /// <param name="register">The PTX register.</param>
            /// <param name="ptxName">The name of the parameter in PTX code.</param>
            /// <param name="parameter">The source parameter.</param>
            public MappedParameter(
                Register register,
                string ptxName,
                Parameter parameter)
            {
                Register = register;
                PTXName = ptxName;
                Parameter = parameter;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated PTX register.
            /// </summary>
            public Register Register { get; }

            /// <summary>
            /// Returns the name of the parameter in PTX code.
            /// </summary>
            public string PTXName { get; }

            /// <summary>
            /// Returns the source parameter.
            /// </summary>
            public Parameter Parameter { get; }

            #endregion
        }

        /// <summary>
        /// Represents a setup logic for function parameters.
        /// </summary>
        internal interface IParameterSetupLogic
        {
            /// <summary>
            /// Handles an intrinsic parameter and returns the
            /// associated allocated register (if any).
            /// </summary>
            /// <param name="parameterOffset">
            /// The current intrinsic parameter index.
            /// </param>
            /// <param name="parameter">The intrinsic parameter.</param>
            /// <returns>The allocated register (if any).</returns>
            Register HandleIntrinsicParameter(int parameterOffset, Parameter parameter);
        }

        /// <summary>
        /// Represents an empty parameter setup logic.
        /// </summary>
        protected readonly struct EmptyParameterSetupLogic : IParameterSetupLogic
        {
            /// <summary>
            /// Does not handle intrinsic parameters.
            /// </summary>
            public Register HandleIntrinsicParameter(
                int parameterOffset,
                Parameter parameter) =>
                null;
        }

        /// <summary>
        /// Represents a specialized phi binding allocator.
        /// </summary>
        private readonly struct PhiBindingAllocator : IPhiBindingAllocator<Forwards>
        {
            /// <summary>
            /// Constructs a new phi binding allocator.
            /// </summary>
            /// <param name="parent">The parent code generator.</param>
            public PhiBindingAllocator(PTXCodeGenerator parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Returns the parent code generator.
            /// </summary>
            public PTXCodeGenerator Parent { get; }

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            public void Process(CFGNode node, Phis phis) { }

            /// <summary>
            /// Allocates a new phi node in the parent code generator.
            /// </summary>
            public void Allocate(CFGNode node, PhiValue phiValue) =>
                Parent.Allocate(phiValue);
        }

        #endregion

        #region Static

        /// <summary>
        /// Maps basic types to basic PTX suffixes.
        /// </summary>
        private static readonly ImmutableArray<string> BasicSuffixes =
            ImmutableArray.Create(
                default, "pred",
                "b8", "b16", "b32", "b64",
                "f32", "f64");

        /// <summary>
        /// Maps basic types to constant-loading target basic types.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType>
            RegisterMovementTypeRemapping =
            ImmutableArray.Create(
                default, BasicValueType.Int1,
                BasicValueType.Int16, BasicValueType.Int16,
                BasicValueType.Int32, BasicValueType.Int64,
                BasicValueType.Float32, BasicValueType.Float64);

        /// <summary>
        /// Resolves the PTX suffix for the given basic value type.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The resolved type suffix.</returns>
        private static string GetBasicSuffix(BasicValueType basicValueType) =>
            BasicSuffixes[(int)basicValueType];

        /// <summary>
        /// Remaps the given basic type for register movement instructions.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The remapped type.</returns>
        private static BasicValueType ResolveRegisterMovementType(
            BasicValueType basicValueType) =>
            RegisterMovementTypeRemapping[(int)basicValueType];

        /// <summary>
        /// Returns a PTX compatible name for the given entity.
        /// </summary>
        /// <param name="name">The source name.</param>
        /// <param name="nodeId">The source node id.</param>
        /// <returns>The resolved PTX name.</returns>
        private static string GetCompatibleName(string name, NodeId nodeId)
        {
            var chars = name.ToCharArray();
            for (int i = 0, e = chars.Length; i < e; ++i)
            {
                ref var charValue = ref chars[i];
                if (!char.IsLetterOrDigit(charValue))
                    charValue = '_';
            }
            return new string(chars) + nodeId.ToString();
        }

        /// <summary>
        /// Returns the PTX function name for the given function.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The resolved PTX function name.</returns>
        protected static string GetMethodName(Method method)
        {
            var handleName = method.Handle.Name;
            return method.HasFlags(MethodFlags.External)
                ? handleName
                : GetCompatibleName(handleName + "_", method.Id);
        }

        /// <summary>
        /// Returns the PTX parameter name for the given parameter.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The resolved PTX parameter name.</returns>
        protected static string GetParameterName(Parameter parameter) =>
            GetCompatibleName("_" + parameter.Name + "_", parameter.Id);

        #endregion

        #region Instance

        private int labelCounter = 0;
        private readonly Dictionary<BasicBlock, string> blockLookup =
            new Dictionary<BasicBlock, string>();
        private readonly Dictionary<string, string> stringConstants =
            new Dictionary<string, string>();
        private readonly string labelPrefix;

        /// <summary>
        /// Constructs a new PTX generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">All local allocas.</param>
        internal PTXCodeGenerator(in GeneratorArgs args, Method method, Allocas allocas)
            : base(args.Backend)
        {
            Method = method;
            DebugInfoGenerator = args.DebugInfoGenerator.BeginScope();
            ImplementationProvider = Backend.IntrinsicProvider;
            Allocas = allocas;

            Architecture = args.Backend.Architecture;
            FastMath = args.ContextFlags.HasFlags(ContextFlags.FastMath);
            EnableAssertions = args.ContextFlags.HasFlags(ContextFlags.EnableAssertions);

            labelPrefix = "L_" + Method.Id.ToString();
            ReturnParamName = "retval_" + Method.Id;

            Builder = new StringBuilder();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated backend.
        /// </summary>
        public new PTXBackend Backend => base.Backend as PTXBackend;

        /// <summary>
        /// Returns the associated method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns all local allocas.
        /// </summary>
        public Allocas Allocas { get; }

        /// <summary>
        /// Returns the currently used PTX architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; }

        /// <summary>
        /// Returns the associated debug information generator.
        /// </summary>
        public PTXDebugInfoGeneratorScope DebugInfoGenerator { get; }

        /// <summary>
        /// Returns the current intrinsic provider for code-generation purposes.
        /// </summary>
        public IntrinsicImplementationProvider<PTXIntrinsic.Handler>
            ImplementationProvider { get; }

        /// <summary>
        /// Returns true if fast math is active.
        /// </summary>
        public bool FastMath { get; }

        /// <summary>
        /// Returns true if assertions are enabled.
        /// </summary>
        public bool EnableAssertions { get; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        public StringBuilder Builder { get; }

        /// <summary>
        /// Returns the name of the return parameter.
        /// </summary>
        protected string ReturnParamName { get; }

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
        public void GenerateConstants(StringBuilder builder) =>
            builder.Append(GenerateConstantDeclarations());

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
            Builder.AppendLine(":");
        }

        /// <summary>
        /// Emits complex phi-value moves.
        /// </summary>
        private readonly struct PhiMoveEmitter : IComplexCommandEmitter
        {
            /// <summary>
            /// Emits phi-based move instructions.
            /// </summary>
            public void Emit(
                CommandEmitter commandEmitter,
                PrimitiveRegister[] registers)
            {
                var primaryRegister = registers[0];

                commandEmitter.AppendRegisterMovementSuffix(
                    primaryRegister.BasicValueType);
                commandEmitter.AppendArgument(primaryRegister);
                commandEmitter.AppendArgument(registers[1]);
            }
        }

        /// <summary>
        /// Prepares the general code generation process.
        /// </summary>
        protected void PrepareCodeGeneration()
        {
            // Emit debug information
            DebugInfoGenerator.ResetLocation();
            DebugInfoGenerator.GenerateDebugInfo(Builder, Method);
        }

        /// <summary>
        /// Generates code for all basic blocks.
        /// </summary>
        /// <param name="registerOffset">The internal register offset.</param>
        protected void GenerateCodeInternal(int registerOffset)
        {
            var blocks = Method.Blocks;

            // Build branch targets
            foreach (var block in blocks)
                blockLookup.Add(block, DeclareLabel());

            // Find all phi nodes, allocate target registers and setup internal mapping
            var cfg = blocks.CreateCFG();
            var phiBindings = PhiBindings.Create(cfg, new PhiBindingAllocator(this));
            Builder.AppendLine();

            // Generate code
            foreach (var block in blocks)
            {
                // Emit debug information
                DebugInfoGenerator.GenerateDebugInfo(Builder, block);

                // Mark block label
                MarkLabel(blockLookup[block]);

                foreach (var value in block)
                {
                    // Emit debug information
                    DebugInfoGenerator.GenerateDebugInfo(Builder, value);

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

                DebugInfoGenerator.ResetLocation();

                // Wire phi nodes
                if (phiBindings.TryGetBindings(block, out var bindings))
                {
                    foreach (var (value, phiValue) in bindings)
                    {
                        var phiTargetRegister = Load(phiValue);
                        var sourceRegister = Load(value);

                        // Prepare move
                        EmitComplexCommand(
                            PTXInstructions.MoveOperation,
                            new PhiMoveEmitter(),
                            phiTargetRegister,
                            sourceRegister);
                    }
                }

                // Build terminator
                this.GenerateCodeFor(block.Terminator);
                Builder.AppendLine();
            }

            // Finish function and append register information
            Builder.AppendLine("}");
            Builder.Insert(registerOffset, GenerateRegisterInformation("\t"));
        }

        /// <summary>
        /// Setups local or shared allocations.
        /// </summary>
        /// <param name="allocas">The allocations to setup.</param>
        /// <param name="addressSpacePrefix">
        /// The source address-space prefix (like .local).
        /// </param>
        /// <param name="namePrefix">The name prefix.</param>
        /// <param name="result">The resulting list of allocations.</param>
        protected void SetupAllocations<TCollection>(
            AllocaKindInformation allocas,
            string addressSpacePrefix,
            string namePrefix,
            TCollection result)
            where TCollection : ICollection<(Alloca, string)>
        {
            var offset = 0;
            foreach (var allocaInfo in allocas)
            {
                Builder.Append('\t');
                Builder.Append(addressSpacePrefix);

                Builder.Append(".align ");
                Builder.Append(allocaInfo.ElementAlignment);
                Builder.Append(" .b8 ");

                var name = namePrefix + offset++;
                Builder.Append(name);
                Builder.Append('[');
                if (!allocaInfo.IsDynamicArray)
                    Builder.Append(allocaInfo.ArraySize * allocaInfo.ElementSize);
                Builder.AppendLine("];");

                result.Add((allocaInfo.Alloca, name));
            }
            Builder.AppendLine();
        }

        /// <summary>
        /// Setups local allocations.
        /// </summary>
        /// <returns>A collection of allocations.</returns>
        internal List<(Alloca, string)> SetupAllocations()
        {
            var result = new List<(Alloca, string)>();
            SetupAllocations(
                Allocas.LocalAllocations,
                ".local ", "__local_depot",
                result);
            SetupAllocations(
                Allocas.SharedAllocations,
                ".shared ", "__shared_alloca",
                result);

            // Register a common name for all dynamic shared memory allocations
            foreach (var alloca in Allocas.DynamicSharedAllocations)
                result.Add((alloca.Alloca, DynamicSharedMemoryAllocationName));
            return result;
        }

        /// <summary>
        /// Setups all method parameters.
        /// </summary>
        /// <typeparam name="TSetupLogic">The specific setup logic.</typeparam>
        /// <param name="targetBuilder">
        /// The target builder to append the information to.
        /// </param>
        /// <param name="logic">The current logic.</param>
        /// <param name="paramOffset">The intrinsic parameter offset.</param>
        /// <returns>A list of mapped parameters.</returns>
        internal List<MappedParameter> SetupParameters<TSetupLogic>(
            StringBuilder targetBuilder,
            ref TSetupLogic logic,
            int paramOffset)
            where TSetupLogic : IParameterSetupLogic
        {
            var parameters = new List<MappedParameter>(
                Method.NumParameters - paramOffset);
            bool attachComma = false;
            int offset = 0;

            foreach (var param in Method.Parameters)
            {
                Register register = null;
                if (offset < paramOffset)
                {
                    register = logic.HandleIntrinsicParameter(offset, param);
                    offset++;
                }
                else
                {
                    register = Allocate(param);
                }

                if (register == null)
                    continue;

                if (attachComma)
                {
                    targetBuilder.Append(',');
                    targetBuilder.AppendLine();
                }

                targetBuilder.Append('\t');
                var paramName = GetParameterName(param);
                AppendParamDeclaration(targetBuilder, param.Type, paramName);

                parameters.Add(new MappedParameter(
                    register,
                    paramName,
                    param));

                attachComma = true;
            }

            return parameters;
        }

        /// <summary>
        /// Emits complex load parameter instructions.
        /// </summary>
        private readonly struct LoadParamEmitter : IComplexCommandEmitterWithOffsets
        {
            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private readonly struct IOEmitter : IIOEmitter<int>
            {
                public IOEmitter(string paramName)
                {
                    ParamName = paramName;
                }

                /// <summary>
                /// Returns the associated parameter name.
                /// </summary>
                public string ParamName { get; }

                public void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister primitiveRegister,
                    int offset)
                {
                    using var commandEmitter = codeGenerator.BeginCommand(command);
                    commandEmitter.AppendSuffix(primitiveRegister.BasicValueType);
                    commandEmitter.AppendArgument(primitiveRegister);
                    commandEmitter.AppendRawValue(ParamName, offset);
                }
            }

            public LoadParamEmitter(string paramName)
            {
                Emitter = new IOEmitter(paramName);
            }

            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private IOEmitter Emitter { get; }

            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister primitiveRegister,
                int offset) =>
                codeGenerator.EmitIOLoad(
                    Emitter,
                    command,
                    primitiveRegister as HardwareRegister,
                    offset);
        }

        /// <summary>
        /// Emits a new set of load param instructions with the
        /// appropriate configuration.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The source register.</param>
        protected void EmitLoadParam(string paramName, Register register) =>
            EmitComplexCommandWithOffsets(
                PTXInstructions.LoadParamOperation,
                new LoadParamEmitter(paramName),
                register,
                0);

        /// <summary>
        /// Emits complex store parameter instructions.
        /// </summary>
        private readonly struct StoreParamEmitter : IComplexCommandEmitterWithOffsets
        {
            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private readonly struct IOEmitter : IIOEmitter<int>
            {
                public IOEmitter(string paramName)
                {
                    ParamName = paramName;
                }

                /// <summary>
                /// Returns the associated parameter name.
                /// </summary>
                public string ParamName { get; }

                public void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister primitiveRegister,
                    int offset)
                {
                    using var commandEmitter = codeGenerator.BeginCommand(command);
                    commandEmitter.AppendSuffix(primitiveRegister.BasicValueType);
                    commandEmitter.AppendRawValue(ParamName, offset);
                    commandEmitter.AppendArgument(primitiveRegister);
                }
            }

            public StoreParamEmitter(string paramName)
            {
                Emitter = new IOEmitter(paramName);
            }

            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private IOEmitter Emitter { get; }

            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset) =>
                codeGenerator.EmitIOStore(
                    Emitter,
                    command,
                    register,
                    offset);
        }

        /// <summary>
        /// Emits a new set of store param instructions with the
        /// appropriate configuration.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The target register.</param>
        protected void EmitStoreParam(string paramName, Register register) =>
            EmitComplexCommandWithOffsets(
                PTXInstructions.StoreParamOperation,
                new StoreParamEmitter(paramName),
                register,
                0);

        /// <summary>
        /// Binds the given mapped parameters.
        /// </summary>
        /// <param name="parameters">A list with mapped parameters.</param>
        internal void BindParameters(List<MappedParameter> parameters)
        {
            foreach (var mappedParameter in parameters)
                EmitLoadParam(mappedParameter.PTXName, mappedParameter.Register);
        }

        /// <summary>
        /// Binds the given list of allocations.
        /// </summary>
        /// <param name="allocations">
        /// A list associating alloca nodes with their local names.
        /// </param>
        internal void BindAllocations(List<(Alloca, string)> allocations)
        {
            foreach (var allocaEntry in allocations)
            {
                var description = ResolveRegisterDescription(
                    Backend.PointerBasicValueType);
                var targetRegister = Allocate(allocaEntry.Item1, description);
                using var command = BeginMove();
                command.AppendSuffix(description.BasicValueType);
                command.AppendArgument(targetRegister);
                command.AppendRawValueReference(allocaEntry.Item2);
            }
        }

        /// <summary>
        /// Generate global constant declarations.
        /// </summary>
        /// <returns>The declared global constants in PTX format.</returns>
        private string GenerateConstantDeclarations()
        {
            var declBuilder = new StringBuilder();
            foreach (var stringConstant in stringConstants)
            {
                declBuilder.Append(".global .align 2 .b8 ");
                declBuilder.Append(stringConstant.Value);
                var stringBytes = Encoding.Unicode.GetBytes(stringConstant.Key);
                declBuilder.Append("[");
                declBuilder.Append(stringBytes.Length + 1);
                declBuilder.Append("]");
                declBuilder.Append(" = {");
                foreach (var value in stringBytes)
                {
                    declBuilder.Append(value);
                    declBuilder.Append(", ");
                }
                declBuilder.AppendLine("0};");
            }
            return declBuilder.ToString();
        }

        /// <summary>
        /// Appends parameter information.
        /// </summary>
        /// <param name="targetBuilder">
        /// The target builder to append the information to.
        /// </param>
        /// <param name="paramType">The param type.</param>
        /// <param name="paramName">The name of the param argument.</param>
        protected void AppendParamDeclaration(
            StringBuilder targetBuilder,
            TypeNode paramType,
            string paramName)
        {
            targetBuilder.Append(".param .");
            switch (paramType)
            {
                case PrimitiveType _:
                case StringType _:
                case PointerType _:
                    var registerDescription =
                        ResolveParameterRegisterDescription(paramType);
                    targetBuilder.Append(
                        GetBasicSuffix(registerDescription.BasicValueType));
                    targetBuilder.Append(' ');
                    targetBuilder.Append(paramName);
                    break;
                default:
                    targetBuilder.Append("align ");
                    targetBuilder.Append(paramType.Alignment);
                    targetBuilder.Append(" .b8 ");
                    targetBuilder.Append(paramName);
                    targetBuilder.Append('[');
                    targetBuilder.Append(paramType.Size);
                    targetBuilder.Append(']');
                    break;
            }
        }

        #endregion
    }
}
