// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
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
using ILGPUC.IR.Rewriting;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Infers and optimizes address spaces using the PointerAddressSpaces analysis.
/// Removes redundant address space casts and specializes phi values and method
/// calls to use more specific address spaces when possible.
/// </summary>
/// <param name="args">The transformation args.</param>
sealed class InferAddressSpaces(TransformationArgs args) : Transformation(args)
{
    private readonly AddressSpaceResults _results = PointerAddressSpaces.AnalyzeModule(
        args.Module.EntryPoint,
        PointerAddressSpaces.AnalysisFlags.IgnoreGenericAddressSpace);

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
    /// Creates casts for a value to match a specialized type.
    /// For structures, creates field-by-field casts where needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value CreateSpecializedValue(
        BasicBlockTransform transform,
        Location location,
        Value sourceValue,
        TypeValue targetType)
    {
        // Simple case: scalar address space cast
        if (sourceValue.Type is AddressSpaceType &&
            targetType is AddressSpaceType targetAddrType)
        {
            return transform.CreateAddressSpaceCast(
                location,
                sourceValue,
                targetAddrType.AddressSpace);
        }

        // Structure case: extract each field and cast if needed
        if (sourceValue.Type is StructureType sourceStruct &&
            targetType is StructureType targetStruct)
        {
            var builder = transform.CreateStructure(location, targetStruct);

            for (int i = 0; i < sourceStruct.NumFields; i++)
            {
                var fieldAccess = new FieldAccess(i);
                var fieldValue = transform.CreateGetField(
                    location,
                    sourceValue,
                    new FieldSpan(fieldAccess));
                var sourceFieldType = sourceStruct[fieldAccess];
                var targetFieldType = targetStruct[fieldAccess];

                // Cast field if address space types differ
                if (!sourceFieldType.Equals(targetFieldType) &&
                    sourceFieldType is AddressSpaceType &&
                    targetFieldType is AddressSpaceType targetFieldAddrType)
                {
                    var castedField = transform.CreateAddressSpaceCast(
                        location,
                        fieldValue,
                        targetFieldAddrType.AddressSpace);
                    builder.Add(castedField);
                }
                else
                {
                    builder.Add(fieldValue);
                }
            }

            return builder.Seal();
        }

        return sourceValue;
    }

    /// <summary>
    /// Analyzes the module to determine address space usage patterns.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        MapPureValue<AddressSpaceCast>(RewriteAddressSpaceCast);
        MapPureValue<PointerCast>(RewritePointerCast);

        MapBasicBlockValue<Alloca>(RewriteAlloca);
        MapBasicBlockValue<PhiValue>(RewritePhiValue);
        MapBasicBlockValue<MethodCall>(RewriteMethodCall);

        MapMethodValue<Parameter>(RewriteParameter);
    }

    /// <summary>
    /// Rewrites address space casts by removing redundant ones.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewriteAddressSpaceCast(
        IPureValueRewriter rewriter,
        AddressSpaceCast cast)
    {
        // Get the inferred address space for the source value
        var sourceValue = cast.Source;
        var inferredSpace = _results.GetUnifiedAddressSpace(sourceValue);

        // If the cast target matches the inferred space and the source already has
        // the target type, the cast is redundant
        if (cast.TargetAddressSpace == inferredSpace &&
            sourceValue.Type is AddressSpaceType sourceType &&
            sourceType.AddressSpace == cast.TargetAddressSpace)
        {
            return sourceValue;
        }

        // Check for trivially redundant casts (source and target are the same)
        if (cast.SourceType.AddressSpace == cast.TargetAddressSpace)
            return sourceValue;

        // If the source is inferred as a specific space (Local, Shared, Global)
        // and the cast targets Generic, the cast is unnecessary — specific-space
        // pointers can be used in Generic contexts. This eliminates spurious
        // Local→Generic casts on alloca-derived pointers.
        if (cast.TargetAddressSpace == MemoryAddressSpace.Generic
            && inferredSpace != MemoryAddressSpace.Generic)
        {
            return sourceValue;
        }

        return cast;
    }

    /// <summary>
    /// Rewrites pointer casts to propagate the source's address space.
    /// PointerCast changes element type but should preserve the source's
    /// actual address space (e.g., Local from alloca). If the result type
    /// has Generic but the source is Local, rebuild with Local.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewritePointerCast(
        IPureValueRewriter rewriter,
        PointerCast cast)
    {
        var sourceSpace = _results.GetUnifiedAddressSpace(cast.Source);
        var resultPtr = cast.Type as PointerType;
        if (resultPtr is null)
            return cast;

        // If source has a specific space and result uses Generic, rebuild
        if (sourceSpace != MemoryAddressSpace.Generic
            && resultPtr.AddressSpace != sourceSpace)
        {
            return rewriter.Builder.CreatePointerCast(
                cast.Location,
                rewriter.Rewrite(cast.Source),
                resultPtr.ElementType);
        }

        return cast;
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

        if (mergedAnalysis == null)
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
    /// Rewrites phi values to use more specific address spaces when possible.
    /// Handles both scalar address space types and structure types with per-field
    /// address space specialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private PhiValue RewritePhiValue(BasicBlockTransform transform, PhiValue phiValue)
    {
        var location = phiValue.Location;

        // Get detailed analysis information for this phi value (including per-field data)
        var analysisResult = _results.GetAnalysisValue(phiValue);

        // Try to specialize the type based on analysis results
        var newPhiType = transform.Rewrite(phiValue.Type);
        var specializedType = SpecializeType(
            transform.ModuleBuilder,
            newPhiType,
            analysisResult);
        if (specializedType == null)
            return phiValue; // No specialization needed

        // Create new phi with specialized type
        var phiBuilder = transform.CreatePhi(
            location,
            specializedType,
            phiValue.NumArguments);

        // Add all arguments with appropriate casts
        for (int i = 0; i < phiValue.NumArguments; i++)
        {
            var node = phiValue.Arguments[i];
            // Resolve source block to current-gen so the converter-created
            // phi doesn't retain old-gen source references (PhiRewriter.Finish
            // skips argument wiring for converter-replaced phis).
            var source = transform.RewriteAs<BasicBlock>(phiValue.Sources[i]);

            // Create specialized value (with casts) to match the new type
            var castedNode = CreateSpecializedValue(
                transform,
                location,
                node,
                specializedType);

            phiBuilder.AddArgument(source, castedNode);
        }

        return phiBuilder.Seal();
    }

    /// <summary>
    /// Checks whether all types in the type graph of <paramref name="type"/>
    /// belong to the expected generation. Types from incompatible generations
    /// cannot be safely rewritten.
    /// </summary>
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
    /// Rewrites parameter values to use more specific address spaces when possible.
    /// Handles both scalar address space types and structure types with per-field
    /// address space specialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Parameter RewriteParameter(MethodTransform transform, Parameter parameter)
    {
        // Skip when the transform context is unavailable (should not happen
        // but guards against null propagation from the framework)
        if (transform?.ModuleTransform?.OldModule is not { } oldModule)
            return parameter;

        // Skip parameters whose type graph cannot be safely rewritten
        if (!CanRewriteParameterType(parameter.Type, oldModule.Generation))
            return parameter;

        // Get detailed analysis information for this parameter (including per-field data)
        var analysisResult = _results.GetAnalysisValue(parameter);

        // Try to specialize the type based on analysis results
        var paramType = transform.Rewrite(parameter.Type);
        var specializedType = SpecializeType(
            transform.ModuleBuilder,
            paramType,
            analysisResult);
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
    /// Rewrites method calls to add casts when calling methods where all call sites
    /// consistently use specific address spaces. Handles both scalar address space types
    /// and structure types with per-field address space specialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private MethodCall RewriteMethodCall(BasicBlockTransform transform, MethodCall call)
    {
        // External methods have no analyzed parameters — skip rewriting
        if (call.Target.IsExternal)
            return call;

        var location = call.Location;
        var callBuilder = transform.CreateCall(location, call.Target);
        bool anyChanges = false;

        // Add arguments, potentially with casts to match parameter types.
        // Note: call.Count includes the target method at index 0 (Values[0] = target,
        // Values[1..] = arguments). Use call.Arguments to iterate only arguments.
        for (int i = 0; i < call.Arguments.Length; ++i)
        {
            var arg = call.Arguments[i];
            var param = call.Target.Parameters[i];

            // Get detailed analysis information for the argument
            var argAnalysis = _results.GetAnalysisValue(arg);
            var paramAnalysis = _results.GetAnalysisValue(param);

            // Check if we need to cast the argument to match the parameter's inferred
            // type
            var argType = transform.Rewrite(arg.Type);
            var argSpecializedType = SpecializeType(
                transform.ModuleBuilder,
                argType,
                argAnalysis);
            var paramType = transform.Rewrite(param.Type);
            var paramSpecializedType = SpecializeType(
                transform.ModuleBuilder,
                paramType,
                paramAnalysis);

            // If both types can be specialized and they differ, we need to cast
            if (argSpecializedType != null && paramSpecializedType != null &&
                !argSpecializedType.Equals(paramSpecializedType))
            {
                // Cast argument to match the parameter's specialized type
                var castedArg = CreateSpecializedValue(
                    transform,
                    location,
                    arg,
                    paramSpecializedType);
                callBuilder.Add(castedArg);
                anyChanges = true;
            }
            // If only argument can be specialized but parameter type matches
            else if (argSpecializedType != null && param.Type.Equals(argSpecializedType))
            {
                // Cast argument to its specialized form
                var castedArg = CreateSpecializedValue(
                    transform,
                    location,
                    arg,
                    argSpecializedType);
                callBuilder.Add(castedArg);
                anyChanges = true;
            }
            else
            {
                callBuilder.Add(arg);
            }
        }

        return anyChanges ? callBuilder.Seal() : call;
    }
}
