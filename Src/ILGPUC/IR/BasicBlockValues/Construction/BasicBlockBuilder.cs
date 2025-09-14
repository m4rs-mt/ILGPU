// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlockBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.BasicBlockValues.Construction;

/// <summary>
/// An IR builder that can construct IR nodes.
/// </summary>
/// <remarks>Members of this class are thread safe.</remarks>
partial class BasicBlockBuilder : PureValueBuilder, IDumpable
{
    #region Nested Types

    /// <summary>
    /// Stores the previously set last value scope and allows to recover it.
    /// </summary>
    /// <param name="builder">The current builder.</param>
    internal readonly struct LastBasicBlockValueScope(BasicBlockBuilder builder)
        : IDisposable
    {
        /// <summary>
        /// Returns the previously last value.
        /// </summary>
        public BasicBlockValue? Last { get; } = builder.Last;

        /// <summary>
        /// Recover the previously stored last value.
        /// </summary>
        public void Pop() => builder.Last = Last;

        /// <summary>
        /// Restores the previous scope.
        /// </summary>
        public void Dispose() => Pop();
    }

    #endregion

    #region Instance

    /// <summary>
    /// The maximum supported chain length.
    /// </summary>
    internal const int MaxChainLength = 100_000;

    /// <summary>
    /// The parent method builder.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly MethodBuilder _methodBuilder;

    /// <summary>
    /// Constructs a new IR builder.
    /// </summary>
    /// <param name="methodBuilder">The parent method builder.</param>
    /// <param name="basicBlock">The newly created basic block.</param>
    public BasicBlockBuilder(MethodBuilder methodBuilder, BasicBlock basicBlock)
        : base(methodBuilder.Generation, basicBlock.Location)
    {
        _methodBuilder = methodBuilder;

        ModuleBuilder = methodBuilder.ModuleBuilder;
        BasicBlock = basicBlock;

        // Initialize with pending termination (no successors yet)
        BasicBlock.SetTermination(BlockTerminationKind.Pending);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the parent method.
    /// </summary>
    public override Method Method => _methodBuilder.Method;

    /// <summary>
    /// Returns the parent module builder.
    /// </summary>
    public override ModuleBuilder ModuleBuilder { get; }

    /// <summary>
    /// Returns the associated basic block.
    /// </summary>
    public BasicBlock BasicBlock { get; }

    /// <summary>
    /// The entry point value.
    /// </summary>
    public BasicBlockValue? Entry { get; private set; }

    /// <summary>
    /// The last basic block value.
    /// </summary>
    public BasicBlockValue? Last { get; protected set; }

    /// <summary>
    /// The last phi value.
    /// </summary>
    public PhiValue? LastPhiValue { get; protected set; }

    /// <summary>
    /// The termination value to use.
    /// </summary>
    public Value? TerminationValue { get; protected set; }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public override string FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(
            $"{message} [block '{BasicBlock.Name}', method '{Method.Name}', " +
            $"generation {Generation.Index}]");

    /// <summary>
    /// Pushes the current last value and sets the given value as last value.
    /// </summary>
    /// <param name="newLastValue">The new last value to append to.</param>
    /// <returns>the newly created last value scope.</returns>
    public LastBasicBlockValueScope PushLastValue(BasicBlockValue? newLastValue)
    {
        var scope = new LastBasicBlockValueScope(this);
        Last = newLastValue;
        return scope;
    }

    /// <summary>
    /// Creates a new initializer that is bound to the current block.
    /// </summary>
    /// <returns>The created value initializer.</returns>
    private BasicBlockValueInitializer GetInitializer(Location location) =>
        _methodBuilder.GetInitializer(this, Last, location);

    /// <summary>
    /// Creates a new initializer that is bound to the current block.
    /// </summary>
    /// <returns>The created value initializer.</returns>
    private BasicBlockValueInitializer GetPhiInitializer(Location location) =>
        _methodBuilder.GetInitializer(this, LastPhiValue, location);

    /// <summary>
    /// Registers this value creation with the parent method.
    /// </summary>
    protected sealed override void OnBeforePureValueCreated(
        in PureValueInitializer initializer) =>
        _methodBuilder.RegisterNewValue();

    /// <summary>
    /// Append a new value.
    /// </summary>
    /// <param name="phiValue">The node to create.</param>
    /// <returns>The created node.</returns>
    private PhiValue Append(PhiValue phiValue)
    {
        LastPhiValue = phiValue;
        return phiValue;
    }

    /// <summary>
    /// Append a new value.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="value">The node to create.</param>
    /// <returns>The created node.</returns>
    protected T Append<T>(T value) where T : BasicBlockValue
    {
        Last = value;
        return value;
    }

    /// <summary>
    /// Dumps the underlying method to the given text writer.
    /// </summary>
    /// <param name="textWriter">The text writer.</param>
    public void Dump(TextWriter textWriter)
    {
        textWriter.Write(ToString());
        textWriter.WriteLine(":");

        // Dump phi values
        ForEachPhiValueInReversePostOrder(phiValue =>
        {
            textWriter.Write("\t");
            textWriter.WriteLine(phiValue.ToString());
        });

        ForEachBasicBlockValueInReversePostOrder(bbValue =>
        {
            textWriter.Write("\t");
            textWriter.WriteLine(bbValue.ToString());
        });

        textWriter.Write("\t");
        textWriter.WriteLine($"termination: {BasicBlock.TerminationKind}");
    }

    /// <summary>
    /// Sets the internal termination kind.
    /// </summary>
    /// <param name="kind">The kind to use.</param>
    /// <param name="value">The current termination value.</param>
    /// <param name="successorCapacity">The intended successor capacity.</param>
    /// <returns>The successors builder.</returns>
    private BasicBlock.SuccessorsBuilder SetTermination(
        BlockTerminationKind kind,
        Value? value = null,
        int successorCapacity = 1)
    {
        TerminationValue = value;
        return BasicBlock.SetTermination(kind, successorCapacity);
    }

    /// <summary>
    /// Executes the given callback for each phi value.
    /// </summary>
    /// <param name="callback">The callback to invoke for each phi value.</param>
    public void ForEachPhiValueInReversePostOrder(Action<PhiValue> callback)
    {
        var allValues = InlineList<PhiValue>.Create(2);
        var current = LastPhiValue;
        while (current is not null)
        {
            this.Assert(allValues.Count <= MaxChainLength,
                $"[ForEachPhi] Phi chain exceeded {MaxChainLength} " +
                $"(seen {allValues.Count})");
            allValues.Add(current);
            current = current.Previous as PhiValue;
        }

        foreach (var value in allValues)
            callback(value);
    }

    /// <summary>
    /// Executes the given callback for each basic block value.
    /// </summary>
    /// <param name="callback">The callback to invoke for each basic block value.</param>
    public void ForEachBasicBlockValueInReversePostOrder(Action<BasicBlockValue> callback)
    {
        var allValues = InlineList<BasicBlockValue>.Create(2);
        var current = Last;
        while (current is not null)
        {
            this.Assert(allValues.Count <= MaxChainLength,
                $"[ForEachBBValue] Value chain exceeded {MaxChainLength} " +
                $"(seen {allValues.Count})");
            allValues.Add(current);
            current = current.Previous;
        }

        foreach (var value in allValues)
            callback(value);
    }

    /// <summary>
    /// Seals this block builder.
    /// </summary>
    /// <returns>Returns the sealed block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected internal BasicBlock Seal()
    {
        // Validate termination value
        this.Assert(
            TerminationValue is null ||
            TerminationValue.Type is not VoidType or KindType,
            $"[Seal] Termination value has invalid type '{TerminationValue?.Type}'");

        // Traverse all block values to find the root value.
        // SetNextInternal sets both Next and BasicBlock on the previous
        // node, but the tail (LastPhiValue) is never the 'previous' in
        // the pair — fix its BasicBlock explicitly.
        var currentPhiValue = LastPhiValue;
        if (currentPhiValue is not null)
            currentPhiValue.BasicBlock = BasicBlock;
        int numPhiValues = 0;
        while (currentPhiValue is not null)
        {
            var previous = currentPhiValue.Previous as PhiValue;
            previous?.SetNextInternal(currentPhiValue, BasicBlock);
            if (previous is null) break;
            currentPhiValue = previous;
            ++numPhiValues;
            this.Assert(numPhiValues <= MaxChainLength,
                $"[Seal/Phi] Phi chain exceeded {MaxChainLength} (phi count " +
                $"{numPhiValues})");
        }

        // Traverse the value chain and setup next relation.
        // Same as above: fix the tail's BasicBlock explicitly.
        var current = Last;
        if (current is not null)
            current.BasicBlock = BasicBlock;
        int numValues = 0;
        while (current is not null)
        {
            var previous = current.Previous;
            previous?.SetNextInternal(current, BasicBlock);
            if (previous is null) break;
            current = previous;
            ++numValues;
            this.Assert(numValues <= MaxChainLength,
                $"[Seal/Value] Value chain exceeded {MaxChainLength} (value count " +
                $"{numValues})");
        }

        // Rewrite termination
        BasicBlock.SealBlock(
            numPhiValues,
            currentPhiValue,
            numValues,
            current,
            Last,
            TerminationValue);
        return BasicBlock;
    }

    #endregion
}
