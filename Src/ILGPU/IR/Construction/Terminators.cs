// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System.Collections.Immutable;

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
        /// Creates a new conditional branch.
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
            CreateTerminator(new IfBranch(
                GetInitializer(location),
                condition,
                trueTarget,
                falseTarget));

        /// <summary>
        /// Creates a switch terminator.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The selection value.</param>
        /// <param name="targets">All switch targets.</param>
        /// <returns>The created terminator.</returns>
        public Branch CreateSwitchBranch(
            Location location,
            Value value,
            ImmutableArray<BasicBlock> targets)
        {
            location.Assert(
                value.BasicValueType.IsInt() &&
                targets.Length > 0);

            value = CreateConvert(
                location,
                value,
                GetPrimitiveType(BasicValueType.Int32));

            // Transformation to create simple predicates
            return targets.Length == 2
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
                    targets));
        }

        /// <summary>
        /// Creates a temporary builder terminator.
        /// </summary>
        /// <param name="targets">All branch targets.</param>
        /// <returns>The created terminator.</returns>
        public BuilderTerminator CreateBuilderTerminator(
            ImmutableArray<BasicBlock> targets) =>
            CreateTerminator(new BuilderTerminator(
                GetInitializer(Location.Unknown),
                targets)) as BuilderTerminator;
    }
}
