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

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single terminator value.
    /// </summary>
    public abstract class TerminatorValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new terminator value that is marked.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="targets">The associated targets.</param>
        protected TerminatorValue(
            in ValueInitializer initializer,
            ImmutableArray<BasicBlock> targets)
            : base(initializer)
        {
            Targets = targets;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated targets.
        /// </summary>
        public ImmutableArray<BasicBlock> Targets { get; }

        /// <summary>
        /// Returns the number of attached targets.
        /// </summary>
        public int NumTargets => Targets.Length;

        #endregion
    }

    /// <summary>
    /// Represents a simple return terminator.
    /// </summary>
    [ValueKind(ValueKind.Return)]
    public sealed class ReturnTerminator : TerminatorValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new return terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="returnValue">The current return value.</param>
        internal ReturnTerminator(
            in ValueInitializer initializer,
            ValueReference returnValue)
            : base(
                  initializer,
                  ImmutableArray<BasicBlock>.Empty)
        {
            Seal(ImmutableArray.Create(returnValue));
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Return;

        /// <summary>
        /// Returns true if the current return terminator is a void return.
        /// </summary>
        public bool IsVoidReturn => Type.IsVoidType;

        /// <summary>
        /// Returns the associated return value.
        /// In case of a void return value the result is a <see cref="NullValue"/>.
        /// </summary>
        public ValueReference ReturnValue => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            ReturnValue.Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateReturn(
                rebuilder.Rebuild(ReturnValue));

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "ret";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => ReturnValue.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a branch-based terminator.
    /// </summary>
    public abstract class Branch : TerminatorValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new branch terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="targets">The jump targets.</param>
        /// <param name="arguments">The branch arguments.</param>
        internal Branch(
            in ValueInitializer initializer,
            ImmutableArray<BasicBlock> targets,
            ImmutableArray<ValueReference> arguments)
            : base(initializer, targets)
        {
            Seal(arguments);
        }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        #endregion
    }

    /// <summary>
    /// Represents an unconditional branch terminator.
    /// </summary>
    [ValueKind(ValueKind.UnconditionalBranch)]
    public sealed class UnconditionalBranch : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a new branch terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="target">The jump target.</param>
        internal UnconditionalBranch(
            in ValueInitializer initializer,
            BasicBlock target)
            : base(
                  initializer,
                  ImmutableArray.Create(target),
                  ImmutableArray<ValueReference>.Empty)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.UnconditionalBranch;

        /// <summary>
        /// Returns the unconditional jump target.
        /// </summary>
        public BasicBlock Target => Targets[0];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateBranch(
                rebuilder.LookupTarget(Target));

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "branch";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Target.ToReferenceString();

        #endregion
    }

    /// <summary>
    /// Represents a conditional branch terminator.
    /// </summary>
    public abstract class ConditionalBranch : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a new conditional branch terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="condition">The jump condition.</param>
        /// <param name="targets">The jump targets.</param>
        protected ConditionalBranch(
            in ValueInitializer initializer,
            ValueReference condition,
            ImmutableArray<BasicBlock> targets)
            : base(
                  initializer,
                  targets,
                  ImmutableArray.Create(condition))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated branch condition.
        /// </summary>
        public ValueReference Condition => this[0];

        /// <summary>
        /// Returns true if this conditional branch can be folded.
        /// </summary>
        /// <remarks>
        /// A branch can be folded if its condition evaluates to a constant value.
        /// </remarks>
        public bool CanFold => Condition.IsPrimitive();

        #endregion

        #region Methods

        /// <summary>
        /// Folds this branch into another branch that might be unconditional.
        /// </summary>
        /// <param name="builder">The builder to use.</param>
        /// <returns>The folded branch.</returns>
        public Branch Fold(IRBuilder builder)
        {
            var condition = Condition.ResolveAs<PrimitiveValue>();
            return condition == null
                ? this
                : FoldBranch(builder, condition);
        }

        /// <summary>
        /// Folds this conditional branch into an unconditional branch.
        /// </summary>
        /// <param name="builder">The builder to use.</param>
        /// <param name="condition">The constant condition value.</param>
        /// <returns>The folded branch.</returns>
        protected abstract Branch FoldBranch(
            IRBuilder builder,
            PrimitiveValue condition);

        #endregion
    }

    /// <summary>
    /// Represents an if branch terminator.
    /// </summary>
    [ValueKind(ValueKind.IfBranch)]
    public sealed class IfBranch : ConditionalBranch
    {
        #region Instance

        /// <summary>
        /// Constructs a new conditional branch terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="condition">The jump condition.</param>
        /// <param name="falseTarget">The false jump target.</param>
        /// <param name="trueTarget">The true jump target.</param>
        internal IfBranch(
            in ValueInitializer initializer,
            ValueReference condition,
            BasicBlock trueTarget,
            BasicBlock falseTarget)
            : base(
                  initializer,
                  condition,
                  ImmutableArray.Create(trueTarget, falseTarget))
        {
            Debug.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType == BasicValueType.Int1,
                "Invalid boolean predicate");
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.IfBranch;

        /// <summary>
        /// Returns the true jump target.
        /// </summary>
        public BasicBlock TrueTarget => Targets[0];

        /// <summary>
        /// Returns the false jump target.
        /// </summary>
        public BasicBlock FalseTarget => Targets[1];

        #endregion

        #region Methods

        /// <summary cref="ConditionalBranch.FoldBranch(IRBuilder, PrimitiveValue)"/>
        protected override Branch FoldBranch(
            IRBuilder builder,
            PrimitiveValue condition)
        {
            var target = condition.Int1Value ?
                TrueTarget :
                FalseTarget;
            return builder.CreateBranch(target);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateIfBranch(
                rebuilder.Rebuild(Condition),
                rebuilder.LookupTarget(TrueTarget),
                rebuilder.LookupTarget(FalseTarget));

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "branch";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Condition} ? {TrueTarget.ToReferenceString()} : " +
            FalseTarget.ToReferenceString();

        #endregion
    }

    /// <summary>
    /// Represents a single switch terminator.
    /// </summary>
    [ValueKind(ValueKind.SwitchBranch)]
    public sealed class SwitchBranch : ConditionalBranch
    {
        #region Instance

        /// <summary>
        /// Constructs a new switch terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to switch over.</param>
        /// <param name="targets">The jump targets.</param>
        internal SwitchBranch(
            in ValueInitializer initializer,
            ValueReference value,
            ImmutableArray<BasicBlock> targets)
            : base(
                  initializer,
                  value,
                  targets)
        {
            Debug.Assert(
                value.Type.IsPrimitiveType &&
                value.Type.BasicValueType.IsInt(),
                "Invalid integer selection value");
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.SwitchBranch;

        /// <summary>
        /// Returns the default block.
        /// </summary>
        public BasicBlock DefaultBlock => Targets[0];

        /// <summary>
        /// Returns the number of actual switch cases without the default case.
        /// </summary>
        public int NumCasesWithoutDefault => Targets.Length - 1;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the case target for the i-th case.
        /// </summary>
        /// <param name="i">The index of the i-th case.</param>
        /// <returns>The resulting jump target.</returns>
        [SuppressMessage(
            "Microsoft.Usage",
            "CA2233: OperationsShouldNotOverflow",
            Justification = "Exception checks avoided for performance reasons")]
        public BasicBlock GetCaseTarget(int i)
        {
            Debug.Assert(i < Targets.Length - 1, "Invalid case argument");
            return Targets[i + 1];
        }

        /// <summary cref="ConditionalBranch.FoldBranch(IRBuilder, PrimitiveValue)"/>
        protected override Branch FoldBranch(
            IRBuilder builder,
            PrimitiveValue condition)
        {
            int caseValue = condition.Int32Value;
            var target = caseValue < 0 || caseValue >= NumCasesWithoutDefault ?
                Targets[0] :
                GetCaseTarget(caseValue);
            return builder.CreateBranch(target);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var targets = ImmutableArray.CreateBuilder<BasicBlock>(Targets.Length);
            foreach (var target in Targets)
                targets.Add(rebuilder.LookupTarget(target));

            return builder.CreateSwitchBranch(
                rebuilder.Rebuild(Condition),
                targets.MoveToImmutable());
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "switch";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = new StringBuilder();
            result.Append(Condition.ToString());
            result.Append(" ");
            for (int i = 1, e = Targets.Length; i < e; ++i)
            {
                result.Append(Targets[i].ToReferenceString());
                if (i + 1 < e)
                    result.Append(", ");
            }
            result.Append(" - default: ");
            result.Append(Targets[0].ToReferenceString());
            return result.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Represents a temporary builder terminator.
    /// </summary>
    [ValueKind(ValueKind.BuilderTerminator)]
    public sealed class BuilderTerminator : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a temporary builder terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="targets">The jump targets.</param>
        internal BuilderTerminator(
            in ValueInitializer initializer,
            ImmutableArray<BasicBlock> targets)
            : base(
                  initializer,
                  targets,
                  ImmutableArray<ValueReference>.Empty)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.BuilderTerminator;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            throw new InvalidOperationException();

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) =>
            throw new InvalidOperationException();

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "builderBr";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = new StringBuilder();
            for (int i = 0, e = Targets.Length; i < e; ++i)
            {
                result.Append(Targets[i].ToReferenceString());
                if (i + 1 < e)
                    result.Append(", ");
            }
            return result.ToString();
        }

        #endregion
    }
}
