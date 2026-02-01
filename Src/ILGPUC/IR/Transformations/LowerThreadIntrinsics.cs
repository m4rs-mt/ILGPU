// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerThreadIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers internal high-level thread intrinsics.
/// </summary>
sealed class LowerThreadIntrinsics(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// Maps IR values that need lowering.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        // Register value mappings for thread values that need lowering
        MapBasicBlockValue<Broadcast>(LowerBroadcast);
        MapBasicBlockValue<Shuffle>(LowerShuffle);
        MapBasicBlockValue<WarpReduce>(LowerWarpReduce);
        MapBasicBlockValue<WarpScan>(LowerWarpScan);
        MapBasicBlockValue<GroupReduce>(LowerGroupReduce);
        MapBasicBlockValue<GroupScan>(LowerGroupScan);
    }

    /// <summary>
    /// Lowers a primitive type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="transform">The basic block transform.</param>
    /// <param name="sourceValue">The source value to get the values from.</param>
    /// <param name="variable">The source variable.</param>
    /// <param name="handler">The lowering handler.</param>
    /// <returns>The lowered thread value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? LowerPrimitive<TValue>(
        BasicBlockTransform transform,
        TValue sourceValue,
        Value? variable,
        Func<BasicBlockTransform, TValue, Value, Value?> handler)
        where TValue : ThreadValue
    {
        if (variable?.Type is not PrimitiveType primitiveType)
            return null;
        Value value = variable;
        if (primitiveType.BasicValueType < BasicValueType.Int32)
        {
            value = transform.CreateConvert(
                sourceValue.Location,
                value,
                transform.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32));
        }

        var result = handler(transform, sourceValue, value);
        if (primitiveType.BasicValueType < BasicValueType.Int32)
        {
            result = transform.CreateConvert(
                sourceValue.Location,
                result,
                variable.Type);
        }
        return result;
    }

    /// <summary>
    /// Lowers a structure value recursively.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="transform">The basic block transform.</param>
    /// <param name="sourceValue">The source value.</param>
    /// <param name="variable">The variable to lower.</param>
    /// <param name="handler">The lowering handler.</param>
    /// <returns>The lowered value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? LowerStructure<TValue>(
        BasicBlockTransform transform,
        TValue sourceValue,
        Value? variable,
        Func<BasicBlockTransform, TValue, Value, Value?> handler)
        where TValue : ThreadValue
    {
        if (variable?.Type is not StructureType structureType)
            return LowerPrimitive(transform, sourceValue, variable, handler);

        var instance = transform.CreateDynamicStructure(
            sourceValue.Location,
            structureType.NumFields);
        for (int i = 0; i < structureType.NumFields; ++i)
        {
            var fieldValue = transform.CreateGetField(
                sourceValue.Location,
                variable,
                new FieldSpan(i));
            var loweredField = LowerStructure(transform, sourceValue, fieldValue, handler);
            instance.Add(loweredField);
        }
        return instance.Seal();
    }

    /// <summary>
    /// Lowers a broadcast value.
    /// </summary>
    private static Value? LowerBroadcast(BasicBlockTransform transform, Broadcast value)
    {
        // Only lower non-built-in broadcasts
        if (value.IsBuiltIn)
            return null;

        var variable = transform.Rewrite(value.Variable)!;
        var origin = transform.Rewrite(value.Origin)!;

        return LowerStructure(
            transform,
            value,
            variable,
            (transform, source, var) =>
                transform.CreateBroadcast(
                    source.Location,
                    var,
                    origin,
                    source.Kind));
    }

    /// <summary>
    /// Lowers a shuffle value.
    /// </summary>
    private static Value? LowerShuffle(BasicBlockTransform transform, Shuffle value)
    {
        // Only lower non-built-in shuffles
        if (value.IsBuiltIn)
            return null;

        var variable = transform.Rewrite(value.Variable)!;
        var origin = transform.Rewrite(value.Origin)!;

        return LowerStructure(
            transform,
            value,
            variable,
            (transform, source, var) =>
                transform.CreateShuffle(
                    source.Location,
                    var,
                    origin,
                    source.Kind));
    }

    /// <summary>
    /// Lowers a warp reduce value. Decomposes struct types into per-field
    /// reductions; leaves built-in types for later shuffle expansion.
    /// </summary>
    private static Value? LowerWarpReduce(
        BasicBlockTransform transform,
        WarpReduce value)
    {
        // Only lower non-built-in (struct) types here; builtin types are
        // expanded to shuffles by LowerWarpCollectives after specialization.
        if (value.IsBuiltIn)
            return value;

        var variable = transform.Rewrite(value.Variable)!;

        return LowerStructure(
            transform,
            value,
            variable,
            (transform, source, field) =>
                source.HasIntrinsicOperation
                ? transform.CreateWarpReduce(
                    source.Location, field, source.IntrinsicOp!.Value, source.Kind)
                : transform.CreateWarpReduce(
                    source.Location, field, source.Operation!, source.Kind));
    }

    /// <summary>
    /// Lowers a warp scan value. Decomposes struct types into per-field
    /// scans; leaves built-in types for later shuffle expansion.
    /// </summary>
    private static Value? LowerWarpScan(
        BasicBlockTransform transform,
        WarpScan value)
    {
        // Only lower non-built-in (struct) types here; builtin types are
        // expanded to shuffles by LowerWarpCollectives after specialization.
        if (value.IsBuiltIn)
            return value;

        var variable = transform.Rewrite(value.Variable)!;

        return LowerStructure(
            transform,
            value,
            variable,
            (transform, source, field) =>
            {
                // For exclusive scans with identity, decompose identity too
                Value? fieldIdentity = null;
                if (source.Identity is not null)
                {
                    // TODO: decompose struct identity per-field
                    fieldIdentity = source.Identity;
                }

                return source.HasIntrinsicOperation
                    ? transform.CreateWarpScan(
                        source.Location, field,
                        source.IntrinsicOp!.Value, source.Kind, fieldIdentity)
                    : transform.CreateWarpScan(
                        source.Location, field,
                        source.Operation!, source.Kind, fieldIdentity);
            });
    }

    /// <summary>
    /// Lowers a group reduce value. Decomposes struct types into per-field
    /// reductions; leaves built-in types for LowerGroupCollectives.
    /// </summary>
    private static Value? LowerGroupReduce(
        BasicBlockTransform transform,
        GroupReduce value)
    {
        if (value.IsBuiltIn)
            return value;

        var variable = transform.Rewrite(value.Variable)!;

        return LowerStructure(
            transform,
            value,
            variable,
            (transform, source, field) =>
                source.HasIntrinsicOperation
                ? transform.CreateGroupReduce(
                    source.Location, field, source.IntrinsicOp!.Value,
                    source.Kind, source.Identity)
                : transform.CreateGroupReduce(
                    source.Location, field, source.Operation!,
                    source.Kind, source.Identity));
    }

    /// <summary>
    /// Lowers a group scan value. Decomposes struct types into per-field
    /// scans; leaves built-in types for LowerGroupCollectives.
    /// </summary>
    private static Value? LowerGroupScan(
        BasicBlockTransform transform,
        GroupScan value)
    {
        if (value.IsBuiltIn)
            return value;

        var variable = transform.Rewrite(value.Variable)!;

        return LowerStructure(
            transform,
            value,
            variable,
            (transform, source, field) =>
                source.HasIntrinsicOperation
                ? transform.CreateGroupScan(
                    source.Location, field, source.IntrinsicOp!.Value,
                    source.Kind, source.Identity)
                : transform.CreateGroupScan(
                    source.Location, field, source.Operation!,
                    source.Kind, source.Identity));
    }

}
