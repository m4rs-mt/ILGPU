// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ModuleTransform.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Rewriting;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// A transform for modules.
/// </summary>
sealed class ModuleTransform : ModuleBuilder, IModuleRewriter, ITransform, ITypeRewriter
{
    private InlineList<GlobalValueMap<Value?>> _mappingStack;
    private readonly ValueMap<Module, Method, MethodTransform> _methods;
    private readonly Transformation _parent;
    private readonly int _defaultValueMapCapacity;

    /// <summary>
    /// Creates a new module transform.
    /// </summary>
    /// <param name="parent">The parent transformation.</param>
    /// <param name="properties">The compilation properties to use.</param>
    /// <param name="oldModule">The old version of the module.</param>
    /// <param name="typeInformationManager">The current type information manager.</param>
    public ModuleTransform(
        Transformation parent,
        CompilationProperties properties,
        Module oldModule,
        TypeInformationManager typeInformationManager)
        : base(
            properties,
            oldModule.Generation.NextGeneration(),
            oldModule.Location,
            typeInformationManager)
    {
        _mappingStack = InlineList<GlobalValueMap<Value?>>.Create(capacity: 3);
        _defaultValueMapCapacity = 0;
        foreach (var method in oldModule.Methods)
        {
            _defaultValueMapCapacity = Math.Max(
                _defaultValueMapCapacity, method.NumValues);
        }
        _defaultValueMapCapacity += oldModule.Count;

        _methods = new(oldModule, oldModule.NumMethods);
        _parent = parent;

        OldModule = oldModule;

        // Prepare initial scope
        PushReplacementScope();
    }

    #region Properties

    /// <inheritdoc/>
    public Module OldModule { get; }

    /// <inheritdoc/>
    ModuleBuilder IRewriter<ModuleBuilder>.Builder => this;

    /// <inheritdoc/>
    ModuleBuilder ITypeRewriter.ModuleBuilder => this;

    internal Value? TryApplyBlockValueConverter(ITransform transform, Value value) =>
        _parent.DeferBlockValueMapping
            ? _parent.TryApplyConverter(transform, value)
            : null;

    #endregion

    #region Methods

    /// <summary>
    /// Returns the corresponding method transform.
    /// </summary>
    /// <param name="oldMethod">
    /// The method for which to get a transform for.
    /// </param>
    /// <returns>The method block transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal MethodTransform CreateMethodTransform(Method oldMethod)
    {
        Generation.ValidatePreviousGeneration(oldMethod);
        oldMethod.Assert(oldMethod.Module == OldModule);

        // Create a new method transform
        var methodBuilder = CreateMethodFromOldMethod(
            oldMethod,
            createBuilder: (in ModuleValueInitializer initializer) =>
                new MethodTransform(this, initializer, oldMethod))
                .AsNotNullCast<MethodTransform>();

        _methods.Add(oldMethod, methodBuilder);
        Replace(oldMethod, methodBuilder.Method);

        return _methods[oldMethod];
    }


    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReplacedOrRemoved(Value? oldValue) => TryGetReplaced(oldValue, out _);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryGetReplaced(Value? oldValue, out Value? newValue)
    {
        newValue = oldValue;
        if (oldValue is null)
            return false;

        Generation.ValidateCurrentOrPreviousGeneration(oldValue);
        oldValue.Assert(oldValue.Module == OldModule || oldValue.Module == Module);

        // Iterate through all scopes
        for (int i = _mappingStack.Count - 1; i >= 0; --i)
        {
            var mapping = _mappingStack[i];
            if (mapping.TryGetValue(oldValue, out newValue))
                return true;
        }

        newValue = null;
        return false;
    }

    /// <summary>
    /// Replaces the given value with the given value.
    /// </summary>
    /// <param name="oldValue">The value to be replaced.</param>
    /// <param name="newValue">The value to replace.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Replace(Value oldValue, Value? newValue)
    {
        oldValue.Assert(oldValue.Module == OldModule || oldValue.Module == Module);

        // Get the latest replacement value
        while (newValue is not null && newValue.Generation == Generation)
        {
            if (!TryGetReplaced(newValue, out var nextValue) || nextValue == newValue)
                break;
            newValue = nextValue;
        }

        // Ensure all replaced values point to the same replacement.
        var finalValue = newValue;
        for (Value? current = oldValue; current is not null;)
        {
            bool wasReplaced = TryGetReplaced(current, out var nextValue);
            if (finalValue is not null && nextValue == finalValue) break;

            // Don't overwrite current-gen Parameters reached via chain-following.
            // Parameters are authoritative identities created by the
            // MethodTransform constructor and must not be remapped by chains
            // that happen to pass through them (e.g. old callee param →
            // old kernel param → new kernel param).
            if (current != oldValue
                && current is Parameter
                && current.Generation == Generation)
            {
                break;
            }

            PeekMapping()[current] = newValue;

            if (!wasReplaced) break;
            current = nextValue;
        }
    }

    /// <summary>
    /// Replaces the given value WITHOUT following the replacement chain.
    /// Used during inlining to set parameter → argument mappings without
    /// corrupting existing entries. Normal <see cref="Replace"/> follows
    /// chains (A→B becomes A→C AND B→C), which corrupts B when the same
    /// method is inlined multiple times with different arguments.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReplaceDirect(Value oldValue, Value? newValue) =>
        PeekMapping()[oldValue] = newValue;

    /// <summary>
    /// Removes a replacement entry for the given value from the topmost scope.
    /// Used to clear stale memoization when re-inlining the same method.
    /// </summary>
    internal bool RemoveReplacement(Value value)
    {
        for (int i = _mappingStack.Count - 1; i >= 0; --i)
        {
            if (_mappingStack[i].Remove(value))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Pushes the current replacement scope.
    /// </summary>
    internal void PushReplacementScope() =>
        _mappingStack.Add(new(
            OldModule.Generation,
            _defaultValueMapCapacity,
            new GenerationValidator(Generation, OldModule.Generation)));

    /// <summary>
    /// Pops the current replacement scope to discard replacement information.
    /// </summary>
    internal void PopReplacementScope()
    {
        Debug.Assert(_mappingStack.Count > 1);
        _mappingStack.Pop();
    }

    /// <summary>
    /// Pops the current replacement scope and merges all entries into
    /// the parent scope. Entries already in the parent are overwritten.
    /// Used by the inliner to persist inlining-specific mappings while
    /// preventing cache poisoning from previous inlinings.
    /// </summary>
    internal void MergeAndPopReplacementScope()
    {
        Debug.Assert(_mappingStack.Count > 2);
        var top = _mappingStack.Pop();
        var parent = PeekMapping();
        foreach (var kvp in top)
            parent[kvp.Key] = kvp.Value;
    }

    /// <summary>
    /// Peeks the current replacement mapping.
    /// </summary>
    private GlobalValueMap<Value?> PeekMapping() => _mappingStack.Peek();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Value? Rewrite(Value oldValue)
    {
        Generation.ValidateCurrentOrPreviousGeneration(oldValue);

        // Check for current generation
        if (oldValue.Generation == Generation)
            return oldValue;

        // Check for replaced values
        if (TryGetReplaced(oldValue, out var newValue))
            return newValue;

        oldValue.Assert(oldValue is ModuleValue && oldValue is not MethodValue);
        oldValue.Assert(oldValue.Module == OldModule);

        // Try to rewrite value
        newValue = oldValue switch
        {
            Global global => global.Rewrite(this),
            TypeValue type => _parent.TryApplyConverter(this, type)
                ?? type.Rewrite(this),
            UndefinedValue _ => UndefinedValue,
            Method method => _methods.TryGetValue(method, out var existingTransform)
                ? existingTransform.Method
                : CreatePlainMethodReplacement(method),
            _ => throw new UnreachableException(
                $"Unhandled value type: {oldValue.GetType().Name} " +
                $"(gen={oldValue.Generation}, module={oldValue.Module})")
        };

        // Register value and return
        PeekMapping().Add(oldValue, newValue);
        return newValue;
    }

    /// <summary>
    /// Creates a plain method replacement when a method is encountered during
    /// module-level value rewriting. This happens when a method is referenced
    /// but wasn't processed by CreateMethodTransform (e.g. inlined methods
    /// no longer reachable from the entry point). The resulting method is
    /// marked External so ModuleBuilder.Seal() skips it.
    /// </summary>
    private Method CreatePlainMethodReplacement(Method method)
    {
        var builder = GetOrCreateMethod(method.Declaration.Rewrite(this));
        // Mark as External — this method won't be fully initialized since
        // it's only being created as a reference target (not through the
        // transformation loop's CreateMethodTransform path).
        builder.Method.AddFlags(MethodFlags.External);
        return builder.Method;
    }

    /// <inheritdoc/>
    public T RewriteAs<T>(Value oldValue) where T : Value =>
        Rewrite(oldValue).AsNotNullCast<T>();

    /// <inheritdoc/>
    public TypeValue Rewrite(TypeValue oldType) =>
        Rewrite(oldType as Value).AsNotNullCast<TypeValue>();

    /// <inheritdoc/>
    public FieldSpan Rewrite(StructureType structureType, FieldSpan fieldSpan) =>
        _parent.ComputeSpan(structureType, fieldSpan);

    #endregion
}
