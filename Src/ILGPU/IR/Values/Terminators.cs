// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using BlockList = ILGPU.Util.InlineList<ILGPU.IR.BasicBlock>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single terminator value.
    /// </summary>
    public abstract class TerminatorValue : Value
    {
        #region Nested Types

        /// <summary>
        /// Remaps basic blocks.
        /// </summary>
        public interface IBlockRemapper
        {
            /// <summary>
            /// Returns true if the given blocks contain a block to remap.
            /// </summary>
            /// <param name="blocks">The blocks to check.</param>
            /// <returns>True, if the given blocks contain the old block.</returns>
            bool CanRemap(in ReadOnlySpan<BasicBlock> blocks);
        }

        /// <summary>
        /// An abstract target remapper.
        /// </summary>
        public interface IDirectTargetRemapper
        {
            /// <summary>
            /// Remaps the given block to a new one.
            /// </summary>
            /// <param name="block">The old block to remap.</param>
            /// <returns>The remapped block.</returns>
            BasicBlock Remap(BasicBlock block);
        }

        /// <summary>
        /// An abstract target remapper.
        /// </summary>
        public interface ITargetRemapper : IBlockRemapper, IDirectTargetRemapper { }

        /// <summary>
        /// An identity remapper that does not remap any targets.
        /// </summary>
        public readonly struct IdentityRemapper : ITargetRemapper
        {
            #region Methods

            /// <summary>
            /// Returns false.
            /// </summary>
            public readonly bool CanRemap(in ReadOnlySpan<BasicBlock> blocks) => false;

            /// <summary>
            /// Returns the same block.
            /// </summary>
            public readonly BasicBlock Remap(BasicBlock block) => block;

            #endregion
        }

        #endregion

        #region Instance

        private BlockList branchTargets;

        /// <summary>
        /// Constructs a new terminator value that is marked.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        protected TerminatorValue(in ValueInitializer initializer)
            : base(initializer)
        {
            branchTargets = BlockList.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated targets.
        /// </summary>
        public ReadOnlySpan<BasicBlock> Targets => branchTargets;

        /// <summary>
        /// Returns the number of attached targets.
        /// </summary>
        public int NumTargets => branchTargets.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Seals all internal branch targets (if any).
        /// </summary>
        /// <param name="targets">The associated targets.</param>
        protected void SealTargets(ref BlockList targets)
        {
            VerifyNotSealed();
            targets.MoveTo(ref branchTargets);
        }

        /// <summary>
        /// Remaps the current block targets.
        /// </summary>
        /// <typeparam name="TTargetRemapper">The target remapper type.</typeparam>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <param name="remapper">The remapper instance.</param>
        /// <returns>The remapped terminator value.</returns>
        public TerminatorValue RemapTargets<TTargetRemapper>(
            Method.Builder methodBuilder,
            TTargetRemapper remapper)
            where TTargetRemapper : struct, ITargetRemapper
        {
            var blockBuilder = methodBuilder[BasicBlock];
            return RemapTargets(blockBuilder, remapper);
        }

        /// <summary>
        /// Remaps the current block targets.
        /// </summary>
        /// <typeparam name="TTargetRemapper">The target remapper type.</typeparam>
        /// <param name="blockBuilder">The current block builder.</param>
        /// <param name="remapper">The remapper instance.</param>
        /// <returns>The remapped terminator value.</returns>
        public TerminatorValue RemapTargets<TTargetRemapper>(
            BasicBlock.Builder blockBuilder,
            TTargetRemapper remapper)
            where TTargetRemapper : struct, ITargetRemapper
        {
            this.Assert(blockBuilder.BasicBlock == BasicBlock);
            if (!remapper.CanRemap(Targets))
                return this;

            var targets = BlockList.Create(NumTargets);
            foreach (var target in Targets)
                targets.Add(remapper.Remap(target));
            return RemapTargets(blockBuilder, ref targets);
        }

        /// <summary>
        /// Remaps the current targets to the given target list.
        /// </summary>
        /// <param name="builder">The builder to use.</param>
        /// <param name="targets">The new targets.</param>
        /// <returns>The new terminator value.</returns>
        protected abstract TerminatorValue RemapTargets(
            BasicBlock.Builder builder,
            ref BlockList targets);

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
            : base(initializer)
        {
            Seal(returnValue);
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
                Location,
                rebuilder.Rebuild(ReturnValue));

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        protected override TerminatorValue RemapTargets(
            BasicBlock.Builder builder,
            ref BlockList targets) =>
            throw this.GetInvalidOperationException();

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
        internal Branch(in ValueInitializer initializer)
            : base(initializer)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer)
        {
            int index = 0;
            writer.Write(nameof(NumTargets), NumTargets);
            foreach (var target in Targets)
                writer.Write($"{nameof(Targets)}[{index++}]", target.Id);
        }

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
            : base(initializer)
        {
            var targets = BlockList.Create(target);
            SealTargets(ref targets);
            Seal();
        }

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
                Location,
                rebuilder.LookupTarget(Target));

        /// <summary>
        /// Creates a new branch using the given targets.
        /// </summary>
        protected override TerminatorValue RemapTargets(
            BasicBlock.Builder builder,
            ref BlockList targets) =>
            builder.CreateBranch(
                Location,
                targets[0]);

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
        protected ConditionalBranch(in ValueInitializer initializer)
            : base(initializer)
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
        /// Returns true if this branch offers two branch targets and one of the
        /// targets is the one provided.
        /// </summary>
        /// <param name="block">The branch target to exclude.</param>
        /// <param name="otherBlock">The other branch target (if any).</param>
        public bool TryGetOtherBranchTarget(
            BasicBlock block,
            [NotNullWhen(true)] out BasicBlock? otherBlock)
        {
            otherBlock = null;
            if (NumTargets != 2)
                return false;

            // Check both branch targets
            if (Targets[0] == block)
            {
                otherBlock = Targets[1];
                return true;
            }
            else if (Targets[1] == block)
            {
                otherBlock = Targets[0];
                return true;
            }
            return false;
        }

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
    /// Specific flags for the <see cref="IfBranch"/> class.
    /// </summary>
    [Flags]
    public enum IfBranchFlags : int
    {
        /// <summary>
        /// No specific branch flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Represents an inverted if branch.
        /// </summary>
        IsInverted = 1 << 0,
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
        /// <param name="flags">The branch flags.</param>
        internal IfBranch(
            in ValueInitializer initializer,
            ValueReference condition,
            BasicBlock trueTarget,
            BasicBlock falseTarget,
            IfBranchFlags flags)
            : base(initializer)
        {
            Location.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType == BasicValueType.Int1);
            Flags = flags;

            var targets = BlockList.Create(trueTarget, falseTarget);
            SealTargets(ref targets);
            Seal(condition);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated branch flags.
        /// </summary>
        public IfBranchFlags Flags { get; }

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

        /// <summary>
        /// Returns true if this branch has been inverted.
        /// </summary>
        public bool IsInverted =>
            (Flags & IfBranchFlags.IsInverted) != IfBranchFlags.None;

        /// <summary>
        /// Returns the original branch targets of the given if branch taking the
        /// current <see cref="IfBranchFlags.IsInverted"/> flag into account.
        /// </summary>
        public (BasicBlock TrueTarget, BasicBlock FalseTarget)
            NotInvertedBranchTargets =>
            IsInverted
            ? (FalseTarget, TrueTarget)
            : (TrueTarget, FalseTarget);

        #endregion

        #region Methods

        /// <summary>
        /// Inverts this if branch by negating the condition and swapping both cases.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        /// <returns>The inverted branch.</returns>
        public Branch Invert(IRBuilder builder)
        {
            this.Assert(builder.BasicBlock == BasicBlock);
            return builder.CreateIfBranch(
                Location,
                builder.CreateArithmetic(
                    Location,
                    Condition,
                    UnaryArithmeticKind.Not),
                FalseTarget,
                TrueTarget,
                Flags ^ IfBranchFlags.IsInverted);
        }

        /// <summary cref="ConditionalBranch.FoldBranch(IRBuilder, PrimitiveValue)"/>
        protected override Branch FoldBranch(
            IRBuilder builder,
            PrimitiveValue condition)
        {
            var target = condition.Int1Value ?
                TrueTarget :
                FalseTarget;
            return builder.CreateBranch(Location, target);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateIfBranch(
                Location,
                rebuilder.Rebuild(Condition),
                rebuilder.LookupTarget(TrueTarget),
                rebuilder.LookupTarget(FalseTarget),
                Flags);

        /// <summary>
        /// Creates a new if branch using the given targets.
        /// </summary>
        protected override TerminatorValue RemapTargets(
            BasicBlock.Builder builder,
            ref BlockList targets) =>
            builder.CreateIfBranch(
                Location,
                Condition,
                targets[0],
                targets[1],
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "branch";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = $"{Condition} ? {TrueTarget.ToReferenceString()} : " +
                FalseTarget.ToReferenceString();
            if (IsInverted)
                result += " [Inverted]";
            return result;
        }

        #endregion
    }

    /// <summary>
    /// Represents a single switch terminator.
    /// </summary>
    [ValueKind(ValueKind.SwitchBranch)]
    public sealed class SwitchBranch : ConditionalBranch
    {
        #region Nested Types

        /// <summary>
        /// An instance builder for switch branches.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private BlockList builder;

            /// <summary>
            /// Initializes a new call builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="condition">The switch condition value.</param>
            /// <param name="capacity">The initial builder capacity.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Builder(
                IRBuilder irBuilder,
                Location location,
                Value condition,
                int capacity)
            {
                builder = BlockList.Create(capacity);
                IRBuilder = irBuilder;
                Location = location;
                Condition = condition;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public IRBuilder IRBuilder { get; }

            /// <summary>
            /// Returns the current location.
            /// </summary>
            public Location Location { get; }

            /// <summary>
            /// Returns the current switch condition.
            /// </summary>
            public Value Condition { get; }

            /// <summary>
            /// The number of arguments.
            /// </summary>
            public int Count => builder.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given value to the switch builder.
            /// </summary>
            /// <param name="target">The target to add.</param>
            public void Add(BasicBlock target)
            {
                IRBuilder.AssertNotNull(target);
                builder.Add(target);
            }

            /// <summary>
            /// Constructs a new value that represents the current branch.
            /// </summary>
            /// <returns>The resulting value reference.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Branch Seal() =>
                IRBuilder.CreateSwitchBranch(Location, Condition, ref builder);

            #endregion
        }

        #endregion

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
            ref BlockList targets)
            : base(initializer)
        {
            Location.Assert(
                value.Type.IsPrimitiveType &&
                value.Type.BasicValueType.IsInt());

            SealTargets(ref targets);
            Seal(value);
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
        public BasicBlock GetCaseTarget(int i)
        {
            Location.Assert(i < Targets.Length - 1);
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
            return builder.CreateBranch(Location, target);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var branchBuilder = builder.CreateSwitchBranch(
                Location,
                rebuilder.Rebuild(Condition),
                NumTargets);
            foreach (var target in Targets)
                branchBuilder.Add(rebuilder.LookupTarget(target));
            return branchBuilder.Seal();
        }

        /// <summary>
        /// Creates a new switch branch using the given targets.
        /// </summary>
        protected override TerminatorValue RemapTargets(
            BasicBlock.Builder builder,
            ref BlockList targets) =>
            builder.CreateSwitchBranch(
                Location,
                Condition,
                ref targets);

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
            result.Append(' ');
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
        #region Nested Types

        /// <summary>
        /// An instance builder for temporary branches.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private BlockList builder;

            /// <summary>
            /// Initializes a new call builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="capacity">The initial builder capacity.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Builder(IRBuilder irBuilder, int capacity)
            {
                builder = BlockList.Create(capacity);
                IRBuilder = irBuilder;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public IRBuilder IRBuilder { get; }

            /// <summary>
            /// The number of arguments.
            /// </summary>
            public int Count => builder.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given value to the custom terminator builder.
            /// </summary>
            /// <param name="target">The target to add.</param>
            public void Add(BasicBlock target)
            {
                IRBuilder.AssertNotNull(target);
                builder.Add(target);
            }

            /// <summary>
            /// Constructs a new value that represents the current branch.
            /// </summary>
            /// <returns>The resulting value reference.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Branch Seal() =>
                IRBuilder.CreateBuilderTerminator(ref builder);

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a temporary builder terminator.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="targets">The jump targets.</param>
        internal BuilderTerminator(
            in ValueInitializer initializer,
            ref BlockList targets)
            : base(initializer)
        {
            SealTargets(ref targets);
            Seal();
        }

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
            throw this.GetInvalidOperationException();

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        protected override TerminatorValue RemapTargets(
            BasicBlock.Builder builder,
            ref BlockList targets) =>
            throw this.GetInvalidOperationException();

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) =>
            throw this.GetInvalidOperationException();

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "builderBr";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            Targets.ToString(new InlineList.DefaultFormatter<BasicBlock>());

        #endregion
    }
}
