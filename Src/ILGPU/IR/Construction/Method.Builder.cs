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

using ILGPU.IR.Analyses.Duplicates;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
            IBasicBlockCollection<ReversePostOrder>
        {
            #region Instance

            private readonly ImmutableArray<Parameter>.Builder parameters;

            private int blockCounter;
            private bool updateBlockOrder = false;

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
            public BasicBlockCollection<ReversePostOrder> SourceBlocks => Method.Blocks;

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
            /// Schedules updates of the block order.
            /// </summary>
            public void ScheduleBlockOrderUpdate() => updateBlockOrder = true;

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
            /// <param name="parameterMapping">
            /// The target value of every parameter.
            /// </param>
            /// <param name="blocks">The block collection.</param>
            /// <returns>The created rebuilder.</returns>
            public IRRebuilder CreateRebuilder(
                ParameterMapping parameterMapping,
                in BasicBlockCollection<ReversePostOrder> blocks) =>
                CreateRebuilder(parameterMapping, blocks, default);

            /// <summary>
            /// Creates a new rebuilder that works on the given scope.
            /// </summary>
            /// <param name="parameterMapping">
            /// The target value of every parameter.
            /// </param>
            /// <param name="blocks">The block collection.</param>
            /// <param name="methodMapping">The method mapping.</param>
            /// <returns>The created rebuilder.</returns>
            public IRRebuilder CreateRebuilder(
                ParameterMapping parameterMapping,
                in BasicBlockCollection<ReversePostOrder> blocks,
                MethodMapping methodMapping) =>
                new IRRebuilder(
                    this,
                    parameterMapping,
                    blocks,
                    methodMapping);

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
                block.SetupBlockIndex(blockCounter++);
                ScheduleBlockOrderUpdate();
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
            /// Computes an updated block order.
            /// </summary>
            /// <typeparam name="TOtherOrder">The collection order.</typeparam>
            /// <typeparam name="TOtherOrderProvider">
            /// The collection order provider.
            /// </typeparam>
            /// <typeparam name="TDuplicates">The duplicate specification.</typeparam>
            /// <returns>The newly ordered collection.</returns>
             public BasicBlockCollection<TOtherOrder> ComputeBlockOrder<
                TOtherOrder,
                TOtherOrderProvider,
                TDuplicates>()
                where TOtherOrder : struct, ITraversalOrderView<TOtherOrderProvider>
                where TOtherOrderProvider : struct, ITraversalOrderProvider
                where TDuplicates : struct, IDuplicates<BasicBlock>
            {
                var newBlocks = ImmutableArray.CreateBuilder<BasicBlock>(blockCounter);
                TOtherOrderProvider orderProvider = default;
                orderProvider.Traverse<ImmutableArray<BasicBlock>.Builder, TDuplicates>(
                    EntryBlock,
                    newBlocks);
                return new BasicBlockCollection<TOtherOrder>(
                    EntryBlock,
                    newBlocks.ToImmutable());
            }

            #endregion

            #region IDisposable

            /// <summary cref="DisposeBase.Dispose(bool)"/>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Assign parameters and adjust their indices
                    for (int i = 0; i < parameters.Count; )
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

                    // Dispose all basic block builders
                    foreach (var builder in basicBlockBuilders)
                        builder.Dispose();

                    // Update block order
                    if (updateBlockOrder)
                    {
                        // Compute new block order
                        var blockOrder = ComputeBlockOrder<
                            ReversePostOrder,
                            PostOrder,
                            NoDuplicates<BasicBlock>>();

                        // Update block indices
                        int blockIndex = 0;
                        foreach (var block in blockOrder)
                            block.SetupBlockIndex(blockIndex++);

                        // Apply changes to the method
                        Method.blocks = blockOrder.AsImmutable();
                    }

                    Method.ReleaseBuilder(this);
                }
                base.Dispose(disposing);
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
