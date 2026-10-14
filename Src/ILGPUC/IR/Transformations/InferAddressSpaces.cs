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
/// arguments, call arguments, stored values and returned values). Otherwise,
/// the value keeps its type.
/// </remarks>
sealed class InferAddressSpaces : Transformation
{
    /// <summary>
    /// The address spaces of all values of the input module.
    /// </summary>
    private readonly AddressSpaceResults _results;

    /// <summary>
    /// The generation of the input module.
    /// </summary>
    private readonly Generation _generation;

    /// <summary>
    /// Caches whether a value of the input module may change its type.
    /// </summary>
    private readonly Dictionary<ValueId, bool> _canRetype = [];

    /// <summary>
    /// Analyzes the address spaces of the given module. Loads from allocations
    /// that may change their type read the values stored into them: such an
    /// allocation takes the address spaces of these values (see
    /// <see cref="RewriteAlloca"/>), and so do its loads.
    /// </summary>
    /// <param name="args">The transformation args.</param>
    public InferAddressSpaces(TransformationArgs args)
        : base(args)
    {
        _generation = args.Module.Generation;
        _results = PointerAddressSpaces.AnalyzeModule(
            args.Module.EntryPoint,
            PointerAddressSpaces.AnalysisFlags.IgnoreGenericAddressSpace,
            CanRetype);
    }

    /// <summary>
    /// Caches the specialized types of all method parameters (null if a
    /// parameter keeps its type).
    /// </summary>
    private readonly Dictionary<ValueId, TypeValue?> _parameterTypes = [];

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

        // Null pointers (e.g. delegate handles of non-capturing lambdas merged
        // with closures in local memory) are null in every address space
        if (value is NullValue && targetType is PointerType)
            return builder.CreateNull(location, targetType);

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
    /// checked recursively. Phi arguments, call arguments of methods with an
    /// implementation, stored values and returned values are converted back by
    /// this pass, and so are the selected values of predicates and the operands
    /// of field insertions whose structure and inserted value do not match any
    /// more. Comparisons with the null pointer give the null pointer the type of
    /// the other operand, and comparisons of two pointers whose types differ
    /// compare them as generic pointers. Structure values take the types of
    /// their fields. All other users require the original type.
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
        // Converted back by this pass
        Method or PhiValue or Store => true,
        MethodCall call => use.Index > 0 && call.Target.HasImplementation,
        BasicBlock block => block.TerminationKind == BlockTerminationKind.Return,
        Predicate => use.Index > 0 && CanRetype(use.Target),
        CompareValue => true,

        // Structures whose fields follow the types of their values
        StructureValue => CanRetype(use.Target),

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
        GetField => use.Index == 0 && CanRetype(use.Target),

        // Field insertions follow the type of their structure; a structure and
        // an inserted value that do not match are converted back
        SetField => CanRetype(use.Target),

        // Users with primitive results
        GetViewLength or PointerAsIntCast => true,
        _ => false,
    };

    /// <summary>
    /// Returns true if the given value may change its type and if this is
    /// worth it: values that are only stored or returned would be converted
    /// back right away.
    /// </summary>
    private bool ShouldRetype(Value value)
    {
        if (!CanRetype(value))
            return false;
        foreach (var use in value.Uses)
        {
            switch (use.Target)
            {
                case Method:
                case Store when use.Index == 1:
                case BasicBlock:
                    break;
                default:
                    return true;
            }
        }
        return false;
    }

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
        MapPureValue<SetField>(RewriteSetField);
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
                if (ShouldRetype(phi) &&
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
            _results.GetUnifiedAddressSpace(cast.Source) != MemoryAddressSpace.Generic &&
            ShouldRetype(cast);
    }

    /// <summary>
    /// Rewrites alloca values to use more specific address spaces when the
    /// values stored into them have been specialized. Examines all stores into
    /// the alloca, including stores through addresses derived from it (such as
    /// field addresses or the address of a pointer local taken by
    /// <c>int** pp = &amp;q</c>), and merges their analysis results field by
    /// field to determine the best element type. Values derived from loads of
    /// the alloca itself (such as <c>h.P++</c> or <c>a = cond ? a : b</c> in a
    /// loop) point into the address spaces of the values stored into it, since
    /// the analysis tracks the loads of allocas that may change their type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Alloca RewriteAlloca(BasicBlockTransform transform, Alloca alloca)
    {
        var allocType = alloca.AllocType;

        // Only specialize types that have address space dependencies
        if (!allocType.HasFlags(TypeFlags.AddressSpaceDependent))
            return alloca;

        // Determine address space from values stored into this alloca. All
        // writes must be stores through addresses derived from the alloca;
        // writes through escaping addresses could store anything.
        var stores = new List<(Store Store, int Field)>();
        if (!PointerAddressSpaces.TryCollectAccesses(alloca, stores) ||
            stores.Count < 1 ||
            !CanRetype(alloca))
        {
            return alloca;
        }

        var fields = new AddressSpaceInfo[
            allocType is StructureType structureType ? structureType.NumFields : 1];
        foreach (var (store, field) in stores)
            MergeStore(store, field, fields);

        // Try to specialize the alloca's element type
        var data = new AddressSpaceInfo();
        foreach (var field in fields)
            data = AddressSpaceInfo.Merge(data, field);
        var newAllocType = transform.Rewrite(allocType);
        var specializedType = SpecializeType(
            transform.ModuleBuilder,
            newAllocType,
            allocType is StructureType
                ? new AnalysisValue<AddressSpaceInfo>(data, fields)
                : new AnalysisValue<AddressSpaceInfo>(data));

        if (specializedType == null)
            return alloca;

        // Create new alloca with specialized type
        if (alloca.IsStaticAllocation(out var arrayLength))
            return transform.CreateAlloca(alloca.Location, specializedType, arrayLength)
                ?? alloca;

        if (alloca.ArrayLengthValue is not { } arrayLengthValue)
            return alloca;
        return transform.CreateAlloca(
            alloca.Location,
            specializedType,
            transform.Rewrite(arrayLengthValue))
            ?? alloca;
    }

    /// <summary>
    /// Merges the analysis result of the value of the given store into the
    /// fields of the allocated type it writes, starting at the given field.
    /// </summary>
    private void MergeStore(Store store, int fieldIndex, AddressSpaceInfo[] fields)
    {
        var value = _results.GetAnalysisValue(store.Value);
        int numFields = PointerAddressSpaces.GetNumFields(store.Value.Type);
        for (int i = 0; i < numFields; ++i)
        {
            fields[fieldIndex + i] = AddressSpaceInfo.Merge(
                fields[fieldIndex + i],
                PointerAddressSpaces.GetField(value, i));
        }
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

        var target = RewriteOperand(transform, store.Target);
        var value = RewriteOperand(transform, store.Value);
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
    /// Rewrites field insertions into retyped structures (see
    /// <see cref="ConvertSetField{TTransform}(TTransform, SetField)"/>).
    /// </summary>
    private static Value RewriteSetField(
        PureValueTransform transform,
        SetField setField) =>
        ConvertSetField(transform, setField) ?? setField;

    /// <summary>
    /// Converts a field insertion whose retyped structure and inserted value do
    /// not match, e.g. the address of a local variable assigned to the pointer
    /// field of a retyped structure parameter. Both are converted back to the
    /// type of the field insertion, which its users follow or convert back.
    /// Returns null if the standard rewrite applies.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? ConvertSetField<TTransform>(
        TTransform transform,
        SetField setField)
        where TTransform : PureValueBuilder, IRewriter, ITypeRewriter
    {
        // Field insertions used by calls and stores are converted first
        if (transform.TryGetReplaced(setField, out var replaced))
            return replaced;
        if (!setField.Value.Type.HasFlags(TypeFlags.AddressSpaceDependent) ||
            transform.Rewrite(setField.Source) is not Value source ||
            transform.Rewrite(setField.Value) is not Value value)
        {
            return null;
        }

        var fieldSpan = transform.Rewrite(setField.StructureType, setField.FieldSpan);
        var fieldType = source.GetTypeAs<StructureType>().Get(
            transform.ModuleBuilder,
            fieldSpan);
        if (fieldType.Equals(value.Type))
            return null;

        var type = transform.Rewrite(setField.Type).As<StructureType>();
        return transform.CreateSetField(
            setField.Location,
            Coerce(transform, setField.Location, source, type),
            fieldSpan,
            Coerce(
                transform,
                setField.Location,
                value,
                type.Get(transform.ModuleBuilder, fieldSpan)));
    }

    /// <summary>
    /// Rewrites a value used by a call or a store. Calls and stores are
    /// rewritten before the pure values they use are mapped, so the field
    /// insertions among these values are converted first.
    /// </summary>
    private static Value? RewriteOperand(BasicBlockTransform transform, Value value)
    {
        ConvertSetFields(transform, value, []);
        return transform.Rewrite(value);
    }

    /// <summary>
    /// Converts the field insertions among the given value and its operands
    /// that carry addresses (see
    /// <see cref="ConvertSetField{TTransform}(TTransform, SetField)"/>).
    /// </summary>
    private static void ConvertSetFields(
        BasicBlockTransform transform,
        Value value,
        HashSet<Value> visited)
    {
        if (value is not PureValue ||
            !value.Type.HasFlags(TypeFlags.AddressSpaceDependent) ||
            !visited.Add(value))
        {
            return;
        }

        foreach (var operand in value.Values)
            ConvertSetFields(transform, operand, visited);
        if (value is SetField setField &&
            ConvertSetField(transform, setField) is Value converted)
        {
            transform.Replace(setField, converted);
        }
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
    /// parameter keeps its type. Parameters and the arguments of all calls
    /// must agree on this type.
    /// </summary>
    private TypeValue? GetSpecializedParameterType(
        ITypeRewriter typeRewriter,
        Parameter parameter)
    {
        if (_parameterTypes.TryGetValue(parameter.Id, out var result))
            return result;

        result = null;
        if (CanRewriteParameterType(parameter.Type, _generation) &&
            ShouldRetype(parameter))
        {
            result = SpecializeType(
                typeRewriter.ModuleBuilder,
                typeRewriter.Rewrite(parameter.Type),
                _results.GetAnalysisValue(parameter));
        }
        _parameterTypes[parameter.Id] = result;
        return result;
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
        if (!target.HasImplementation)
            return call;

        var arguments = new Value?[call.Arguments.Length];
        bool anyChanges = false;
        for (int i = 0; i < arguments.Length; ++i)
        {
            var arg = call.Arguments[i];
            if (!arg.Type.HasFlags(TypeFlags.AddressSpaceDependent) ||
                RewriteOperand(transform, arg) is not Value newArg)
            {
                continue;
            }

            var param = target.Parameters[i];
            var paramType = GetSpecializedParameterType(transform, param)
                ?? transform.Rewrite(param.Type);
            if (newArg.Type.Equals(paramType))
                continue;

            arguments[i] = Coerce(transform, call.Location, newArg, paramType);
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
