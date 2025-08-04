// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Construction;
using ILGPUC.IR.Transformations;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ILGPUC.Frontend;

/// <summary>
/// Represents an IR code generator for .Net methods.
/// </summary>
/// <remarks>Members of this class are not thread safe.</remarks>
sealed partial class CodeGenerator
{
    #region Instance

    /// <summary>
    /// Will be invoked once a new method has been called that could not be resolved.
    /// </summary>
    public event EventHandler<MethodBase>? OnNewMethodCalled;

    private readonly Block.CFGBuilder _cfgBuilder;
    private readonly HashSet<VariableRef> _variables = [];
    private readonly Dictionary<VariableRef, (TypeNode Type, ConvertFlags Flags)>
        _variableTypes = [];

    /// <summary>
    /// Constructs a new code generator.
    /// </summary>
    /// <param name="context">The parent IR context.</param>
    /// <param name="methodBuilder">The current method builder.</param>
    /// <param name="disassembledMethod">
    /// The corresponding disassembled method.
    /// </param>
    public CodeGenerator(
        IRContext context,
        Method.Builder methodBuilder,
        DisassembledMethod disassembledMethod)
    {
        Context = context;
        DisassembledMethod = disassembledMethod;

        _cfgBuilder = new Block.CFGBuilder(this, methodBuilder);
        EntryBlock = _cfgBuilder.EntryBlock;
        Location = disassembledMethod.FirstLocation;

        SSABuilder = SSABuilder<VariableRef>.Create(
            methodBuilder,
            _cfgBuilder.Blocks);

        // Setup variables and inlining attributes
        SetupVariables();
        Inliner.SetupInliningAttributes(
            context.Properties,
            methodBuilder.Method,
            disassembledMethod);

        // NB: Initialized during GenerateCode.
        Block = Utilities.InitNotNullable<Block>();
    }

    /// <summary>
    /// Setups all parameter and local bindings.
    /// </summary>
    private void SetupVariables()
    {
        var builder = EntryBlock.Builder;
        LambdaArgumentOffset = Method.IsNotCapturingLambda() ? 1 : 0;

        // Check for SSA variables
        for (int i = 0, e = DisassembledMethod.Count; i < e; ++i)
        {
            var instruction = DisassembledMethod[i];
            switch (instruction.InstructionType)
            {
                case ILInstructionType.Ldarga:
                    _variables.Add(new VariableRef(
                        instruction.GetArgumentAs<int>() - LambdaArgumentOffset,
                        VariableRefType.Argument));
                    break;
                case ILInstructionType.Ldloca:
                    _variables.Add(new VariableRef(
                        instruction.GetArgumentAs<int>(),
                        VariableRefType.Local));
                    break;
            }
        }

        // Initialize params
        if (!Method.IsStatic && !Method.IsNotCapturingLambda())
        {
            var declaringType = builder.CreateType(Method.DeclaringType.AsNotNull());
            declaringType = builder.CreatePointerType(
                declaringType,
                MemoryAddressSpace.Generic);
            var paramRef = new VariableRef(0, VariableRefType.Argument);
            EntryBlock.SetValue(
                paramRef,
                MethodBuilder.InsertParameter(declaringType, "this"));
            _variableTypes[paramRef] = (declaringType, ConvertFlags.None);
        }

        var methodParameters = Method.GetParameters();
        var parameterOffset = Method.GetParameterOffset();
        for (int i = 0, e = methodParameters.Length; i < e; ++i)
        {
            var parameter = methodParameters[i];
            var paramType = builder.CreateType(parameter.ParameterType);
            Value ssaValue = MethodBuilder.AddParameter(paramType, parameter.Name);
            var argRef = new VariableRef(
                i + parameterOffset,
                VariableRefType.Argument);
            if (_variables.Contains(argRef))
            {
                // Address was taken... emit a temporary alloca and store
                // the argument value to it
                var alloca = CreateTempAlloca(paramType);
                builder.CreateStore(
                    Location,
                    alloca,
                    ssaValue);
                ssaValue = alloca;
            }
            EntryBlock.SetValue(argRef, ssaValue);
            _variableTypes[argRef] = (
                paramType,
                parameter.ParameterType.ToTargetUnsignedFlags());
        }

        // Initialize locals
        var localVariables = Method.GetMethodBody().AsNotNull().LocalVariables;
        for (int i = 0, e = localVariables.Count; i < e; ++i)
        {
            var variable = localVariables[i];
            var variableType = builder.CreateType(variable.LocalType);
            var localRef = new VariableRef(i, VariableRefType.Local);
            Value initValue = builder.CreateNull(
                Location,
                variableType);
            if (_variables.Contains(localRef))
            {
                // Address was taken... emit a temporary alloca and store
                // an empty value to it
                var alloca = CreateTempAlloca(variableType);
                builder.CreateStore(
                    Location,
                    alloca,
                    initValue);
                initValue = alloca;
            }

            EntryBlock.SetValue(localRef, initValue);
            _variableTypes[localRef] = (
                variableType,
                variable.LocalType.ToTargetUnsignedFlags());
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the current IR context.
    /// </summary>
    public IRContext Context { get; }

    /// <summary>
    /// Returns compilation properties.
    /// </summary>
    public CompilationProperties Properties => Context.Properties;

    /// <summary>
    /// Returns the current type context.
    /// </summary>
    public IRTypeContext TypeContext => Context.TypeContext;

    /// <summary>
    /// Returns the current method builder.
    /// </summary>
    public Method.Builder MethodBuilder => SSABuilder.MethodBuilder;

    /// <summary>
    /// Returns the current disassembled method.
    /// </summary>
    public DisassembledMethod DisassembledMethod { get; }

    /// <summary>
    /// Returns the current managed method.
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

    #region Builder Properties

    /// <summary>
    /// Gets or sets the current block being processing.
    /// </summary>
    private Block Block { get; set; }

    /// <summary>
    /// Returns the current block builder.
    /// </summary>
    private BasicBlock.Builder Builder => Block.Builder;

    /// <summary>
    /// Gets or sets the current location.
    /// </summary>
    private Location Location { get; set; }

    /// <summary>
    /// Gets or sets the offset for load/store argument instructions in a lambda.
    /// This is used to shift arguments because of the unused 'this' argument.
    /// </summary>
    private int LambdaArgumentOffset { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Gets an internal method.
    /// </summary>
    /// <param name="methodBase">The method to declare.</param>
    /// <returns>The declared method.</returns>
    public Method GetMethod(MethodBase methodBase)
    {
        var result = Context.Declare(methodBase, out bool created);
        if (created) OnNewMethodCalled?.Invoke(this, methodBase);
        return result;
    }

    /// <summary>
    /// Creates a temporary alloca for the given type.
    /// </summary>
    /// <param name="type">The type to allocate.</param>
    /// <returns>The created alloca.</returns>
    public ValueReference CreateTempAlloca(TypeNode type) =>
        EntryBlock.Builder.CreateAlloca(
            Location,
            type,
            MemoryAddressSpace.Local);

    /// <summary>
    /// Generates code for the current function.
    /// </summary>
    /// <returns>The created top-level function.</returns>
    public Method GenerateCode()
    {
        // Iterate over all blocks in reverse post order
        foreach (BasicBlock basicBlock in _cfgBuilder.Blocks)
        {
            Block = _cfgBuilder[basicBlock];
            Location = basicBlock.Location;

            GenerateCodeForBlock();
        }

        SSABuilder.AssertAllSealed();

        // Ensure that we have a unique exit block
        MethodBuilder.EnsureUniqueExitBlock();

        return MethodBuilder.Method;
    }

    /// <summary>
    /// Generates code for the given block.
    /// </summary>
    private void GenerateCodeForBlock()
    {
        if (!SSABuilder.ProcessAndSeal(Block.BasicBlock))
            return;

        int endOffset = Block.InstructionOffset + Block.InstructionCount;
        for (int i = Block.InstructionOffset; i < endOffset; ++i)
        {
            var instruction = DisassembledMethod[i];

            // Setup debug information
            Location = instruction.Location;

            // Try to generate code for this instruction
            bool generated;
            try
            {
                generated = TryGenerateCode(instruction);
            }
            catch (InternalCompilerException)
            {
                // If we already have an internal compiler exception, re-throw it.
                throw;
            }
            catch (Exception e)
            {
                // Wrap generic exceptions with location information.
                throw Location.GetException(e);
            }
            if (!generated)
            {
                throw Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedInstruction,
                    instruction,
                    Method.Name);
            }
        }

        // Handle implicit branches to successor blocks
        if (Builder.Terminator is BuilderTerminator builderTerminator)
        {
            Location = DisassembledMethod[endOffset].Location;
            // Verify that implicit branches have one successor only
            Location.Assert(builderTerminator.NumTargets == 1);
            Builder.CreateBranch(
                Location,
                builderTerminator.Targets[0]);
        }

        // Try to seal successor back edges
        SSABuilder.TrySealSuccessors(Block.BasicBlock);
    }

    #endregion

    #region Verification

    /// <summary>
    /// Verifies that the given method is not a .Net-runtime-dependent method.
    /// If it depends on the runtime, this method will throw a
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    private void VerifyNotRuntimeMethod(MethodBase method)
    {
        if (method.DeclaringType == null || method.DeclaringType.FullName == null)
            return;
        var @namespace = method.DeclaringType.FullName;
        // Internal unsafe intrinsic methods
        if (@namespace.StartsWith(
            "System.Runtime.CompilerServices",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (NotInsideKernelAttribute.IsDefined(method) ||
            @namespace.StartsWith(
            "System.Runtime",
            StringComparison.OrdinalIgnoreCase) ||
            @namespace.StartsWith(
                "System.Reflection",
                StringComparison.OrdinalIgnoreCase))
        {
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedRuntimeMethod,
                method.Name);
        }
    }

    /// <summary>
    /// Verifies a static-field load operation.
    /// </summary>
    /// <param name="field">The static field to load.</param>
    private void VerifyStaticFieldLoad(FieldInfo field)
    {
        Debug.Assert(field != null && field.IsStatic, "Invalid field");

        bool isInitOnly = (field.Attributes & FieldAttributes.InitOnly) !=
            FieldAttributes.InitOnly;
        if (isInitOnly &&
            Context.Properties.StaticFieldMode < StaticFieldMode.MutableStaticFields)
        {
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedLoadOfStaticField,
                field);
        }
    }

    /// <summary>
    /// Verifies a static-field store operation.
    /// </summary>
    /// <param name="field">The static field to store to.</param>
    private void VerifyStaticFieldStore(FieldInfo field)
    {
        Debug.Assert(field != null && field.IsStatic, "Invalid field");

        if (Context.Properties.StaticFieldMode
            < StaticFieldMode.IgnoreStaticFieldStores)
        {
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedStoreToStaticField,
                field);
        }
    }

    #endregion

    #region Code Generation

    /// <summary>
    /// Realizes a no-operation instruction.
    /// </summary>
    private static void MakeNop() { }

    /// <summary>
    /// Realizes a trap instruction.
    /// </summary>
    private static void MakeTrap() { }

    /// <summary>
    /// Converts the given value (already loaded) into its corresponding
    /// evaluation-stack representation.
    /// </summary>
    /// <param name="value">The source value to load (already loaded).</param>
    /// <param name="flags">The conversion flags.</param>
    private Value LoadOntoEvaluationStack(Value value, ConvertFlags flags)
    {
        Debug.Assert(value != null, "Invalid value to load");

        // Extent small basic types
        switch (value.BasicValueType)
        {
            case BasicValueType.Int8:
            case BasicValueType.Int16:
                return CreateConversion(
                    value,
                    Builder.GetPrimitiveType(BasicValueType.Int32),
                    flags.ToSourceUnsignedFlags());
            default:
                return value;
        }
    }

    /// <summary>
    /// Realizes an indirect load instruction.
    /// </summary>
    /// <param name="address">The source address.</param>
    /// <param name="type">The target type.</param>
    /// <param name="flags">The conversion flags.</param>
    private Value CreateLoad(
        Value address,
        TypeNode type,
        ConvertFlags flags)
    {
        if (!address.Type.IsPointerType)
            throw Location.GetInvalidOperationException();

        address = CreateConversion(
            address,
            Builder.CreatePointerType(type, MemoryAddressSpace.Generic),
            ConvertFlags.None);
        var value = Builder.CreateLoad(Location, address);
        return LoadOntoEvaluationStack(value, flags);
    }

    /// <summary>
    /// Realizes an indirect store instruction.
    /// </summary>
    /// <param name="address">The target address.</param>
    /// <param name="value">The value to store.</param>
    private void CreateStore(Value address, Value value)
    {
        if (!address.Type.IsPointerType)
            throw Location.GetInvalidOperationException();

        address = CreateConversion(
            address,
            Builder.CreatePointerType(value.Type, MemoryAddressSpace.Generic),
            ConvertFlags.None);
        Builder.CreateStore(Location, address, value);
    }

    /// <summary>
    /// Realizes a duplicate operation.
    /// </summary>
    private void MakeDup() => Block.Dup();

    /// <summary>
    /// Realizes a pop operation.
    /// </summary>
    private void MakePop() => Block.Pop();

    /// <summary>
    /// Realizes an internal load-token operation.
    /// </summary>
    /// <param name="handleValue">The managed handle object.</param>
    private void MakeLoadToken(object handleValue)
    {
        var handle = Builder.CreateRuntimeHandle(
            Location,
            handleValue);
        Block.Push(handle);
    }

    #endregion
}
