// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PhiValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single control-flow dependent phi node.
    /// </summary>
    [ValueKind(ValueKind.Phi)]
    public sealed class PhiValue : Value
    {
        #region Nested Types

        /// <summary>
        /// Remaps phi argument blocks.
        /// </summary>
        public interface IArgumentRemapper
        {
            /// <summary>
            /// Returns true if the given blocks contain a block to remap.
            /// </summary>
            /// <param name="blocks">The blocks to check.</param>
            /// <returns>True, if the given blocks contain the old block.</returns>
            bool CanRemap(ImmutableArray<BasicBlock> blocks);

            /// <summary>
            /// Tries to remap the given block to a new one.
            /// </summary>
            /// <param name="block">The old block to remap.</param>
            /// <param name="newBlock">The (possible) remapped new block.</param>
            /// <returns>True, if the block could be remapped.</returns>
            bool TryRemap(BasicBlock block, out BasicBlock newBlock);
        }

        /// <summary>
        /// A simple remapper that allows to map an old block to a new block.
        /// </summary>
        public readonly struct BlockRemapper : IArgumentRemapper
        {
            /// <summary>
            /// Constructs a new block remapper.
            /// </summary>
            /// <param name="oldBlock">The old block.</param>
            /// <param name="newBlock">The new block.</param>
            public BlockRemapper(BasicBlock oldBlock, BasicBlock newBlock)
            {
                OldBlock = oldBlock;
                NewBlock = newBlock;
            }

            /// <summary>
            /// Returns the old block.
            /// </summary>
            public BasicBlock OldBlock { get; }

            /// <summary>
            /// Returns the new block.
            /// </summary>
            public BasicBlock NewBlock { get; }

            /// <summary>
            /// Returns true if the given blocks contain the old block.
            /// </summary>
            /// <param name="blocks">The blocks to check.</param>
            /// <returns>True, if the given blocks contain the old block.</returns>
            public bool CanRemap(ImmutableArray<BasicBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (OldBlock == block)
                        return true;
                }

                return false;
            }

            /// <summary>
            /// Tries to remap the old block to the new block.
            /// </summary>
            /// <returns>Returns always true.</returns>
            public bool TryRemap(BasicBlock block, out BasicBlock newBlock)
            {
                newBlock = block == OldBlock ? NewBlock : block;
                return true;
            }
        }

        /// <summary>
        /// A phi builder.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Naming",
            "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is the correct name of the current entity")]
        public sealed class Builder : IReadOnlyCollection<ValueReference>
        {
            #region Instance

            private readonly ImmutableArray<ValueReference>.Builder arguments;
            private readonly ImmutableArray<BasicBlock>.Builder argumentBlocks;

            /// <summary>
            /// Constructs a new phi builder.
            /// </summary>
            /// <param name="phiValue">The phi value.</param>
            internal Builder(PhiValue phiValue)
            {
                Debug.Assert(phiValue != null, "Invalid phi value");
                PhiValue = phiValue;

                arguments = ImmutableArray.CreateBuilder<ValueReference>();
                argumentBlocks = ImmutableArray.CreateBuilder<BasicBlock>();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated phi value.
            /// </summary>
            public PhiValue PhiValue { get; }

            /// <summary>
            /// Returns the node type.
            /// </summary>
            public TypeNode Type => PhiValue.PhiType;

            /// <summary>
            /// Returns the number of attached arguments.
            /// </summary>
            public int Count => arguments.Count;

            /// <summary>
            /// Returns the i-th argument.
            /// </summary>
            /// <param name="index">The argument index.</param>
            /// <returns>The resolved argument.</returns>
            public ValueReference this[int index] => arguments[index];

            /// <summary>
            /// Returns the parent basic block.
            /// </summary>
            public BasicBlock BasicBlock => PhiValue.BasicBlock;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given argument.
            /// </summary>
            /// <param name="predecessor">
            /// The input block associated with the argument value.
            /// </param>
            /// <param name="value">The argument value to add.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddArgument(BasicBlock predecessor, Value value)
            {
                Debug.Assert(value != null, "Invalid phi argument");
                Debug.Assert(value.Type == Type, "Incompatible phi argument");

                arguments.Add(value);
                argumentBlocks.Add(predecessor);
            }

            /// <summary>
            /// Seals this phi node.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PhiValue Seal()
            {
                PhiValue.SealPhiArguments(
                    argumentBlocks.ToImmutable(),
                    arguments.ToImmutable());
                return PhiValue;
            }

            #endregion

            #region IEnumerable

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            public IEnumerator<ValueReference> GetEnumerator() =>
                arguments.GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Tries to remove a trivial phi value.
        /// </summary>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <param name="phiValue">The phi value to check.</param>
        /// <returns>The resolved value.</returns>
        public static Value TryRemoveTrivialPhi(
            Method.Builder methodBuilder,
            PhiValue phiValue)
        {
            // Implements a part of the SSA-construction algorithm from the paper:
            // Simple and Efficient Construction of Static Single Assignment Form
            // See also: SSABuilder.cs

            Debug.Assert(phiValue != null, "Invalid phi value to remove");
            Value same = null;
            foreach (Value argument in phiValue.Nodes)
            {
                if (same == argument || argument == phiValue)
                    continue;
                if (same != null)
                    return phiValue;
                same = argument;
            }

            // Unreachable phi
            if (same == null)
                return phiValue;

            var uses = phiValue.Uses.Clone();
            phiValue.Replace(same);

            // Remove the phi node from the current block
            var builder = methodBuilder[phiValue.BasicBlock];
            builder.Remove(phiValue);

            foreach (var use in uses)
            {
                if (use.Resolve() is PhiValue usedPhi)
                    TryRemoveTrivialPhi(methodBuilder, usedPhi);
            }

            return same;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new phi node.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="type">The phi type.</param>
        internal PhiValue(BasicBlock basicBlock, TypeNode type)
            : base(basicBlock, type)
        {
            Debug.Assert(type != null, "Invalid type");
            Debug.Assert(!type.IsVoidType, "Invalid void type");

            PhiType = type;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Phi;

        /// <summary>
        /// Returns the basic phi type.
        /// </summary>
        public TypeNode PhiType { get; }

        /// <summary>
        /// Returns all associated blocks from which the values have to be resolved
        /// from.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public ImmutableArray<BasicBlock> Sources { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Remaps the current phi arguments.
        /// </summary>
        /// <typeparam name="TArgumentRemaper">The argument remapper type.</typeparam>
        /// <param name="blockBuilder">The current block builder.</param>
        /// <param name="remapper">The remapper instance.</param>
        /// <returns>The remapped phi value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PhiValue RemapArguments<TArgumentRemaper>(
            BasicBlock.Builder blockBuilder,
            TArgumentRemaper remapper)
            where TArgumentRemaper : IArgumentRemapper
        {
            Debug.Assert(blockBuilder.BasicBlock == BasicBlock, "Invalid block builder");
            if (!remapper.CanRemap(Sources))
                return this;

            var phiBuilder = blockBuilder.CreatePhi(Type);
            for (int i = 0, e = Nodes.Length; i < e; ++i)
            {
                if (!remapper.TryRemap(Sources[i], out var newSource))
                    continue;

                phiBuilder.AddArgument(newSource, Nodes[i]);
            }

            var newPhi = phiBuilder.Seal();
            Replace(newPhi);
            blockBuilder.Remove(this);
            return newPhi;
        }

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) => PhiType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            // Phi values have already been mapped in the beginning
            rebuilder.Rebuild(this);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary>
        /// Seals the given phi arguments.
        /// </summary>
        /// <param name="sources">The associated block sources.</param>
        /// <param name="arguments">The phi arguments.</param>
        internal void SealPhiArguments(
            ImmutableArray<BasicBlock> sources,
            ImmutableArray<ValueReference> arguments)
        {
            Debug.Assert(arguments.Length == sources.Length);
            Seal(arguments);
            Sources = sources;
        }

        /// <summary>
        /// Tries to remove a trivial phi value.
        /// </summary>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <returns>The resolved value.</returns>
        public Value TryRemoveTrivialPhi(Method.Builder methodBuilder) =>
            TryRemoveTrivialPhi(methodBuilder, this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "phi";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            Nodes.IsDefaultOrEmpty
            ? string.Empty
            : string.Join(", ", Nodes);

        #endregion
    }
}
