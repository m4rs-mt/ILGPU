// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeadLoadElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Represents a DCE transformation.
/// </summary>
/// <param name="args">The transformation args.</param>
sealed class DeadLoadElimination(TransformationArgs args) :
    Transformation<ValueSet<Method, Value<Method>>>(args)
{
    /// <summary>
    /// Returns true if the basic block value is potentially dead.
    /// </summary>
    /// <param name="memoryValue">The memory value.</param>
    /// <returns>True if the basic block value is potentially dead.</returns>
    private static bool IsPotentiallyDead(BasicBlockValue memoryValue) =>
        memoryValue switch
        {
            Load _ => true,
            Shuffle _ => true,
            Broadcast _ => true,
            WarpReduce _ => true,
            WarpScan _ => true,
            GroupReduce _ => true,
            GroupScan _ => true,
            WarpRadixSort _ => true,
            GroupRadixSort _ => true,
            _ => false
        };

    /// <summary>
    /// Determines all values that are not dead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override ValueSet<Method, Value<Method>> CreateIntermediate(
        ModuleTransform transform,
        Method method)
    {
        var toProcess = InlineList<Value<Method>>.Create(method.Count);
        void Push(Value value)
        {
            if (value is not Value<Method> valueInMethod)
                return;
            if (valueInMethod.Scope == method)
                toProcess.Add(valueInMethod);
        }

        // Mark all terminators and their values as non dead
        foreach (var block in method.Blocks)
        {
            foreach (BasicBlockValue value in block)
            {
                // Mark all memory values as non dead (except dead loads)
                if (!IsPotentiallyDead(value))
                    Push(value);
            }

            // Register termination value dependencies (condition or return value)
            // For conditional/switch: the condition value
            // For return: the return value
            var terminationValue = block.TerminationValue;
            if (terminationValue is not null)
                Push(terminationValue);
        }

        // Mark all nodes as live
        var liveValues = method.CreateSet<Value<Method>>();
        while (toProcess.Count > 0)
        {
            var current = toProcess.Pop();
            if (!liveValues.Add(current))
                continue;

            foreach (var node in current.Values)
                Push(node);
        }

        return liveValues;
    }

    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        Value? Remove<T>(BasicBlockTransform transform, T value)
            where T : Value<Method> =>
            GetIntermediate(transform).Contains(value) ? value : null;

        MapBasicBlockValue<Load>(Remove);
        MapBasicBlockValue<Shuffle>(Remove);
        MapBasicBlockValue<Broadcast>(Remove);
        MapBasicBlockValue<WarpReduce>(Remove);
        MapBasicBlockValue<WarpScan>(Remove);
        MapBasicBlockValue<GroupReduce>(Remove);
        MapBasicBlockValue<GroupScan>(Remove);
        MapBasicBlockValue<WarpRadixSort>(Remove);
        MapBasicBlockValue<GroupRadixSort>(Remove);
    }
}
