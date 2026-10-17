// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InferAddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using ILGPUC.IR.Rewriting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Infers and optimizes address spaces using the PointerAddressSpaces analysis.
/// Removes redundant address space casts and specializes phi values, allocations
/// and method parameters to use more specific address spaces when possible.
/// </summary>
/// <remarks>
/// Changing the type of a value changes the types of all address computations
/// derived from it. A value is only retyped when every (transitive) user either
/// follows the new type or receives a cast back to the type it expects (phi
/// arguments, call arguments, selected values, stored values and returned
/// values). Otherwise,
/// the value keeps its type.
/// </remarks>
/// <param name="args">The transformation args.</param>
sealed class InferAddressSpaces(TransformationArgs args) : Transformation(args)
{
    private readonly AddressSpaceResults _results = PointerAddressSpaces.AnalyzeModule(
        args.Module.EntryPoint,
        PointerAddressSpaces.AnalysisFlags.IgnoreGenericAddressSpace);

    /// <summary>
    /// Caches whether a value of the input module may change its type.
    /// </summary>
    private readonly Dictionary<ValueId, bool> _canRetype = [];

    /// <summary>
    /// The generation of the input module.
    /// </summary>
    private readonly Generation _generation = args.Module.Generation;

    /// <summary>
    /// Specializes a type based on detailed analysis value information.
    /// For scalars, uses unified address space. For structures, specializes
    /// each field based on per-field analysis data.
    /// </summary>
    private static TypeValue? SpecializeType(
        ModuleBuilder moduleBuilder,
        TypeValue type,
        AnalysisValue<AddressSpaceInfo> analysisValue) =>
        moduleBuilder.TrySpecializeAddressSpace(
            type,
            access => analysisValue.IsScalar
                ? analysisValue.Data.UnifiedAddressSpace
                : analysisValue[access.Index].UnifiedAddressSpace);

    /// <summary>
    /// Converts a rewritten value into the given target type, which differs
    /// from the value's type in address spaces only (possibly nested in the
    /// element types of pointers and views or in structure fields).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value Coerce(
        PureValueBuilder builder,
        Location location,
        Value value,
        TypeValue targetType)
    {
        if (value.Type.Equals(targetType))
            return value;

        switch (value.Type)
        {
            case PointerType sourcePointer when targetType is PointerType target:
                {
                    var result = sourcePointer.ElementType.Equals(target.ElementType)
                        ? value
                        : builder.CreatePointerCast(
                            location,
                            value,
                            target.ElementType);
                    return builder.CreateAddressSpaceCast(
                        location,
                        result,
                        target.AddressSpace);
                }
            case ViewType sourceView when targetType is ViewType target:
                {
                    var result = sourceView.ElementType.Equals(target.ElementType)
                        ? value
                        : builder.CreateViewCast(
                            location,
                            value,
                            target.ElementType);
                    return builder.CreateAddressSpaceCast(
                        location,
                        result,
                        target.AddressSpace);
                }
            case StructureType sourceStructure
                when targetType is StructureType target &&
                sourceStructure.NumFields == target.NumFields:
                {
                    var instance = builder.CreateStructure(location, target);
                    for (int i = 0; i < target.NumFields; ++i)
                    {
                        var field = builder.CreateGetField(
                            location,
                            value,
                            new FieldSpan(i));
                        instance.Add(Coerce(builder, location, field!, target[i]));
                    }
                    return instance.Seal();
                }
            default:
                throw location.GetInvalidOperationException(
                    $"Cannot convert {value} into {targetType}");
        }
    }

    /// <summary>
    /// Returns true if the given value of the input module may change its type
    /// without breaking any of its users.
    /// </summary>
    /// <remarks>
    /// Address computations derived from the value follow its type and are
    /// checked recursively. Phi arguments, stored values and returned values
    /// are converted back by this pass, call arguments of methods with an
    /// implementation are cast by <see cref="RewriteMethodCall"/> and the
    /// selected values of predicates by <see cref="RewritePredicate"/>. All
    /// other users (e.g. structure values or comparisons) require the original
    /// type.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool CanRetype(Value value)
    {
        if (!value.Type.HasFlags(TypeFlags.AddressSpaceDependent))
            return true;
        if (_canRetype.TryGetValue(value.Id, out bool result))
            return result;

        // Optimistic assumption for cycles (which pass through phis only)
        _canRetype[value.Id] = true;
        result = true;
        foreach (var use in value.Uses)
        {
            if (!CanRetypeUse(use))
            {
                result = false;
                break;
            }
        }
        _canRetype[value.Id] = result;
        return result;
    }

    /// <summary>
    /// Returns true if the given use tolerates a retyped operand.
    /// </summary>
    private bool CanRetypeUse(Use use) => use.Target switch
    {
        // Converted back by this pass (or cast by RewriteMethodCall)
        Method or PhiValue or Store => true,
        MethodCall call => use.Index > 0 && call.Target.HasImplementation,
        BasicBlock block => block.TerminationKind == BlockTerminationKind.Return,
        Predicate => use.Index > 0 && CanRetype(use.Target),

        // Users whose results follow the type of their source
        Load or
        AtomicValue or
        AddressSpaceCast or
        PointerCast or
        ViewCast or
        LoadElementAddress or
        LoadFieldAddress or
        NewView or
        SubView or
        BaseAlignOperationValue or
        GetField or
        SetField => use.Index == 0 && CanRetype(use.Target),

        // Users with primitive results
        GetViewLength or PointerAsIntCast => true,
        _ => false,
    };

    /// <summary>
    /// Analyzes the module to determine address space usage patterns.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        MapBasicBlockValue<Alloca>(RewriteAlloca);
        MapBasicBlockValue<MethodCall>(RewriteMethodCall);
        MapBasicBlockValue<Store>(RewriteStore);

        MapMethodValue<Parameter>(RewriteParameter);
        MapPureValue<Predicate>(RewritePredicate);
    }

    /// <summary>
    /// Removes redundant casts, specializes all phi values and converts phi
    /// arguments and return values to the types their users expect.
    /// </summary>
    protected override void OnTransform(MethodTransform transform)
    {
        var oldMethod = transform.OldMethod;

        // Remove redundant casts before the converters of their users rewrite
        // them (users are converted before their pure operands)
        oldMethod.ForEachValue<AddressSpaceCast>(cast =>
        {
            if (IsRedundant(cast))
                transform.Replace(cast, cast.Source);
        });

        // Replace all phis that may carry addresses before any rewrite can
        // reach them. Otherwise, a rewrite might create a phi with the old
        // type whose (retyped) arguments do not match the phi any more.
        var phis = new List<(PhiValue Phi, PhiValue.Builder Builder)>();
        foreach (var block in oldMethod.Blocks)
        {
            foreach (PhiValue phi in block.PhiValues)
            {
                if (!phi.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                    continue;

                var phiType = transform.Rewrite(phi.Type);
                if (CanRetype(phi) &&
                    SpecializeType(
                        transform.ModuleBuilder,
                        phiType,
                        _results.GetAnalysisValue(phi)) is { } specializedType)
                {
                    phiType = specializedType;
                }

                var builder = transform.GetBasicBlockTransform(block).CreatePhi(
                    phi.Location,
                    phiType,
                    phi.NumArguments);
                transform.Replace(phi, builder.PhiValue);
                phis.Add((phi, builder));
            }
        }

        base.OnTransform(transform);

        // Wire all phi arguments
        foreach (var (phi, builder) in phis)
        {
            var phiType = builder.PhiValue.Type;
            for (int i = 0; i < phi.NumArguments; ++i)
            {
                if (transform.Rewrite(phi.Sources[i]) is not BasicBlock source ||
                    transform.Rewrite(phi.Arguments[i]) is not Value argument)
                {
                    continue;
                }
                builder.AddArgument(
                    source,
                    Coerce(transform, phi.Location, argument, phiType));
            }
            builder.Seal();
        }

        // Convert all returned values to the (unchanged) return type
        if (!oldMethod.Type.HasFlags(TypeFlags.AddressSpaceDependent))
            return;
        var returnType = transform.Rewrite(oldMethod.Type);
        foreach (var block in oldMethod.Blocks)
        {
            if (!ReturnView.TryCreate(block, out var returnView) ||
                transform.Rewrite(returnView.ReturnValue) is not Value returnValue ||
                returnValue.Type.Equals(returnType))
            {
                continue;
            }
            var blockTransform = transform.GetBasicBlockTransform(block);
            blockTransform.CreateReturnTermination(Coerce(
                blockTransform,
                returnView.Location,
                returnValue,
                returnType));
        }
    }

    /// <summary>
    /// Returns true if the given address space cast is redundant.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool IsRedundant(AddressSpaceCast cast)
    {
        // Check for trivially redundant casts (source and target are the same)
        if (cast.SourceType.AddressSpace == cast.TargetAddressSpace)
            return true;

        // If all sources point into a specific space (Local, Shared, Global)
        // and the cast targets Generic, the cast is unnecessary as long as all
        // users of the cast accept the specific type. This eliminates spurious
        // Local→Generic casts on alloca-derived pointers.
        return cast.TargetAddressSpace == MemoryAddressSpace.Generic &&
            _results.GetUnifiedAddressSpace(cast.Source) !=
                MemoryAddressSpace.Generic &&
            CanRetype(cast);
    }

    /// <summary>
    /// Rewrites alloca values to use more specific address spaces when the
    /// values stored into them have been specialized. Examines Store uses of
    /// the alloca and merges their analysis results to determine the best
    /// element type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Alloca RewriteAlloca(BasicBlockTransform transform, Alloca alloca)
    {
        var allocType = alloca.AllocType;

        // Only specialize types that have address space dependencies
        if (!allocType.HasFlags(TypeFlags.AddressSpaceDependent))
            return alloca;

        // Determine address space from values stored into this alloca
        AnalysisValue<AddressSpaceInfo>? mergedAnalysis = null;
        foreach (var use in alloca.Uses)
        {
            if (use.Target is Store store && store.Target == alloca)
            {
                var valueAnalysis = _results.GetAnalysisValue(store.Value);
                mergedAnalysis = mergedAnalysis == null
                    ? valueAnalysis
                    : Merge(mergedAnalysis.Value, valueAnalysis);
            }
        }

        if (mergedAnalysis == null || !CanRetype(alloca))
            return alloca;

        // Try to specialize the alloca's element type
        var newAllocType = transform.Rewrite(allocType);
        var specializedType = SpecializeType(
            transform.ModuleBuilder,
            newAllocType,
            mergedAnalysis.Value);

        if (specializedType == null)
            return alloca;

        // Create new alloca with specialized type
        if (alloca.IsStaticAllocation(out var arrayLength))
            return transform.CreateAlloca(alloca.Location, specializedType, arrayLength)
                ?? alloca;

        return transform.CreateAlloca(
            alloca.Location,
            specializedType,
            alloca.ArrayLengthValue)
            ?? alloca;
    }

    /// <summary>
    /// Merges two analysis values by combining their address space information.
    /// </summary>
    private static AnalysisValue<AddressSpaceInfo> Merge(
        AnalysisValue<AddressSpaceInfo> first,
        AnalysisValue<AddressSpaceInfo> second)
    {
        if (first.IsScalar)
            return new AnalysisValue<AddressSpaceInfo>(
                AddressSpaceInfo.Merge(first.Data, second.Data));

        var fieldData = new AddressSpaceInfo[first.NumFields];
        for (int i = 0; i < first.NumFields; i++)
            fieldData[i] = AddressSpaceInfo.Merge(first[i], second[i]);

        return new AnalysisValue<AddressSpaceInfo>(
            AddressSpaceInfo.Merge(first.Data, second.Data),
            fieldData);
    }

    /// <summary>
    /// Rewrites stores to convert the stored value into the element type of the
    /// (rewritten) target address.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value RewriteStore(BasicBlockTransform transform, Store store)
    {
        if (!store.Value.Type.HasFlags(TypeFlags.AddressSpaceDependent))
            return store;

        var target = transform.Rewrite(store.Target);
        var value = transform.Rewrite(store.Value);
        if (target?.Type is not PointerType targetType || value is null ||
            value.Type.Equals(targetType.ElementType))
        {
            return store;
        }

        return transform.CreateStore(
            store.Location,
            target,
            Coerce(transform, store.Location, value, targetType.ElementType))
            ?? store;
    }

    /// <summary>
    /// Rewrites predicates whose selected values have been retyped differently
    /// to convert both values into a common type: the type of the predicate,
    /// specialized if both values point into the same address space.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value RewritePredicate(PureValueTransform transform, Predicate predicate)
    {
        if (!predicate.Type.HasFlags(TypeFlags.AddressSpaceDependent))
            return predicate;

        var trueValue = transform.Rewrite(predicate.TrueValue);
        var falseValue = transform.Rewrite(predicate.FalseValue);
        if (trueValue is null || falseValue is null ||
            trueValue.Type.Equals(falseValue.Type))
        {
            return predicate;
        }

        var type = SpecializeType(
            transform.ModuleBuilder,
            transform.Rewrite(predicate.Type),
            _results.GetAnalysisValue(predicate))
            ?? transform.Rewrite(predicate.Type);
        return transform.CreatePredicate(
            predicate.Location,
            transform.Rewrite(predicate.Condition),
            Coerce(transform, predicate.Location, trueValue, type),
            Coerce(transform, predicate.Location, falseValue, type))
            ?? predicate;
    }

    /// <summary>
    /// Checks whether all types in the type graph of <paramref name="type"/>
    /// belong to the expected generation. Types from incompatible generations
    /// cannot be safely rewritten.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool CanRewriteParameterType(TypeValue? type, Generation expected)
    {
        if (type is null || type.Count == 0 && type is PointerType or ViewType)
            return false;
        if (type.Generation != expected)
            return false;
        return type switch
        {
            PointerType pt => CanRewriteParameterType(pt.ElementType, expected),
            ViewType vt => CanRewriteParameterType(vt.ElementType, expected),
            ArrayType at => CanRewriteParameterType(at.ElementType, expected),
            StructureType st => CanRewriteStructure(st, expected),
            _ => true
        };

        static bool CanRewriteStructure(StructureType st, Generation gen)
        {
            foreach (var field in st.DirectFields)
            {
                if (!CanRewriteParameterType(field, gen))
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Returns the specialized type of the given parameter, or null if the
    /// parameter keeps its type.
    /// </summary>
    private TypeValue? GetSpecializedParameterType(
        ITypeRewriter typeRewriter,
        Parameter parameter)
    {
        // Skip parameters whose type graph cannot be safely rewritten and
        // parameters with users that require the original type
        if (!CanRewriteParameterType(parameter.Type, _generation) ||
            !CanRetype(parameter))
        {
            return null;
        }

        // Try to specialize the type based on analysis results (including
        // per-field data)
        return SpecializeType(
            typeRewriter.ModuleBuilder,
            typeRewriter.Rewrite(parameter.Type),
            _results.GetAnalysisValue(parameter));
    }

    /// <summary>
    /// Rewrites parameter values to use more specific address spaces when possible.
    /// Handles both scalar address space types and structure types with per-field
    /// address space specialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Parameter RewriteParameter(MethodTransform transform, Parameter parameter)
    {
        var specializedType = GetSpecializedParameterType(transform, parameter);
        if (specializedType is null)
            return parameter; // No specialization needed

        // Find the constructor-created parameter for this old parameter
        // and replace it in-place to preserve parameter ordering. When the
        // MethodTransform constructor pre-creates parameters, CreateParameter
        // appends them in order. If we use CreateParameter here, the
        // replacement parameter would be appended at the END, and the
        // original (now-replaced) constructor parameter gets filtered at
        // seal time, causing parameters to be reordered — which breaks
        // callers whose MethodCall arguments are still in the original order.
        if (transform.TryGetReplaced(parameter, out var existingValue)
            && existingValue is Parameter existingParam)
        {
            var newParameter = transform.ReplaceParameter(
                existingParam,
                specializedType,
                parameter.Name);
            return newParameter;
        }

        // Fallback: create new parameter (e.g. parameter wasn't pre-created)
        return transform.CreateParameter(specializedType, parameter.Name);
    }

    /// <summary>
    /// Rewrites method calls to convert all arguments into the (possibly
    /// specialized) parameter types of the target method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private MethodCall RewriteMethodCall(BasicBlockTransform transform, MethodCall call)
    {
        // External methods have no analyzed parameters — skip rewriting
        var target = call.Target;
        if (target.IsExternal)
            return call;

        // Note: call.Count includes the target method at index 0 (Values[0] = target,
        // Values[1..] = arguments). Use call.Arguments to iterate only arguments.
        var arguments = new Value?[call.Arguments.Length];
        bool anyChanges = false;
        for (int i = 0; i < arguments.Length; ++i)
        {
            var arg = call.Arguments[i];
            if (!arg.Type.HasFlags(TypeFlags.AddressSpaceDependent) ||
                transform.Rewrite(arg) is not Value newArg)
            {
                continue;
            }

            var param = target.Parameters[i];
            var paramType = GetSpecializedParameterType(transform, param)
                ?? transform.Rewrite(param.Type);
            if (newArg.Type.Equals(paramType))
                continue;

            // Pointers whose element types differ (e.g. a local copy of a
            // structure holding a global view) keep their element type and
            // only change their own address space, as reinterpreting the
            // element type prevents simplifying the copy after inlining
            arguments[i] =
                newArg.Type is PointerType argPointer &&
                paramType is PointerType paramPointer &&
                !argPointer.ElementType.Equals(paramPointer.ElementType)
                ? transform.CreateAddressSpaceCast(
                    call.Location,
                    newArg,
                    paramPointer.AddressSpace)
                : Coerce(transform, call.Location, newArg, paramType);
            anyChanges = true;
        }

        if (!anyChanges)
            return call;

        // The standard rewrite is bypassed when returning a new value: rewrite
        // the target and all remaining arguments here
        var callBuilder = transform.CreateCall(
            call.Location,
            transform.RewriteAs<Method>(target));
        for (int i = 0; i < arguments.Length; ++i)
            callBuilder.Add(arguments[i] ?? transform.Rewrite(call.Arguments[i]));
        return callBuilder.Seal();
    }
}
