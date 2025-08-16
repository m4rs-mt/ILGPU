// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using ILGPUC.IR.Rewriting;
using ILGPUC.Util;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using BlockCollection = ILGPUC.IR.MethodValues.BasicBlockCollection<
    ILGPUC.IR.Analyses.ReversePostOrder<ILGPUC.IR.MethodValues.BasicBlock>,
    ILGPUC.IR.MethodValues.Forwards>;

namespace ILGPUC.IR.ModuleValues.Construction;

/// <summary>
/// Represents a self contained and fully featured method builder.
/// </summary>
class MethodBuilder : PureValueBuilder
{
    #region Nested Types

    /// <summary>
    /// An exit block visitor to gather exit blocks.
    /// </summary>
    private ref struct PrepareExitBlockVisitor : ITraversalVisitor<BasicBlock>
    {
        /// <summary>
        /// The exit block in case of a single one.
        /// </summary>
        private BasicBlock? _exitBlock;

        /// <summary>
        /// Returns true if there is more than one exit block.
        /// </summary>
        public readonly bool HasMultipleExitBlocks => ExitBlocks != null;

        /// <summary>
        /// Returns the list of exit blocks in the case of multiple exit blocks.
        /// </summary>
        public List<BasicBlock>? ExitBlocks { get; private set; }

        /// <summary>
        /// Checks whether the given block is an exit block and collects this
        /// block if it turns out to be an exit block.
        /// </summary>
        /// <param name="block">The block to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Visit(BasicBlock block)
        {
            // Clear predecessors
            block.ClearPredecessor();

            // Check whether this block is an exit block
            if (block.Successors.Length > 0)
                return;

            if (_exitBlock is null)
            {
                _exitBlock = block;
            }
            else
            {
                block.Assert(_exitBlock != block);
                ExitBlocks?.Add(block);
                ExitBlocks ??= new List<BasicBlock>(4) { _exitBlock, block };
            }
        }
    }

    #endregion

    #region MethodBuilder Instance

    private readonly ValueMap<Method, BasicBlock, BasicBlockBuilder> _blocks;
    private int _numValues;
    private InlineList<Parameter> _parameters;

    /// <summary>
    /// Constructs a new IR builder.
    /// </summary>
    /// <param name="moduleBuilder">The main module builder.</param>
    /// <param name="initializer">The module value initializer.</param>
    /// <param name="declaration">The current method declaration.</param>
    /// <param name="initialBlockCapacity">The initial block capacity.</param>
    public MethodBuilder(
        ModuleBuilder moduleBuilder,
        in ModuleValueInitializer initializer,
        in MethodDeclaration declaration,
        int initialBlockCapacity = 4)
        : this(
            moduleBuilder,
            initializer,
            declaration,
            initialBlockCapacity,
            setupEntryBuilder: true)
    { }

    /// <summary>
    /// Constructs a new IR builder.
    /// </summary>
    /// <param name="moduleBuilder">The main module builder.</param>
    /// <param name="initializer">The module value initializer.</param>
    /// <param name="declaration">The current method declaration.</param>
    /// <param name="initialBlockCapacity">The initial block capacity.</param>
    /// <param name="setupEntryBuilder">True to setup an entry-point builder.</param>
    protected MethodBuilder(
        ModuleBuilder moduleBuilder,
        in ModuleValueInitializer initializer,
        in MethodDeclaration declaration,
        int initialBlockCapacity,
        bool setupEntryBuilder)
        : base(moduleBuilder.Generation, initializer.Location)
    {
        int paramCapacity = declaration.Source?.GetNumParametersIncludingOffset() ?? 2;
        _parameters = InlineList<Parameter>.Create(paramCapacity);

        ModuleBuilder = moduleBuilder;
        Method = new(initializer, declaration);
        _blocks = new(Method, initialBlockCapacity);

        EntryBuilder = setupEntryBuilder
            ? CreateBasicBlock(Method.Location, "Entry")
            : Utilities.InitNotNullable<BasicBlockBuilder>();
    }

    /// <summary>
    /// Returns the method being built.
    /// </summary>
    public sealed override Method Method { get; }

    /// <summary>
    /// Returns the parent module builder.
    /// </summary>
    public sealed override ModuleBuilder ModuleBuilder { get; }

    /// <summary>
    /// Returns the entry block builder.
    /// </summary>
    public BasicBlockBuilder EntryBuilder { get; protected set; }

    /// <summary>
    /// Returns the number of basic blocks currently registered.
    /// </summary>
    public int NumBasicBlocks => _blocks.Count;

    /// <summary>
    /// Returns the total number of values in this method.
    /// </summary>
    public int NumValues => _numValues;

    /// <summary>
    /// Returns the basic block builder belonging to the given basic block.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <returns>The determined basic block builder.</returns>
    public BasicBlockBuilder this[BasicBlock block] => _blocks[block];

    /// <summary>
    /// Creates a new initializer that is bound to the current method.
    /// </summary>
    private MethodValueInitializer GetInitializer(Location location)
    {
        RegisterNewValue();
        return new(this, location);
    }

    /// <summary>
    /// Creates a new initializer that is bound to the current method.
    /// </summary>
    internal BasicBlockValueInitializer GetInitializer(
        BasicBlockBuilder builder,
        BasicBlockValue? last,
        Location location)
    {
        RegisterNewValue();
        return new(builder, last, location);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Registers a new value.
    /// </summary>
    internal void RegisterNewValue() => Interlocked.Increment(ref _numValues);

    /// <summary>
    /// Registers a new value.
    /// </summary>
    protected sealed override void OnBeforePureValueCreated(
        in PureValueInitializer initializer) =>
        RegisterNewValue();

    /// <summary>
    /// Creates a new pure value rewriter.
    /// </summary>
    /// <returns>The pure value rewriter to use.</returns>
    public PureValueRewriter CreatePureValueRewriter() => new(this);

    /// <summary>
    /// Creates a new parameter.
    /// </summary>
    /// <param name="type">The parameter type.</param>
    /// <param name="name">The parameter name (for debugging purposes).</param>
    /// <returns>The created node.</returns>
    public Parameter CreateParameter(TypeValue type, string? name)
    {
        var param = new Parameter(
            GetInitializer(Method.Location),
            type,
            name,
            _parameters.Count);
        _parameters.Add(param);
        return param;
    }

    /// <summary>
    /// Replaces an existing parameter in the parameter list at its current
    /// position with a new parameter of the given type. This preserves
    /// parameter ordering when a transform mapper rewrites a parameter
    /// that was already created by the constructor.
    /// </summary>
    /// <param name="existing">The existing parameter to replace.</param>
    /// <param name="type">The new parameter type.</param>
    /// <param name="name">The parameter name (for debugging purposes).</param>
    /// <returns>The replacement parameter.</returns>
    public Parameter ReplaceParameter(
        Parameter existing,
        TypeValue type,
        string? name)
    {
        for (int i = 0; i < _parameters.Count; i++)
        {
            if (ReferenceEquals(_parameters[i], existing))
            {
                var param = new Parameter(
                    GetInitializer(Method.Location),
                    type,
                    name,
                    i);
                _parameters[i] = param;
                return param;
            }
        }

        // Fallback: existing not found, append as new
        return CreateParameter(type, name);
    }

    /// <summary>
    /// Creates a new basic block.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="name">The name of the basic block.</param>
    /// <param name="oldBlock">The source block (if any).</param>
    /// <returns>The created basic block builder.</returns>
    public BasicBlockBuilder CreateBasicBlock(
        Location location,
        string? name = null,
        BasicBlock? oldBlock = null)
    {
        var basicBlock = new BasicBlock(GetInitializer(location), name);
        var builder = CreateBasicBlockBuilder(basicBlock, oldBlock);

        _blocks.Add(builder.BasicBlock, builder);
        return builder;
    }

    /// <summary>
    /// Creates a new basic block builder.
    /// </summary>
    /// <param name="basicBlock">The newly created basic block.</param>
    /// <param name="oldBlock">The old block (if any).</param>
    /// <returns>The basic block builder to use.</returns>
    protected virtual BasicBlockBuilder CreateBasicBlockBuilder(
        BasicBlock basicBlock,
        BasicBlock? oldBlock) =>
        new(this, basicBlock);

    /// <summary>
    /// Creates a new basic block.
    /// </summary>
    /// <param name="oldBlock">The old block this block is constructed from.</param>
    /// <param name="name">The optional name.</param>
    /// <returns>The created basic block builder.</returns>
    protected internal BasicBlockBuilder CreateBasicBlockFromOldBlock(
        BasicBlock oldBlock,
        string? name = null)
    {
        Generation.ValidatePreviousGeneration(oldBlock);
        name ??= oldBlock.Name;

        return CreateBasicBlock(Location, name, oldBlock);
    }

    /// <summary>
    /// Tries to get a basic block builder for the given basic block.
    /// </summary>
    /// <param name="basicBlock">The given basic block.</param>
    /// <param name="builder">The basic block builder (if any).</param>
    /// <returns>True if a basic block builder could be determined.</returns>
    public bool TryGetBasicBlockBuilder(
        BasicBlock basicBlock,
        [NotNullWhen(true)] out BasicBlockBuilder? builder) =>
        _blocks.TryGetValue(basicBlock, out builder);

    /// <summary>
    /// Traverses the current state of the control flow and returns an updated basic
    /// block collection.
    /// </summary>
    /// <returns>The updated basic block collection.</returns>
    public BlockCollection TraverseToCollection() =>
        EntryBuilder.BasicBlock.TraverseToCollection<
            ReversePostOrder<BasicBlock>,
            BasicBlock.SuccessorsProvider<Forwards>,
            Forwards>(_blocks.Count);

    /// <summary>
    /// Ensures that there is only one exit block.
    /// </summary>
    /// <remarks>
    /// CAUTION: This function changes the control flow.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void EnsureUniqueExitBlock()
    {
        // Find exit blocks. Note that initial lookup indices have been setup during
        // basic block construction. In this case, it is safe to use BasicBlockSet.
        var visitor = new PrepareExitBlockVisitor();
        PreOrder<BasicBlock>.Traverse<
            ValueSet<Method, BasicBlock>,
            PrepareExitBlockVisitor,
            BasicBlock.SuccessorsProvider<Forwards>,
            Forwards>(
                EntryBuilder.BasicBlock,
                new ValueSet<Method, BasicBlock>(Method, _numValues),
                ref visitor);

        // This is the default case since most .Net compilers usually emit a
        // single exit block by default
        if (!visitor.HasMultipleExitBlocks)
            return;

        // If we arrive here we have to update the control-flow and append
        // a new unique exit block
        var exitBlocks = visitor.ExitBlocks.AsNotNull();
        var exitBlock = CreateBasicBlock(exitBlocks[0].Location, "Exit");
        var location = exitBlock.BasicBlock.Location;

        if (Method.IsVoid)
        {
            // Create a simple void return
            exitBlock.CreateReturnTermination();
        }
        else
        {
            // We require a phi node that merges the different values together
            var phiBuilder = exitBlock.CreatePhi(location, Method.Type);
            foreach (var block in exitBlocks)
            {
                // Read from the builder — the BasicBlock hasn't been
                // sealed yet, so block.TerminationValue would read from
                // the empty value array and assert Count > 0.
                var returnValue = this[block].TerminationValue;
                phiBuilder.AddArgument(block, returnValue.AsNotNull());
            }

            // We require a custom phi parameter
            exitBlock.CreateReturnTermination(phiBuilder.Seal());
        }

        // Wire branches
        foreach (var block in exitBlocks)
        {
            // CAUTION: change the control flow to jump to the exit block
            // => this will introduce an additional successor
            this[block].CreateUnconditionalTermination(exitBlock.BasicBlock);
        }
    }

    /// <summary>
    /// Seals the underlying method.
    /// </summary>
    /// <return>The created method.</return>
    internal virtual Method Seal() => SealInternal(null);

    /// <summary>
    /// Seals the underlying method.
    /// </summary>
    /// <return>The created method.</return>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected Method SealInternal(System.Predicate<Parameter>? paramPredicate)
    {
        if (Method.IsSealed)
            return Method;

        // Finish all blocks
        EnsureUniqueExitBlock();

        // Find all blocks (implicit UCE)
        var entryBlock = EntryBuilder.BasicBlock;
        var allBlocks = entryBlock.TraverseToCollection<
            ReversePostOrder<BasicBlock>,
            BasicBlock.SuccessorsProvider<Forwards>,
            Forwards>(_blocks.Count);
        var exitBlock = allBlocks.FindExitBlock();

        // Seal all reachable blocks
        foreach (var block in allBlocks)
            this[block].Seal();

        // Build final value list
        var values = ValueBuilderList.Create(Generation, _parameters.Count + 1);
        values.Add(entryBlock);

        // Ensure parameters are in a proper shape
        int paramIndex = 0;
        foreach (var param in _parameters)
        {
            if (!(paramPredicate?.Invoke(param.AsNotNullCast<Parameter>()) ?? true))
                continue;

            values.Add(param);
            param.SealParameter(paramIndex++);
        }

        // Seal method
        Method.SealMethod(_numValues, exitBlock, ref values, allBlocks);
        return Method;
    }

    #endregion
}
