// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Frontend
{
    /// <summary>
    /// Represents an IR code generator for .Net methods.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed partial class CodeGenerator : ICodeGenerationContext
    {
        #region Instance

        private readonly Block.CFGBuilder cfgBuilder;
        private readonly HashSet<VariableRef> variables = new HashSet<VariableRef>();
        private readonly Dictionary<VariableRef, (TypeNode, ConvertFlags)> variableTypes =
            new Dictionary<VariableRef, (TypeNode, ConvertFlags)>();

        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="frontend">The current frontend instance.</param>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <param name="disassembledMethod">The corresponding disassembled method.</param>
        /// <param name="detectedMethods">The set of newly detected methods.</param>
        public CodeGenerator(
            ILFrontend frontend,
            Method.Builder methodBuilder,
            DisassembledMethod disassembledMethod,
            HashSet<MethodBase> detectedMethods)
        {
            Frontend = frontend;
            DisassembledMethod = disassembledMethod;
            DetectedMethods = detectedMethods;

            cfgBuilder = new Block.CFGBuilder(this, methodBuilder);
            EntryBlock = cfgBuilder.EntryBlock;

            SSABuilder = SSABuilder<VariableRef>.Create(
                methodBuilder,
                cfgBuilder.CFG);

            // Setup the initial sequence point of the first instruction
            methodBuilder.SetupInitialSequencePoint(
                disassembledMethod.FirstSequencePoint);
            SetupVariables();
        }

        /// <summary>
        /// Setups all parameter and local bindings.
        /// </summary>
        private void SetupVariables()
        {
            var builder = EntryBlock.Builder;

            // Check for SSA variables
            for (int i = 0, e = DisassembledMethod.Count; i < e; ++i)
            {
                var instruction = DisassembledMethod[i];
                switch (instruction.InstructionType)
                {
                    case ILInstructionType.Ldarga:
                        variables.Add(new VariableRef(
                            instruction.GetArgumentAs<int>(), VariableRefType.Argument));
                        break;
                    case ILInstructionType.Ldloca:
                        variables.Add(new VariableRef(
                            instruction.GetArgumentAs<int>(), VariableRefType.Local));
                        break;
                }
            }

            // Init params
            if (!Method.IsStatic)
            {
                var declaringType = builder.CreateType(Method.DeclaringType);
                declaringType = builder.CreatePointerType(
                    declaringType,
                    MemoryAddressSpace.Generic);
                EntryBlock.SetValue(
                    new VariableRef(0, VariableRefType.Argument),
                    Builder.InsertParameter(declaringType, "this"));
            }

            var methodParameters = Method.GetParameters();
            var parameterOffset = Method.GetParameterOffset();
            for (int i = 0, e = methodParameters.Length; i < e; ++i)
            {
                var parameter = methodParameters[i];
                var paramType = builder.CreateType(parameter.ParameterType);
                Value ssaValue = Builder.AddParameter(paramType, parameter.Name);
                var argRef = new VariableRef(i + parameterOffset, VariableRefType.Argument);
                if (variables.Contains(argRef))
                {
                    // Address was taken... emit a temporary alloca and store the arg value to it
                    var alloca = CreateTempAlloca(paramType);
                    builder.CreateStore(alloca, ssaValue);
                    ssaValue = alloca;
                }
                EntryBlock.SetValue(argRef, ssaValue);
                variableTypes[argRef] = (paramType, parameter.ParameterType.ToTargetUnsignedFlags());
            }

            // Init locals
            var localVariables = Method.GetMethodBody().LocalVariables;
            for (int i = 0, e = localVariables.Count; i < e; ++i)
            {
                var variable = localVariables[i];
                var variableType = builder.CreateType(variable.LocalType);
                var localRef = new VariableRef(i, VariableRefType.Local);
                Value initValue = builder.CreateNull(variableType);
                if (variables.Contains(localRef))
                {
                    // Address was taken... emit a temporary alloca and store empty value to it
                    var alloca = CreateTempAlloca(variableType);
                    builder.CreateStore(alloca, initValue);
                    initValue = alloca;
                }

                EntryBlock.SetValue(localRef, initValue);
                variableTypes[localRef] = (variableType, variable.LocalType.ToTargetUnsignedFlags());
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the set of detected methods.
        /// </summary>
        private HashSet<MethodBase> DetectedMethods { get; }

        /// <summary>
        /// Returns the associated frontend.
        /// </summary>
        public ILFrontend Frontend { get; }

        /// <summary>
        /// Returns the current IR context.
        /// </summary>
        public IRContext Context => Builder.Context;

        /// <summary>
        /// Returns the current method builder.
        /// </summary>
        public Method.Builder Builder => SSABuilder.MethodBuilder;

        /// <summary>
        /// Returns the current disassembled method.
        /// </summary>
        public DisassembledMethod DisassembledMethod { get; }

        /// <summary>
        /// Returns the current mananged method.
        /// </summary>
        public MethodBase Method => DisassembledMethod.Method;

        /// <summary>
        /// Returns the current SSA builder.
        /// </summary>
        public SSABuilder<VariableRef> SSABuilder { get; }

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public Block EntryBlock { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="methodBase">The method to declare.</param>
        /// <returns>The declared method.</returns>
        public Method DeclareMethod(MethodBase methodBase)
        {
            Debug.Assert(methodBase != null, "Invalid function to declare");
            var result = Builder.DeclareMethod(methodBase, out bool created);
            if (created)
                DetectedMethods.Add(methodBase);
            return result;
        }

        /// <summary>
        /// Creates a temporary alloca for the given type.
        /// </summary>
        /// <param name="type">The type to allocate.</param>
        /// <returns>The created alloca.</returns>
        public ValueReference CreateTempAlloca(TypeNode type) =>
            EntryBlock.Builder.CreateAlloca(type, MemoryAddressSpace.Local);

        /// <summary>
        /// Generates code for the current function.
        /// </summary>
        /// <returns>The created top-level function.</returns>
        public Method GenerateCode()
        {
            // Iterate over all blocks in reverse postorder
            foreach (BasicBlock basicBlock in cfgBuilder.Scope)
            {
                var block = cfgBuilder[basicBlock];
                GenerateCodeForBlock(block);
            }

            return Builder.Method;
        }

        /// <summary>
        /// Generates code for the given block.
        /// </summary>
        /// <param name="block">The current block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GenerateCodeForBlock(Block block)
        {
            if (!SSABuilder.ProcessAndSeal(block.CFGNode))
                return;

            var blockBuilder = block.Builder;
            blockBuilder.SetupSequencePoint(
                DisassembledMethod[block.InstructionOffset].SequencePoint);

            for (int i = block.InstructionOffset, e = block.InstructionOffset + block.InstructionCount;
                i < e; ++i)
            {
                var instruction = DisassembledMethod[i];

                // Setup debug information
                Builder.SequencePoint = instruction.SequencePoint;

                // Try to generate code for this instruction
                if (!TryGenerateCode(block, blockBuilder, instruction))
                    throw this.GetNotSupportedException(
                        ErrorMessages.NotSupportedInstruction, Method.Name, instruction);
            }

            // Handle implicit branches to successor blocks
            if (blockBuilder.Terminator is BuilderTerminator builderTerminator)
            {
                Debug.Assert(
                    builderTerminator.NumTargets == 1,
                    "Implicit branches can have one successor only");
                blockBuilder.CreateUnconditionalBranch(builderTerminator.Targets[0]);
            }
        }

        #endregion

        #region Verification

        /// <summary cref="ICodeGenerationContext.GetException{TException}(string, object[])"/>
        public TException GetException<TException>(
            string message,
            params object[] args)
            where TException : Exception
        {
            var builder = new StringBuilder();
            builder.AppendFormat(message, args);
            var currentMethod = Method;
            if (currentMethod != null)
            {
                builder.AppendLine();
                builder.Append("Current method: ");
                builder.Append(Method.DeclaringType.Name);
                builder.Append('.');
                builder.Append(Method.Name);
            }
            var instance = Activator.CreateInstance(
                typeof(TException),
                builder.ToString()) as TException;
            return instance;
        }

        /// <summary>
        /// Verifies that the given method is not a .Net-runtime-dependent method.
        /// If it depends on the runtime, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="method">The method to verify.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void VerifyNotRuntimeMethod(MethodBase method)
        {
            Debug.Assert(method != null, "Invalid method");
            var @namespace = method.DeclaringType.FullName;
            // Internal unsafe intrinsic methods
            if (@namespace == "System.Runtime.CompilerServices.Unsafe")
                return;
            if (@namespace.StartsWith("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                @namespace.StartsWith("System.Reflection", StringComparison.OrdinalIgnoreCase))
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedRuntimeMethod, method.Name);
        }

        /// <summary>
        /// Verifies a static-field load operation.
        /// </summary>
        /// <param name="field">The static field to load.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void VerifyStaticFieldLoad(FieldInfo field)
        {
            Debug.Assert(field != null || !field.IsStatic, "Invalid field");

            if ((field.Attributes & FieldAttributes.InitOnly) != FieldAttributes.InitOnly &&
                !Context.HasFlags(ContextFlags.InlineMutableStaticFieldValues))
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedLoadOfStaticField, field);
        }

        /// <summary>
        /// Verifies a static-field store operation.
        /// </summary>
        /// <param name="field">The static field to store to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void VerifyStaticFieldStore(FieldInfo field)
        {
            Debug.Assert(field != null || !field.IsStatic, "Invalid field");

            if (!Context.HasFlags(ContextFlags.IgnoreStaticFieldStores))
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedStoreToStaticField, field);
        }

        #endregion

        #region Code Generation

        /// <summary>
        /// Realizes a nop instruction.
        /// </summary>
        private static void MakeNop() { }

        /// <summary>
        /// Realizes a trap instruction.
        /// </summary>
        private static void MakeTrap() { }

        /// <summary>
        /// Realizes an indirect load instruction.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="address">The source address.</param>
        /// <param name="type">The target type.</param>
        /// <param name="flags">The conversion flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Value CreateLoad(
            IRBuilder builder,
            Value address,
            TypeNode type,
            ConvertFlags flags)
        {
            if (type == null || !address.Type.IsPointerType)
                throw this.GetInvalidILCodeException();

            address = CreateConversion(
                builder,
                address,
                builder.CreatePointerType(
                    type,
                    MemoryAddressSpace.Generic),
                ConvertFlags.None);
            var load = builder.CreateLoad(address);

            // Extent small basic types
            switch (type.BasicValueType)
            {
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                    load = CreateConversion(
                        builder,
                        load,
                        builder.GetPrimitiveType(BasicValueType.Int32),
                        flags.ToSourceUnsignedFlags());
                    break;
                default:
                    break;
            }
            return load;
        }

        /// <summary>
        /// Realizes an indirect store instruction.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="address">The target address.</param>
        /// <param name="value">The value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateStore(
            IRBuilder builder,
            Value address,
            Value value)
        {
            if (!address.Type.IsPointerType)
                throw this.GetInvalidILCodeException();

            address = CreateConversion(
                builder,
                address,
                builder.CreatePointerType(
                    value.Type,
                    MemoryAddressSpace.Generic),
                ConvertFlags.None);
            builder.CreateStore(address, value);
        }

        /// <summary>
        /// Realizes a dup operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private static void MakeDup(Block block)
        {
            block.Dup();
        }

        /// <summary>
        /// Realizes a pop operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private static void MakePop(Block block)
        {
            block.Pop();
        }

        #endregion
    }
}
