// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Transformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Rewriting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Transformation arguments for all transformations.
/// </summary>
/// <param name="Properties">The properties to use.</param>
/// <param name="TypeInformationManager">The shared type information manager.</param>
/// <param name="Module">The module to transform.</param>
sealed record class TransformationArgs(
    CompilationProperties Properties,
    TypeInformationManager TypeInformationManager,
    Module Module);

/// <summary>
/// Represents a generic transformation.
/// </summary>
/// <param name="args">The transformation args.</param>
abstract partial class Transformation(TransformationArgs args)
{
    #region Type Conversion Utilities

    /// <summary>
    /// Resolves the number of element fields per type instance.
    /// </summary>
    /// <param name="type">The parent type.</param>
    protected virtual int GetNumTypeFields(TypeValue type) =>
        type is StructureType structureType ? structureType.NumFields : 1;

    /// <summary>
    /// Computes a new field span while taking all structure field changes into
    /// account.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="fieldAccess">The source access.</param>
    /// <returns>The target field access.</returns>
    protected internal FieldAccess ComputeAccess(
        TypeValue sourceType,
        FieldAccess fieldAccess) =>
        ComputeSpan(sourceType, new FieldSpan(fieldAccess, 0)).Access;

    /// <summary>
    /// Computes a new field span while taking all structure field changes into
    /// account.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="fieldSpan">The source span.</param>
    /// <returns>The target field span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected internal FieldSpan ComputeSpan(TypeValue sourceType, FieldSpan fieldSpan)
    {
        if (sourceType is not StructureType sourceStructureType)
            return fieldSpan;
        int sourceIndex = fieldSpan.Index;
        int index = 0;
        for (int i = 0; i < sourceIndex; ++i)
        {
            // Check whether we need new offset information
            index += GetNumTypeFields(sourceStructureType[i]);
        }
        // Check whether we have to adapt the field span
        int span = 0;
        for (int i = 0, e = fieldSpan.Span; i < e; ++i)
        {
            // Check whether we need new offset information
            span += GetNumTypeFields(sourceStructureType[i + sourceIndex]);
        }

        return new FieldSpan(index, span);
    }

    #endregion

    #region Mapping

    private readonly Func<ITransform, Value, Value?>?[] _converters =
        new Func<ITransform, Value, Value?>[ValueKinds.NumValueKinds];
    private readonly Func<ITransform, Value, Value?>?[] _classConverters =
        new Func<ITransform, Value, Value?>[ValueKinds.NumValueClasses];

    /// <summary>
    /// Maps a whole class of values to a given converter.
    /// </summary>
    /// <typeparam name="TTransform">The transform type.</typeparam>
    /// <typeparam name="TValueClass">The value class type to transform.</typeparam>
    /// <param name="converter">The converter to use.</param>
    protected void MapValueClass<TTransform, TValueClass>(
        Func<TTransform, TValueClass, Value?> converter)
        where TTransform : IPureValueRewriter
        where TValueClass : Value, IValueClassInformation
    {
        UsesPureValueMapping |= TValueClass.ValueClass == ValueClass.Pure;
        UsesBlockValueMapping |= TValueClass.ValueClass == ValueClass.BasicBlock;
        UsesMethodValueMapping |= TValueClass.ValueClass == ValueClass.Method;
        UsesTypeMapping |= TValueClass.ValueClass == ValueClass.Type;
        UsesGlobalMapping |= TValueClass.ValueClass == ValueClass.Global;

        _classConverters[(int)TValueClass.ValueClass] =
            (rewriter, value) =>
                converter((TTransform)rewriter,
                    value.AsNotNullCast<TValueClass>());
    }

    /// <summary>
    /// Registers a given mapping.
    /// </summary>
    /// <param name="kind">The value kind to register.</param>
    /// <param name="converter">The converter to register for the given kind.</param>
    private void RegisterMapping(
        ValueKind kind,
        Func<ITransform, Value, Value?> converter)
    {
        Debug.Assert(_converters[(int)kind] is null);
        _converters[(int)kind] = converter;
    }

    /// <summary>
    /// Maps pure values of a specific type to a converter.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="converter">The converter to register.</param>
    protected void MapPureValue<TValue>(
        Func<PureValueTransform, TValue, Value?> converter)
        where TValue : PureValue, IValueInformation
    {
        UsesPureValueMapping = true;
        RegisterMapping(
            TValue.ValueKind,
            (rewriter, value) => converter(
                new(rewriter.AsNotNullCast<MethodTransform>()),
                value.AsNotNullCast<TValue>()));
    }

    /// <summary>
    /// Maps basic block values of a specific type to a converter.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="converter">The converter to register.</param>
    protected void MapBasicBlockValue<TValue>(
        Func<BasicBlockTransform, TValue, Value?> converter)
        where TValue : BasicBlockValue, IValueInformation
    {
        UsesBlockValueMapping = true;

        RegisterMapping(
            TValue.ValueKind,
            (rewriter, value) => converter(
                rewriter.AsNotNullCast<BasicBlockTransform>(),
                value.AsNotNullCast<TValue>()));
    }

    /// <summary>
    /// Maps method values of a specific type to a converter.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="converter">The converter to register.</param>
    protected void MapMethodValue<TValue>(
        Func<MethodTransform, TValue, Value?> converter)
        where TValue : MethodValue, IValueInformation
    {
        UsesMethodValueMapping = true;

        RegisterMapping(
            TValue.ValueKind,
            (rewriter, value) => converter(
                rewriter.AsNotNullCast<MethodTransform>(),
                value.AsNotNullCast<TValue>()));
    }

    /// <summary>
    /// Maps module values of a specific type to a converter.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="converter">The converter to register.</param>
    protected void MapModuleValue<TValue>(
        Func<ModuleTransform, TValue, Value?> converter)
        where TValue : ModuleValue, IValueInformation, IValueClassInformation
    {
        UsesTypeMapping |= TValue.ValueClass == ValueClass.Type;
        UsesGlobalMapping |= TValue.ValueClass == ValueClass.Global;
        UsesMethodMapping |= TValue.ValueClass == ValueClass.Method;

        RegisterMapping(
            TValue.ValueKind,
            (rewriter, value) => converter(
                rewriter.AsNotNullCast<ModuleTransform>(),
                value.AsNotNullCast<TValue>()));
    }

    /// <summary>
    /// Returns true if this transformation uses type mappings.
    /// </summary>
    public bool UsesTypeMapping { get; private set; }

    /// <summary>
    /// Returns true if this transformation uses global mappings.
    /// </summary>
    public bool UsesGlobalMapping { get; private set; }

    /// <summary>
    /// Returns true if this transformation uses method mappings.
    /// </summary>
    public bool UsesMethodMapping { get; private set; }

    /// <summary>
    /// Returns true if this transformation uses mappings of method values.
    /// </summary>
    public bool UsesMethodValueMapping { get; private set; }

    /// <summary>
    /// Returns true if this transformation uses mappings of block values.
    /// </summary>
    public bool UsesBlockValueMapping { get; private set; }

    /// <summary>
    /// When true, BBV converters are not invoked during the OnMap phase.
    /// They are instead invoked lazily during DemandRewriteBlock via
    /// <c>RebuildAndMemoize</c>. Set this for transforms whose BBV
    /// converters produce multi-value side effects (stores, barriers)
    /// that must be ordered correctly relative to surrounding values.
    /// </summary>
    internal bool DeferBlockValueMapping { get; set; }

    /// <summary>
    /// Returns true if this transformation uses mappings of pure values.
    /// </summary>
    public bool UsesPureValueMapping { get; private set; }

    #endregion

    /// <summary>
    /// Stores all compilation properties.
    /// </summary>
    protected CompilationProperties Properties { get; } = args.Properties;

    /// <summary>
    /// Returns the underlying type information manager.
    /// </summary>
    protected TypeInformationManager TypeInformationManager { get; } =
        args.TypeInformationManager;

    /// <summary>
    /// Tries to rewrite the given value using the transform provided.
    /// </summary>
    /// <typeparam name="TTransform">The underlying transform type.</typeparam>
    /// <param name="transform">The transform instance.</param>
    /// <param name="value">The value to rewrite.</param>
    /// <param name="rewritten">
    /// The rewritten value (or null in case of replacement).
    /// </param>
    /// <returns>True if the value was rewritten.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool TryRewrite<TTransform>(
        TTransform transform,
        Value value,
        out Value? rewritten)
        where TTransform : ITransform
    {
        // Reset output
        rewritten = null;

        // Check for registered value class converter first
        var classConverter =
            _classConverters.AsSpan().GetItemRef((int)value.ValueClass);
        if (classConverter is not null)
        {
            rewritten = classConverter(transform, value);
            if (rewritten != value)
                return true;
        }

        // Invoke converter if possible
        var converter = _converters.AsSpan().GetItemRef((int)value.ValueKind);
        if (converter is null)
            return false;

        // Invoke converter and check change/s
        rewritten = converter.Invoke(transform, value);
        return rewritten != value;
    }

    /// <summary>
    /// Rewrites the given value using the transform provided.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Rewrite<TTransform>(TTransform transform, Value value)
        where TTransform : ITransform
    {
        if (!TryRewrite(transform, value, out var rewritten))
            return false;

        transform.Replace(value, rewritten);
        return true;
    }

    /// <summary>
    /// Rewrites internal types in the presence of any mapping.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RewriteMapped<T>(ModuleTransform transform, ReadOnlySpan<T> elements)
        where T : Value
    {
        foreach (var element in elements)
            Rewrite(transform, element);
    }

    /// <summary>
    /// Tries to apply a registered ValueKind-specific converter to the given
    /// value. Used by <see cref="ModuleTransform"/> to transform type values
    /// that were not visited during <see cref="RewriteMapped{T}"/> (e.g.,
    /// ViewType instances nested inside StructureType fields that are not
    /// directly in <c>module.Types</c>).
    /// Only checks <see cref="_converters"/> (not <see cref="_classConverters"/>)
    /// to avoid applying method-level converters in a module-level context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Value? TryApplyConverter(ITransform transform, Value value)
    {
        var converter = _converters[(int)value.ValueKind];
        if (converter is null)
            return null;
        var result = converter(transform, value);
        return result != value ? result : null;
    }

    /// <summary>
    /// Transforms the underlying module.
    /// </summary>
    /// <returns>The new module instance.</returns>
    public Module Transform() => TransformInternal(args.Module);

    /// <summary>
    /// Transforms the underlying module.
    /// </summary>
    /// <returns>The new module instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected virtual Module TransformInternal(Module module)
    {
        // Verify input module integrity before transformation
        IRVerifier.Verify(module, $"INPUT to {GetType().Name}");

        var transform = new ModuleTransform(
            this,
            args.Properties,
            module,
            args.TypeInformationManager);
        OnMap(transform);

        // Transform everything
        OnTransform(transform);

        // Phase 1: Create all MethodTransforms upfront. This registers
        // old→new method mappings so that forward references during
        // Phase 2's CompleteRewrite resolve correctly. Without this,
        // when a caller's CompleteRewrite encounters a MethodCall to a
        // [NoInline] callee processed later, ModuleTransform.Rewrite
        // would hit CreatePlainMethodReplacement and mark it External.
        var methodTransforms = new List<(Method OldMethod, MethodTransform Transform)>(
            module.NumMethods);
        foreach (var method in module.MethodsInReversePostOrder)
            methodTransforms.Add((method, transform.CreateMethodTransform(method)));

        // Phase 2: Transform all methods
        foreach ((Method method, MethodTransform methodTransform) in methodTransforms)
        {
            // Push current replacement scope
            transform.PushReplacementScope();

            // Transform the given method
            OnTransform(methodTransform);

            // Rewrite the complete method
            methodTransform.CompleteRewrite(method);

            // Finish method transformation to ensure methods processed next see latest
            // changes and can work with updated IR values
            var newMethod = methodTransform.Seal();

            // Pop current replacement scope
            transform.PopReplacementScope();

            // Update replacement to point to the sealed method
            transform.Replace(method, newMethod);
        }

        // Verify all methods before module seal (ComputeUses would crash on stale refs)
#if DEBUG
        try
        {
#endif
            // Finish rewriting the module
            return transform.Seal(module.EntryPointHandle);
#if DEBUG
        }
        catch (Exception ex) when (ex is not IRVerificationException)
        {
            // Add transformation name context and run verifier for detailed report
            try
            {
                IRVerifier.Verify(transform.Module, $"PRE-SEAL of {GetType().Name}");
            }
            catch (IRVerificationException verifyEx)
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name}] Seal failed. " +
                    $"Verifier found issues:\n{verifyEx.Message}", ex);
            }
            throw new InvalidOperationException(
                $"[{GetType().Name}] Seal failed (verifier passed — " +
                $"issue may be in ComputeUses traversal).", ex);
        }
#endif
    }

    /// <summary>
    /// Pre-transforms the given module to enable pre-hooks.
    /// </summary>
    /// <param name="transform">The module transform to use.</param>
    protected virtual void OnMap(ModuleTransform transform) { }

    /// <summary>
    /// Transforms the given module.
    /// </summary>
    /// <param name="transform">The module transform to use.</param>
    protected virtual void OnTransform(ModuleTransform transform)
    {
        // Map all types
        if (UsesTypeMapping)
            RewriteMapped(transform, args.Module.Types);

        // Map all globals
        if (UsesGlobalMapping)
            RewriteMapped(transform, args.Module.Globals);

        // Map all main methods
        if (UsesMethodMapping)
            RewriteMapped(transform, args.Module.Methods);
    }

    /// <summary>
    /// Transforms the given method.
    /// </summary>
    /// <param name="transform">The method transform to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected virtual void OnTransform(MethodTransform transform)
    {
        // Map all parameters
        if (UsesMethodValueMapping)
        {
            foreach (var param in transform.OldMethod.Parameters)
                Rewrite(transform, param);
        }

        // Map all blocks
        ValueSet<Method, PureValue> visited = UsesPureValueMapping
            ? transform.OldMethod.CreateSet<PureValue>()
            : default;
        if (UsesBlockValueMapping || UsesPureValueMapping)
        {
            void MapPureValue(Value value)
            {
                if (value is not PureValue pureValue || !visited.Add(pureValue))
                    return;

                // Visit children first (bottom-up) so that converters for
                // child values have already run before a parent converter
                // tries to rewrite them via transform.Rewrite(child).
                pureValue.VisitDFS(MapPureValue);
                Rewrite(transform, pureValue);
            }

            foreach (var block in transform.OldMethod.Blocks)
            {
                if (transform.TryGetReplaced(block, out var newBlock) &&
                    transform.GetBasicBlockTransform(block).BasicBlock != newBlock)
                {
                    continue;
                }

                // Map block itself
                var blockTransform = transform.GetBasicBlockTransform(block);
                if (UsesMethodValueMapping)
                    Rewrite(blockTransform, block);

                // Check for basic block values or pure values
                if (UsesBlockValueMapping || UsesPureValueMapping)
                {
                    foreach (var (bbValue, _) in block.BasicBlockValues)
                    {
                        if (!DeferBlockValueMapping)
                            Rewrite(blockTransform, bbValue);

                        // Map all pure values if necessary
                        if (UsesPureValueMapping)
                        {
                            foreach (var childValue in bbValue.Values)
                                MapPureValue(childValue);
                        }
                    }

                    // Also visit pure values reachable only via the termination
                    // condition (e.g. a CompareValue used by br.cond). These are NOT
                    // in the block.Values chain and would otherwise be missed.
                    if (UsesPureValueMapping && block.TerminationValue is { } termValue)
                        MapPureValue(termValue);
                }
            }
        }
    }
}

/// <summary>
/// Represents an abstract transformation based on an intermediate value computed and
/// stored for each value.
/// </summary>
/// <typeparam name="TIntermediate">The value type of the intermediate value.</typeparam>
/// <param name="args">The transformation args.</param>
abstract class Transformation<TIntermediate>(TransformationArgs args) :
    Transformation(args)
{
    private readonly ValueMap<Module, Method, TIntermediate> _methodMapping =
        args.Module.CreateMap<Method, TIntermediate>();

    /// <summary>
    /// Creates a new intermediate value for the given method.
    /// </summary>
    /// <param name="transform">The parent module transform.</param>
    /// <param name="method">The current method.</param>
    /// <returns>The created intermediate value instance.</returns>
    protected abstract TIntermediate CreateIntermediate(
        ModuleTransform transform,
        Method method);

    /// <summary>
    /// Returns the intermediate value for the given method.
    /// </summary>
    /// <param name="method">The method to get the intermediate for.</param>
    protected TIntermediate GetIntermediate(Method method) => _methodMapping[method];

    /// <summary>
    /// Tries to get the intermediate value for the given method.
    /// Returns false when the method is not in the analysis map
    /// (e.g., external/intrinsic methods).
    /// </summary>
    protected bool TryGetIntermediate(
        Method method,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)]
        out TIntermediate intermediate) =>
        _methodMapping.TryGetValue(method, out intermediate);

    /// <summary>
    /// Gets the intermediate transformation information for the given transform.
    /// </summary>
    /// <param name="pureValueTransform">
    /// The transform to get the intermediate for.
    /// </param>
    protected TIntermediate GetIntermediate(PureValueTransform pureValueTransform) =>
        GetIntermediate(pureValueTransform.MethodTransform);

    /// <summary>
    /// Gets the intermediate transformation information for the given transform.
    /// </summary>
    /// <param name="basicBlockTransform">
    /// The transform to get the intermediate for.
    /// </param>
    protected TIntermediate GetIntermediate(BasicBlockTransform basicBlockTransform) =>
        GetIntermediate(basicBlockTransform.OldMethod);

    /// <summary>
    /// Gets the intermediate transformation information for the given transform.
    /// </summary>
    /// <param name="methodTransform">
    /// The transform to get the intermediate for.
    /// </param>
    protected TIntermediate GetIntermediate(MethodTransform methodTransform) =>
        GetIntermediate(methodTransform.OldMethod);

    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        // Prepare all intermediate entries
        foreach (var method in transform.OldModule.Methods)
            _methodMapping.Add(method, CreateIntermediate(transform, method));

        // Prepare mapping
        base.OnMap(transform);
    }
}
