// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Converts structure values into distinct values by lowering specific type patterns.
/// </summary>
/// <remarks>
/// This transformation does not change function parameters and calls to other functions.
/// </remarks>
/// <remarks>
/// Constructs a new type lowering transformation.
/// </remarks>
/// <param name="args">The transformation args.</param>
abstract class LowerTypes<TType>(TransformationArgs args) : Transformation(args)
    where TType : TypeValue, IValueInformation, IValueClassInformation
{
    /// <summary>
    /// Maps types to their field counts.
    /// </summary>
    private readonly Dictionary<TypeValue, int> _fieldCounts = [];

    /// <summary>
    /// Maps IR values that need lowering.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        // Register value mappings for values that need type lowering

        MapPureValue<StructureValue>(LowerStructureValue);
        MapPureValue<GetField>(LowerGetField);
        MapPureValue<SetField>(LowerSetField);
        MapPureValue<LoadFieldAddress>(LowerLoadFieldAddress);

        MapModuleValue<TType>(TransformType);
    }

    /// <summary>
    /// Returns true if the given type depends on the target type.
    /// </summary>
    protected virtual bool IsTypeDependent(TypeValue type) => type is TType;

    /// <summary>
    /// Gets the number of fields for the given type.
    /// </summary>
    protected abstract int GetNumFields(TType type);

    /// <summary>
    /// Transforms the target type into another type.
    /// </summary>
    /// <param name="transform">The parent transform.</param>
    /// <param name="type">The type value to convert.</param>
    /// <returns>The transformed type value.</returns>
    protected abstract TypeValue TransformType(ModuleTransform transform, TType type);

    /// <summary>
    /// Returns the number of type fields for the given type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected sealed override int GetNumTypeFields(TypeValue type)
    {
        if (type is TType targetType)
        {
            if (!_fieldCounts.TryGetValue(type, out var count))
            {
                count = GetNumFields(targetType);
                _fieldCounts[type] = count;
            }
            return count;
        }
        return base.GetNumTypeFields(type);
    }

    /// <summary>
    /// Squeezes the given structure value into a scalar if required.
    /// </summary>
    /// <param name="transform">The parent transform.</param>
    /// <param name="value">The value to squeeze.</param>
    /// <returns>Returns null by default.</returns>
    protected virtual Value? Squeeze(PureValueTransform transform, Value<Method> value) =>
        null;

    /// <summary>
    /// Lowers structure values with nested types.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerStructureValue(PureValueTransform transform, StructureValue value)
    {
        var sourceType = value.Type.AsNotNullCast<StructureType>();
        var targetType = transform.Rewrite(value.Type);
        if (targetType is not StructureType targetStructureType)
            return Squeeze(transform, value);

        var instance = transform.Builder.CreateStructure(
            value.Location,
            targetStructureType);
        for (int i = 0, e = sourceType.NumFields; i < e; ++i)
        {
            var fieldValue = transform.Rewrite(value.Values[i])!;
            if (IsTypeDependent(sourceType[i]))
            {
                var numFields = GetNumTypeFields(sourceType[i]);
                for (int j = 0; j < numFields; ++j)
                {
                    var viewField = transform.Builder.CreateGetField(
                        value.Location,
                        fieldValue,
                        new FieldSpan(j));
                    instance.Add(viewField);
                }
            }
            else
            {
                instance.Add(fieldValue);
            }
        }

        return instance.Seal();
    }

    /// <summary>
    /// Lowers get field operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerGetField(PureValueTransform transform, GetField getValue)
    {
        var structureType = getValue.StructureType;
        var objectValue = transform.Rewrite(getValue.Source);
        if (objectValue is null)
            return null;

        // Check whether we have to extract a nested type implementation
        var span = ComputeSpan(structureType, getValue.FieldSpan);
        if (IsTypeDependent(getValue.Type))
        {
            // Extract multiple elements from this structure
            var instance = transform.Builder.CreateDynamicStructure(
                getValue.Location,
                span.Span);
            for (int i = 0; i < span.Span; ++i)
            {
                var viewField = transform.Builder.CreateGetField(
                    getValue.Location,
                    objectValue,
                    new FieldSpan(span.Index + i));
                instance.Add(viewField);
            }
            return instance.Seal();
        }
        else
        {
            // Simple field access
            return transform.Builder.CreateGetField(
                getValue.Location,
                objectValue,
                span);
        }
    }

    /// <summary>
    /// Lowers set field operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerSetField(PureValueTransform transform, SetField setValue)
    {
        var structureType = setValue.StructureType;
        var objectValue = transform.Rewrite(setValue.Source);
        var fieldValue = transform.Rewrite(setValue.Value);
        var span = ComputeSpan(structureType, setValue.FieldSpan);

        // Get the field type from the structure type
        var fieldType = structureType[setValue.FieldSpan.Index];

        // Check whether we have to insert multiple elements
        Value? targetValue = objectValue;
        if (IsTypeDependent(fieldType))
        {
            for (int i = 0; i < span.Span; ++i)
            {
                var viewField = transform.Builder.CreateGetField(
                    setValue.Location,
                    fieldValue,
                    new FieldSpan(i));
                targetValue = transform.Builder.CreateSetField(
                    setValue.Location,
                    targetValue,
                    new FieldSpan(span.Index + i),
                    viewField);
            }
        }
        else
        {
            // Simple field access
            targetValue = transform.Builder.CreateSetField(
                setValue.Location,
                targetValue,
                span,
                fieldValue);
        }

        return targetValue;
    }

    /// <summary>
    /// Lowers LFA operations into an adapted version.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerLoadFieldAddress(
        PureValueTransform transform,
        LoadFieldAddress lfa)
    {
        var structureType = lfa.StructureType;
        if (!IsTypeDependent(structureType))
            return lfa;

        var source = transform.Rewrite(lfa.Source).AsNotNull();

        // Only adjust the field span if the rewritten source struct type
        // was actually changed. When the struct has not been lowered yet
        // ComputeSpan can invalidly compute the field index.
        var rewrittenStructType = source
            .GetTypeAs<PointerType>()
            .ElementType
            .As<StructureType>();
        var span = !rewrittenStructType.Equals(structureType)
            ? ComputeSpan(structureType, lfa.FieldSpan)
            : lfa.FieldSpan;

        return transform.Builder.CreateLoadFieldAddress(
            lfa.Location,
            source,
            span);
    }
}
