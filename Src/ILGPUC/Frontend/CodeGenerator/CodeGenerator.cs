// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    private readonly Dictionary<VariableRef, (TypeValue Type, ConvertFlags Flags)>
        _variableTypes = [];

    /// <summary>
    /// Delegate de-virtualization state. Set by ldftn; consumed by newobj Delegate.ctor
    /// in MakeNewDelegate.
    /// </summary>
    private MethodBase? _pendingLdFunctionMethod;


    /// <summary>
    /// Most recent delegate method from <see cref="MakeNewDelegate"/>. Used as
    /// a fallback when <c>stloc</c> stores a phi-merged delegate value that
    /// doesn't match the direct <see cref="_delegateTable"/> (static lambda
    /// caching pattern).
    /// </summary>
    private MethodBase? _pendingDelegateForLocal;

    /// <summary>
    /// Maps a delegate handle Value to the underlying lambda MethodBase.
    /// Used for same-block resolution.
    /// </summary>
    private readonly Dictionary<Value, MethodBase> _delegateTable = [];

    /// <summary>
    /// Maps a local variable VariableRef to the lambda MethodBase when a delegate
    /// value was stored to a local. Used for cross-block resolution.
    /// </summary>
    private readonly Dictionary<VariableRef, MethodBase> _delegateLocalTable = [];

    /// <summary>
    /// Constructs a new code generator.
    /// </summary>
    /// <param name="methodBuilder">The current method builder.</param>
    /// <param name="disassembledMethod">
    /// The corresponding disassembled method.
    /// </param>
    /// <param name="backendType">
    /// Backend the IR is being generated for. Used by codegen-time
    /// intrinsic resolution to pick the backend-specific implementation.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public CodeGenerator(
        MethodBuilder methodBuilder,
        DisassembledMethod disassembledMethod,
        Backends.BackendType backendType = default)
    {
        ModuleBuilder = methodBuilder.ModuleBuilder;
        MethodBuilder = methodBuilder;
        DisassembledMethod = disassembledMethod;
        BackendType = backendType;

        _cfgBuilder = new Block.CFGBuilder(this, methodBuilder);
        EntryBlock = _cfgBuilder.EntryBlock;
        Location = disassembledMethod.FirstLocation;

        SSABuilder = SSABuilder<VariableRef>.Create(methodBuilder, _cfgBuilder.Blocks);

        // Setup variables and inlining attributes
        SetupVariables();
        Inliner.SetupInliningAttributes(
            Properties,
            methodBuilder.Method,
            disassembledMethod);

        // NB: Initialized during GenerateCode.
        Block = Utilities.InitNotNullable<Block>();
    }

    /// <summary>
    /// Setups all parameter and local bindings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
            var declaringType = ModuleBuilder.CreateType(
                Method.DeclaringType.AsNotNull());
            // Guard against double-wrapping: CreateType already returns a PointerType
            // for class types (sealed/compiler-generated classes and delegates).
            if (declaringType is not PointerType)
            {
                declaringType = ModuleBuilder.CreatePointerType(
                    declaringType,
                    MemoryAddressSpace.Generic);
            }
            var paramRef = new VariableRef(0, VariableRefType.Argument);
            EntryBlock.SetValue(
                paramRef,
                MethodBuilder.CreateParameter(declaringType, "this"));
            _variableTypes[paramRef] = (declaringType, ConvertFlags.None);
        }

        var methodParameters = Method.GetParameters();
        var parameterOffset = Method.GetParameterOffset();
        for (int i = 0, e = methodParameters.Length; i < e; ++i)
        {
            var parameter = methodParameters[i];
            var paramType = ModuleBuilder.CreateType(parameter.ParameterType);
            Value ssaValue = MethodBuilder.CreateParameter(paramType, parameter.Name);
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
            var variableType = ModuleBuilder.CreateType(variable.LocalType);
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
    /// Returns the current module builder.
    /// </summary>
    public ModuleBuilder ModuleBuilder { get; }

    /// <summary>
    /// Backend type for which IR is being generated. Read by
    /// <see cref="Intrinsic.Intrinsics.TryGenerateCode"/> when resolving
    /// backend-specific Implemented intrinsics that may be reached via the
    /// codegen-time on-the-fly disassembly path (round-3 lazy walk). Default
    /// is <see cref="Backends.BackendType.CPU"/> for legacy callers.
    /// </summary>
    public Backends.BackendType BackendType { get; }

    /// <summary>
    /// Returns compilation properties.
    /// </summary>
    public CompilationProperties Properties => ModuleBuilder.Properties;

    /// <summary>
    /// Returns the current method builder.
    /// </summary>
    public MethodBuilder MethodBuilder { get; }

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
    private BasicBlockBuilder Builder => Block.Builder;

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
        var declaration = ModuleBuilder.CreateMethodDeclaration(methodBase);
        var result = ModuleBuilder.GetOrCreateMethod(declaration);
        OnNewMethodCalled?.Invoke(this, methodBase);
        return result.Method;
    }

    /// <summary>
    /// Creates a temporary alloca for the given type.
    /// </summary>
    /// <param name="type">The type to allocate.</param>
    /// <returns>The created alloca.</returns>
    public Value CreateTempAlloca(TypeValue type) =>
        EntryBlock.Builder.CreateAlloca(Location, type).AsNotNull();

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

        return MethodBuilder.Method;
    }

    /// <summary>
    /// Synthesizes a one-block IR body for an intrinsic method by
    /// invoking its registered <c>IntrinsicGenerator</c> with the method's
    /// own <see cref="ILGPUC.IR.MethodValues.Parameter"/> values as stack
    /// arguments. Used when the intrinsic is referenced via a delegate
    /// handle (<c>ldftn</c>) and downstream passes need to introspect
    /// its body (e.g. <c>TryRecognizeOperation</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The surrounding <see cref="CodeGenerator"/> was constructed against
    /// a real <see cref="Frontend.DisassembledMethod"/> so the entry block
    /// already exists and
    /// <see cref="SetupVariables"/> has bound <see cref="IR.MethodValues.Parameter"/>
    /// values to it — we simply pull those parameter values from the
    /// entry block, feed them into a synthetic
    /// <see cref="InvocationContext"/>, and run the intrinsic generator
    /// in place of the method's IL body.
    /// </para>
    /// <para>
    /// The generator writes an IR value (e.g. a
    /// <c>BinaryArithmeticValue</c>) directly to the entry block builder
    /// and returns it; we wrap it in a return terminator.
    /// </para>
    /// </remarks>
    /// <param name="methodBase">The intrinsic method to synthesize.</param>
    public void GenerateIntrinsicBody(MethodBase methodBase)
    {
        // Mirror GenerateCode's block iteration so the SSA builder sees
        // every block, but replace IL processing with a single generator
        // invocation on the first instruction-bearing block (the
        // CFG-builder-synthesized internal entry).
        bool synthesized = false;
        foreach (BasicBlock basicBlock in _cfgBuilder.Blocks)
        {
            Block = _cfgBuilder[basicBlock];
            Location = basicBlock.Location;

            if (!SSABuilder.ProcessAndSeal(Block.BasicBlock))
                continue;

            // The main entry has zero instructions and an
            // unconditional termination into the internal entry; skip
            // code emission for it.
            if (Block.InstructionCount == 0 || synthesized)
                continue;

            synthesized = true;

            // Pull parameter values bound by SetupVariables.
            var parameters = methodBase.GetParameters();
            int parameterOffset = methodBase.IsStatic ? 0 : 1;
            int numArgs = parameters.Length + parameterOffset;
            var argValues = ValueBuilderList.Create(
                MethodBuilder.Generation, numArgs);
            for (int i = 0; i < numArgs; i++)
            {
                var argRef = new VariableRef(i, VariableRefType.Argument);
                argValues.Add(Block.GetValue(argRef));
            }

            // Invoke the generator via the same dispatch path direct
            // calls use. The result is appended to this block's builder.
            var ctx = new InvocationContext(
                this,
                Location,
                Block,
                callerMethod: methodBase,
                method: methodBase,
                ref argValues);
            if (!Intrinsics.TryGenerateCode(ref ctx, out var result))
            {
                throw new InvalidOperationException(
                    $"No intrinsic generator registered for {methodBase.Name}");
            }

            // Replace the pending termination with a real return.
            var returnType = methodBase is MethodInfo mi
                ? mi.ReturnType
                : typeof(void);
            if (returnType == typeof(void) || result is null)
                Block.Builder.CreateReturnTermination(null);
            else
                Block.Builder.CreateReturnTermination(result);
        }

        if (!synthesized)
        {
            throw new InvalidOperationException(
                $"No instruction-bearing block found to synthesize " +
                $"body of {methodBase.Name}");
        }

        SSABuilder.AssertAllSealed();
    }

    /// <summary>
    /// Generates code for the given block.
    /// </summary>
    private void GenerateCodeForBlock()
    {
        if (!SSABuilder.ProcessAndSeal(Block.BasicBlock))
            return;

        // Skip blocks that became unreachable after branch folding
        // (e.g. the dead target of a folded delegate-caching brtrue).
        // ProcessAndSeal above already marked the block as processed
        // and sealed so that SSA builder assertions pass, but we must
        // not generate code — the SSA builder would assert on
        // GetValueRecursive with zero predecessors.
        if (Block.BasicBlock.Predecessors.Length == 0 &&
            Block.BasicBlock != EntryBlock.BasicBlock)
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
        if (Block.BasicBlock.TerminationKind == BlockTerminationKind.Pending &&
            Block.BasicBlock.Successors.Length == 1)
        {
            Location = DisassembledMethod[endOffset].Location;
            Builder.CreateUnconditionalTermination(
                Block.BasicBlock.Successors[0]);
        }

        // Try to seal successor back edges
        SSABuilder.TrySealSuccessors(Block.BasicBlock);
    }

    #endregion

    #region Verification

    /// <summary>
    /// Verifies a static-field load operation.
    /// </summary>
    /// <param name="field">The static field to load.</param>
    private void VerifyStaticFieldLoad(FieldInfo field)
    {
        Debug.Assert(field.IsStatic, "Invalid field");

        bool isMutable = (field.Attributes & FieldAttributes.InitOnly) !=
            FieldAttributes.InitOnly;
        if (isMutable &&
            Properties.StaticFieldMode < StaticFieldMode.MutableStaticFields)
        {
            // Allow all static fields in compiler-generated closure classes
            // (<>c). These include delegate caches (<>9__*) and closure
            // singletons (<>9). They're handled by CreateLoadStaticFieldValue
            // which returns a null pointer for the delegate devirtualization
            // mechanism.
            if (field.DeclaringType?.Name.StartsWith(
                "<>",
                StringComparison.Ordinal) == true)
            {
                return;
            }

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
        Debug.Assert(field.IsStatic, "Invalid field");

        if (Properties.StaticFieldMode < StaticFieldMode.IgnoreStaticFieldStores)
        {
            // Allow stores to compiler-generated closure class fields.
            // These are no-ops in the IR (MakeStoreStaticField just pops).
            if (field.DeclaringType?.Name.StartsWith(
                "<>",
                StringComparison.Ordinal) == true)
            {
                return;
            }

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
                    ModuleBuilder.GetPrimitiveType(BasicValueType.Int32),
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
    private Value CreateLoad(Value address, TypeValue type, ConvertFlags flags)
    {
        if (address.Type is not PointerType)
            throw Location.GetInvalidOperationException();

        address = CreateConversion(
            address,
            ModuleBuilder.CreatePointerType(type, MemoryAddressSpace.Generic),
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
        if (address.Type is not PointerType)
            throw Location.GetInvalidOperationException();

        address = CreateConversion(
            address,
            ModuleBuilder.CreatePointerType(value.Type, MemoryAddressSpace.Generic),
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
