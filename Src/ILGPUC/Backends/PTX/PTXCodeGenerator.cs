// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using ILGPUC.Backends.EntryPoints;
using ILGPUC.Backends.PTX.Analyses;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ILGPUC.Backends.PTX;

/// <summary>
/// Generates PTX code out of IR values.
/// </summary>
/// <remarks>The code needs to be prepared for this code generator.</remarks>
abstract partial class PTXCodeGenerator :
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
            [.. CudaDriverVersionUtils.InstructionSetLookup
                .Keys
                .Where(x => x >= CudaInstructionSet.ISA_60)]);

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
    /// <param name="Capabilities">Target accelerator capabilities.</param>
    /// <param name="KernelContext">The current kernel context.</param>
    /// <param name="EntryPoint">The main entry point.</param>
    /// <param name="BackendMode">Current backend mode.</param>
    /// <param name="Allocations">Allocation information for the kernel program.</param>
    /// <param name="DebugInfoGenerator">The debug info generator.</param>
    /// <param name="PointerAlignments">Pointer alignment information.</param>
    /// <param name="Uniforms">Uniform values.</param>
    internal sealed record GeneratorArgs(
        CudaCapabilityContext Capabilities,
        IRContext KernelContext,
        EntryPoint EntryPoint,
        PTXBackendMode BackendMode,
        Backend.Allocations Allocations,
        PTXDebugInfoGenerator DebugInfoGenerator,
        PointerAlignments.AlignmentInfo PointerAlignments,
        Uniforms.Info Uniforms)
    {
        /// <summary>
        /// Returns current compilation properties.
        /// </summary>
        public CompilationProperties Properties => KernelContext.Properties;
    }

    /// <summary>
    /// Represents a parameter that is mapped to PTX.
    /// </summary>
    /// <param name="Register">The PTX register.</param>
    /// <param name="PTXName">The name of the parameter in PTX code.</param>
    /// <param name="Parameter">The source parameter.</param>
    internal readonly record struct MappedParameter(
        Register Register,
        string PTXName,
        Parameter Parameter);

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
        Register? HandleIntrinsicParameter(int parameterOffset, Parameter parameter);
    }

    /// <summary>
    /// Represents an empty parameter setup logic.
    /// </summary>
    internal readonly struct EmptyParameterSetupLogic : IParameterSetupLogic
    {
        /// <summary>
        /// Does not handle intrinsic parameters.
        /// </summary>
        public Register? HandleIntrinsicParameter(
            int parameterOffset,
            Parameter parameter) =>
            null;
    }

    #endregion

    #region Static

    /// <summary>
    /// Maps basic types to basic PTX suffixes.
    /// </summary>
    private static readonly ImmutableArray<string> BasicSuffixes =
        ImmutableArray.Create(
            string.Empty, "pred",
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
    /// Returns the PTX function name for the given function.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The resolved PTX function name.</returns>
    protected static string GetMethodName(Method method)
    {
        var handleName = method.Handle.Name;
        return method.HasFlags(MethodFlags.External)
            ? handleName
            : method.Id.GetCompatibleName(handleName);
    }

    /// <summary>
    /// Returns the PTX parameter name for the given parameter.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The resolved PTX parameter name.</returns>
    protected static string GetParameterName(Parameter parameter) =>
        parameter.Id.GetCompatibleName($"_{parameter.Name}");

    #endregion

    #region Instance

    private int labelCounter;
    private readonly Dictionary<BasicBlock, string> _blockLookup = new(
        new BasicBlock.Comparer());

    private readonly Dictionary<(Encoding, string), string> _stringConstants = [];
    private readonly PhiBindings _phiBindings;
    private readonly Dictionary<Value, Register> _intermediatePhiRegisters;
    private readonly string _labelPrefix;

    /// <summary>
    /// Constructs a new PTX generator.
    /// </summary>
    /// <param name="args">The generator arguments.</param>
    /// <param name="method">The current method.</param>
    public PTXCodeGenerator(GeneratorArgs args, Method method)
        : base(args.KernelContext)
    {
        Capabilities = args.Capabilities;
        Method = method;
        DebugInfoGenerator = args.DebugInfoGenerator.BeginScope();
        Allocas = args.Allocations[method];
        Uniforms = args.Uniforms;

        FastMath = args.Properties.MathMode >= MathMode.Fast;

        _labelPrefix = "L_" + Method.Id.ToString();
        ReturnParamName = "retval_" + Method.Id;

        Builder = new StringBuilder();
        PointerAlignments = args.PointerAlignments;

        // Use the defined PTX backend block schedule to avoid unnecessary branches
        Schedule =
            args.BackendMode == PTXBackendMode.Enhanced
            ? Method.Blocks.CreateOptimizedPTXSchedule()
            : Method.Blocks.CreateDefaultPTXSchedule();

        // Create phi bindings and initialize temporary phi registers
        _phiBindings = Schedule.ComputePhiBindings(
            (_, phiValue) => Allocate(phiValue));
        _intermediatePhiRegisters = new Dictionary<Value, Register>(
            _phiBindings.MaxNumIntermediatePhis);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns targeted capabilities.
    /// </summary>
    public CudaCapabilityContext Capabilities { get; }

    /// <summary>
    /// Returns the associated method.
    /// </summary>
    public Method Method { get; }

    /// <summary>
    /// Returns all local allocas.
    /// </summary>
    public Allocas Allocas { get; }

    /// <summary>
    /// Returns the associated debug information generator.
    /// </summary>
    public PTXDebugInfoGeneratorScope DebugInfoGenerator { get; }

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
    internal PTXBlockSchedule Schedule { get; }

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
    private string DeclareLabel() => _labelPrefix + labelCounter++;

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
                _blockLookup.Add(block, DeclareLabel());
        }

        Builder.AppendLine();

        // Generate code
        foreach (var block in Schedule)
        {
            // Emit debug information
            DebugInfoGenerator.GenerateDebugInfo(Builder, block);

            // Mark block label
            if (_blockLookup.TryGetValue(block, out var blockLabel))
                MarkLabel(blockLabel);

            foreach (var value in block)
            {
                // Emit debug information
                DebugInfoGenerator.GenerateDebugInfo(Builder, value);

                // Generator code
                GenerateCodeFor(value);
            }

            DebugInfoGenerator.ResetLocation();

            // Build terminator
            GenerateCodeFor(block.Terminator.AsNotNull());
            Builder.AppendLine();

            // Free temporary registers
            foreach (var register in _intermediatePhiRegisters.Values)
                Free(register);
            _intermediatePhiRegisters.Clear();
        }

        // Finish function and append register information
        Builder.AppendLine("}");
        Builder.Insert(registerOffset, GenerateRegisterInformation("\t"));
    }

    /// <summary>
    /// Binds all phi values of the current block flowing through an edge to the
    /// target block.
    /// </summary>
    private void BindPhis(
        PhiBindings.PhiBindingCollection bindings,
        BasicBlock? target)
    {
        // Assign all phi values
        foreach (var (phiValue, value) in bindings)
        {
            // Reject phis not flowing to the target edge
            if (target is not null && phiValue.BasicBlock != target)
                continue;

            // Load the current phi target register
            var phiTargetRegister = Load(phiValue);

            // Check for an intermediate phi value
            if (bindings.IsIntermediate(phiValue))
            {
                var intermediateRegister = AllocateType(phiValue.Type);
                _intermediatePhiRegisters.Add(phiValue, intermediateRegister);

                // Move this phi value into a temporary register for reuse
                EmitComplexCommand(
                    PTXInstructions.MoveOperation,
                    new PhiMoveEmitter(),
                    intermediateRegister,
                    phiTargetRegister);
            }

            // Determine the source value from which we need to copy from
            var sourceRegister = _intermediatePhiRegisters
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
            Register? register = null;
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
    private readonly struct LoadParamEmitter(
        string paramName,
        HardwareRegister tempRegister) : IComplexCommandEmitterWithOffsets
    {
        /// <summary>
        /// The underlying IO emitter.
        /// </summary>
        private readonly struct IOEmitter(
            string paramName,
            HardwareRegister tempRegister) : IIOEmitter<int>
        {
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
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister primitiveRegister,
                int offset)
            {
                // Load into the temporary register in the case of a pointer type
                if (primitiveRegister.Type is AddressSpaceType addressSpaceType &&
                    addressSpaceType.AddressSpace != MemoryAddressSpace.Generic)
                {
                    using (var commandEmitter = BeginEmitLoad(
                        codeGenerator,
                        command,
                        tempRegister))
                    {
                        commandEmitter.AppendRawValue(paramName, offset);
                    }

                    // Convert the source value into the specialized address space
                    // using the previously allocated temp register
                    codeGenerator.CreateAddressSpaceCast(
                        tempRegister,
                        primitiveRegister.AsNotNullCast<HardwareRegister>(),
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
                    commandEmitter.AppendRawValue(paramName, offset);
                }
            }
        }

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
                new IOEmitter(paramName, tempRegister),
                command,
                primitiveRegister.AsNotNullCast<HardwareRegister>(),
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
    private readonly struct StoreParamEmitter(
        string paramName,
        HardwareRegister tempRegister) : IComplexCommandEmitterWithOffsets
    {
        /// <summary>
        /// The underlying IO emitter.
        /// </summary>
        private readonly struct IOEmitter(
            string paramName,
            HardwareRegister tempRegister) : IIOEmitter<int>
        {
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
                    primitiveRegister.AssertNotNull(tempRegister);

                    // Convert the source value into the generic address space
                    // using the previously allocated temp register
                    codeGenerator.CreateAddressSpaceCast(
                        primitiveRegister,
                        tempRegister,
                        addressSpaceType.AddressSpace,
                        MemoryAddressSpace.Generic);
                    primitiveRegister = tempRegister;
                }

                // Store the actual (possibly converted) parameter value
                using var commandEmitter = codeGenerator.BeginCommand(command);
                commandEmitter.AppendSuffix(
                    ResolveParameterBasicValueType(
                        primitiveRegister.BasicValueType));
                commandEmitter.AppendRawValue(paramName, offset);
                commandEmitter.AppendArgument(primitiveRegister);
            }
        }

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
                new IOEmitter(paramName, tempRegister),
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
        foreach (var stringConstant in _stringConstants)
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
