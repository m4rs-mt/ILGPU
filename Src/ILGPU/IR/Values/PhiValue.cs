// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PhiValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BlockList = ILGPU.Util.InlineList<ILGPU.IR.BasicBlock>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

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
            /// Returns true if the given phi contains a block to remap.
            /// </summary>
            /// <param name="phiValue">The source phi.</param>
            /// <returns>True, if the given phi contains the old block.</returns>
            bool CanRemap(PhiValue phiValue);

            /// <summary>
            /// Tries to remap the given block to a new one.
            /// </summary>
            /// <param name="phiValue">The source phi.</param>
            /// <param name="block">The old block to remap.</param>
            /// <param name="newBlock">The (possible) remapped new block.</param>
            /// <returns>True, if the block could be remapped.</returns>
            bool TryRemap(PhiValue phiValue, BasicBlock block, out BasicBlock newBlock);

            /// <summary>
            /// Remaps the given value.
            /// </summary>
            /// <param name="phiValue">The source phi.</param>
            /// <param name="updatedBlock">The updated source information.</param>
            /// <param name="value">The original source value passed to the phi.</param>
            /// <returns>The new value to use instead.</returns>
            Value RemapValue(PhiValue phiValue, BasicBlock updatedBlock, Value value);
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
            /// <returns>True, if the given blocks contain the old block.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool CanRemap(PhiValue phiValue) =>
                phiValue.Sources.Contains(OldBlock, new BasicBlock.Comparer());

            /// <summary>
            /// Tries to remap the old block to the new block.
            /// </summary>
            /// <returns>Returns always true.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool TryRemap(
                PhiValue phiValue,
                BasicBlock block,
                out BasicBlock newBlock)
            {
                newBlock = block == OldBlock ? NewBlock : block;
                return true;
            }

            /// <summary>
            /// Returns the value of <paramref name="value"/>.
            /// </summary>
            public readonly Value RemapValue(
                PhiValue phiValue,
                BasicBlock updatedBlock,
                Value value) =>
                value;
        }

        /// <summary>
        /// A phi builder.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Naming",
            "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is the correct name of the current entity")]
        public sealed class Builder
        {
            #region Instance

            private ValueList arguments;
            private BlockList argumentBlocks;

            /// <summary>
            /// Constructs a new phi builder.
            /// </summary>
            /// <param name="phiValue">The phi value.</param>
            /// <param name="capacity">The initial capacity.</param>
            internal Builder(PhiValue phiValue, int capacity)
            {
                Debug.Assert(phiValue != null, "Invalid phi value");
                PhiValue = phiValue;

                arguments = ValueList.Create(capacity);
                argumentBlocks = BlockList.Create(capacity);
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
                predecessor.AssertNotNull(value);
                value.Assert(value.Type == Type);

                arguments.Add(value);
                argumentBlocks.Add(predecessor);
            }

            /// <summary>
            /// Seals this phi node.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PhiValue Seal()
            {
                PhiValue.SealPhiArguments(ref argumentBlocks, ref arguments);
                return PhiValue;
            }

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns a new enumerator.
            /// </summary>
            /// <returns>The created enumerator.</returns>
            public ReadOnlySpan<ValueReference>.Enumerator GetEnumerator() =>
                arguments.GetEnumerator();

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
            Value? same = null;
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

        private BlockList sourceBlocks;

        /// <summary>
        /// Constructs a new phi node.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="type">The phi type.</param>
        internal PhiValue(in ValueInitializer initializer, TypeNode type)
            : base(initializer)
        {
            Location.Assert(!type.IsVoidType);
            sourceBlocks = BlockList.Empty;
            PhiType = type;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Phi;

        /// <summary>
        /// Returns the basic phi type.
        /// </summary>
        public TypeNode PhiType { get; private set; }

        /// <summary>
        /// Returns all associated blocks from which the values have to be resolved
        /// from.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public ReadOnlySpan<BasicBlock> Sources => sourceBlocks;

        #endregion

        #region Methods

        /// <summary>
        /// Gets the value for the given source block.
        /// </summary>
        /// <param name="source">The source block.</param>
        /// <returns>The value for the given source block (if any).</returns>
        public Value? GetValue(BasicBlock source)
        {
            var index = sourceBlocks.IndexOf(source, new BasicBlock.Comparer());
            return index >= 0 ? this[index].Resolve() : null;
        }

        /// <summary>
        /// Updates the current phi type.
        /// </summary>
        /// <typeparam name="TTypeContext">The type context.</typeparam>
        /// <typeparam name="TTypeConverter">The type converter.</typeparam>
        /// <param name="typeContext">The type context instance.</param>
        /// <param name="typeConverter">The type converter instance.</param>
        internal void UpdateType<TTypeContext, TTypeConverter>(
            TTypeContext typeContext,
            TTypeConverter typeConverter)
            where TTypeContext : IIRTypeContext
            where TTypeConverter : ITypeConverter<TypeNode>
        {
            PhiType = typeConverter.ConvertType(typeContext, PhiType);
            InvalidateType();
        }

        /// <summary>
        /// Remaps the current phi arguments.
        /// </summary>
        /// <typeparam name="TArgumentRemaper">The argument remapper type.</typeparam>
        /// <param name="methodBuilder">The parent method builder.</param>
        /// <param name="remapper">The remapper instance.</param>
        /// <returns>The remapped phi value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value RemapArguments<TArgumentRemaper>(
            Method.Builder methodBuilder,
            TArgumentRemaper remapper)
            where TArgumentRemaper : struct, IArgumentRemapper
        {
            // Check for a valid block association
            this.Assert(methodBuilder.Method == BasicBlock.Method);
            if (!remapper.CanRemap(this))
                return this;

            var blockBuilder = methodBuilder[BasicBlock];
            var phiBuilder = blockBuilder.CreatePhi(Location, Type);
            for (int i = 0, e = Nodes.Length; i < e; ++i)
            {
                if (!remapper.TryRemap(this, Sources[i], out var newSource))
                    continue;

                phiBuilder.AddArgument(
                    newSource,
                    remapper.RemapValue(this, newSource, Nodes[i]));
            }

            var newPhi = phiBuilder.Seal();
            Replace(newPhi);
            blockBuilder.Remove(this);
            return newPhi.TryRemoveTrivialPhi(methodBuilder);
        }

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            PhiType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            // Phi values have already been mapped in the beginning
            rebuilder.Rebuild(this);

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer)
        {
            int index = 0;
            foreach (var source in Sources)
                writer.Write($"{nameof(Sources)}[{index++}]", source.Id);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary>
        /// Seals the given phi arguments.
        /// </summary>
        /// <param name="sources">The associated block sources.</param>
        /// <param name="arguments">The phi arguments.</param>
        internal void SealPhiArguments(
            ref BlockList sources,
            ref ValueList arguments)
        {
            this.Assert(arguments.Count == sources.Count);
            Seal(ref arguments);
            sources.MoveTo(ref sourceBlocks);
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
            Count < 1 ? string.Empty : ToFullArgString();

        /// <summary>
        /// Returns a full argument string representation.
        /// </summary>
        private string ToFullArgString() =>
            Nodes.ToString(new ValueReference.ToReferenceFormatter()) +
            $" [{Sources.ToString(new BasicBlock.ToReferenceFormatter())}]";

        #endregion
    }
}
