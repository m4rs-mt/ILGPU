// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SSAVariableAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Backends;

/// <summary>
/// Allocates variables for SSA values that cannot be represented as
/// inline expressions. Handles phi nodes, values with multiple uses, etc.
/// </summary>
/// <remarks>
/// Constructs a new SSA variable allocator.
/// </remarks>
sealed class SSAVariableAllocator(GenerationContext context, Method method)
{
    private readonly TypeEmitter _typeEmitter = new(context);
    private readonly ValueSet<Method, Value<Method>> _needsVariable =
        method.CreateSet<Value<Method>>();

    /// <summary>
    /// Analyzes the method and allocates variables for values that need them.
    /// All variables are pre-declared at function scope to avoid C block-scoping
    /// issues when values defined inside control flow blocks (loops, ifs) are
    /// used after the block ends.
    /// </summary>
    public void AllocateVariables()
    {
        // First pass: determine which values need variables
        method.ForEachValue<Value<Method>>(AnalyzeValue);

        // Second pass: pre-declare all variables at function scope
        foreach (var value in _needsVariable)
        {
            // Alloca: skip — EmitAllocaDeclaration in ExpressionEmitter
            // handles its own dual-declaration (storage array + pointer)
            if (value is Alloca)
            {
                context.MarkNeedsVariable(value);
                continue;
            }

            // AddressSpaceCast: use source type when address spaces differ
            // (cross-space casts are stripped at emit time)
            if (value is AddressSpaceCast cast)
            {
                EmitAddressSpaceCastDeclaration(cast);
                continue;
            }

            EmitVariableDeclaration(value);
        }
    }

    /// <summary>
    /// Analyzes a value to determine if it needs a variable.
    /// </summary>
    private void AnalyzeValue<TValue>(TValue value) where TValue : Value<Method>
    {
        // Skip void-typed values
        if (value.Type is VoidType)
            return;

        // Skip parameters and globals (already have names)
        if (value is Parameter or Global)
            return;

        // Use smart heuristics to decide
        if (ShouldAllocateVariable(value))
        {
            _needsVariable.Add(value);
        }
    }

    /// <summary>
    /// Checks if a value should have a variable allocated using use-count heuristics.
    /// </summary>
    private static bool ShouldAllocateVariable(Value value)
    {
        // Phi values MUST have variables (SSA semantics requirement)
        if (value is PhiValue)
            return true;

        // Values with zero uses are dead code
        if (value.Uses.Count == 0)
            return false;

        // BasicBlockValues with non-void results (Load, MethodCall, atomics, etc.)
        // have side effects and must always be assigned to variables — they cannot
        // be safely re-inlined as expressions
        if (value is BasicBlockValue)
            return true;

        // Values with multiple uses need variables to avoid recomputation
        if (value.Uses.Count > 1)
            return true;

        // Single-use values: only allocate if they can't be inlined
        // Alloca needs a variable because it creates an address
        if (value is Alloca)
            return true;

        // Everything else with single use can be inlined
        return false;
    }

    /// <summary>
    /// Emits a variable declaration for a value.
    /// </summary>
    private void EmitVariableDeclaration(Value value)
    {
        var typeName = _typeEmitter.GetTypeName(value.Type);
        var varName = context.GetValueName(value);

        context.WriteLine($"{typeName} {varName};");

        // Mark in context so MethodEmitter knows this needs assignment
        context.MarkNeedsVariable(value);
    }

    /// <summary>
    /// Emits a variable declaration for an AddressSpaceCast, using the
    /// source type when the cast crosses address spaces (since the backend
    /// strips cross-space casts and uses the source expression directly).
    /// </summary>
    private void EmitAddressSpaceCastDeclaration(AddressSpaceCast cast)
    {
        TypeValue declType = cast.Type;
        if (cast.Source.Type is PointerType srcPtr
            && declType is PointerType tgtPtr)
        {
            var srcKey = context.LanguageConfig.GetAddressSpaceKeyword(
                srcPtr.AddressSpace);
            var tgtKey = context.LanguageConfig.GetAddressSpaceKeyword(
                tgtPtr.AddressSpace);
            if (srcKey != tgtKey)
                declType = cast.Source.Type;
        }
        var typeName = _typeEmitter.GetTypeName(declType);
        var varName = context.GetValueName(cast);
        context.WriteLine($"{typeName} {varName};");
        context.MarkNeedsVariable(cast);
    }
}
