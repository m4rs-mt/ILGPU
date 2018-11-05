// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionImporter.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// An import specializer.
    /// </summary>
    public interface IFunctionImportSpecializer
    {
        /// <summary>
        /// Performs custom mapping operations on the given rebuilder.
        /// </summary>
        /// <param name="sourceContext">The source context.</param>
        /// <param name="sourceFunction">The source function.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="rebuilder">The associated rebuilder.</param>
        void Map(
            IRContext sourceContext,
            TopLevelFunction sourceFunction,
            IRBuilder builder,
            IRRebuilder rebuilder);
    }

    partial class IRBuilder
    {
        #region Nested Types

        /// <summary>
        /// Represents no import specializer.
        /// </summary>
        private readonly struct NoImportSpecializer : IFunctionImportSpecializer
        {
            /// <summary cref="IFunctionImportSpecializer.Map(IRContext, TopLevelFunction, IRBuilder, IRRebuilder)"/>
            public void Map(
                IRContext sourceContext,
                TopLevelFunction sourceFunction,
                IRBuilder builder,
                IRRebuilder rebuilder) { }
        }

        #endregion

        #region Function Importer

        /// <summary>
        /// Imports the given function into this context using
        /// the default (empty) import specializer.
        /// </summary>
        /// <param name="sourceContext">The source context.</param>
        /// <param name="sourceFunction">The function to import.</param>
        /// <returns>The imported function.</returns>
        public TopLevelFunction Import(
            IRContext sourceContext,
            TopLevelFunction sourceFunction) =>
            Import(sourceContext, sourceFunction, new NoImportSpecializer());

        /// <summary>
        /// Imports the given function into this context.
        /// </summary>
        /// <typeparam name="TSpecializer">The specializer type.</typeparam>
        /// <param name="sourceContext">The source context.</param>
        /// <param name="sourceFunction">The function to import.</param>
        /// <param name="specializer">The specializer.</param>
        /// <returns>The imported function.</returns>
        public TopLevelFunction Import<TSpecializer>(
            IRContext sourceContext,
            TopLevelFunction sourceFunction,
            in TSpecializer specializer)
            where TSpecializer : IFunctionImportSpecializer
        {
            if (sourceFunction == null)
                throw new ArgumentNullException(nameof(sourceFunction));

            lock (syncRoot)
            {
                if (functionMapping.TryGetData(sourceFunction.Handle, out FunctionBuilder builder))
                    return builder.FunctionValue as TopLevelFunction;
            }

            var scope = Scope.Create(sourceContext, sourceFunction);
            return Import(sourceContext, scope, specializer);
        }

        /// <summary>
        /// Imports the given function into this context using
        /// the default (empty) import specializer.
        /// </summary>
        /// <param name="sourceContext">The source context.</param>
        /// <param name="sourceScope">The function scope to import.</param>
        /// <returns>The imported function.</returns>
        public TopLevelFunction Import(
            IRContext sourceContext,
            Scope sourceScope) =>
            Import(sourceContext, sourceScope, new NoImportSpecializer());

        /// <summary>
        /// Imports the given function into this context.
        /// </summary>
        /// <typeparam name="TSpecializer">The specializer type.</typeparam>
        /// <param name="sourceContext">The source context.</param>
        /// <param name="sourceScope">The function scope to import.</param>
        /// <param name="specializer">The specializer.</param>
        /// <returns>The imported function.</returns>
        public TopLevelFunction Import<TSpecializer>(
            IRContext sourceContext,
            Scope sourceScope,
            in TSpecializer specializer)
            where TSpecializer : IFunctionImportSpecializer
        {
            if (sourceContext == null)
                throw new ArgumentNullException(nameof(sourceContext));
            var entry = sourceScope.Entry as TopLevelFunction;
            if (sourceScope == null || entry == null)
                throw new ArgumentNullException(nameof(sourceScope));

            if (!sourceContext.TryGetFunction(entry.Handle, out TopLevelFunction _))
                throw new InvalidOperationException("Cannot import a function from a different context");

            lock (syncRoot)
            {
                if (functionMapping.TryGetData(entry.Handle, out FunctionBuilder builder))
                    return builder.FunctionValue as TopLevelFunction;
            }

            if (sourceContext == Context ||
                Context.TryGetFunction(entry.Handle, out TopLevelFunction other) &&
                other == entry)
                throw new InvalidOperationException("Cannot import a function into the same context");

            var mainReferences = sourceScope.ComputeFunctionReferences(
                new FunctionCollections.AllFunctions());
            var mapping = new Dictionary<TopLevelFunction, (IRRebuilder, FunctionReferences)>()
            {
                { entry, (CreateRebuilder(sourceScope), mainReferences) }
            };

            if (mainReferences.TryGetFirstReference(
                out TopLevelFunction current,
                out FunctionReferences.Enumerator mainEnumerator))
            {
                var toProcess = new Stack<TopLevelFunction>();
                for (; ; )
                {
                    if (!mapping.ContainsKey(current))
                    {
                        var scope = Scope.Create(this, current);
                        var rebuilder = CreateRebuilder(scope);
                        var references = scope.ComputeFunctionReferences(
                            new FunctionCollections.AllFunctions());
                        mapping.Add(current, (rebuilder, references));

                        foreach (var reference in references)
                            toProcess.Push(reference);
                    }
                    if (toProcess.Count < 1)
                    {
                        if (mainEnumerator.MoveNext())
                            current = mainEnumerator.Current;
                        else
                            break;
                    }
                    else
                        current = toProcess.Pop();
                }
            }
            mainEnumerator.Dispose();

            // Register mappings
            foreach (var mappingEntry in mapping)
            {
                var rebuilder = mappingEntry.Value.Item1;
                specializer.Map(
                    sourceContext,
                    mappingEntry.Key as TopLevelFunction,
                    this,
                    rebuilder);
                foreach (var reference in mappingEntry.Value.Item2)
                {
                    var remappedReference = mapping[reference];
                    rebuilder.Map(reference, remappedReference.Item1.NewEntry);
                }
            }

            // Rebuild functions and mark the as not transformed since we have
            // changed the IR context.
            foreach (var mappingValue in mapping.Values)
            {
                var function = mappingValue.Item1.Rebuild() as TopLevelFunction;
                function.RemoveTransformationFlags(
                    TopLevelFunctionTransformationFlags.Transformed);
            }

            lock (syncRoot)
            {
                var result = functionMapping[entry.Handle].FunctionValue as TopLevelFunction;
                Debug.Assert(result != null, "Invalid function handle");
                return result;
            }
        }

        #endregion
    }
}
