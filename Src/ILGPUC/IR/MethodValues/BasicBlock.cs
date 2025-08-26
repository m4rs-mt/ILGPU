// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Rewriting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// Represents a single basic block.
/// </summary>
/// <param name="initializer">The initializer to use.</param>
/// <param name="name">The name of the block (or null).</param>
[SuppressMessage(
    "Microsoft.Naming",
    "CA1710: IdentifiersShouldHaveCorrectSuffix",
    Justification = "This is the correct name of the current entity")]
sealed partial class BasicBlock(in MethodValueInitializer initializer, string? name) :
    MethodValue(initializer, initializer.ModuleBuilder.VoidType),
    IValueScope,
    IDumpable
{
    #region Nested Types

    /// <summary>
    /// An equality comparer for basic blocks.
    /// </summary>
    internal readonly struct BlockComparer : IEqualityComparer<BasicBlock>
    {
        /// <summary>
        /// Returns true if both blocks represent the same block.
        /// </summary>
        public bool Equals(BasicBlock? x, BasicBlock? y) => x == y;

        /// <summary>
        /// Returns true if both blocks represent the same block.
        /// </summary>
        public int GetHashCode(BasicBlock obj) => obj.GetHashCode();
    }

    /// <summary>
    /// Represents a value reference within a single basic block.
    /// </summary>
    /// <param name="Value">The actual value reference.</param>
    /// <param name="Index">The index within the block.</param>
    internal readonly record struct ValueEntry(BasicBlockValue Value, int Index)
    {
        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => Value.BasicBlock;

        /// <summary>
        /// Returns the string representation of the underlying value.
        /// </summary>
        /// <returns>The string representation of the underlying value.</returns>
        public override string ToString() =>
            $"{Value} @ {BasicBlock.ToReferenceString()}";

        /// <summary>
        /// Implicitly converts the given value entry to its associated value.
        /// </summary>
        /// <param name="valueEntry">The value entry to convert.</param>
        public static implicit operator BasicBlockValue(ValueEntry valueEntry) =>
            valueEntry.Value;

        /// <summary>
        /// Implicitly converts the given tuple entry to its associated value entry.
        /// </summary>
        /// <param name="tuple">The tuple to convert.</param>
        public static implicit operator ValueEntry(
            (BasicBlock block, BasicBlockValue Value, int Index) tuple) =>
            new(tuple.Value, tuple.Index);
    }

    /// <summary>
    /// Represents a value reference within a single basic block.
    /// </summary>
    /// <param name="Value">The actual value reference.</param>
    /// <param name="Index">The index within the block.</param>
    internal readonly record struct ValueEntry<TValue>(TValue Value, int Index)
        where TValue : BasicBlockValue
    {
        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => Value.BasicBlock;

        /// <summary>
        /// Returns the string representation of the underlying value.
        /// </summary>
        /// <returns>The string representation of the underlying value.</returns>
        public override string ToString() =>
            $"{Value} @ {BasicBlock.ToReferenceString()}";
    }

    /// <summary>
    /// A collection for non-termination values.
    /// </summary>
    /// <param name="basicBlock">The parent basic block.</param>
    internal readonly struct BasicBlockValueCollection(BasicBlock basicBlock)
    {
        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => basicBlock;

        /// <summary>
        /// Returns the number of all directly associated basic block values.
        /// </summary>
        public int Count => basicBlock.NumPhiValues + basicBlock.NumValues;

        /// <summary>
        /// Returns the first basic block value of this block (if any).
        /// </summary>
        public BasicBlockValue? FirstValue =>
            basicBlock.FirstPhiValue ?? basicBlock.FirstValue;

        /// <summary>
        /// Returns the last basic block value of this block (if any).
        /// </summary>
        public BasicBlockValue? LastValue => basicBlock.LastValue;

        /// <summary>
        /// Returns the current enumerator.
        /// </summary>
        public BasicBlockValueCollectionEnumerator GetEnumerator() => new(basicBlock);
    }

    /// <summary>
    /// An enumerator for non-terminator values.
    /// </summary>
    /// <param name="basicBlock">The basic block to iterate over.</param>
    internal ref struct BasicBlockValueCollectionEnumerator(BasicBlock basicBlock)
    {
        private int _index;
        private BasicBlockValue? _current = null;
        private BasicBlockValue? _next =
            basicBlock.FirstPhiValue ?? basicBlock.FirstValue;

        /// <summary>
        /// Returns the current node.
        /// </summary>
        public readonly ValueEntry Current => new(_current.AsNotNull(), _index);

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext()
        {
            if (_next is null) return false;

            _current = _next;
            _next = _next.Next;

            if (_next is null && _current is PhiValue)
                _next = basicBlock.FirstValue;

            ++_index;
            return true;
        }
    }

    /// <summary>
    /// A collection for all values in this basic block.
    /// </summary>
    /// <param name="basicBlock">The parent basic block.</param>
    internal readonly struct ValueCollection(BasicBlock basicBlock)
    {
        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => basicBlock;

        /// <summary>
        /// Returns the number of all directly associated values.
        /// </summary>
        public int Count =>
            basicBlock.NumValues
            + basicBlock.NumPhiValues
            + (basicBlock.TerminationValue is not null ? 1 : 0);

        /// <summary>
        /// Returns the first value of this block (if any).
        /// </summary>
        public Value? FirstValue => basicBlock.FirstPhiValue
            ?? basicBlock.FirstValue
            ?? basicBlock.TerminationValue;

        /// <summary>
        /// Returns the current enumerator.
        /// </summary>
        public ValueCollectionEnumerator GetEnumerator() => new(basicBlock);
    }

    /// <summary>
    /// An enumerator for non-terminator values.
    /// </summary>
    /// <param name="basicBlock">The basic block to iterate over.</param>
    internal ref struct ValueCollectionEnumerator(BasicBlock basicBlock)
    {
        private BasicBlockValueCollectionEnumerator _enumerator = new(basicBlock);

        /// <summary>
        /// Returns the current node.
        /// </summary>
        public Value Current { readonly get; private set; } = default!;

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_enumerator.MoveNext())
            {
                Current = _enumerator.Current;
                return true;
            }

            // Check for the termination value
            if (basicBlock.TerminationValue is not null &&
                Current != basicBlock.TerminationValue)
            {
                Current = basicBlock.TerminationValue;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// A collection for all phi values in this basic block.
    /// </summary>
    /// <param name="basicBlock">The parent basic block.</param>
    internal readonly struct PhiValueCollection(BasicBlock basicBlock)
    {
        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => basicBlock;

        /// <summary>
        /// Returns the first phi value (if any).
        /// </summary>
        public PhiValue? First => basicBlock.FirstPhiValue;

        /// <summary>
        /// Returns the number of phi values.
        /// </summary>
        public int Count => basicBlock.NumPhiValues;

        /// <summary>
        /// Returns the current enumerator.
        /// </summary>
        public PhiValueCollectionEnumerator GetEnumerator() => new(basicBlock);
    }

    /// <summary>
    /// An enumerator for non-terminator values.
    /// </summary>
    /// <param name="basicBlock">The basic block to iterate over.</param>
    internal ref struct PhiValueCollectionEnumerator(BasicBlock basicBlock)
    {
        private PhiValue? _current = null;
        private PhiValue? _next = basicBlock.FirstPhiValue;

        /// <summary>
        /// Returns the current node.
        /// </summary>
        public readonly PhiValue Current => _current.AsNotNull();

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_next is null) return false;
            _current = _next;
            _next = _next.Next as PhiValue;
            return true;
        }
    }

    /// <summary>
    /// A provider that uses registered successors and predecessors of a block.
    /// </summary>
    /// <typeparam name="TDirection">The direction type information.</typeparam>
    internal readonly struct SuccessorsProvider<TDirection> :
        ITraversalSuccessorsProvider<BasicBlock, TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        /// <summary>
        /// Returns registered successors or predecessors of a block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<BasicBlock> GetSuccessors(BasicBlock basicBlock) =>
            basicBlock.GetSuccessors<TDirection>();
    }

    /// <summary>
    /// Builder for successors of blocks.
    /// </summary>
    internal readonly struct SuccessorsBuilder(BasicBlock basicBlock)
    {
        /// <summary>
        /// Gets the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => basicBlock;

        /// <summary>
        /// Adds a successor.
        /// </summary>
        public SuccessorsBuilder AddSuccessor(BasicBlock target)
        {
            basicBlock._successors.Add(target);
            return this;
        }

        /// <summary>
        /// Adds successors.
        /// </summary>
        public void AddSuccessors(ReadOnlySpan<BasicBlock> targets) =>
            basicBlock._successors.AddRange(targets);
    }

    #endregion

    #region Instance

    /// <summary>
    /// A list of all successors.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private InlineList<BasicBlock> _successors = InlineList<BasicBlock>.Create(2);

    /// <summary>
    /// A list of all predecessors.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private InlineList<BasicBlock> _predecessors = InlineList<BasicBlock>.Create(2);

    #endregion

    #region Properties

    /// <summary>
    /// Returns the block name.
    /// </summary>
    public string Name { get; } = name ?? "BB";

    /// <summary>
    /// The list of predecessors (see <see cref="Backwards"/>).
    /// </summary>
    public ReadOnlySpan<BasicBlock> Predecessors => GetPredecessors<Forwards>();

    /// <summary>
    /// The current list of successors (see <see cref="Forwards"/>).
    /// </summary>
    public ReadOnlySpan<BasicBlock> Successors => GetSuccessors<Forwards>();

    /// <summary>
    /// Returns the number of phi values.
    /// </summary>
    public int NumPhiValues { get; private set; }

    /// <summary>
    /// Returns the number of values.
    /// </summary>
    public int NumValues { get; private set; }

    /// <summary>
    /// Returns all values without the termination value (for iteration during
    /// transformation).
    /// </summary>
    public BasicBlockValueCollection BasicBlockValues => new(this);

    /// <summary>
    /// Returns all basic block values including the termination value.
    /// </summary>
    public new ValueCollection Values => new(this);

    /// <summary>
    /// Returns the termination kind of this block.
    /// </summary>
    public BlockTerminationKind TerminationKind { get; private set; }

    /// <summary>
    /// Returns all phi values in this block (separate from the monad chain).
    /// </summary>
    public PhiValueCollection PhiValues => new(this);

    /// <summary>
    /// Gets the condition value for conditional or switch terminations.
    /// Returns null for unconditional branches and returns.
    /// </summary>
    public Value? TerminationCondition => TerminationKind switch
    {
        BlockTerminationKind.Conditional or
        BlockTerminationKind.Switch => GetValue<Value>(Count - 1),
        _ => null
    };

    /// <summary>
    /// Gets the return value for return terminations.
    /// Returns null for non-return terminations.
    /// </summary>
    public Value? TerminationValue
    {
        get
        {
            if (HasTerminationValue)
            {
                this.Assert(Count > 0,
                    $"Block '{Name}' has HasTerminationValue=true but Count=0");
                return GetValue<Value>(Count - 1);
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the return value for return terminations.
    /// Returns null for non-return terminations.
    /// </summary>
    public bool HasTerminationValue => TerminationKind switch
    {
        BlockTerminationKind.Conditional or
        BlockTerminationKind.Switch or
        BlockTerminationKind.Return => true,
        _ => false
    };

    /// <summary>
    /// Returns the first phi value in this block (if any).
    /// </summary>
    public PhiValue? FirstPhiValue { get; private set; }

    /// <summary>
    /// Returns the first value in this block (if any).
    /// </summary>
    public BasicBlockValue? FirstValue { get; private set; }

    /// <summary>
    /// Returns the last value in this block (if any).
    /// </summary>
    public BasicBlockValue? LastValue { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Determines the actual predecessors based on the specified direction.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<BasicBlock> GetPredecessors<TDirection>()
        where TDirection : struct, IControlFlowDirection =>
        TDirection.IsForwards ? _predecessors : _successors;

    /// <summary>
    /// Determines the actual successors based on the specified direction.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<BasicBlock> GetSuccessors<TDirection>()
        where TDirection : struct, IControlFlowDirection
    {
        Debug.Assert(IsSealed | TDirection.IsForwards);
        return TDirection.IsForwards ? _successors : _predecessors;
    }

    /// <summary>
    /// Tries to find the first value of the given type that fulfills the given
    /// predicate in this block.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="predicate">The predicate.</param>
    /// <param name="entry">
    /// The result pair consisting of a value index and the matched value itself.
    /// </param>
    /// <returns>True, if a value could be matched.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryFindFirstValueOf<T>(Predicate<T> predicate, out ValueEntry<T> entry)
        where T : BasicBlockValue
    {
        entry = default;
        foreach (var (value, index) in BasicBlockValues)
        {
            if (value is T tValue && predicate(tValue))
            {
                entry = new(tValue, index);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Dumps this block to the given text writer.
    /// </summary>
    public override void Dump(TextWriter textWriter)
    {
        textWriter.Write(ToString());
        textWriter.WriteLine(":");
        foreach (PhiValue value in PhiValues)
        {
            textWriter.Write("\t");
            textWriter.WriteLine(value.ToString());
        }
        foreach (BasicBlockValue value in this)
        {
            textWriter.Write("\t");
            textWriter.WriteLine(value.ToString());
        }

        textWriter.Write("\t");
        this.DumpTermination(textWriter);
    }

    /// <summary>
    /// Executes the given callback for each value in this block.
    /// </summary>
    /// <param name="callback">The callback.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachValue<TValue>(Action<TValue> callback)
        where TValue : Value<Method>
    {
        ValueSet<Method, TValue>? set = null;
        ForEachValue(callback, ref set);
    }

    /// <summary>
    /// Executes the given callback for each value in this block.
    /// </summary>
    /// <param name="callback">The callback.</param>
    /// <param name="set">The value set to ensure values are visited once.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ForEachValue<TValue>(
        Action<TValue> callback,
        ref ValueSet<Method, TValue>? set)
        where TValue : Value<Method>
    {
        if (ValueUtil<TValue>.IsPureValueOrBase)
            set ??= Method.CreateSet<TValue>();

        if (ValueUtil<TValue>.IsBasicBlockValueOrBase)
        {
            foreach (BasicBlockValue value in this)
            {
                if (value is TValue reinterpretedValue)
                    callback(reinterpretedValue);

                if (ValueUtil<TValue>.IsPureValueOrBase)
                {
                    foreach (var child in value.Values)
                    {
                        if (child is PureValue pureValue)
                            ForEachValueRecursive(pureValue, set!.Value, callback);
                    }
                }
            }

            if (ValueUtil<TValue>.IsPureValueOrBase &&
                TerminationValue is PureValue pureValueTerminator)
            {
                ForEachValueRecursive(pureValueTerminator, set!.Value, callback);
            }
        }
        else if (ValueUtil<TValue>.IsPureValueOrBase)
        {
            foreach (BasicBlockValue value in this)
            {
                foreach (var child in value.Values)
                {
                    if (child is PureValue pureValue)
                        ForEachValueRecursive(pureValue, set!.Value, callback);
                }
            }

            if (TerminationValue is PureValue pureValueTerminator)
                ForEachValueRecursive(pureValueTerminator, set!.Value, callback);
        }
    }

    /// <summary>
    /// Internal recursive helper to iterate over pure values.
    /// </summary>
    /// <param name="pureValue">The pure value to traverse.</param>
    /// <param name="set">The value set to ensure values are visited once.</param>
    /// <param name="callback">The callback.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ForEachValueRecursive<TValue>(
        PureValue pureValue,
        ValueSet<Method, TValue> set,
        Action<TValue> callback)
        where TValue : Value<Method>
    {
        if (pureValue is TValue cast)
        {
            if (!set.Add(cast)) return;
            callback(cast);
        }

        foreach (var value in pureValue.Values)
        {
            if (value is PureValue nestedPureValue)
                ForEachValueRecursive(nestedPureValue, set, callback);
        }
    }

    /// <summary>
    /// Sets up internal predecessors.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    internal void SetupPredecessors()
    {
        Debug.Assert(!IsSealed);

        // Wire predecessors and successors properly
        foreach (var successor in _successors)
            successor._predecessors.Add(this);
    }

    /// <summary>
    /// Clears all predecessors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ClearPredecessor()
    {
        Debug.Assert(!IsSealed);

        _predecessors.Clear();
    }

    /// <summary>
    /// Removes a specific predecessor from this block.
    /// </summary>
    /// <param name="predecessor">The predecessor to remove.</param>
    /// <returns>True if the predecessor was found and removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool RemovePredecessor(BasicBlock predecessor)
    {
        Debug.Assert(!IsSealed);

        for (int i = 0; i < _predecessors.Count; ++i)
        {
            if (_predecessors[i] == predecessor)
            {
                _predecessors.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets the termination kind for this block.
    /// </summary>
    /// <param name="kind">The termination kind.</param>
    /// <param name="successorCapacity">The initial number of successors.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal SuccessorsBuilder SetTermination(
        BlockTerminationKind kind,
        int successorCapacity = 1)
    {
        TerminationKind = kind;

        _successors.Clear();
        if (successorCapacity > 0)
            _successors.Reserve(successorCapacity);
        return new(this);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override bool ComputeUses(GlobalValueSet visited)
    {
        if (!base.ComputeUses(visited)) return false;

        foreach (var (bbValue, _) in BasicBlockValues)
            bbValue.ComputeUses(visited);

        TerminationValue?.AddUseInternal(new Use(this, Index: 0));

        return true;
    }

    /// <summary>
    /// Seals this block using provided information by a builder.
    /// </summary>
    /// <param name="numPhiValues">The number of phi values.</param>
    /// <param name="firstPhiValue">The first phi value (if any).</param>
    /// <param name="numValues">The number of basic block values.</param>
    /// <param name="firstValue">The first basic block value (if any).</param>
    /// <param name="lastValue">The last basic block value (if any).</param>
    /// <param name="terminationValue">The termination value (if any).</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void SealBlock(
        int numPhiValues,
        PhiValue? firstPhiValue,
        int numValues,
        BasicBlockValue? firstValue,
        BasicBlockValue? lastValue,
        Value? terminationValue)
    {
        this.Assert(TerminationKind > BlockTerminationKind.Pending,
            $"Block '{Name}' is being sealed without a termination " +
            $"(TerminationKind ={TerminationKind})");

        // Setup predecessors
        SetupPredecessors();

        // Store values
        NumPhiValues = numPhiValues;
        FirstPhiValue = firstPhiValue;

        NumValues = numValues;
        FirstValue = firstValue;
        LastValue = lastValue;

        if (terminationValue is not null)
            Seal(terminationValue);
        else
            Seal();
    }

    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    /// <param name="returnBlock">The return block to use for returns.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void CopyTerminationTo<TRewriter>(
        in TRewriter rewriter,
        BasicBlock? returnBlock = null)
        where TRewriter : IBasicBlockRewriter, allows ref struct
    {
        switch (TerminationKind)
        {
            case BlockTerminationKind.Pending:
                var pendingTargets = new BasicBlock[_successors.Count];
                for (int i = 0; i < _successors.Count; i++)
                    pendingTargets[i] = rewriter.RewriteAs<BasicBlock>(_successors[i]);
                rewriter.Builder.CreatePendingTermination(pendingTargets);
                break;
            case BlockTerminationKind.Unconditional:
                this.AsUnconditionalView().CopyTo(rewriter);
                break;
            case BlockTerminationKind.Conditional:
                this.AsConditionalView().CopyTo(rewriter);
                break;
            case BlockTerminationKind.Switch:
                this.AsSwitchView().CopyTo(rewriter);
                break;
            case BlockTerminationKind.Return:
                this.AsReturnView().CopyTo(rewriter, returnBlock);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>
    /// Creates a new basic block rewriter from this block.
    /// </summary>
    /// <param name="methodBuilder">The method builder to use.</param>
    /// <returns>The basic block rewriter for this block.</returns>
    public BasicBlockRewriter CreateRewriter(MethodBuilder methodBuilder) =>
        new(methodBuilder.CreateBasicBlockFromOldBlock(this), this);

    /// <summary>
    /// Returns a value enumerator.
    /// </summary>
    /// <returns>The resolved enumerator.</returns>
    public new BasicBlockValueCollectionEnumerator GetEnumerator() => new(this);

    #endregion

    #region Object

    /// <summary>
    /// Converts this basic block into a joined string of all successor targets.
    /// </summary>
    /// <returns>The successor string representation.</returns>
    public string ToSuccessorsString()
    {
        var result = new StringBuilder(32 * _successors.Count);
        foreach (var successor in _successors)
        {
            if (result.Length > 0)
                result.Append(", ");
            result.Append(successor.ToReferenceString());
        }
        return result.ToString();
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => Name;

    #endregion
}
