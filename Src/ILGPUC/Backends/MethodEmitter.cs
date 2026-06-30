// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;

namespace ILGPUC.Backends;

/// <summary>
/// Handles emission of method declarations and implementations.
/// </summary>
/// <remarks>
/// Constructs a new method emitter.
/// </remarks>
sealed partial class MethodEmitter(
    GenerationContext context,
    TypeEmitter typeEmitter,
    ExpressionEmitter expressionEmitter)
{
    /// <summary>
    /// Per-method counter for emitter-synthesized locals (jump variables,
    /// phi-copy temporaries and state variables). Every synthesized local
    /// claims a unique suffix, so that no two of them can redeclare or
    /// shadow each other. Reset per method body.
    /// </summary>
    private int _emitterLocalId;

    /// <summary>
    /// Emits method declarations (forward declarations).
    /// </summary>
    public void EmitMethodDeclarations()
    {
        foreach (var method in context.Module.MethodsInReversePostOrder)
        {
            if (method == context.Module.EntryPoint)
                continue; // Entry point declaration is different

            // Prepend the device-function attribute (CUDA/HIP __device__)
            // so the forward declaration matches the definition. nvcc
            // rejects mismatched function attributes between decl and def.
            var deviceAttr = context.LanguageConfig.DeviceFunctionAttribute;
            if (!string.IsNullOrEmpty(deviceAttr))
                context.Write(deviceAttr + " ");

            EmitMethodSignature(method);
            context.WriteLine(";");
            context.WriteLine();
        }
    }

    /// <summary>
    /// Emits all method implementations.
    /// </summary>
    public void EmitMethodImplementations()
    {
        foreach (var method in context.Module.MethodsInReversePostOrder)
        {
            EmitMethod(method);
            context.WriteLine();
        }
    }

    /// <summary>
    /// Emits a complete method implementation.
    /// </summary>
    private void EmitMethod(Method method)
    {
        // Add kernel attribute for the entry point, device attribute for
        // every other method. CUDA / HIP need __device__ on non-entry
        // functions so the kernel can call them; Metal / OpenCL / CPU
        // return empty here and behave as before.
        if (method == context.Module.EntryPoint)
            context.Write(context.LanguageConfig.KernelAttribute + " ");
        else
        {
            var deviceAttr = context.LanguageConfig.DeviceFunctionAttribute;
            if (!string.IsNullOrEmpty(deviceAttr))
                context.Write(deviceAttr + " ");
        }

        // Emit signature
        EmitMethodSignature(method);
        context.WriteLine();
        context.OpenScope();

        // Emit method body
        if (method.HasImplementation)
        {
            EmitMethodBody(method);
        }

        context.CloseScope();
    }

    /// <summary>
    /// Emits a method signature.
    /// For the kernel entry point, this also appends language-specific thread
    /// built-in parameters (Metal only).
    /// </summary>
    private void EmitMethodSignature(Method method)
    {
        bool isEntryPoint = method == context.Module.EntryPoint;

        var returnType = typeEmitter.GetTypeName(method.Type);
        var methodName = context.GetValueName(method);

        context.Write($"{returnType} {methodName}(");

        bool first = true;
        int bufferIndex = 0;
        bool skipsIndex = isEntryPoint
            && context.LanguageConfig.SkipsIndexParameter
            && context.Launch.HasIndexParameter;

        foreach (var param in method.Parameters)
        {
            // GPU backends: skip the first parameter (thread index) —
            // it will be computed from built-in thread attributes
            if (skipsIndex && param.Index == 0)
                continue;

            if (!first) context.Write(", ");
            first = false;
            EmitParameter(param, isEntryPoint, ref bufferIndex);
        }

        // Append the hidden launch-extent parameters of implicitly grouped
        // launches (see LanguageConfiguration.LaunchExtentTypeName).
        if (UsesLaunchExtents(method))
        {
            var extentType = context.LanguageConfig.LaunchExtentTypeName;
            for (int i = 0; i < context.Launch.IndexDimensions; ++i)
            {
                if (!first) context.Write(", ");
                first = false;
                context.Write(
                    $"{extentType} {EntryPointLaunch.GetExtentParameterName(i)}");
            }
        }

        // Append language-specific built-in parameters (Metal thread position, etc.)
        if (isEntryPoint)
        {
            foreach (var builtIn in
                context.LanguageConfig.GetKernelBuiltInParameters())
            {
                if (!first) context.Write(", ");
                first = false;
                context.Write(builtIn);
            }
        }

        context.Write(")");
    }

    /// <summary>
    /// Emits a parameter declaration.
    /// When emitting the kernel entry point and the backend requires buffer index
    /// attributes (Metal), delegates to
    /// <see cref="LanguageConfiguration.EmitNonFlattenedKernelParam"/>.
    /// </summary>
    private void EmitParameter(Parameter param, bool isEntryPoint, ref int bufferIndex)
    {
        var typeName = typeEmitter.GetTypeName(param.Type);
        var paramName = context.GetValueName(param);

        if (isEntryPoint)
        {
            var paramCode = context.LanguageConfig.EmitNonFlattenedKernelParam(
                param.Type,
                typeName,
                paramName,
                bufferIndex);
            context.Write(paramCode);
            ++bufferIndex;
        }
        else
        {
            context.Write($"{typeName} {paramName}");
        }
    }

    /// <summary>
    /// Returns true if the given method is an entry point that receives the
    /// hidden launch-extent parameters of an implicitly grouped launch.
    /// </summary>
    private bool UsesLaunchExtents(Method method) =>
        method == context.Module.EntryPoint
        && context.LanguageConfig.SkipsIndexParameter
        && context.LanguageConfig.LaunchExtentTypeName is not null
        && context.Launch.IsImplicit;

    /// <summary>
    /// Emits the early return for threads outside the launch extent.
    /// </summary>
    private void EmitExtentGuard(string indexName, TypeValue indexType)
    {
        var conditions = new List<string>();
        if (indexType is StructureType structType)
        {
            int dimensions = System.Math.Min(
                structType.NumFields, context.Launch.IndexDimensions);
            for (int i = 0; i < dimensions; ++i)
            {
                conditions.Add(
                    $"{indexName}.{StructureType.GetFieldName(i)} >= " +
                    EntryPointLaunch.GetExtentParameterName(i));
            }
        }
        else
        {
            conditions.Add(
                $"{indexName} >= {EntryPointLaunch.GetExtentParameterName(0)}");
        }
        context.WriteLine($"  if ({string.Join(" || ", conditions)}) return;");
    }

    /// <summary>
    /// Emits the body of a method.
    /// </summary>
    private void EmitMethodBody(Method method)
    {
        // Reset the synthesized-local counter — names only need to be
        // unique within one function.
        _emitterLocalId = 0;
        // Entry point parameter set-up (e.g. applying OpenCL buffer offsets).
        if (method == context.Module.EntryPoint)
        {
            bool skipsIndex = context.LanguageConfig.SkipsIndexParameter
                && context.Launch.HasIndexParameter;
            foreach (var param in method.Parameters)
            {
                if (skipsIndex && param.Index == 0)
                    continue;
                var prologue = context.LanguageConfig.EmitKernelParamPrologue(
                    param.Type,
                    typeEmitter.GetTypeName(param.Type),
                    context.GetValueName(param));
                if (prologue is not null)
                    context.WriteLine(prologue);
            }
        }

        // GPU backends: compute the thread index from built-in attributes
        // instead of receiving it as a buffer parameter. Grouped
        // (explicit-config) kernels have no implicit index — parameter 0 is
        // a real user parameter received via the signature, so nothing may
        // be computed (and redeclared) here.
        if (method == context.Module.EntryPoint
            && context.LanguageConfig.SkipsIndexParameter
            && context.Launch.HasIndexParameter
            && method.NumParameters > 0)
        {
            var indexParam = method.Parameters[0];
            var typeName = typeEmitter.GetTypeName(indexParam.Type);
            var paramName = context.GetValueName(indexParam);
            var computation = context.LanguageConfig.EmitIndexComputation(
                typeName, paramName, indexParam.Type, context.Launch);
            if (computation != null)
                context.WriteLine(computation);

            // Skip the threads the launch added to fill the last group.
            if (UsesLaunchExtents(method))
                EmitExtentGuard(paramName, indexParam.Type);
        }

        // Emit function-scoped globals (e.g. Metal thread/threadgroup arrays).
        // These can't be at file scope in some languages.
        EmitFunctionScopedGlobals(method);

        // Create code placement analysis to order values correctly
        var codePlacement = method.CreateCodePlacement();

        // Translate the control-flow graph into structured statements
        var controlFlow = StructuredControlFlow.Create(
            method,
            codePlacement.Dominators);

        // Allocate variables for SSA values that need them
        var ssaAllocator = new SSAVariableAllocator(context, method);
        ssaAllocator.AllocateVariables();

        // Emit the control flow structure. Irreducible control flow has no
        // structured form and is emitted as a state machine instead.
        if (controlFlow.IsReducible)
            EmitStructuredControlFlow(controlFlow, codePlacement);
        else
            EmitStateMachine(method, codePlacement);
    }

    /// <summary>
    /// Emits global variable declarations that must be function-scoped
    /// for this backend (e.g. Metal thread/threadgroup variables).
    /// </summary>
    private void EmitFunctionScopedGlobals(Method method)
    {
        // Only the entry point owns the globals
        if (method != context.Module.EntryPoint)
            return;

        foreach (var global in context.Module.Globals)
        {
            if (context.LanguageConfig.IsFileScopeAddressSpace(global.AddressSpace))
                continue;
            CodeGenerator.EmitGlobalDeclaration(context, typeEmitter, global);
        }
    }

    /// <summary>
    /// Emits a basic block's contents (without control flow).
    /// </summary>
    private void EmitBasicBlock(BasicBlock block, CodePlacement placement)
    {
        // Get the placed block with all values in correct order
        var placedBlock = placement.GetPlacedBlock(block);

        // Emit all values in placement order (excluding phi values)
        foreach (var value in placedBlock)
        {
            // Skip phi values (handled separately below)
            if (value is PhiValue)
                continue;

            EmitValue(value);
        }

        // Emit return statement if this block has return termination
        if (block.TerminationKind == BlockTerminationKind.Return)
        {
            var returnView = block.AsReturnView();
            if (returnView.IsVoidReturn)
                context.WriteLine("return;");
            else
            {
                var returnExpr = expressionEmitter.EmitExpression(returnView.ReturnValue);
                context.WriteLine($"return {returnExpr};");
            }
        }
    }

    /// <summary>
    /// Emits a value (either BasicBlockValue or PureValue).
    /// </summary>
    private void EmitValue(Value<Method> value)
    {
        // Pure values are typically inlined, but if they're placed explicitly,
        // we may need to emit them as variable assignments
        if (value is PureValue pureValue)
        {
            EmitPureValue(pureValue);
            return;
        }

        // Handle basic block values
        if (value is BasicBlockValue bbValue)
        {
            EmitBasicBlockValue(bbValue);
        }
    }

    /// <summary>
    /// Emits a pure value as a variable assignment if needed.
    /// </summary>
    private void EmitPureValue(PureValue value)
    {
        // Only emit assignment if this value was declared as a variable
        if (!context.NeedsVariable(value))
            return;

        var varName = context.GetValueName(value);
        if (value is SetField setField)
        {
            EmitSetField(setField, varName);
            return;
        }
        var expr = expressionEmitter.EmitDefinition(value);
        EmitVariableAssignment(value, varName, expr);
    }

    /// <summary>
    /// Emits a field insertion as a copy of the source structure followed by
    /// an assignment of the inserted field (or fields, for a field span).
    /// </summary>
    private void EmitSetField(SetField setField, string varName)
    {
        var source = expressionEmitter.EmitExpression(setField.Source);
        EmitVariableAssignment(setField, varName, source);
        var fieldValue = expressionEmitter.EmitExpression(setField.Value);
        var span = setField.FieldSpan;
        if (span.Span == 1)
        {
            context.WriteLine(
                $"{varName}.{StructureType.GetFieldName(span.Index)} = " +
                $"{fieldValue};");
            return;
        }
        for (int i = 0; i < span.Span; i++)
        {
            context.WriteLine(
                $"{varName}.{StructureType.GetFieldName(span.Index + i)} = " +
                $"({fieldValue}).{StructureType.GetFieldName(i)};");
        }
    }

    /// <summary>
    /// Emits a basic block value (instruction).
    /// </summary>
    private void EmitBasicBlockValue(BasicBlockValue value)
    {
        // Alloca needs special handling: emit as array + pointer pair
        if (value is Alloca alloca)
        {
            var decl = expressionEmitter.EmitAllocaDeclaration(alloca);
            foreach (var line in decl.Split('\n'))
                context.WriteLine(line);
            return;
        }

        // Check if this value produces a result that needs to be stored
        if (value.Type is not VoidType && context.NeedsVariable(value))
        {
            var varName = context.GetValueName(value);
            var expr = expressionEmitter.EmitDefinition(value);
            EmitVariableAssignment(value, varName, expr);
        }
        else
        {
            // Statement-only instruction (e.g., store, barrier)
            var stmt = expressionEmitter.EmitStatement(value);
            if (!string.IsNullOrEmpty(stmt))
                context.WriteLine(stmt);
        }
    }

    /// <summary>
    /// Emits a variable assignment. All variables are pre-declared at
    /// function scope by SSAVariableAllocator, so this only emits the
    /// assignment. Alloca values never reach this path (they use
    /// ExpressionEmitter.EmitAllocaDeclaration instead).
    /// </summary>
    private void EmitVariableAssignment(Value value, string varName, string expr) =>
        context.WriteLine($"{varName} = {expr};");
}
