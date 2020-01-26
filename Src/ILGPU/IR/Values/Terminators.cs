// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="targets">The associated targets.</param>
        /// <param name="initialType">The initial node type.</param>
        protected TerminatorValue(
            ValueKind kind,
            BasicBlock basicBlock,
            ImmutableArray<BasicBlock> targets,
            TypeNode initialType)
            : base(kind, basicBlock, initialType)
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
    public sealed class ReturnTerminator : TerminatorValue
    {
        #region Static

        /// <summary>
        /// Computes a return node type.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(TypeNode returnType) =>
            returnType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new return terminator.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="returnValue">The current return value.</param>
        internal ReturnTerminator(
            BasicBlock basicBlock,
            ValueReference returnValue)
            : base(
                  ValueKind.Return,
                  basicBlock,
                  ImmutableArray<BasicBlock>.Empty,
                  ComputeType(returnValue.Type))
        {
            Seal(ImmutableArray.Create(returnValue));
        }

        #endregion

        #region Properties

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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ReturnValue.Type);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
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
        #region Static

        /// <summary>
        /// Computes a branch node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context) =>
            context.VoidType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new branch terminator.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="targets">The jump targets.</param>
        /// <param name="arguments">The branch arguments.</param>
        internal Branch(
            ValueKind kind,
            IRContext context,
            BasicBlock basicBlock,
            ImmutableArray<BasicBlock> targets,
            ImmutableArray<ValueReference> arguments)
            : base(
                  kind,
                  basicBlock,
                  targets,
                  ComputeType(context))
        {
            Seal(arguments);
        }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

        #endregion
    }

    /// <summary>
    /// Represents an unconditional branch terminator.
    /// </summary>
    public sealed class UnconditionalBranch : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a new branch terminator.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="target">The jump target.</param>
        internal UnconditionalBranch(
            IRContext context,
            BasicBlock basicBlock,
            BasicBlock target)
            : base(
                  ValueKind.UnconditionalBranch,
                  context,
                  basicBlock,
                  ImmutableArray.Create(target),
                  ImmutableArray<ValueReference>.Empty)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the unconditional jump target.
        /// </summary>
        public BasicBlock Target => Targets[0];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateUnconditionalBranch(
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
    public sealed class ConditionalBranch : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a new conditional branch terminator.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="condition">The jump condition.</param>
        /// <param name="falseTarget">The false jump target.</param>
        /// <param name="trueTarget">The true jump target.</param>
        internal ConditionalBranch(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference condition,
            BasicBlock trueTarget,
            BasicBlock falseTarget)
            : base(
                  ValueKind.ConditionalBranch,
                  context,
                  basicBlock,
                  ImmutableArray.Create(trueTarget, falseTarget),
                  ImmutableArray.Create(condition))
        {
            Debug.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType == BasicValueType.Int1,
                "Invalid boolean predicate");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated branch condition.
        /// </summary>
        public ValueReference Condition => this[0];

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

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateConditionalBranch(
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
            $"{Condition} ? {TrueTarget.ToReferenceString()} : {FalseTarget.ToReferenceString()}";

        #endregion
    }

    /// <summary>
    /// Represents a single switch terminator.
    /// </summary>
    public sealed class SwitchBranch : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a new switch terminator.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The value to switch over.</param>
        /// <param name="targets">The jump targets.</param>
        internal SwitchBranch(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference value,
            ImmutableArray<BasicBlock> targets)
            : base(
                  ValueKind.SwitchBranch,
                  context,
                  basicBlock,
                  targets,
                  ImmutableArray.Create(value))
        {
            Debug.Assert(
                value.Type.IsPrimitiveType &&
                value.Type.BasicValueType.IsInt(),
                "Invalid integer selection value");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated predicate value.
        /// </summary>
        public ValueReference Condition => this[0];

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
        [SuppressMessage("Microsoft.Usage", "CA2233: OperationsShouldNotOverflow",
            Justification = "Exception checks avoided for performance reasons")]
        public BasicBlock GetCaseTarget(int i)
        {
            Debug.Assert(i < Targets.Length - 1, "Invalid case argument");
            return Targets[i + 1];
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
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
    public sealed class BuilderTerminator : Branch
    {
        #region Instance

        /// <summary>
        /// Constructs a temporary builder terminator.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="targets">The jump targets.</param>
        internal BuilderTerminator(
            IRContext context,
            BasicBlock basicBlock,
            ImmutableArray<BasicBlock> targets)
            : base(
                  ValueKind.BuilderTerminator,
                  context,
                  basicBlock,
                  targets,
                  ImmutableArray<ValueReference>.Empty)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
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
