// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2026 ILGPU Project
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
    /// <param name="identity">
    /// The identity of the same type as the variable, or null.
    /// </param>
    /// <param name="handler">
    /// The lowering handler, which gets the variable and the identity.
    /// </param>
    /// <returns>The lowered thread value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? LowerPrimitive<TValue>(
        BasicBlockTransform transform,
        TValue sourceValue,
        Value? variable,
        Value? identity,
        Func<BasicBlockTransform, TValue, Value, Value?, Value?> handler)
        where TValue : ThreadValue
    {
        if (variable?.Type is not PrimitiveType primitiveType)
        {
            // Returning null from a converter deletes the value (see the
            // converter contract) — never a valid outcome here. Reject leaf
            // types that are neither structures nor primitives explicitly, such
            // as a pointer or a pointer field of a structure: no backend can
            // exchange an address with its shuffles and broadcasts.
            throw CollectiveDiagnostics.UnsupportedType(sourceValue, variable?.Type);
        }
        Value value = variable;
        if (primitiveType.BasicValueType < BasicValueType.Int32)
        {
            // Widen the identity with the variable, so that both have one type.
            // The values of an unsigned operation are zero-extended, so that
            // they compare in 32 bits as they do in their own type.
            var int32Type = transform.ModuleBuilder.GetPrimitiveType(
                BasicValueType.Int32);
            var widenFlags = IsUnsignedOperation(sourceValue)
                ? ConvertFlags.SourceUnsigned
                : ConvertFlags.None;
            value = transform.CreateConvert(
                sourceValue.Location,
                value,
                int32Type,
                widenFlags);
            if (identity is not null)
            {
                identity = transform.CreateConvert(
                    sourceValue.Location,
                    identity,
                    int32Type,
                    widenFlags);
            }
        }

        var result = handler(transform, sourceValue, value, identity);
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
    /// Returns true if the given thread value computes an unsigned operation,
    /// an unsigned minimum or maximum.
    /// </summary>
    private static bool IsUnsignedOperation(ThreadValue value) =>
        value switch
        {
            WarpReduce reduce => reduce.IsUnsigned,
            WarpScan scan => scan.IsUnsigned,
            GroupReduce reduce => reduce.IsUnsigned,
            GroupScan scan => scan.IsUnsigned,
            _ => false
        };

    /// <summary>
    /// Lowers a structure value recursively.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="transform">The basic block transform.</param>
    /// <param name="sourceValue">The source value.</param>
    /// <param name="variable">The variable to lower.</param>
    /// <param name="identity">
    /// The identity of the same type as the variable, or null. It is split into
    /// its fields together with the variable, so that the handler gets the
    /// identity of each field.
    /// </param>
    /// <param name="handler">
    /// The lowering handler, which gets each field of the variable and the
    /// matching field of the identity.
    /// </param>
    /// <returns>The lowered value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? LowerStructure<TValue>(
        BasicBlockTransform transform,
        TValue sourceValue,
        Value? variable,
        Value? identity,
        Func<BasicBlockTransform, TValue, Value, Value?, Value?> handler)
        where TValue : ThreadValue
    {
        if (variable?.Type is not StructureType structureType)
            return LowerPrimitive(transform, sourceValue, variable, identity, handler);

        var instance = transform.CreateDynamicStructure(
            sourceValue.Location,
            structureType.NumFields);
        for (int i = 0; i < structureType.NumFields; ++i)
        {
            var fieldValue = transform.CreateGetField(
                sourceValue.Location,
                variable,
                new FieldSpan(i));
            var fieldIdentity = identity is null
                ? null
                : transform.CreateGetField(
                    sourceValue.Location,
                    identity,
                    new FieldSpan(i));
            var loweredField = LowerStructure(
                transform,
                sourceValue,
                fieldValue,
                fieldIdentity,
                handler);
            instance.Add(loweredField);
        }
        return instance.Seal();
    }

    /// <summary>
    /// Lowers a broadcast value.
    /// </summary>
    private static Value? LowerBroadcast(BasicBlockTransform transform, Broadcast value)
    {
        // Only lower non-built-in broadcasts; returning the same reference
        // means "leave unchanged" (returning null would delete the value).
        if (value.IsBuiltIn)
            return value;

        var variable = transform.Rewrite(value.Variable)!;
        var origin = transform.Rewrite(value.Origin)!;

        return LowerStructure(
            transform,
            value,
            variable,
            null,
            (transform, source, var, _) =>
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
        // Only lower non-built-in shuffles; returning the same reference
        // means "leave unchanged" (returning null would delete the value).
        if (value.IsBuiltIn)
            return value;

        var variable = transform.Rewrite(value.Variable)!;
        var origin = transform.Rewrite(value.Origin)!;

        return LowerStructure(
            transform,
            value,
            variable,
            null,
            (transform, source, var, _) =>
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
            null,
            (transform, source, field, _) =>
                source.HasIntrinsicOperation
                ? transform.CreateWarpReduce(
                    source.Location, field, source.IntrinsicOp!.Value,
                    source.IntrinsicFlags, source.Kind)
                : transform.CreateWarpReduce(
                    source.Location, field,
                    transform.Rewrite(source.Operation!), source.Kind));
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

        // An exclusive scan splits its identity into fields with the variable
        return LowerStructure(
            transform,
            value,
            variable,
            RewriteOrNull(transform, value.Identity),
            (transform, source, field, fieldIdentity) =>
                source.HasIntrinsicOperation
                ? transform.CreateWarpScan(
                    source.Location, field,
                    source.IntrinsicOp!.Value, source.IntrinsicFlags, source.Kind,
                    fieldIdentity)
                : transform.CreateWarpScan(
                    source.Location, field,
                    transform.Rewrite(source.Operation!), source.Kind,
                    fieldIdentity));
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
            transform.Rewrite(value.Identity),
            (transform, source, field, fieldIdentity) =>
                source.HasIntrinsicOperation
                ? transform.CreateGroupReduce(
                    source.Location, field, source.IntrinsicOp!.Value,
                    source.IntrinsicFlags, source.Kind, fieldIdentity)
                : transform.CreateGroupReduce(
                    source.Location, field, transform.Rewrite(source.Operation!),
                    source.Kind, fieldIdentity));
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
            transform.Rewrite(value.Identity),
            (transform, source, field, fieldIdentity) =>
                source.HasIntrinsicOperation
                ? transform.CreateGroupScan(
                    source.Location, field, source.IntrinsicOp!.Value,
                    source.IntrinsicFlags, source.Kind, fieldIdentity)
                : transform.CreateGroupScan(
                    source.Location, field, transform.Rewrite(source.Operation!),
                    source.Kind, fieldIdentity));
    }

    /// <summary>
    /// Resolves an optional operand into the current generation, preserving null.
    /// </summary>
    /// <param name="transform">The block transform.</param>
    /// <param name="value">The operand, or null.</param>
    /// <returns>The resolved operand, or null.</returns>
    private static Value? RewriteOrNull(BasicBlockTransform transform, Value? value) =>
        value is null ? null : transform.Rewrite(value);

}
