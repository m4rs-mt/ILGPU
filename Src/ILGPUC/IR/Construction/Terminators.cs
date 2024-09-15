// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System.Runtime.CompilerServices;
using BlockList = ILGPU.Util.InlineList<ILGPU.IR.BasicBlock>;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new return terminator.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>The created terminator.</returns>
        public TerminatorValue CreateReturn(Location location) =>
            CreateReturn(location, CreateUndefined());

        /// <summary>
        /// Creates a new return terminator.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="returnValue">The return value.</param>
        /// <returns>The created terminator.</returns>
        public TerminatorValue CreateReturn(
            Location location,
            Value returnValue)
        {
            location.Assert(returnValue.Type == Method.ReturnType);
            return CreateTerminator(new ReturnTerminator(
                GetInitializer(location),
                returnValue));
        }

        /// <summary>
        /// Creates a new unconditional branch.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The target block.</param>
        /// <returns>The created terminator.</returns>
        public Branch CreateBranch(Location location, BasicBlock target) =>
            CreateTerminator(new UnconditionalBranch(
                GetInitializer(location),
                target));

        /// <summary>
        /// Creates a new conditional branch using no specific flags.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The branch condition.</param>
        /// <param name="trueTarget">The true target block.</param>
        /// <param name="falseTarget">The false target block.</param>
        /// <returns>The created terminator.</returns>
        public Branch CreateIfBranch(
            Location location,
            Value condition,
            BasicBlock trueTarget,
            BasicBlock falseTarget) =>
            CreateIfBranch(
                location,
                condition,
                trueTarget,
                falseTarget,
                IfBranchFlags.None);

        /// <summary>
        /// Creates a new conditional branch using the given flags.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The branch condition.</param>
        /// <param name="trueTarget">The true target block.</param>
        /// <param name="falseTarget">The false target block.</param>
        /// <param name="flags">The branch flags.</param>
        /// <returns>The created terminator.</returns>
        public Branch CreateIfBranch(
            Location location,
            Value condition,
            BasicBlock trueTarget,
            BasicBlock falseTarget,
            IfBranchFlags flags)
        {
            // Simplify unnecessary if branches and fold them to unconditional branches
            if (trueTarget == falseTarget)
                return CreateBranch(location, trueTarget);

            // Create an if branch in all other cases
            return CreateTerminator(new IfBranch(
                GetInitializer(location),
                condition,
                trueTarget,
                falseTarget,
                flags));
        }

        /// <summary>
        /// Creates a switch terminator builder.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The selection value.</param>
        /// <returns>The created switch builder.</returns>
        public SwitchBranch.Builder CreateSwitchBranch(
            Location location,
            Value value) =>
            CreateSwitchBranch(location, value, 2);

        /// <summary>
        /// Creates a switch terminator builder.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The selection value.</param>
        /// <param name="capacity">The expected number of cases to append.</param>
        /// <returns>The created switch builder.</returns>
        public SwitchBranch.Builder CreateSwitchBranch(
            Location location,
            Value value,
            int capacity) =>
            new SwitchBranch.Builder(this, location, value, capacity);

        /// <summary>
        /// Creates a switch terminator.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The selection value.</param>
        /// <param name="targets">The list of target blocks.</param>
        /// <returns>The created terminator.</returns>
        internal Branch CreateSwitchBranch(
            Location location,
            Value value,
            ref BlockList targets)
        {
            location.Assert(
                value.BasicValueType.IsInt() &&
                targets.Count > 0);

            value = CreateConvert(
                location,
                value,
                GetPrimitiveType(BasicValueType.Int32));

            // Transformation to create simple predicates
            return targets.Count == 2
                ? CreateIfBranch(
                    location,
                    CreateCompare(
                        location,
                        value,
                        CreatePrimitiveValue(
                            location,
                            0),
                        CompareKind.Equal),
                    targets[0],
                    targets[1])
                : CreateTerminator(new SwitchBranch(
                    GetInitializer(location),
                    value,
                    ref targets));
        }

        /// <summary>
        /// Creates a temporary builder terminator.
        /// </summary>
        /// <param name="capacity">The expected number of branch targets.</param>
        /// <returns>The created terminator builder.</returns>
        public BuilderTerminator.Builder CreateBuilderTerminator(int capacity) =>
            new BuilderTerminator.Builder(this, capacity);

        /// <summary>
        /// Creates a temporary builder terminator.
        /// </summary>
        /// <param name="targets">All branch targets.</param>
        /// <returns>The created terminator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BuilderTerminator CreateBuilderTerminator(
            ref BlockList targets) =>
            CreateTerminator(new BuilderTerminator(
                GetInitializer(Location.Unknown),
                ref targets));
    }
}
