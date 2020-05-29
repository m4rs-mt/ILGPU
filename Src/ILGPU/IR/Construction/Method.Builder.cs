// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Method.Builder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    partial class Method
    {
        /// <summary>
        /// A builder to build methods.
        /// </summary>
        public sealed class Builder :
            DisposeBase,
            IMethodMappingObject,
            ILocation,
            IDumpable
        {
            #region Static

            /// <summary>
            /// Checks whether we have to update the control-flow structure.
            /// </summary>
            /// <param name="oldTerminator">The old terminator (if any).</param>
            /// <param name="newTerminator">The new terminator.</param>
            /// <returns>
            /// True, if we have to update the control-flow structure.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool RequiresControlFlowUpdate(
                TerminatorValue oldTerminator,
                TerminatorValue newTerminator)
            {
                if (oldTerminator == newTerminator)
                    return false;
                if (oldTerminator == null || newTerminator == null)
                    return true;

                // Check whether the new terminator has the identical list of
                // successors. In theory we could check whether both lists contain
                // the same blocks without paying attention to the order; however,
                // this is very unlikely and expensive to verify.
                var oldSuccessors = oldTerminator.Targets;
                var newSuccessors = newTerminator.Targets;
                if (oldSuccessors.Length != newSuccessors.Length)
                    return true;
                for (int i = 0, e = oldSuccessors.Length; i < e; ++i)
                {
                    if (oldSuccessors[i] != newSuccessors[i])
                        return true;
                }
                return false;
            }

            #endregion

            #region Instance

            private readonly ImmutableArray<Parameter>.Builder parameters;

            private int blockCounter;
            private bool updateControlFlow = false;
            private bool acceptControlFlowUpdates = false;

            /// <summary>
            /// All created basic block builders.
            /// </summary>
            private readonly List<BasicBlock.Builder> basicBlockBuilders =
                new List<BasicBlock.Builder>();

            /// <summary>
            /// Constructs a new method builder.
            /// </summary>
            /// <param name="method">The parent method.</param>
            internal Builder(Method method)
            {
                Method = method;
                parameters = method.parameters.ToBuilder();
                blockCounter = method.Blocks.Count;
                EntryBlock = method.EntryBlock;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated IR context.
            /// </summary>
            public IRContext Context => Method.Context;

            /// <summary>
            /// Returns the associated method.
            /// </summary>
            public Method Method { get; }

            /// <summary>
            /// Returns the location of the underlying method.
            /// </summary>
            public Location Location => Method.Location;

            /// <summary>
            /// Gets the current entry block.
            /// </summary>
            public BasicBlock EntryBlock { get; }

            /// <summary>
            /// Returns the builder of the entry block.
            /// </summary>
            public BasicBlock.Builder EntryBlockBuilder => this[EntryBlock];

            /// <summary>
            /// Returns the associated function handle.
            /// </summary>
            public MethodHandle Handle => Method.Handle;

            /// <summary>
            /// Returns the original source method (may be null).
            /// </summary>
            public MethodBase Source => Method.Source;

            /// <summary>
            /// Returns all blocks of the source method.
            /// </summary>
            public BasicBlockCollection<ReversePostOrder, Forwards> SourceBlocks =>
                Method.Blocks;

            /// <summary>
            /// Returns the parameter with the given index.
            /// </summary>
            /// <param name="index">The parameter index.</param>
            /// <returns>The resolved parameter.</returns>
            public Parameter this[int index] => parameters[index];

            /// <summary>
            /// Returns the associated basic block builder.
            /// </summary>
            /// <param name="basicBlock">
            /// The basic block to resolve the builder for.
            /// </param>
            /// <returns>The resolved basic block builder.</returns>
            public BasicBlock.Builder this[BasicBlock basicBlock]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    Debug.Assert(
                        basicBlock.Method == Method,
                        "Invalid associated function");
                    if (basicBlock.GetOrCreateBuilder(
                        this,
                        out BasicBlock.Builder basicBlockBuilder))
                    {
                        basicBlockBuilders.Add(basicBlockBuilder);
                    }
                    return basicBlockBuilder;
                }
            }

            /// <summary>
            /// Returns the number of parameters.
            /// </summary>
            public int NumParams => parameters.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Schedules control-flow updates.
            /// </summary>
            public void ScheduleControlFlowUpdate() => updateControlFlow = true;

            /// <summary>
            /// Schedules control-flow updates if the successor relation has been
            /// changed by setting a new terminator.
            /// </summary>
            public void ScheduleControlFlowUpdate(
                TerminatorValue oldTerminator,
                TerminatorValue newTerminator)
            {
                if (RequiresControlFlowUpdate(oldTerminator, newTerminator))
                    ScheduleControlFlowUpdate();
            }

            /// <summary>
            /// Accepts or rejects control-flow updates in case of control-flow changes.
            /// </summary>
            /// <param name="accept">True, if all changes will be accepted.</param>
            /// <remarks>
            /// This operation is only available in debug mode.
            /// </remarks>
            [Conditional("DEBUG")]
            public void AcceptControlFlowUpdates(bool accept) =>
                acceptControlFlowUpdates = accept;

            /// <summary>
            /// Asserts that no control-flow update has happened and the predecessor
            /// and successor relations are still up to date.
            /// </summary>
            /// <remarks>
            /// This operation is only available in debug mode.
            /// </remarks>
            [Conditional("DEBUG")]
            public void AssertNoControlFlowUpdate() =>
                Method.Assert(!updateControlFlow | acceptControlFlowUpdates);

            /// <summary>
            /// Formats an error message to include the current debug information.
            /// </summary>
            public string FormatErrorMessage(string message) =>
                Method.FormatErrorMessage(message);

            /// <summary>
            /// Converts the return type.
            /// </summary>
            /// <typeparam name="TTypeConverter">The type converter.</typeparam>
            /// <param name="typeConverter">The type converter instance.</param>
            public void UpdateReturnType<TTypeConverter>(
                TTypeConverter typeConverter)
                where TTypeConverter : ITypeConverter<TypeNode>
            {
                var returnType = typeConverter.ConvertType(Context, Method.ReturnType);
                Method.Declaration = Method.Declaration.Specialize(returnType);
            }

            /// <summary>
            /// Converts all parameter types.
            /// </summary>
            /// <typeparam name="TTypeConverter">The type converter.</typeparam>
            /// <param name="typeConverter">The type converter instance.</param>
            public void UpdateParameterTypes<TTypeConverter>(
                TTypeConverter typeConverter)
                where TTypeConverter : ITypeConverter<TypeNode>
            {
                foreach (var param in parameters)
                    param.UpdateType(Context, typeConverter);
            }

            /// <summary>
            /// Creates a new rebuilder that works on the given scope.
            /// </summary>
            /// <typeparam name="TMode">The rebuilder mode.</typeparam>
            /// <param name="parameterMapping">
            /// The target value of every parameter.
            /// </param>
            /// <param name="blocks">The block collection.</param>
            /// <returns>The created rebuilder.</returns>
            public IRRebuilder CreateRebuilder<TMode>(
                ParameterMapping parameterMapping,
                in BasicBlockCollection<ReversePostOrder, Forwards> blocks)
                where TMode : IRRebuilder.IMode =>
                CreateRebuilder<TMode>(parameterMapping, default, blocks);

            /// <summary>
            /// Creates a new rebuilder that works on the given scope.
            /// </summary>
            /// <typeparam name="TMode">The rebuilder mode.</typeparam>
            /// <param name="parameterMapping">
            /// The target value of every parameter.
            /// </param>
            /// <param name="methodMapping">The method mapping.</param>
            /// <param name="blocks">The block collection.</param>
            /// <returns>The created rebuilder.</returns>
            public IRRebuilder CreateRebuilder<TMode>(
                ParameterMapping parameterMapping,
                MethodMapping methodMapping,
                in BasicBlockCollection<ReversePostOrder, Forwards> blocks)
                where TMode : IRRebuilder.IMode =>
                IRRebuilder.Create<TMode>(
                    this,
                    parameterMapping,
                    methodMapping,
                    blocks);

            /// <summary>
            /// Adds a new parameter to the encapsulated function.
            /// </summary>
            /// <param name="type">The parameter type.</param>
            /// <returns>The created parameter.</returns>
            public Parameter AddParameter(TypeNode type) =>
                AddParameter(type, null);

            /// <summary>
            /// Adds a new parameter to the encapsulated function.
            /// </summary>
            /// <param name="type">The parameter type.</param>
            /// <param name="name">The parameter name (for debugging purposes).</param>
            /// <returns>The created parameter.</returns>
            public Parameter AddParameter(TypeNode type, string name)
            {
                var param = CreateParam(type, name);
                parameters.Add(param);
                return param;
            }

            /// <summary>
            /// Inserts a new parameter to the encapsulated function at the beginning.
            /// </summary>
            /// <param name="type">The parameter type.</param>
            /// <returns>The created parameter.</returns>
            public Parameter InsertParameter(TypeNode type) =>
                InsertParameter(type, null);

            /// <summary>
            /// Inserts a new parameter to the encapsulated function at the beginning.
            /// </summary>
            /// <param name="type">The parameter type.</param>
            /// <param name="name">The parameter name (for debugging purposes).</param>
            /// <returns>The created parameter.</returns>
            public Parameter InsertParameter(TypeNode type, string name)
            {
                var param = CreateParam(type, name);
                parameters.Insert(0, param);
                return param;
            }

            /// <summary>
            /// Creates a parameter with the given index and type information.
            /// </summary>
            /// <param name="type">The parameter type.</param>
            /// <param name="name">The parameter name (for debugging purposes).</param>
            /// <returns>The created parameter.</returns>
            private Parameter CreateParam(TypeNode type, string name) =>
                new Parameter(
                    new ValueInitializer(
                        Context,
                        Method,
                        Method.Location),
                    type,
                    name);

            /// <summary>
            /// Creates a new basic block.
            /// </summary>
            /// <param name="location">The current location.</param>
            /// <returns>The created basic block.</returns>
            public BasicBlock.Builder CreateBasicBlock(Location location) =>
                CreateBasicBlock(location, null);

            /// <summary>
            /// Creates a new basic block.
            /// </summary>
            /// <param name="location">The current location.</param>
            /// <param name="name">The block name.</param>
            /// <returns>The created basic block.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BasicBlock.Builder CreateBasicBlock(Location location, string name)
            {
                var block = new BasicBlock(Method, location, name);
                block.BeginControlFlowUpdate(blockCounter++);
                ScheduleControlFlowUpdate();
                return this[block];
            }

            /// <summary>
            /// Declares a method.
            /// </summary>
            /// <param name="methodBase">The method base.</param>
            /// <param name="created">True, if the method has been created.</param>
            /// <returns>The declared method.</returns>
            public Method DeclareMethod(
                MethodBase methodBase,
                out bool created) =>
                Context.Declare(methodBase, out created);

            /// <summary>
            /// Declares a method.
            /// </summary>
            /// <param name="declaration">The method declaration.</param>
            /// <param name="created">True, if the method has been created.</param>
            /// <returns>The declared method.</returns>
            public Method DeclareMethod(
                in MethodDeclaration declaration,
                out bool created) =>
                Context.Declare(declaration, out created);

            /// <summary>
            /// Computes an updated block collection using the latest terminator
            /// information.
            /// </summary>
            /// <typeparam name="TOrder">The collection order.</typeparam>
            /// <returns>The newly ordered collection.</returns>
            public BasicBlockCollection<TOrder, Forwards>
                ComputeBlockCollection<TOrder>()
                where TOrder : struct, ITraversalOrder
            {
                // Compute new block order
                var newBlocks = ImmutableArray.CreateBuilder<BasicBlock>(blockCounter);
                TOrder order = default;
                order.Traverse<
                    ImmutableArray<BasicBlock>.Builder,
                    BasicBlock.TerminatorSuccessorsProvider,
                    Forwards,
                    ImmutableArray<BasicBlock>>(
                    EntryBlock,
                    newBlocks,
                    new BasicBlock.TerminatorSuccessorsProvider());

                // Return new block collection
                return new BasicBlockCollection<TOrder, Forwards>(
                    EntryBlock,
                    newBlocks.ToImmutable());
            }

            /// <summary>
            /// Dumps the underlying method to the given text writer.
            /// </summary>
            /// <param name="textWriter">The text writer.</param>
            public void Dump(TextWriter textWriter) => Method.Dump(textWriter);

            #endregion

            #region IDisposable

            /// <summary>
            /// Updates all parameter bindings.
            /// </summary>
            private void UpdateParameters()
            {
                // Assign parameters and adjust their indices
                for (int i = 0; i < parameters.Count;)
                {
                    if (parameters[i].IsReplaced)
                        parameters.RemoveAt(i);
                    else
                        ++i;
                }
                var @params = parameters.ToImmutable();
                for (int i = 0, e = @params.Length; i < e; ++i)
                    @params[i].Index = i;
                Method.parameters = @params;
            }

            /// <summary>
            /// Updates the whole control-flow information of all blocks.
            /// </summary>
            /// <remarks>
            /// CAUTION: Applying a control-flow update to all blocks will cause all
            /// block instances to be modified.
            /// </remarks>
            internal BasicBlockCollection<ReversePostOrder, Forwards> UpdateControlFlow()
            {
                if (!updateControlFlow)
                    return SourceBlocks;
                updateControlFlow = false;

                // Compute new block order
                var newBlocks = ComputeBlockCollection<ReversePostOrder>();

                // Update block indices and setup links
                int blockIndex = 0;
                foreach (var block in newBlocks)
                    block.BeginControlFlowUpdate(blockIndex++);

                // Update all CFG links
                foreach (var block in newBlocks)
                    block.PropagateSuccessors();

                // Apply changes to the method
                Method.blocks = newBlocks.AsImmutable();
                return newBlocks;
            }

            /// <summary cref="DisposeBase.Dispose(bool)"/>
            protected override void Dispose(bool disposing)
            {
                // Update parameter bindings
                UpdateParameters();

                // Dispose all basic block builders
                foreach (var builder in basicBlockBuilders)
                    builder.Dispose();

                // Update control-flow
                UpdateControlFlow();

                // Release builder
                Method.ReleaseBuilder(this);
            }

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of the underlying function.
            /// </summary>
            /// <returns>The string representation of the underlying function.</returns>
            public override string ToString() => Method.ToString();

            #endregion
        }
    }
}
