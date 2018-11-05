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

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        #region Nested Types

        /// <summary>
        /// The function provider for CPS construction.
        /// </summary>
        private struct FunctionProvider : ICPSBuilderFunctionProvider<BasicBlock>
        {
            /// <summary cref="ICPSBuilderFunctionProvider{TNode}.GetFunctionBuilder(IRBuilder, TNode)"/>
            public FunctionBuilder GetFunctionBuilder(IRBuilder builder, BasicBlock childNode) =>
                childNode.FunctionBuilder;

            /// <summary cref="ICPSBuilderFunctionProvider{TNode}.ResolveFunctionCallArgument{TCallback}(IR.Values.FunctionValue, FunctionBuilder, FunctionCall, int, ref TCallback)"/>
            public void ResolveFunctionCallArgument<TCallback>(
                FunctionValue attachedFunction,
                FunctionBuilder targetBuilder,
                FunctionCall call,
                int argumentIdx,
                ref TCallback callback)
                where TCallback : ICPSBuilderFunctionCallArgumentCallback
            {
                var arg = call.GetArgument(argumentIdx);
                callback.AddArgument(arg);
            }
        }

        #endregion

        #region Instance

        private readonly FunctionProvider functionProvider = new FunctionProvider();
        private readonly Dictionary<int, BasicBlock> basicBlockMapping =
            new Dictionary<int, BasicBlock>();
        private readonly HashSet<VariableRef> variables = new HashSet<VariableRef>();
        private readonly Dictionary<VariableRef, (TypeNode, ConvertFlags)> variableTypes =
            new Dictionary<VariableRef, (TypeNode, ConvertFlags)>();

        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="frontend">The current frontend instance.</param>
        /// <param name="builder">The associated IR builder.</param>
        /// <param name="functionBuilder">The current function builder.</param>
        /// <param name="disassembledMethod">The corresponding disassembled method.</param>
        /// <param name="detectedMethods">The set of newly detected methods.</param>
        public CodeGenerator(
            ILFrontend frontend,
            IRBuilder builder,
            FunctionBuilder functionBuilder,
            DisassembledMethod disassembledMethod,
            HashSet<MethodBase> detectedMethods)
        {
            Frontend = frontend;
            Builder = builder;
            FunctionBuilder = functionBuilder;
            DisassembledMethod = disassembledMethod;
            DetectedMethods = detectedMethods;

            EntryBlock = new BasicBlock(this, FunctionBuilder, 0);
            basicBlockMapping.Add(0, EntryBlock);

            CPSBuilder = CPSBuilder<BasicBlock, BasicBlock.Enumerator, VariableRef>.Create(
                builder, EntryBlock, functionProvider);

            SetupVariables();
        }

        /// <summary>
        /// Setups all parameter and local bindings.
        /// </summary>
        private void SetupVariables()
        {
            EntryBlock.PushMemory(FunctionBuilder.MemoryParam);

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
                var declaringType = Builder.CreateType(Method.DeclaringType);
                declaringType = Builder.CreatePointerType(
                    declaringType,
                    MemoryAddressSpace.Generic);
                EntryBlock.SetValue(
                    new VariableRef(0, VariableRefType.Argument),
                    FunctionBuilder.AddParameter(declaringType));
            }
            var methodParameters = Method.GetParameters();
            var parameterOffset = Method.GetParameterOffset();
            for (int i = 0, e = methodParameters.Length; i < e; ++i)
            {
                var parameter = methodParameters[i];
                var paramType = Builder.CreateType(parameter.ParameterType);
                Value ssaValue = FunctionBuilder.AddParameter(paramType, parameter.Name);
                var argRef = new VariableRef(i + parameterOffset, VariableRefType.Argument);
                if (variables.Contains(argRef))
                {
                    // Address was taken... emit a temporary alloca and store the arg value to it
                    var alloca = CreateTempAlloca(EntryBlock, paramType);
                    var store = Builder.CreateStore(EntryBlock.PopMemory(), alloca, ssaValue);
                    EntryBlock.PushMemory(store);
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
                var variableType = Builder.CreateType(variable.LocalType);
                var localRef = new VariableRef(i, VariableRefType.Local);
                Value initValue = Builder.CreateNull(variableType);
                if (variables.Contains(localRef))
                {
                    // Address was taken... emit a temporary alloca and store empty value to it
                    var alloca = CreateTempAlloca(EntryBlock, variableType);
                    var store = Builder.CreateStore(
                        EntryBlock.PopMemory(),
                        alloca,
                        initValue);
                    EntryBlock.PushMemory(store);
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
        /// Returns the current IR builder.
        /// </summary>
        public IRBuilder Builder { get; }

        /// <summary>
        /// Returns the current function builder.
        /// </summary>
        public FunctionBuilder FunctionBuilder { get; }

        /// <summary>
        /// Returns the current function value.
        /// </summary>
        public TopLevelFunction FunctionValue => FunctionBuilder.FunctionValue as TopLevelFunction;

        /// <summary>
        /// Returns the current disassembled method.
        /// </summary>
        public DisassembledMethod DisassembledMethod { get; }

        /// <summary>
        /// Returns the current mananged method.
        /// </summary>
        public MethodBase Method => DisassembledMethod.Method;

        /// <summary>
        /// Returns the current CPS builder.
        /// </summary>
        public CPSBuilder<BasicBlock, BasicBlock.Enumerator, VariableRef> CPSBuilder { get; }

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Declares a new top-level function.
        /// </summary>
        /// <param name="methodBase">The method to declare.</param>
        /// <returns>The declared top-level function.</returns>
        public TopLevelFunction DeclareFunction(MethodBase methodBase)
        {
            Debug.Assert(methodBase != null, "Invalid function to declare");
            var result = Builder.DeclareFunction(methodBase);
            DetectedMethods.Add(methodBase);
            return result;
        }

        /// <summary>
        /// Creates a temporary alloca for the given type.
        /// </summary>
        /// <param name="block">The current basic block..</param>
        /// <param name="type">The type to allocate.</param>
        /// <returns>The created alloca.</returns>
        public ValueReference CreateTempAlloca(BasicBlock block, TypeNode type)
        {
            var memory = block.PopMemory();
            var alloca = Builder.CreateAlloca(memory, type, MemoryAddressSpace.Local);
            block.PushMemory(alloca);
            return alloca;
        }

        /// <summary>
        /// Appends a new basic block.
        /// </summary>
        /// <returns>The created basic block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BasicBlock AppendBasicBlock()
        {
            var blockIdx = CPSBuilder.Count;
            var block = new BasicBlock(
                this,
                Builder.CreateFunction("Block" + blockIdx),
                blockIdx);
            CPSBuilder.AppendBlock(block, functionProvider);

            return block;
        }

        /// <summary>
        /// Appends a basic block with the given target.
        /// </summary>
        /// <param name="target">The block target.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BasicBlock AppendBasicBlock(int target)
        {
            if (!basicBlockMapping.TryGetValue(target, out BasicBlock block))
            {
                block = AppendBasicBlock();
                basicBlockMapping.Add(target, block);
            }
            return block;
        }

        /// <summary>
        /// Build all required basic blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Dictionary<int, int> BuildBasicBlocks()
        {
            var result = new Dictionary<int, int>();
            for (int i = 0, e = DisassembledMethod.Count; i < e; ++i)
            {
                var instruction = DisassembledMethod[i];
                result[instruction.Offset] = i;
                if (!instruction.IsTerminator)
                    continue;
                if (instruction.Argument is ILInstructionBranchTargets targets)
                {
                    foreach (var target in targets.GetTargetOffsets())
                    {
                        if (basicBlockMapping.ContainsKey(target))
                            continue;
                        AppendBasicBlock(target);
                    }
                }
                else if (instruction.InstructionType != ILInstructionType.Ret)
                {
                    // We have to emit a jump-target CPS block
                    AppendBasicBlock(DisassembledMethod[i + 1].Offset);
                }
            }
            return result;
        }

        /// <summary>
        /// Setups all basic blocks (fills in the required information).
        /// </summary>
        /// <param name="offsetMapping">The offset mapping that maps il-byte offsets to indices.</param>
        /// <param name="current">The current block.</param>
        /// <param name="instructionIdx">The starting instruction index.</param>
        private void SetupBasicBlocks<TMapping>(
            TMapping offsetMapping,
            BasicBlock current,
            int instructionIdx)
            where TMapping : IReadOnlyDictionary<int, int>
        {
            if (!current.Visit(1))
                return;
            current.InstructionOffset = instructionIdx;
            var stackCounter = current.StackCounter;
            for (int e = DisassembledMethod.Count; instructionIdx < e; ++instructionIdx)
            {
                var instruction = DisassembledMethod[instructionIdx];
                // Handle implicit cases: jumps to blocks without a jump instruction
                if (basicBlockMapping.TryGetValue(instruction.Offset, out BasicBlock other) &&
                    current != other)
                {
                    // Wire current and new block
                    CPSBuilder.AddSuccessor(current, other);
                    other.StackCounter = stackCounter;
                    SetupBasicBlocks(offsetMapping, other, instructionIdx);
                    break;
                }
                else
                {
                    // Update the current block
                    stackCounter += (instruction.PushCount - instruction.PopCount);
                    current.InstructionCount += 1;

                    if (instruction.IsTerminator)
                    {
                        if (instruction.Argument is ILInstructionBranchTargets targets)
                        {
                            // Create appropriate temp targets
                            var targetOffsets = targets.GetTargetOffsets();
                            if (targetOffsets.Length > 1)
                            {
                                foreach (var target in targetOffsets)
                                {
                                    var tempTarget = AppendBasicBlock();
                                    CPSBuilder.AddSuccessor(current, tempTarget);
                                    SetupBasicBlock(offsetMapping, tempTarget, stackCounter, target);
                                }
                            }
                            else
                            {
                                SetupBasicBlock(offsetMapping, current, stackCounter, targetOffsets[0]);
                            }
                        }
                        else if (instruction.InstructionType != ILInstructionType.Ret)
                        {
                            // A call jumps to the next instruction
                            var target = DisassembledMethod[instructionIdx + 1].Offset;
                            var tempTarget = AppendBasicBlock();
                            CPSBuilder.AddSuccessor(current, tempTarget);
                            SetupBasicBlock(offsetMapping, tempTarget, stackCounter, target);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Setups a single basic block.
        /// </summary>
        /// <typeparam name="TMapping"></typeparam>
        /// <param name="offsetMapping"></param>
        /// <param name="current"></param>
        /// <param name="stackCounter"></param>
        /// <param name="target"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupBasicBlock<TMapping>(
            TMapping offsetMapping,
            BasicBlock current,
            int stackCounter,
            int target) where TMapping : IReadOnlyDictionary<int, int>
        {
            var targetBlock = basicBlockMapping[target];
            CPSBuilder.AddSuccessor(current, targetBlock);
            targetBlock.StackCounter = stackCounter;
            var targetIdx = offsetMapping[target];
            SetupBasicBlocks(offsetMapping, targetBlock, targetIdx);
        }

        /// <summary>
        /// Generates code for the current function.
        /// </summary>
        /// <returns>The created top-level function.</returns>
        public TopLevelFunction GenerateCode()
        {
            var offsetMapping = BuildBasicBlocks();
            SetupBasicBlocks(offsetMapping, EntryBlock, 0);

            var reversePostOrder = CPSBuilder.ComputeReversePostOrder();
            foreach (var block in reversePostOrder)
                GenerateCodeForBlock(block);

            // Generate code for the exit block
            GenerateCodeForExitBlock();

            CPSBuilder.Finish();

            return FunctionValue;
        }

        /// <summary>
        /// Generates code for the given block.
        /// </summary>
        /// <param name="block">The current block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GenerateCodeForBlock(BasicBlock block)
        {
            if (!CPSBuilder.ProcessAndSeal(block))
                return;

            for (int i = block.InstructionOffset, e = block.InstructionOffset + block.InstructionCount;
                i < e; ++i)
            {
                var instruction = DisassembledMethod[i];
                if (!TryGenerateCode(block, instruction))
                    throw this.GetNotSupportedException(
                        ErrorMessages.NotSupportedInstruction, Method.Name, instruction);
            }
        }

        /// <summary>
        /// Generates code for the final exit block.
        /// </summary>
        /// <returns>The generated exit block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BasicBlock GenerateCodeForExitBlock()
        {
            var exitFunction = Builder.CreateFunction("exit");
            var exitBlock = new BasicBlock(this, exitFunction, CPSBuilder.Count);
            CPSBuilder.LinkToExitBlock(exitBlock, functionProvider);

            CPSBuilder.ProcessAndSeal(exitBlock);
            var returnMemory = exitBlock.PopMemory();
            FunctionCall call;
            var returnType = FunctionValue.ReturnType;
            if (returnType.IsVoidType)
            {
                call = Builder.CreateFunctionCall(
                    FunctionBuilder.ReturnParam,
                    ImmutableArray.Create<ValueReference>(returnMemory));
            }
            else
            {
                exitBlock.StackCounter = 1;
                var returnValue = exitBlock.Pop(
                    returnType,
                    Method.GetReturnType().ToTargetUnsignedFlags());
                call = Builder.CreateFunctionCall(
                    FunctionBuilder.ReturnParam,
                    ImmutableArray.Create<ValueReference>(returnMemory, returnValue));
            }
            CPSBuilder.SetTerminator(exitBlock, call);

            return exitBlock;
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
                (Context.Flags & IRContextFlags.InlineMutableStaticFieldValues) !=
                IRContextFlags.InlineMutableStaticFieldValues)
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

            if ((Context.Flags & IRContextFlags.IgnoreStaticFieldStores) !=
                IRContextFlags.IgnoreStaticFieldStores)
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedStoreToStaticField, field);
        }

        #endregion

        #region Methods

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
        /// <param name="block">The current block.</param>
        /// <param name="address">The source address.</param>
        /// <param name="type">The target type.</param>
        /// <param name="flags">The conversion flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Value CreateLoad(
            BasicBlock block,
            Value address,
            TypeNode type,
            ConvertFlags flags)
        {
            if (type == null || !address.Type.IsPointerType)
                throw this.GetInvalidILCodeException();
            address = CreateConversion(
                address,
                Builder.CreatePointerType(
                    type,
                    MemoryAddressSpace.Generic),
                ConvertFlags.None);
            var load = Builder.CreateLoad(
                block.PopMemory(),
                address);
            block.PushMemory(load);
            // Extent small basic types
            switch (type.BasicValueType)
            {
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                    load = CreateConversion(
                        load,
                        Builder.CreatePrimitiveType(BasicValueType.Int32),
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
        /// <param name="block">The current block.</param>
        /// <param name="address">The target address.</param>
        /// <param name="value">The value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateStore(BasicBlock block, Value address, Value value)
        {
            if (!address.Type.IsPointerType)
                throw this.GetInvalidILCodeException();
            address = CreateConversion(
                address,
                Builder.CreatePointerType(
                    value.Type,
                    MemoryAddressSpace.Generic),
                ConvertFlags.None);
            var store = Builder.CreateStore(
                block.PopMemory(),
                address,
                value);
            block.PushMemory(store);
        }

        #endregion

        #region Constants

        /// <summary>
        /// Loads an int.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="value">The value.</param>
        private void Load(BasicBlock block, int value)
        {
            block.Push(Builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a long.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="value">The value.</param>
        private void Load(BasicBlock block, long value)
        {
            block.Push(Builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a float.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="value">The value.</param>
        private void Load(BasicBlock block, float value)
        {
            block.Push(Builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a double.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="value">The value.</param>
        private void Load(BasicBlock block, double value)
        {
            block.Push(Builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a string.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="value">The value.</param>
        private void LoadString(BasicBlock block, string value)
        {
            block.Push(Builder.CreatePrimitiveValue(value));
        }

        #endregion

        #region Arithmetic

        /// <summary>
        /// Realizes an arithmetic operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="kind">The kind of the arithmetic operation.</param>
        /// <param name="instruction">The current IL instruction.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MakeArithmetic(
            BasicBlock block,
            BinaryArithmeticKind kind,
            ILInstruction instruction)
        {
            var arithmeticFlags = ArithmeticFlags.None;
            var convertFlags = ConvertFlags.None;
            if (instruction.HasFlags(ILInstructionFlags.Overflow))
                arithmeticFlags |= ArithmeticFlags.Overflow;
            if (instruction.HasFlags(ILInstructionFlags.Unsigned))
            {
                convertFlags |= ConvertFlags.TargetUnsigned;
                arithmeticFlags |= ArithmeticFlags.Unsigned;
            }
            block.PopArithmeticArgs(convertFlags, out Value left, out Value right);
            switch (kind)
            {
                case BinaryArithmeticKind.Shl:
                case BinaryArithmeticKind.Shr:
                    // Convert right operand to 32bits
                    right = CreateConversion(right,
                        Builder.CreatePrimitiveType(BasicValueType.Int32),
                        convertFlags);
                    break;
            }
            var arithmetic = Builder.CreateArithmetic(left, right, kind, arithmeticFlags);
            block.Push(arithmetic);
        }

        /// <summary>
        /// Realizes an arithmetic operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="kind">The kind of the arithmetic operation.</param>
        private void MakeArithmetic(BasicBlock block, UnaryArithmeticKind kind)
        {
            var value = block.PopCompareOrArithmeticValue(ConvertFlags.None);
            var arithmetic = Builder.CreateArithmetic(value, kind);
            block.Push(arithmetic);
        }

        #endregion

        #region Compare

        /// <summary>
        /// Realizes a compare instruction of the given type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private void MakeCompare(
            BasicBlock block,
            CompareKind compareKind,
            ILInstructionFlags instructionFlags)
        {
            var value = CreateCompare(block, compareKind, instructionFlags);
            block.Push(value);
        }

        /// <summary>
        /// Creates a compare instruction of the given type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private Value CreateCompare(
            BasicBlock block,
            CompareKind compareKind,
            ILInstructionFlags instructionFlags)
        {
            var compareFlags = CompareFlags.None;
            if (instructionFlags.HasFlags(ILInstructionFlags.Unsigned))
                compareFlags |= CompareFlags.UnsignedOrUnordered;
            return CreateCompare(block, compareKind, compareFlags);
        }

        /// <summary>
        /// Creates a compare instruction of the given type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="flags">The comparison flags.</param>
        private Value CreateCompare(
            BasicBlock block,
            CompareKind compareKind,
            CompareFlags flags)
        {
            var right = block.PopCompareValue(ConvertFlags.None);
            var left = block.PopCompareValue(ConvertFlags.None);
            var convertFlags = ConvertFlags.None;
            if ((flags & CompareFlags.UnsignedOrUnordered) == CompareFlags.UnsignedOrUnordered)
                convertFlags = ConvertFlags.SourceUnsigned;
            right = CreateConversion(right, left.Type, convertFlags);
            left = CreateConversion(left, right.Type, convertFlags);
            Debug.Assert(left.BasicValueType == right.BasicValueType);
            return Builder.CreateCompare(left, right, compareKind, flags);
        }

        #endregion

        #region Convert

        /// <summary>
        /// Realizes a convert instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private void MakeConvert(
            BasicBlock block,
            Type targetType,
            ILInstructionFlags instructionFlags)
        {
            var value = block.Pop();
            var convertFlags = ConvertFlags.None;
            if (instructionFlags.HasFlags(ILInstructionFlags.Unsigned))
                convertFlags |= ConvertFlags.SourceUnsigned;
            if (instructionFlags.HasFlags(ILInstructionFlags.Overflow))
                convertFlags |= ConvertFlags.Overflow;
            if (targetType.IsUnsignedInt())
            {
                convertFlags |= ConvertFlags.SourceUnsigned;
                convertFlags |= ConvertFlags.TargetUnsigned;
            }
            var type = targetType.GetBasicValueType();
            var targetTypeNode = Builder.CreatePrimitiveType(type);
            block.Push(CreateConversion(value, targetTypeNode, convertFlags));
        }

        /// <summary>
        /// Conerts the given value to the target type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="flags">True, if the comparison should be forced to be unsigned.</param>
        public Value CreateConversion(Value value, TypeNode targetType, ConvertFlags flags)
        {
            if (value.Type is AddressSpaceType pointerType)
            {
                var otherType = targetType as AddressSpaceType;
                if (otherType.AddressSpace == pointerType.AddressSpace)
                    return Builder.CreatePointerCast(
                        value,
                        otherType.ElementType);
                else
                    return Builder.CreateAddressSpaceCast(
                        value,
                        otherType.AddressSpace);
            }
            return Builder.CreateConvert(value, targetType, flags);
        }

        #endregion

        #region Control Flow

        /// <summary>
        /// Realizes a return instruction.
        /// </summary>
        private static void MakeReturn() { }

        /// <summary>
        /// Realizes an uncoditional branch instruction.
        /// </summary>
        private static void MakeBranch() { }

        /// <summary>
        /// Realizes a conditional branch instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="compareKind">The comparison type of the condition.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private void MakeBranch(
            BasicBlock block,
            CompareKind compareKind,
            ILInstructionFlags instructionFlags)
        {
            var condition = CreateCompare(block, compareKind, instructionFlags);
            CPSBuilder.SetTerminator(block, condition);
        }

        /// <summary>
        /// Make a true branch.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private void MakeBranchTrue(BasicBlock block)
        {
            var comparisonValue = block.PopCompareValue(ConvertFlags.None);
            block.Push(Builder.CreatePrimitiveValue(comparisonValue.BasicValueType, 0));
            block.Push(comparisonValue);
            CPSBuilder.SetTerminator(block,
                CreateCompare(block, CompareKind.NotEqual, CompareFlags.None));
        }

        /// <summary>
        /// Make a false branch.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private void MakeBranchFalse(BasicBlock block)
        {
            var comparisonValue = block.PopCompareValue(ConvertFlags.None);
            block.Push(Builder.CreatePrimitiveValue(comparisonValue.BasicValueType, 0));
            block.Push(comparisonValue);
            CPSBuilder.SetTerminator(block,
                CreateCompare(block, CompareKind.Equal, CompareFlags.None));
        }

        /// <summary>
        /// Realizes a switch instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private void MakeSwitch(BasicBlock block)
        {
            var switchValue = block.PopInt(ConvertFlags.TargetUnsigned);
            CPSBuilder.SetTerminator(block, switchValue);
        }

        #endregion

        #region Variables

        /// <summary>
        /// Loads a variable. This can be an argument or a local reference.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="var">The variable reference.</param>
        private void LoadVariable(BasicBlock block, VariableRef var)
        {
            Debug.Assert(var.RefType == VariableRefType.Argument || var.RefType == VariableRefType.Local);
            var addressOrValue = block.GetValue(var);
            if (variables.Contains(var))
            {
                var type = variableTypes[var];
                block.Push(CreateLoad(
                    block,
                    addressOrValue,
                    type.Item1,
                    type.Item2));
            }
            else
                block.Push(addressOrValue);
        }

        /// <summary>
        /// Loads a variable address. This can be an argument or a local reference.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="var">The variable reference.</param>
        private void LoadVariableAddress(BasicBlock block, VariableRef var)
        {
            Debug.Assert(var.RefType == VariableRefType.Argument || var.RefType == VariableRefType.Local);
            Debug.Assert(variables.Contains(var), "Cannot load address of SSA value");
            var address = block.GetValue(var);
            block.Push(address);
        }

        /// <summary>
        /// Stores a value to the argument with index idx.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="var">The variable reference.</param>
        private void StoreVariable(BasicBlock block, VariableRef var)
        {
            Debug.Assert(var.RefType == VariableRefType.Argument || var.RefType == VariableRefType.Local);
            var variableType = variableTypes[var];
            var storeValue = block.Pop(variableType.Item1, variableType.Item2);
            if (variables.Contains(var))
            {
                var address = block.GetValue(var);
                var memory = block.PopMemory();
                var store = Builder.CreateStore(memory, address, storeValue);
                block.PushMemory(store);
            }
            else
                block.SetValue(var, storeValue);
        }

        #endregion

        #region Stack

        /// <summary>
        /// Realizes a dup operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private static void MakeDup(BasicBlock block)
        {
            block.Dup();
        }

        /// <summary>
        /// Realizes a pop operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private static void MakePop(BasicBlock block)
        {
            block.Pop();
        }

        #endregion

        #region Calls

        /// <summary>
        /// Creates a call instruction to the given method with the given arguments.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="method">The target method to invoke.</param>
        /// <param name="arguments">The call arguments.</param>
        /// <param name="targetBlock">The target block.</param>
        private void CreateCall(
            BasicBlock block,
            MethodBase method,
            BasicBlock targetBlock,
            ImmutableArray<ValueReference> arguments)
        {
            var intrinsicContext = new InvocationContext(this, block, Method, method, arguments);

            // Check for remapping first
            RemappedIntrinsics.RemapIntrinsic(ref intrinsicContext);
            Frontend.RemapIntrinsic(ref intrinsicContext);

            // Early rejection for runtime-dependent methods
            VerifyNotRuntimeMethod(intrinsicContext.Method);

            // Handle device functions
            bool returnsSomething = false;
            if (!Intrinsics.HandleIntrinsic(intrinsicContext, out ValueReference result) &&
                !Frontend.HandleIntrinsic(intrinsicContext, out result))
            {
                var targetFunction = DeclareFunction(intrinsicContext.Method);

                // Mark the target block as call target
                CPSBuilder.MakeCallTarget(targetBlock, targetFunction.ReturnType);

                var call = Builder.CreateFunctionCall(
                    targetFunction,
                    intrinsicContext.Arguments);
                CPSBuilder.SetTerminator(block, call);

                returnsSomething = !targetFunction.IsVoid;
            }
            else
            {
                // Mark the target block as call target
                var returnType = result.IsValid ? result.Type : Builder.VoidType;
                returnsSomething = CPSBuilder.MakeCallTarget(targetBlock, returnType);

                var targetMemory = block.PopMemory();
                var args = result.IsValid ?
                    ImmutableArray.Create(targetMemory, result) :
                    ImmutableArray.Create<ValueReference>(targetMemory);
                var call = Builder.CreateFunctionCall(targetBlock.FunctionValue, args);
                CPSBuilder.SetTerminator(block, call);
            }

            // Setup target block
            var targetBlockBuilder = targetBlock.FunctionBuilder;
            if (returnsSomething)
            {
                block.Push(
                    targetBlockBuilder[TopLevelFunction.ReturnParameterIndex]);
            }
            block.PushMemory(
                targetBlockBuilder[TopLevelFunction.MemoryParameterIndex]);
        }

        /// <summary>
        /// Realizes a call instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="target">The target method to invoke.</param>
        private void MakeCall(BasicBlock block, MethodBase target)
        {
            if (target == null)
                throw this.GetInvalidILCodeException();
            var values = block.PopMethodArgs(
                target,
                CPSBuilder.GetSuccessors(block),
                null,
                out BasicBlock targetBlock);
            CreateCall(block, target, targetBlock, values);
        }

        /// <summary>
        /// Resolves the virtual call target of the given virtual (or abstract) method.
        /// </summary>
        /// <param name="target">The virtual method to call.</param>
        /// <param name="constrainedType">The constrained type of the virtual call.</param>
        /// <returns>The resolved call target.</returns>
        private MethodInfo ResolveVirtualCallTarget(MethodInfo target, Type constrainedType)
        {
            if (!target.IsVirtual)
                return target;
            if (constrainedType == null)
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCallToUnconstrainedInstance, target.Name);
            var sourceGenerics = target.GetGenericArguments();
            // This can only happen in constrained generic cases like:
            // Val GetVal<T>(T instance) where T : IValProvider
            // {
            //      return instance.GetVal();
            // }

            // However, there are two special cases that are supported:
            // x.GetHashCode(), x.ToString()
            // where GetHashCode and ToString are defined in Object.
            MethodInfo actualTarget = null;
            if (target.DeclaringType == typeof(object))
            {
                var @params = target.GetParameters();
                var types = new Type[@params.Length];
                for (int i = 0, e = @params.Length; i < e; ++i)
                    types[i] = @params[i].ParameterType;
                actualTarget = constrainedType.GetMethod(
                    target.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    types,
                    null);
                if (actualTarget != null && actualTarget.DeclaringType != constrainedType)
                    throw this.GetNotSupportedException(
                        ErrorMessages.NotSupportedVirtualMethodCallToObject,
                        target.Name,
                        actualTarget.DeclaringType,
                        constrainedType);
            }
            else
            {
                // Resolve the actual call target
                if (sourceGenerics.Length > 0)
                    target = target.GetGenericMethodDefinition();
                var interfaceMapping = constrainedType.GetInterfaceMap(target.DeclaringType);
                for (int i = 0, e = interfaceMapping.InterfaceMethods.Length; i < e; ++i)
                {
                    if (interfaceMapping.InterfaceMethods[i] != target)
                        continue;
                    actualTarget = interfaceMapping.TargetMethods[i];
                    break;
                }
            }
            if (actualTarget == null)
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCall, target.Name);
            if (sourceGenerics.Length > 0)
                return actualTarget.MakeGenericMethod(sourceGenerics);
            else
                return actualTarget;
        }

        /// <summary>
        /// Realizes a virtual-call instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="instruction">The current IL instruction.</param>
        private void MakeVirtualCall(BasicBlock block, ILInstruction instruction)
        {
            var method = instruction.GetArgumentAs<MethodInfo>();
            if (instruction.HasFlags(ILInstructionFlags.Constrained))
                MakeVirtualCall(block, method, instruction.FlagsContext.Argument as Type);
            else
                MakeVirtualCall(block, method, null);
        }

        /// <summary>
        /// Realizes a virtual-call instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="target">The target method to invoke.</param>
        /// <param name="constrainedType">The target type on which to invoke the method.</param>
        private void MakeVirtualCall(
            BasicBlock block,
            MethodInfo target,
            Type constrainedType)
        {
            target = ResolveVirtualCallTarget(target, constrainedType);
            MakeCall(block, target);
        }

        /// <summary>
        /// Realizes an indirect call instruction.
        /// </summary>
        /// <param name="signature">The target signature.</param>
        private void MakeCalli(object signature)
        {
            throw this.GetNotSupportedException(
                ErrorMessages.NotSupportedIndirectMethodCall, signature);
        }

        /// <summary>
        /// Realizes a jump instruction.
        /// </summary>
        /// <param name="target">The target method to invoke.</param>
        private void MakeJump(MethodBase target)
        {
            throw this.GetNotSupportedException(
                ErrorMessages.NotSupportedMethodJump, target.Name);
        }

        #endregion

        #region Fields

        /// <summary>
        /// Loads the value of a field specified by the given metadata token.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadField(BasicBlock block, FieldInfo field)
        {
            if (field == null)
                throw this.GetInvalidILCodeException();

            var fieldValue = block.Pop();
            if (fieldValue.Type.IsPointerType)
            {
                // Load field from address
                block.Push(fieldValue);
                MakeLoadFieldAddress(block, field);
                var fieldAddress = block.Pop();
                var fieldType = Builder.CreateType(field.FieldType);
                block.Push(CreateLoad(
                    block,
                    fieldAddress,
                    fieldType,
                    field.FieldType.ToTargetUnsignedFlags()));
            }
            else
            {
                // Load field from value
                var typeInfo = Context.TypeInformationManager.GetTypeInfo(field.DeclaringType);
                if (!typeInfo.TryResolveIndex(field, out int fieldIndex))
                    throw this.GetInvalidILCodeException();
                block.Push(Builder.CreateGetField(
                    fieldValue,
                    fieldIndex));
            }
        }

        /// <summary>
        /// Loads the address of a field specified by the given metadata token.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadFieldAddress(BasicBlock block, FieldInfo field)
        {
            if (field == null)
                throw this.GetInvalidILCodeException();
            var targetType = field.DeclaringType;
            var targetPointerType = Builder.CreatePointerType(
                Builder.CreateType(targetType),
                MemoryAddressSpace.Generic);
            var address = block.Pop(targetPointerType, ConvertFlags.None);

            var typeInfo = Context.TypeInformationManager.GetTypeInfo(targetType);
            if (!typeInfo.TryResolveIndex(field, out int fieldIndex))
                throw this.GetInvalidILCodeException();
            var fieldAddress = Builder.CreateLoadFieldAddress(address, fieldIndex);

            block.Push(fieldAddress);
        }

        /// <summary>
        /// Loads a static field specified by the given metadata token.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="field">The field.</param>
        private void MakeLoadStaticField(BasicBlock block, FieldInfo field)
        {
            if (field == null)
                throw this.GetInvalidILCodeException();
            VerifyStaticFieldLoad(field);

            var fieldValue = field.GetValue(null);
            var value = fieldValue == null ?
                Builder.CreateObjectValue(field.FieldType) :
                Builder.CreateObjectValue(fieldValue);
            block.Push(value);
        }

        /// <summary>
        /// Loads the address of a static field specified by the given metadata token.
        /// </summary>
        /// <param name="field">The field.</param>
        private void MakeLoadStaticFieldAddress(FieldInfo field)
        {
            throw this.GetNotSupportedException(
                ErrorMessages.NotSupportedLoadOfStaticFieldAddress, field);
        }

        /// <summary>
        /// Stores a value to a field.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="field">The field.</param>
        private void MakeStoreField(BasicBlock block, FieldInfo field)
        {
            var fieldType = Builder.CreateType(field.FieldType);
            var value = block.Pop(fieldType, field.FieldType.ToTargetUnsignedFlags());
            MakeLoadFieldAddress(block, field);
            var address = block.Pop();
            CreateStore(block, address, value);
        }

        /// <summary>
        /// Stores a value to a static field.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="field">The field.</param>
        private void MakeStoreStaticField(BasicBlock block, FieldInfo field)
        {
            VerifyStaticFieldStore(field);

            // Consume the current value from the stack but do not emit a global store,
            // since we dont have any valid target address.
            // TODO: Stores to static fields could be automatically propagated to the .Net
            // runtime after kernel invocation. However, this remains as a future feature.
            block.Pop();
        }

        #endregion

        #region Objects

        /// <summary>
        /// Realizes a boxing operation that boxes a value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        private void MakeBox(BasicBlock block)
        {
            var value = block.Pop();
            if (!value.Type.IsValueType)
                throw this.GetInvalidILCodeException();
            var alloca = CreateTempAlloca(block, value.Type);
            CreateStore(block, alloca, value);
        }

        /// <summary>
        /// Realizes an un-boxing operation that unboxes a previously boxed value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="type">The target type.</param>
        private void MakeUnbox(BasicBlock block, Type type)
        {
            if (type == null || !type.IsValueType)
                throw this.GetInvalidILCodeException();
            var address = block.Pop();
            var typeNode = Builder.CreateType(type);
            block.Push(CreateLoad(
                block,
                address,
                typeNode,
                type.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes a new-object operation that creates a new instance of a specified type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="method">The target method.</param>
        private void MakeNewObject(BasicBlock block, MethodBase method)
        {
            var constructor = method as ConstructorInfo;
            if (constructor == null)
                throw this.GetInvalidILCodeException();
            var type = constructor.DeclaringType;
            var typeNode = Builder.CreateType(type);
            var alloca = CreateTempAlloca(block, typeNode);

            var value = Builder.CreateNull(typeNode);
            CreateStore(block, alloca, value);

            // Invoke constructor for type
            var values = block.PopMethodArgs(
                method,
                CPSBuilder.GetSuccessors(block),
                alloca,
                out BasicBlock targetBlock);
            CreateCall(block, constructor, targetBlock, values);

            // Push created instance on the stack
            block.Push(CreateLoad(
                block,
                alloca,
                typeNode,
                ConvertFlags.None));
        }

        /// <summary>
        /// Realizes a managed-object initialization.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="type">The target type.</param>
        private void MakeInitObject(BasicBlock block, Type type)
        {
            if (type == null)
                throw this.GetInvalidILCodeException();

            var address = block.Pop();
            var typeNode = Builder.CreateType(type);
            var value = Builder.CreateNull(typeNode);
            CreateStore(block, address, value);
        }

        /// <summary>
        /// Realizes an is-instance instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="type">The target type.</param>
        private void MakeIsInstance(BasicBlock block, Type type)
        {
            throw this.GetNotSupportedException(ErrorMessages.NotSupportedIsInstance);
        }

        /// <summary>
        /// Realizes an indirect load instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="type">The target type.</param>
        private void MakeLoadObject(BasicBlock block, Type type)
        {
            var address = block.Pop();
            var targetElementType = Builder.CreateType(type);
            block.Push(CreateLoad(
                block,
                address,
                targetElementType,
                type.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes an indirect store instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="type">The target type.</param>
        private void MakeStoreObject(BasicBlock block, Type type)
        {
            var typeNode = Builder.CreateType(type);
            var value = block.Pop(typeNode, ConvertFlags.None);
            var address = block.Pop();
            CreateStore(block, address, value);
        }

        #endregion
    }
}
