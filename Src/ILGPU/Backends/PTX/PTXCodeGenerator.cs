// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX.Analyses;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime.Cuda;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

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
        public static readonly IImmutableSet<CudaInstructionSet>
            SupportedInstructionSets = ImmutableSortedSet.Create(
                Comparer<CudaInstructionSet>.Create((first, second) =>
                    second.CompareTo(first)),
                CudaDriverVersionUtils.InstructionSetLookup
                    .Keys
                    .Where(x => x >= CudaInstructionSet.ISA_60)
                    .ToArray());

        /// <summary>
        /// The name for the globally registered dynamic shared memory alloca (if any).
        /// </summary>
        protected const string DynamicSharedMemoryAllocationName = "__dyn_shared_alloca";

        /// <summary>
        /// The maximum vector size in bytes (128 bits in PTX).
        /// </summary>
        private const int MaxVectorSizeInBytes = 128 / 8;

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
                ContextProperties contextProperties,
                PTXDebugInfoGenerator debugInfoGenerator,
                PointerAlignments.AlignmentInfo pointerAlignments,
                Uniforms.Info uniforms)
            {
                Backend = backend;
                EntryPoint = entryPoint;
                Properties = contextProperties;
                DebugInfoGenerator = debugInfoGenerator;
                PointerAlignments = pointerAlignments;
                Uniforms = uniforms;
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
            /// Returns the current context properties.
            /// </summary>
            public ContextProperties Properties { get; }

            /// <summary>
            /// Returns the debug-information code generator.
            /// </summary>
            public PTXDebugInfoGenerator DebugInfoGenerator { get; }

            /// <summary>
            /// Returns detailed information about all pointer alignments.
            /// </summary>
            public PointerAlignments.AlignmentInfo PointerAlignments { get; }

            /// <summary>
            /// Returns detailed information about uniform values, terminators in
            /// particular.
            /// </summary>
            public Uniforms.Info Uniforms { get; }
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
        private readonly struct PhiBindingAllocator : IPhiBindingAllocator
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
            public void Process(BasicBlock block, Phis phis) { }

            /// <summary>
            /// Allocates a new phi node in the parent code generator.
            /// </summary>
            public void Allocate(BasicBlock block, PhiValue phiValue) =>
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
                "f16", "f32", "f64");

        /// <summary>
        /// Maps basic types to constant-loading target basic types.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType>
            RegisterMovementTypeRemapping =
            ImmutableArray.Create(
                default, BasicValueType.Int1,
                BasicValueType.Int16, BasicValueType.Int16,
                BasicValueType.Int32, BasicValueType.Int64,
                BasicValueType.Int16, BasicValueType.Float32, BasicValueType.Float64);

        /// <summary>
        /// Maps basic types to constant-loading target basic types.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType>
            RegisterIOTypeRemapping =
            ImmutableArray.Create(
                default, BasicValueType.Int8,
                BasicValueType.Int8, BasicValueType.Int16,
                BasicValueType.Int32, BasicValueType.Int64,
                BasicValueType.Int16, BasicValueType.Float32, BasicValueType.Float64);

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
        /// Remaps the given basic type for global IO movement instructions.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The remapped type.</returns>
        private static BasicValueType ResolveIOType(
            BasicValueType basicValueType) =>
            RegisterIOTypeRemapping[(int)basicValueType];

        /// <summary>
        /// Returns a PTX compatible name for the given entity.
        /// </summary>
        /// <param name="name">The source name.</param>
        /// <param name="nodeId">The source node id.</param>
        /// <returns>The resolved PTX name.</returns>
        private static string GetCompatibleName(string name, NodeId nodeId) =>
            KernelNameAttribute.GetCompatibleName(name) + nodeId.ToString();

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

        private int labelCounter;
        private readonly Dictionary<BasicBlock, string> blockLookup =
            new Dictionary<BasicBlock, string>();
        private readonly Dictionary<(Encoding, string), string> stringConstants =
            new Dictionary<(Encoding, string), string>();
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
            Uniforms = args.Uniforms;

            Architecture = args.Backend.Architecture;
            FastMath = args.Properties.MathMode >= MathMode.Fast;

            labelPrefix = "L_" + Method.Id.ToString();
            ReturnParamName = "retval_" + Method.Id;

            Builder = new StringBuilder();
            PointerAlignments = args.PointerAlignments;

            // Use the defined PTX backend block schedule to avoid unnecessary branches
            Schedule =
                args.Properties.GetPTXBackendMode() == PTXBackendMode.Enhanced
                ? Method.Blocks.CreateOptimizedPTXSchedule()
                : Method.Blocks.CreateDefaultPTXSchedule();
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
        public CudaArchitecture Architecture { get; }

        /// <summary>
        /// Returns the associated debug information generator.
        /// </summary>
        public PTXDebugInfoGeneratorScope DebugInfoGenerator { get; }

        /// <summary>
        /// Returns the current intrinsic provider for code-generation purposes.
        /// </summary>
        public IntrinsicImplementationProvider<PTXIntrinsic.Handler>
            ImplementationProvider
        { get; }

        /// <summary>
        /// Returns true if fast math is active.
        /// </summary>
        public bool FastMath { get; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        public StringBuilder Builder { get; }

        /// <summary>
        /// Returns the name of the return parameter.
        /// </summary>
        protected string ReturnParamName { get; }

        /// <summary>
        /// Returns detailed information about all pointer alignments.
        /// </summary>
        public PointerAlignments.AlignmentInfo PointerAlignments { get; }

        /// <summary>
        /// Returns information about whether a branch is a uniform control-flow branch.
        /// </summary>
        public Uniforms.Info Uniforms { get; }

        /// <summary>
        /// Returns all blocks in an appropriate schedule.
        /// </summary>
        public PTXBlockSchedule Schedule { get; }

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
            Builder.AppendLine(":");
        }

        /// <summary>
        /// Emits complex phi-value moves.
        /// </summary>
        private readonly struct PhiMoveEmitter : IComplexCommandEmitter
        {
            /// <summary>
            /// Returns the same command.
            /// </summary>
            public string AdjustCommand(string command, PrimitiveRegister[] registers) =>
                command;

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
            // Build branch targets
            foreach (var block in Schedule)
            {
                // Detect whether we should emit an explicit label
                if (Schedule.NeedBranchTarget(block))
                    blockLookup.Add(block, DeclareLabel());
            }

            // Find all phi nodes, allocate target registers and setup internal mapping
            var phiBindings = Schedule.ComputePhiBindings(
                new PhiBindingAllocator(this));
            var intermediatePhiRegisters = new Dictionary<Value, Register>(
                phiBindings.MaxNumIntermediatePhis);
            Builder.AppendLine();

            // Generate code
            foreach (var block in Schedule)
            {
                // Emit debug information
                DebugInfoGenerator.GenerateDebugInfo(Builder, block);

                // Mark block label
                if (blockLookup.TryGetValue(block, out var blockLabel))
                    MarkLabel(blockLabel);

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
                    // Assign all phi values
                    foreach (var (phiValue, value) in bindings)
                    {
                        // Load the current phi target register
                        var phiTargetRegister = Load(phiValue);

                        // Check for an intermediate phi value
                        if (bindings.IsIntermediate(phiValue))
                        {
                            var intermediateRegister = AllocateType(phiValue.Type);
                            intermediatePhiRegisters.Add(phiValue, intermediateRegister);

                            // Move this phi value into a temporary register for reuse
                            EmitComplexCommand(
                                PTXInstructions.MoveOperation,
                                new PhiMoveEmitter(),
                                intermediateRegister,
                                phiTargetRegister);
                        }

                        // Determine the source value from which we need to copy from
                        var sourceRegister = intermediatePhiRegisters
                            .TryGetValue(value, out var tempRegister)
                            ? tempRegister
                            : Load(value);

                        // Move contents
                        EmitComplexCommand(
                            PTXInstructions.MoveOperation,
                            new PhiMoveEmitter(),
                            phiTargetRegister,
                            sourceRegister);
                    }

                    // Free temporary registers
                    foreach (var register in intermediatePhiRegisters.Values)
                        Free(register);
                    intermediatePhiRegisters.Clear();
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
                Builder.Append(PointerAlignments.GetAllocaAlignment(allocaInfo.Alloca));
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
            where TSetupLogic : struct, IParameterSetupLogic
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
                public IOEmitter(string paramName, HardwareRegister tempRegister)
                {
                    ParamName = paramName;
                    TempRegister = tempRegister;
                }

                /// <summary>
                /// Returns the associated parameter name.
                /// </summary>
                public string ParamName { get; }

                /// <summary>
                /// Returns the associated temp register.
                /// </summary>
                public HardwareRegister TempRegister { get; }

                private static CommandEmitter BeginEmitLoad(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister sourceRegister)
                {
                    var commandEmitter = codeGenerator.BeginCommand(command);
                    commandEmitter.AppendSuffix(
                        ResolveParameterBasicValueType(
                            sourceRegister.BasicValueType));
                    commandEmitter.AppendArgument(sourceRegister);
                    return commandEmitter;
                }

                /// <summary>
                /// Emits a new parameter load operation that converts generic address-
                /// space pointers into a specialized address space.
                /// </summary>
                public readonly void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister primitiveRegister,
                    int offset)
                {
                    // Load into the temporary register in the case of a pointer type
                    if (primitiveRegister.Type is AddressSpaceType addressSpaceType &&
                        addressSpaceType.AddressSpace != MemoryAddressSpace.Generic)
                    {
                        primitiveRegister.AssertNotNull(TempRegister);

                        using (var commandEmitter = BeginEmitLoad(
                            codeGenerator,
                            command,
                            TempRegister))
                        {
                            commandEmitter.AppendRawValue(ParamName, offset);
                        }

                        // Convert the source value into the specialized address space
                        // using the previously allocated temp register
                        codeGenerator.CreateAddressSpaceCast(
                            TempRegister,
                            primitiveRegister as HardwareRegister,
                            MemoryAddressSpace.Generic,
                            addressSpaceType.AddressSpace);
                    }
                    else
                    {
                        // Load the parameter value directly into the target register
                        using var commandEmitter = BeginEmitLoad(
                            codeGenerator,
                            command,
                            primitiveRegister);
                        commandEmitter.AppendRawValue(ParamName, offset);
                    }
                }
            }

            public LoadParamEmitter(string paramName, HardwareRegister tempRegister)
            {
                Emitter = new IOEmitter(paramName, tempRegister);
            }

            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private IOEmitter Emitter { get; }

            /// <summary>
            /// Emits a new parameter load operation that converts generic address-
            /// space pointers into a specialized address space.
            /// </summary>
            public readonly void Emit(
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
        /// Emits a new set of load param instructions with the appropriate configuration
        /// that converts pointers from the generic address space into specialized
        /// target address-spaces.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The source register.</param>
        protected void EmitLoadParam(string paramName, Register register)
        {
            // Allocate a temporary pointer register to cast the address spaces of
            // pointer arguments
            var tempRegister = AllocatePlatformRegister(out var _);

            EmitLoadParam(paramName, register, tempRegister);

            // Free the previously allocated temp register
            FreeRegister(tempRegister);
        }

        /// <summary>
        /// Emits a new set of load param instructions with the appropriate configuration
        /// that converts pointers from the generic address space into specialized
        /// target address-spaces.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The source register.</param>
        /// <param name="tempRegister">
        /// A temporary hardware register to perform address-space casts based on the
        /// PTX-specific calling convention.
        /// </param>
        protected void EmitLoadParam(
            string paramName,
            Register register,
            HardwareRegister tempRegister) =>
            EmitComplexCommandWithOffsets(
                PTXInstructions.LoadParamOperation,
                new LoadParamEmitter(paramName, tempRegister),
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
                public IOEmitter(string paramName, HardwareRegister tempRegister)
                {
                    ParamName = paramName;
                    TempRegister = tempRegister;
                }

                /// <summary>
                /// Returns the associated parameter name.
                /// </summary>
                public string ParamName { get; }

                /// <summary>
                /// Returns the associated temp register.
                /// </summary>
                public HardwareRegister TempRegister { get; }

                /// <summary>
                /// Emits a new parameter store operation that converts non-generic
                /// address-space pointers into the generic address space.
                /// </summary>
                public readonly void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister primitiveRegister,
                    int offset)
                {
                    // Check for a pointer type stored in this register
                    if (primitiveRegister.Type is AddressSpaceType addressSpaceType &&
                        addressSpaceType.AddressSpace != MemoryAddressSpace.Generic)
                    {
                        primitiveRegister.AssertNotNull(TempRegister);

                        // Convert the source value into the generic address space
                        // using the previously allocated temp register
                        codeGenerator.CreateAddressSpaceCast(
                            primitiveRegister,
                            TempRegister,
                            addressSpaceType.AddressSpace,
                            MemoryAddressSpace.Generic);
                        primitiveRegister = TempRegister;
                    }

                    // Store the actual (possibly converted) parameter value
                    using var commandEmitter = codeGenerator.BeginCommand(command);
                    commandEmitter.AppendSuffix(
                        ResolveParameterBasicValueType(
                            primitiveRegister.BasicValueType));
                    commandEmitter.AppendRawValue(ParamName, offset);
                    commandEmitter.AppendArgument(primitiveRegister);
                }
            }

            public StoreParamEmitter(
                string paramName,
                HardwareRegister tempRegister)
            {
                Emitter = new IOEmitter(paramName, tempRegister);
            }

            /// <summary>
            /// The underlying IO emitter.
            /// </summary>
            private IOEmitter Emitter { get; }

            /// <summary>
            /// Emits a new parameter store operation that converts non-generic
            /// address-space pointers into the generic address space.
            /// </summary>
            public readonly void Emit(
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
        /// Emits a new set of store param instructions with the appropriate
        /// configuration that converts pointers to the generic address space before
        /// passing them to the target function being called.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The target register.</param>
        protected void EmitStoreParam(string paramName, Register register)
        {
            // Allocate a temporary pointer register to cast the address spaces of
            // pointer arguments
            var tempRegister = AllocatePlatformRegister(out var _);

            EmitComplexCommandWithOffsets(
                PTXInstructions.StoreParamOperation,
                new StoreParamEmitter(paramName, tempRegister),
                register,
                0);

            // Free the previously allocated temp register
            FreeRegister(tempRegister);
        }

        /// <summary>
        /// Binds the given mapped parameters.
        /// </summary>
        /// <param name="parameters">A list with mapped parameters.</param>
        internal void BindParameters(List<MappedParameter> parameters)
        {
            // Allocate a temporary register for all conversion operations
            var tempRegister = AllocatePlatformRegister(out var _);

            foreach (var mappedParameter in parameters)
            {
                EmitLoadParam(
                    mappedParameter.PTXName,
                    mappedParameter.Register,
                    tempRegister);
            }

            FreeRegister(tempRegister);
        }

        /// <summary>
        /// Binds the given list of allocations.
        /// </summary>
        /// <param name="allocations">
        /// A list associating alloca nodes with their local names.
        /// </param>
        internal void BindAllocations(List<(Alloca, string)> allocations)
        {
            // Early exit for methods without any allocations
            if (allocations.Count < 1)
                return;

            foreach (var (alloca, valueReference) in allocations)
            {
                // Allocate a type-specific target register holding the pointer.
                // Note that this pointer will directly point to either the local or
                // the shared address space and does not need to be converted.
                var targetRegister = AllocateHardware(alloca);
                using var command = BeginMove();
                command.AppendSuffix(targetRegister.BasicValueType);
                command.AppendArgument(targetRegister);
                command.AppendRawValueReference(valueReference);
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
                var (encoding, stringValue) = stringConstant.Key;
                declBuilder.Append(".global .align ");
                declBuilder.Append(encoding.GetMaxByteCount(1));
                declBuilder.Append(" .b8 ");
                declBuilder.Append(stringConstant.Value);

                var stringBytes = encoding.GetBytes(stringValue);
                declBuilder.Append('[');
                declBuilder.Append(stringBytes.Length + 1);
                declBuilder.Append(']');
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
