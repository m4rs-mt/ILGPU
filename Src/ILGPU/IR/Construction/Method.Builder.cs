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

using ILGPU.IR.Analyses;
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
        public sealed class Builder : DisposeBase, IMethodMappingObject, ILocation
        {
            #region Instance

            private readonly ImmutableArray<Parameter>.Builder parameters;

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
            /// Gets or sets the current entry block.
            /// </summary>
            public BasicBlock EntryBlock
            {
                get => Method.EntryBlock;
                set => Method.EntryBlock = value;
            }

            /// <summary>
            /// Returns the associated function handle.
            /// </summary>
            public MethodHandle Handle => Method.Handle;

            /// <summary>
            /// Returns the original source method (may be null).
            /// </summary>
            public MethodBase Source => Method.Source;

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
                    Debug.Assert(basicBlock != null, "Invalid basic block to lookup");
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
            /// Creates a new method scope with default flags.
            /// </summary>
            /// <returns>A new method scope.</returns>
            public Scope CreateScope() => Method.CreateScope();

            /// <summary>
            /// Creates a new method scope with custom flags.
            /// </summary>
            /// <param name="scopeFlags">The scope flags.</param>
            /// <returns>A new method scope.</returns>
            public Scope CreateScope(ScopeFlags scopeFlags) =>
                Method.CreateScope(scopeFlags);

            /// <summary>
            /// Creates a new rebuilder that works on the given scope.
            /// </summary>
            /// <param name="parameterMapping">
            /// The target value of every parameter.
            /// </param>
            /// <param name="scope">The used scope.</param>
            /// <returns>The created rebuilder.</returns>
            public IRRebuilder CreateRebuilder(
                ParameterMapping parameterMapping,
                Scope scope) =>
                CreateRebuilder(parameterMapping, scope, default);

            /// <summary>
            /// Creates a new rebuilder that works on the given scope.
            /// </summary>
            /// <param name="parameterMapping">
            /// The target value of every parameter.
            /// </param>
            /// <param name="scope">The used scope.</param>
            /// <param name="methodMapping">The method mapping.</param>
            /// <returns>The created rebuilder.</returns>
            public IRRebuilder CreateRebuilder(
                ParameterMapping parameterMapping,
                Scope scope,
                MethodMapping methodMapping)
            {
                this.Assert(parameterMapping.Method == scope.Method);
                this.Assert(scope != null);
                this.Assert(scope.Method != Method);

                return new IRRebuilder(
                    this,
                    parameterMapping,
                    scope,
                    methodMapping);
            }

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
            /// Creates a new entry block.
            /// </summary>
            /// <returns>The created entry block.</returns>
            public BasicBlock.Builder CreateEntryBlock()
            {
                var block = CreateBasicBlock(Method.Location, "Entry");
                EntryBlock = block.BasicBlock;
                return block;
            }

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
