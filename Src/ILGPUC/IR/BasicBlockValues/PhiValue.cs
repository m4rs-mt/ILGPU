// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PhiValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BlockList = ILGPU.Util.InlineList<ILGPUC.IR.MethodValues.BasicBlock>;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents a single control-flow dependent phi node.
/// </summary>
/// <remarks>
/// Constructs a new phi node.
/// </remarks>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">The phi type.</param>
sealed partial class PhiValue(
    in BasicBlockValueInitializer initializer,
    TypeValue type) : BasicBlockValue(initializer, type)
{
    #region Nested Types

    /// <summary>
    /// A phi builder.
    /// </summary>
    /// <param name="builder">The parent builder.</param>
    /// <param name="phiValue">The phi value.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <param name="argumentMapper">The argument mapper to use.</param>
    internal sealed class Builder(
        BasicBlockBuilder builder,
        PhiValue phiValue,
        int capacity,
        Func<Value, Value>? argumentMapper)
    {
        private ValueBuilderList _arguments =
            ValueBuilderList.Create(builder.Generation, capacity);
        private BlockList _argumentBlocks = BlockList.Create(capacity);
        private readonly Func<Value, Value> _argumentMapper =
            argumentMapper ?? (static x => x);

        /// <summary>
        /// Returns the associated phi value.
        /// </summary>
        public PhiValue PhiValue { get; } = phiValue;

        /// <summary>
        /// Returns the node type.
        /// </summary>
        public TypeValue Type => PhiValue.Type;

        /// <summary>
        /// Returns the number of attached arguments.
        /// </summary>
        public int Count => _arguments.Count;

        /// <summary>
        /// Returns the i-th argument.
        /// </summary>
        /// <param name="index">The argument index.</param>
        /// <returns>The resolved argument.</returns>
        public Value this[int index] => _arguments.Get<Value>(index);

        /// <summary>
        /// Adds the given argument.
        /// </summary>
        /// <param name="predecessor">
        /// The input block associated with the argument value.
        /// </param>
        /// <param name="value">The argument value to add.</param>
        public void AddArgument(BasicBlock predecessor, Value value)
        {
            predecessor.AssertNotNull(value);
            value.Assert(value.Type.Equals(Type));

            _arguments.Add(_argumentMapper(value));
            _argumentBlocks.Add(predecessor);
        }

        /// <summary>
        /// Seals this phi node.
        /// </summary>
        public PhiValue Seal()
        {
            PhiValue.SealPhiArguments(ref _argumentBlocks, ref _arguments);
            return PhiValue;
        }

        /// <summary>
        /// Returns a new enumerator.
        /// </summary>
        /// <returns>The created enumerator.</returns>
        public ReadOnlySpan<Value>.Enumerator GetEnumerator() =>
            _arguments.GetEnumerator();
    }

    /// <summary>
    /// Represents a collection of phi-value sources.
    /// </summary>
    /// <param name="phiValue">The phi value.</param>
    internal readonly struct SourcesCollection(PhiValue phiValue)
    {
        /// <summary>
        /// Returns the number of sources.
        /// </summary>
        public int Count => phiValue.NumSources;

        /// <summary>
        /// Returns the i-th source.
        /// </summary>
        /// <param name="index">The source index.</param>
        /// <returns>The basic block.</returns>
        public BasicBlock this[int index] =>
            RawSources[index].AsNotNullCast<BasicBlock>();

        /// <summary>
        /// Returns a span of raw value sources.
        /// </summary>
        public ReadOnlySpan<Value> RawSources => phiValue.Values[Count..];

        /// <summary>
        /// Returns an enumerator to iterate over all sources.
        /// </summary>
        public SourcesCollectionEnumerator GetEnumerator() => new(this);
    }

    /// <summary>
    /// Represents an enumerator for phi-value sources.
    /// </summary>
    /// <param name="sourcesCollection">The sources collection.</param>
    internal ref struct SourcesCollectionEnumerator(SourcesCollection sourcesCollection)
    {
        private ReadOnlySpan<Value>.Enumerator _enumerator =
            sourcesCollection.RawSources.GetEnumerator();

        /// <inheritdoc cref="IEnumerator.Current"/>
        public BasicBlock Current => _enumerator.Current.AsNotNullCast<BasicBlock>();

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    #endregion

    #region PhiValue Instance

    /// <summary>
    /// Returns all associated blocks from which the values have to be resolved
    /// from.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
    public SourcesCollection Sources => new(this);

    /// <summary>
    /// Returns all associated values received by incoming sources.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
    public ReadOnlySpan<Value> Arguments => Values[..NumArguments];

    /// <summary>
    /// Returns the number of sources.
    /// </summary>
    public int NumSources => NumArguments;

    /// <summary>
    /// Returns the number of arguments.
    /// </summary>
    public int NumArguments { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var builder = rewriter.Builder.CreatePhi(
            Location,
            rewriter.RewriteAs<TypeValue>(Type),
            capacity: NumArguments);

        for (int i = 0; i < NumArguments; ++i)
        {
            if (rewriter.Rewrite(Sources[i]) is not BasicBlock source) continue;

            var argument = rewriter.Rewrite(Arguments[i]);
            if (argument is null) continue;

            builder.AddArgument(source, argument);
        }

        return builder.Seal();
    }

    /// <summary>
    /// Seals the given phi arguments.
    /// </summary>
    /// <param name="sources">The associated block sources.</param>
    /// <param name="arguments">The phi arguments.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void SealPhiArguments(
        ref BlockList sources,
        ref ValueBuilderList arguments)
    {
        this.Assert(
            arguments.Count == sources.Count,
            $"PhiValue argument/source count mismatch: {arguments.Count} arguments vs."
            + $" {sources.Count} sources");

        NumArguments = arguments.Count;
        foreach (var sourceBlock in sources)
            arguments.Add(sourceBlock);
        Seal(ref arguments);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "phi";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        Count < 1 ? string.Empty : ToFullArgString();

    /// <summary>
    /// Returns a full argument string representation.
    /// </summary>
    private string ToFullArgString()
    {
        var argumentsString = Arguments.ToString(static t => t.ToReferenceString());
        var sourcesString = Sources.RawSources.ToString(
            static t => t.ToReferenceString());
        return $"{argumentsString} [{sourcesString}]";
    }

    #endregion
}
