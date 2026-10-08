// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Rewriting;
using System;

namespace ILGPUC.IR.BasicBlockValues.Construction;

partial class BasicBlockBuilder
{
    /// <summary>
    /// Builder for switch termination.
    /// </summary>
    /// <remarks>
    /// Creates a new switch termination builder.
    /// </remarks>
    internal struct SwitchBuilder(
        BasicBlockBuilder builder,
        Value condition,
        int capacity)
    {
        private BasicBlock? _defaultTarget = null;
        private InlineList<BasicBlock> _caseTargets =
            InlineList<BasicBlock>.Create(capacity);

        /// <summary>
        /// Gets the switch condition.
        /// </summary>
        public readonly Value Condition => condition;

        /// <summary>
        /// Adds the default target.
        /// </summary>
        public void AddDefault(BasicBlock target) => _defaultTarget = target;

        /// <summary>
        /// Adds a case target.
        /// </summary>
        public void AddCase(BasicBlock target) => _caseTargets.Add(target);

        /// <summary>
        /// Seals the switch termination.
        /// </summary>
        public readonly void Seal() =>
            builder.CreateSwitchTermination(
                condition,
                _defaultTarget,
                _caseTargets.AsReadOnlySpan());
    }

    /// <summary>
    /// Sets up return termination for this block.
    /// </summary>
    /// <param name="returnValue">The return value (or null for void returns).</param>
    public BasicBlock CreateReturnTermination(Value? returnValue = null)
    {
        returnValue ??= ModuleBuilder.UndefinedValue;

        SetTermination(BlockTerminationKind.Return, value: returnValue);
        return BasicBlock;
    }

    /// <summary>
    /// Sets up unconditional termination for this block.
    /// </summary>
    /// <param name="target">The target block.</param>
    /// <returns>True if termination was set, false if target was null.</returns>
    public BasicBlock CreateUnconditionalTermination(BasicBlock? target)
    {
        if (target is null)
            return BasicBlock;

        SetTermination(
            BlockTerminationKind.Unconditional,
            value: null,
            successorCapacity: 1)
            .AddSuccessor(target);

        return BasicBlock;
    }

    /// <summary>
    /// Sets up conditional (if-branch) termination for this block using no specific
    /// flags.
    /// </summary>
    /// <param name="condition">The branch condition.</param>
    /// <param name="trueTarget">The true target block.</param>
    /// <param name="falseTarget">The false target block.</param>
    /// <returns>
    /// True if conditional termination was set, false if folded to unconditional.
    /// </returns>
    public BasicBlock CreateConditionalTermination(
        Value? condition,
        BasicBlock? trueTarget,
        BasicBlock? falseTarget)
    {
        if (condition is null || trueTarget is null)
            return BasicBlock;

        // Simplify unnecessary if branches and fold them to unconditional branches
        if (trueTarget == falseTarget || falseTarget is null)
            return CreateUnconditionalTermination(trueTarget);

        if (condition is PrimitiveValue primitiveValue)
        {
            bool isTrue = primitiveValue.Int1Value;
            return CreateUnconditionalTermination(isTrue ? trueTarget : falseTarget);
        }

        // Create conditional termination
        SetTermination(
            BlockTerminationKind.Conditional,
            condition,
            successorCapacity: 2)
            .AddSuccessor(trueTarget)
            .AddSuccessor(falseTarget);
        return BasicBlock;
    }

    /// <summary>
    /// Creates a switch termination builder.
    /// </summary>
    /// <param name="value">The selection value.</param>
    /// <param name="capacity">The expected number of cases to append.</param>
    /// <returns>The created switch builder.</returns>
    public SwitchBuilder CreateSwitchTermination(Value value, int capacity = 2) =>
        new(this, value, capacity);

    /// <summary>
    /// Sets up switch termination for this block.
    /// </summary>
    /// <param name="value">The switch value.</param>
    /// <param name="defaultTarget">The default case target.</param>
    /// <param name="caseTargets">The case targets.</param>
    /// <returns>
    /// True if switch termination was set, false if folded to simpler form.
    /// </returns>
    public BasicBlock CreateSwitchTermination(
        Value? value,
        BasicBlock? defaultTarget,
        ReadOnlySpan<BasicBlock> caseTargets)
    {
        if (defaultTarget is null)
            return BasicBlock;
        if (value is null)
            return CreateUnconditionalTermination(defaultTarget);

        value = CreateConvert(
            value.Location,
            value,
            ModuleBuilder.GetPrimitiveType(BasicValueType.Int32)).AsNotNull();

        if (value is PrimitiveValue primitiveValue)
        {
            int caseValue = primitiveValue.Int32Value;
            var target = caseValue < 0 || caseValue >= caseTargets.Length
                ? defaultTarget
                : caseTargets[caseValue];
            return CreateUnconditionalTermination(target);
        }

        // Two targets can be simplified to conditional
        if (caseTargets.Length == 1)
        {
            return CreateConditionalTermination(
                CreateCompare(
                    value.Location,
                    value,
                    CreatePrimitiveValue(value.Location, 0),
                    CompareKind.Equal),
                defaultTarget,
                caseTargets[0]);
        }

        // Full switch termination
        var builder = SetTermination(
            BlockTerminationKind.Switch,
            value: value,
            successorCapacity: 1 + caseTargets.Length);
        builder.AddSuccessor(defaultTarget.AsNotNull());
        builder.AddSuccessors(caseTargets);

        return BasicBlock;
    }

    /// <summary>
    /// Sets up pending termination for this block (used during building).
    /// </summary>
    /// <param name="targets">The pending target blocks.</param>
    public void CreatePendingTermination(ReadOnlySpan<BasicBlock> targets)
    {
        var builder = SetTermination(
            BlockTerminationKind.Pending,
            successorCapacity: targets.Length);
        builder.AddSuccessors(targets);
    }
}
