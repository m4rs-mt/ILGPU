﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Generates PTX code out of IR values.
    /// </summary>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    partial class PTXCodeGenerator : PTXRegisterAllocator, IValueVisitor
    {
        #region Constants

        /// <summary>
        /// The supported PTX version.
        /// </summary>
        public const string PTXVersion = "6.4";

        #endregion

        #region Nested Types

        /// <summary>
        /// Generation arguments for code-generator construction.
        /// </summary>
        public readonly ref struct GeneratorArgs
        {
            public GeneratorArgs(
                EntryPoint entryPoint,
                PTXDebugInfoGenerator debugInfoGenerator,
                StringBuilder stringBuilder,
                PTXArchitecture architecture,
                ABI abi,
                ContextFlags contextFlags)
            {
                EntryPoint = entryPoint;
                DebugInfoGenerator = debugInfoGenerator;
                StringBuilder = stringBuilder;
                Architecture = architecture;
                ABI = abi;
                ContextFlags = contextFlags;
            }

            public EntryPoint EntryPoint { get; }

            public StringBuilder StringBuilder { get; }

            public PTXArchitecture Architecture { get; }

            public ABI ABI { get; }

            public ContextFlags ContextFlags { get; }

            public PTXDebugInfoGenerator DebugInfoGenerator { get; }
        }

        /// <summary>
        /// Represents a parameter that is mapped to PTX.
        /// </summary>
        protected readonly struct MappedParameter
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
        protected interface IParameterSetupLogic
        {
            /// <summary>
            /// Handles an intrinsic parameter and returns the
            /// associated allocated register (if any).
            /// </summary>
            /// <param name="parameterOffset">The current intrinsic parameter index.</param>
            /// <param name="parameter">The intrinsic parameter.</param>
            /// <returns>The allocated register (if any).</returns>
            Register HandleIntrinsicParameter(int parameterOffset, Parameter parameter);
        }

        /// <summary>
        /// Represents an empty parameter setup logic.
        /// </summary>
        protected readonly struct EmptyParameterSetupLogic : IParameterSetupLogic
        {
            /// <summary cref="IParameterSetupLogic.HandleIntrinsicParameter(int, Parameter)"/>
            public Register HandleIntrinsicParameter(int parameterOffset, Parameter parameter) =>
                null;
        }

        #endregion

        #region Static

        /// <summary>
        /// Maps basic types to basic PTX suffixes.
        /// </summary>
        private static readonly ImmutableArray<string> BasicSuffixes = ImmutableArray.Create(
            default, "pred",
            "b8", "b16", "b32", "b64",
            "f32", "f64");

        /// <summary>
        /// Maps basic types to constant-loading target basic types.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType> RegisterMovementTypeRemapping = ImmutableArray.Create(
            default, BasicValueType.Int1,
            BasicValueType.Int16, BasicValueType.Int16, BasicValueType.Int32, BasicValueType.Int64,
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
        private static BasicValueType ResolveRegisterMovementType(BasicValueType basicValueType) =>
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
            if (method.HasFlags(MethodFlags.External))
                return handleName;
            return GetCompatibleName(handleName + "_", method.Id);
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
        protected readonly string returnParamName;

        /// <summary>
        /// Constructs a new PTX generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="scope">The current scope.</param>
        protected PTXCodeGenerator(in GeneratorArgs args, Scope scope)
            : base(args.ABI)
        {
            Builder = args.StringBuilder;
            Scope = scope;
            DebugInfoGenerator = args.DebugInfoGenerator;

            Architecture = args.Architecture;
            FastMath = args.ContextFlags.HasFlags(ContextFlags.FastMath);
            EnableAssertions = args.ContextFlags.HasFlags(ContextFlags.EnableAssertions);

            labelPrefix = "L_" + Method.Id.ToString();
            returnParamName = "retval_" + Method.Id;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated top-level function.
        /// </summary>
        public Method Method => Scope.Method;

        /// <summary>
        /// Returns the current function scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the currently used PTX architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; }

        /// <summary>
        /// Returns the associated debug information generator.
        /// </summary>
        public PTXDebugInfoGenerator DebugInfoGenerator { get; }

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
            /// <summary cref="IComplexCommandEmitter.Emit(CommandEmitter, RegisterAllocator{PTXRegisterKind}.PrimitiveRegister[])"/>
            public void Emit(CommandEmitter commandEmitter, PrimitiveRegister[] registers)
            {
                var primaryRegister = registers[0];

                commandEmitter.AppendRegisterMovementSuffix(primaryRegister.BasicValueType);
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
            DebugInfoGenerator.ResetSequencePoints();
            DebugInfoGenerator.GenerateDebugInfo(Builder, Method);
        }

        /// <summary>
        /// Generates code for all basic blocks.
        /// </summary>
        /// <param name="registerOffset">
        /// The offset in chars to append general register information.
        /// </param>
        /// <param name="constantOffset">
        /// The constant offset (in chars) to append global constant information.
        /// </param>
        protected void GenerateCode(
            int registerOffset,
            ref int constantOffset)
        {
            // Build branch targets
            foreach (var block in Scope)
                blockLookup.Add(block, DeclareLabel());

            // Find all phi nodes, allocate target registers and prepare
            // register mapping for all arguments
            var cfg = Scope.CreateCFG();
            var phiMapping = new Dictionary<BasicBlock, List<(Value, PhiValue)>>();
            foreach (var block in Scope.PostOrder)
            {
                // Gather phis in this block and allocate registers
                var cfgNode = cfg[block];
                var phis = Phis.Create(block);
                foreach (var phi in phis)
                {
                    Allocate(phi);

                    // Map all phi arguments
                    Debug.Assert(cfgNode.NumPredecessors == phi.Nodes.Length, "Invalid phi value");
                    for (int i = 0, e = cfgNode.NumPredecessors; i < e; ++i)
                    {
                        BasicBlock argumentBlock = cfgNode.Predecessors[i].Block;
                        if (!phiMapping.TryGetValue(argumentBlock, out List<(Value, PhiValue)> arguments))
                        {
                            arguments = new List<(Value, PhiValue)>();
                            phiMapping.Add(argumentBlock, arguments);
                        }
                        arguments.Add((phi[i], phi));
                    }
                }
            }
            Builder.AppendLine();

            // Generate code
            foreach (var block in Scope)
            {
                // Emit debug information
                DebugInfoGenerator.GenerateDebugInfo(Builder, block);

                // Mark block label
                MarkLabel(blockLookup[block]);

                foreach (var value in block)
                {
                    // Emit debug information
                    DebugInfoGenerator.GenerateDebugInfo(Builder, value);

                    // Emit value
                    value.Accept(this);
                }

                DebugInfoGenerator.ResetSequencePoints();

                // Wire phi nodes
                if (phiMapping.TryGetValue(block, out List<(Value, PhiValue)> phiArguments))
                {
                    foreach (var (value, phiValue) in phiArguments)
                    {
                        var phiTargetRegister = Load(phiValue);
                        var sourceRegister = Load(value);

                        // Prepare move
                        EmitComplexCommand(
                            Instructions.MoveOperation,
                            new PhiMoveEmitter(),
                            phiTargetRegister,
                            sourceRegister);
                    }
                }

                // Build terminator
                block.Terminator.Accept(this);
                Builder.AppendLine();
            }

            // Finish kernel and append register information
            Builder.AppendLine("}");

            var registerInfo = GenerateRegisterInformation("\t");
            Builder.Insert(registerOffset, registerInfo);
            var constantDeclarations = GenerateConstantDeclarations();
            Builder.Insert(constantOffset, constantDeclarations);
            constantOffset += constantDeclarations.Length;
        }

        /// <summary>
        /// Setups local allocations.
        /// </summary>
        /// <param name="allocas">The allocations to setup.</param>
        /// <returns>A list of pairs associating alloca nodes with thei local variable names.</returns>
        protected List<(Alloca, string)> SetupLocalAllocations(Allocas allocas)
        {
            var result = new List<(Alloca, string)>();

            var offset = 0;
            foreach (var allocaInfo in allocas.LocalAllocations)
            {
                Builder.Append('\t');
                Builder.Append(".local ");
                var elementType = allocaInfo.ElementType;
                ABI.GetAlignmentAndSizeOf(
                    elementType,
                    out int elementSize,
                    out int elementAlignment);

                Builder.Append(".align ");
                Builder.Append(elementAlignment);
                Builder.Append(" .b8 ");

                var name = "__local_depot" + offset++;
                Builder.Append(name);
                Builder.Append('[');
                Builder.Append(allocaInfo.ArraySize * elementSize);
                Builder.AppendLine("];");

                result.Add((allocaInfo.Alloca, name));
            }
            Builder.AppendLine();

            return result;
        }

        /// <summary>
        /// Setups all method parameters.
        /// </summary>
        /// <typeparam name="TSetupLogic">The specific setup logic.</typeparam>
        /// <param name="logic">The current logic.</param>
        /// <param name="paramOffset">The intrinsic parameter offset.</param>
        /// <returns>A list of mapped parameters.</returns>
        protected List<MappedParameter> SetupParameters<TSetupLogic>(ref TSetupLogic logic, int paramOffset)
            where TSetupLogic : IParameterSetupLogic
        {
            var parameters = new List<MappedParameter>(Method.NumParameters - paramOffset);
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
                    register = Allocate(param);

                if (register == null)
                    continue;

                if (attachComma)
                {
                    Builder.Append(',');
                    Builder.AppendLine();
                }

                Builder.Append('\t');
                var paramName = GetParameterName(param);
                AppendParamDeclaration(param.Type, paramName);

                parameters.Add(new MappedParameter(
                    register,
                    paramName,
                    param));

                attachComma = true;
            }

            return parameters;
        }

        /// <summary>
        /// Emits complex load params instructions.
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

                /// <summary cref="IIOEmitter{T}.Emit(PTXCodeGenerator, string, RegisterAllocator{PTXRegisterKind}.PrimitiveRegister, T)"/>
                public void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister register,
                    int offset)
                {
                    using (var commandEmitter = codeGenerator.BeginCommand(command))
                    {
                        commandEmitter.AppendSuffix(register.BasicValueType);
                        commandEmitter.AppendArgument(register);
                        commandEmitter.AppendRawValue(ParamName, offset);
                    }
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

            /// <summary cref="IComplexCommandEmitterWithOffsets.Emit(PTXCodeGenerator, string, RegisterAllocator{PTXRegisterKind}.PrimitiveRegister, int)"/>
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset) =>
                codeGenerator.EmitIOLoad(Emitter, command, register, offset);
        }

        /// <summary>
        /// Emits a new set of load param instructions with the
        /// appropriate configuration.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The source register.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EmitLoadParam(string paramName, Register register)
        {
            EmitComplexCommandWithOffsets(
                Instructions.LoadParamOperation,
                new LoadParamEmitter(paramName),
                register,
                0);
        }

        /// <summary>
        /// Emits complex store params instructions.
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

                /// <summary cref="IIOEmitter{T}.Emit(PTXCodeGenerator, string, RegisterAllocator{PTXRegisterKind}.PrimitiveRegister, T)"/>
                public void Emit(
                    PTXCodeGenerator codeGenerator,
                    string command,
                    PrimitiveRegister register,
                    int offset)
                {
                    using (var commandEmitter = codeGenerator.BeginCommand(command))
                    {
                        commandEmitter.AppendSuffix(register.BasicValueType);
                        commandEmitter.AppendRawValue(ParamName, offset);
                        commandEmitter.AppendArgument(register);
                    }
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

            /// <summary cref="IComplexCommandEmitterWithOffsets.Emit(PTXCodeGenerator, string, RegisterAllocator{PTXRegisterKind}.PrimitiveRegister, int)"/>
            public void Emit(
                PTXCodeGenerator codeGenerator,
                string command,
                PrimitiveRegister register,
                int offset) =>
                codeGenerator.EmitIOStore(Emitter, command, register, offset);
        }

        /// <summary>
        /// Emits a new set of store param instructions with the
        /// appropriate configuration.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="register">The target register.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EmitStoreParam(string paramName, Register register)
        {
            EmitComplexCommandWithOffsets(
                Instructions.StoreParamOperation,
                new StoreParamEmitter(paramName),
                register,
                0);
        }

        /// <summary>
        /// Binds the given mapped parameters.
        /// </summary>
        /// <param name="parameters">A list with mapped parameters.</param>
        protected void BindParameters(List<MappedParameter> parameters)
        {
            foreach (var mappedParameter in parameters)
                EmitLoadParam(mappedParameter.PTXName, mappedParameter.Register);
        }

        /// <summary>
        /// Binds the given list of allocations.
        /// </summary>
        /// <param name="allocations">A list associating alloca nodes with thei local names.</param>
        protected void BindAllocations(List<(Alloca, string)> allocations)
        {
            foreach (var allocaEntry in allocations)
            {
                var description = ResolveRegisterDescription(ABI.PointerBasicValueType);
                var targetRegister = Allocate(allocaEntry.Item1, description);
                using (var command = BeginMove())
                {
                    command.AppendSuffix(description.BasicValueType);
                    command.AppendArgument(targetRegister);
                    command.AppendRawValueReference(allocaEntry.Item2);
                }
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
        /// <param name="paramType">The param type.</param>
        /// <param name="paramName">The name of the param argument.</param>
        protected void AppendParamDeclaration(TypeNode paramType, string paramName)
        {
            Builder.Append(".param .");
            switch (paramType)
            {
                case PrimitiveType _:
                case StringType _:
                case PointerType _:
                    var registerDescription = ResolveParameterRegisterDescription(paramType);
                    Builder.Append(GetBasicSuffix(registerDescription.BasicValueType));
                    Builder.Append(' ');
                    Builder.Append(paramName);
                    break;
                default:
                    ABI.GetAlignmentAndSizeOf(
                        paramType,
                        out int paramSize,
                        out int paramAlignment);
                    Builder.Append("align ");
                    Builder.Append(paramAlignment);
                    Builder.Append(" .b8 ");
                    Builder.Append(paramName);
                    Builder.Append('[');
                    Builder.Append(paramSize);
                    Builder.Append(']');
                    break;
            }
        }

        #endregion
    }
}
