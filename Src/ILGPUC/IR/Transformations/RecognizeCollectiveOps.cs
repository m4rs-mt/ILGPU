// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecognizeCollectiveOps.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Converts custom (lambda/method-group-based) collective IR nodes to their
/// intrinsic variants by inspecting the referenced <see cref="Method"/>'s
/// compiled body for a recognized <see cref="BinaryArithmeticKind"/>.
/// </summary>
/// <remarks>
/// <para>
/// The frontend always creates collective nodes in their <b>custom</b> form:
/// the binary operation is stored as a <see cref="Method"/> reference (the
/// lambda or method-group target), not as a <see cref="BinaryArithmeticKind"/>.
/// This is because the Method body may not be compiled yet at frontend time
/// (lazy compilation of lambda bodies).
/// </para>
/// <para>
/// This pass runs after the <see cref="Inliner"/> (which ensures all Method
/// bodies are available) and <b>before</b> <see cref="DeadLoadElimination"/>
/// (which would remove unused Method references). It calls
/// <see cref="BasicBlockBuilder.TryRecognizeOperation"/> to scan the Method's
/// blocks for a single <see cref="BinaryArithmeticValue"/> and, if found,
/// replaces the custom node with an intrinsic variant that stores the
/// <see cref="BinaryArithmeticKind"/> directly. This enables downstream
/// lowering passes (<see cref="LowerWarpCollectives"/>,
/// <see cref="LowerGroupCollectives"/>) and backend emitters to generate
/// efficient code without inspecting the lambda body at codegen time.
/// </para>
/// <para>
/// Nodes that are already intrinsic (e.g., created by a previous pass) or
/// whose Method body cannot be recognized are returned unchanged.
/// </para>
/// </remarks>
/// <param name="args">The transformation args.</param>
sealed class RecognizeCollectiveOps(TransformationArgs args) : Transformation(args)
{
    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapBasicBlockValue<WarpReduce>(Recognize);
        MapBasicBlockValue<WarpScan>(RecognizeScan);
        MapBasicBlockValue<GroupReduce>(RecognizeGroup);
        MapBasicBlockValue<GroupScan>(RecognizeGroupScan);
    }

    /// <summary>
    /// Attempts to recognize a custom <see cref="WarpReduce"/> and convert
    /// it to an intrinsic variant with a known <see cref="BinaryArithmeticKind"/>.
    /// </summary>
    private static Value? Recognize(BasicBlockTransform transform, WarpReduce value)
    {
        if (value.HasIntrinsicOperation)
            return value;

        var op = BasicBlockBuilder.TryRecognizeOperation(value.Operation);
        if (!op.HasValue)
            return value;

        return transform.CreateWarpReduce(
            value.Location,
            transform.Rewrite(value.Variable),
            op.Value,
            value.Kind);
    }

    /// <summary>
    /// Attempts to recognize a custom <see cref="WarpScan"/> and convert
    /// it to an intrinsic variant.
    /// </summary>
    private static Value? RecognizeScan(BasicBlockTransform transform, WarpScan value)
    {
        if (value.HasIntrinsicOperation)
            return value;

        var op = BasicBlockBuilder.TryRecognizeOperation(value.Operation);
        if (!op.HasValue)
            return value;

        return transform.CreateWarpScan(
            value.Location,
            transform.Rewrite(value.Variable),
            op.Value,
            value.Kind,
            value.Identity is not null
                ? transform.Rewrite(value.Identity)
                : null);
    }

    /// <summary>
    /// Attempts to recognize a custom <see cref="GroupReduce"/> and convert
    /// it to an intrinsic variant.
    /// </summary>
    private static Value? RecognizeGroup(
        BasicBlockTransform transform,
        GroupReduce value)
    {
        if (value.HasIntrinsicOperation)
            return value;

        var op = BasicBlockBuilder.TryRecognizeOperation(value.Operation);
        if (!op.HasValue)
            return value;

        return transform.CreateGroupReduce(
            value.Location,
            transform.Rewrite(value.Variable),
            op.Value,
            value.Kind,
            transform.Rewrite(value.Identity));
    }

    /// <summary>
    /// Attempts to recognize a custom <see cref="GroupScan"/> and convert
    /// it to an intrinsic variant.
    /// </summary>
    private static Value? RecognizeGroupScan(
        BasicBlockTransform transform,
        GroupScan value)
    {
        if (value.HasIntrinsicOperation)
            return value;

        var op = BasicBlockBuilder.TryRecognizeOperation(value.Operation);
        if (!op.HasValue)
            return value;

        return transform.CreateGroupScan(
            value.Location,
            transform.Rewrite(value.Variable),
            op.Value,
            value.Kind,
            transform.Rewrite(value.Identity));
    }
}
