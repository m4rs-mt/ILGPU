// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionLandscape.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents the structure of multiple <see cref="TopLevelFunction"/> objects.
    /// This includes the call graph, function size and dependency information.
    /// </summary>
    /// <typeparam name="T">Custom information type per entry.</typeparam>
    public class FunctionLandscape<T>
    {
        #region Nested Types

        /// <summary>
        /// Represents a landscape entry.
        /// </summary>
        public sealed class Entry
        {
            private readonly HashSet<TopLevelFunction> uses = new HashSet<TopLevelFunction>();

            internal Entry(
                TopLevelFunction function,
                Scope scope,
                FunctionReferences references,
                in T data)
            {
                Debug.Assert(function != null, "Invalid function");
                Debug.Assert(scope != null, "Invalid scope");
                Function = function;
                Scope = scope;
                References = references;
                Data = data;
            }

            /// <summary>
            /// Returns the associated function.
            /// </summary>
            public TopLevelFunction Function { get; }

            /// <summary>
            /// Returns the associated scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns custom information.
            /// </summary>
            public T Data { get; }

            /// <summary>
            /// Returns the number of nodes.
            /// </summary>
            public int NodeCount => Scope.Count;

            /// <summary>
            /// Returns the number of nested (local) functions.
            /// </summary>
            public int NumFunctions => Scope.NumFunctions;

            /// <summary>
            /// Returns the number of uses.
            /// </summary>
            public int NumUses => Function.AllNumUses;

            /// <summary>
            /// Returns true if this function has references.
            /// </summary>
            public bool HasReferences => !References.IsEmpty;

            /// <summary>
            /// Returns all function references to other functions.
            /// </summary>
            public FunctionReferences References { get; }

            /// <summary>
            /// Returns true if this function is used by the given one.
            /// </summary>
            /// <param name="function">The function.</param>
            /// <returns>True, if this function is used by the given one.</returns>
            public bool IsUsedBy(TopLevelFunction function) => uses.Contains(function);

            /// <summary>
            /// Registers the given function use.
            /// </summary>
            /// <param name="function">The function to register.</param>
            internal void AddUse(TopLevelFunction function) => uses.Add(function);

            /// <summary>
            /// Returns an enumerator to enumerate all function entries that
            /// depend on this one (backward edges).
            /// </summary>
            /// <returns>An enumerator to enumerate all depending function entries.</returns>
            internal HashSet<TopLevelFunction>.Enumerator GetEnumerator() => uses.GetEnumerator();

            /// <summary>
            /// Returns the string representation of this entry.
            /// </summary>
            /// <returns>The string representation of this entry.</returns>
            public override string ToString() => Function.ToString();
        }

        /// <summary>
        /// An abstract data provider per node.
        /// </summary>
        public interface IDataProvider
        {
            /// <summary>
            /// Resolves custom entry information for the given node.
            /// </summary>
            /// <param name="scope">The current scope.</param>
            /// <param name="functionReferences">All references to other functions.</param>
            /// <returns>The resolved custom data.</returns>
            T GetData(Scope scope, FunctionReferences functionReferences);
        }

        /// <summary>
        /// Enumerates all functions in the call graph scope in post order.
        /// </summary>
        public struct Enumerator : IEnumerator<Entry>
        {
            #region Instance

            private int index;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="landscape">The parent landscape.</param>
            internal Enumerator(FunctionLandscape<T> landscape)
            {
                Debug.Assert(landscape != null, "Invalid landscape");
                Landscape = landscape;
                index = landscape.Count;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent scope;
            /// </summary>
            public FunctionLandscape<T> Landscape { get; }

            /// <summary>
            /// Returns the current function entry.
            /// </summary>
            public Entry Current => Landscape.postOrder[index];

            /// <summary cref="IEnumerator.Current" />
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.MoveNext" />
            public bool MoveNext()
            {
                return --index >= 0;
            }

            /// <summary cref="IEnumerator.Reset" />
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IDisposable.Dispose" />
            public void Dispose() { }

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a function structure instance.
        /// </summary>
        /// <typeparam name="TPredicate">The view predicate.</typeparam>
        /// <typeparam name="TDataProvider">The custom data provider type.</typeparam>
        /// <param name="functionView">The source function view.</param>
        /// <param name="dataProvider">A custom data provider.</param>
        /// <returns>The created function structure object.</returns>
        public static FunctionLandscape<T> Create<TPredicate, TDataProvider>(
            in FunctionCollection<TPredicate> functionView,
            in TDataProvider dataProvider)
            where TPredicate : IFunctionCollectionPredicate
            where TDataProvider : IDataProvider =>
            Create<FunctionCollection<TPredicate>, TPredicate, TDataProvider>(
                functionView,
                dataProvider);

        /// <summary>
        /// Creates a function structure instance.
        /// </summary>
        /// <typeparam name="TPredicate">The view predicate.</typeparam>
        /// <typeparam name="TDataProvider">The custom data provider type.</typeparam>
        /// <param name="functionView">The source function view.</param>
        /// <param name="dataProvider">A custom data provider.</param>
        /// <returns>The created function structure object.</returns>
        public static FunctionLandscape<T> Create<TPredicate, TDataProvider>(
            in UnsafeFunctionCollection<TPredicate> functionView,
            in TDataProvider dataProvider)
            where TPredicate : IFunctionCollectionPredicate
            where TDataProvider : IDataProvider =>
            Create<UnsafeFunctionCollection<TPredicate>, TPredicate, TDataProvider>(
                functionView,
                dataProvider);

        /// <summary>
        /// Creates a function structure instance.
        /// </summary>
        /// <typeparam name="TFunctionView">The type of the function view.</typeparam>
        /// <typeparam name="TPredicate">The view predicate.</typeparam>
        /// <typeparam name="TDataProvider">The custom data provider type.</typeparam>
        /// <param name="functionView">The source function view.</param>
        /// <param name="dataProvider">A custom data provider.</param>
        /// <returns>The created function structure object.</returns>
        public static FunctionLandscape<T> Create<TFunctionView, TPredicate, TDataProvider>(
            in TFunctionView functionView,
            in TDataProvider dataProvider)
            where TFunctionView : IFunctionCollection<TPredicate>
            where TPredicate : IFunctionCollectionPredicate
            where TDataProvider : IDataProvider
        {
            var result = new FunctionLandscape<T>();
            result.Init<TFunctionView, TPredicate, TDataProvider>(functionView, dataProvider);
            return result;
        }

        #endregion

        #region Instance

        private readonly Dictionary<TopLevelFunction, Entry> entries = new Dictionary<TopLevelFunction, Entry>();
        private readonly List<Entry> sinks = new List<Entry>();
        private readonly List<Entry> postOrder = new List<Entry>();

        /// <summary>
        /// Constructs a new function structure instance.
        /// </summary>
        protected FunctionLandscape() { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the landscape entry of the given function.
        /// </summary>
        /// <param name="topLevelFunction"></param>
        /// <returns>The resolved landscape entry.</returns>
        public Entry this[TopLevelFunction topLevelFunction]
        {
            get
            {
                if (TryGetEntry(topLevelFunction, out Entry entry))
                    return entry;
                throw new KeyNotFoundException("Could not find the given function in this landscape");
            }
        }

        /// <summary>
        /// Returns the number of function entries.
        /// </summary>
        public int Count => postOrder.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the landscape entry of the given function.
        /// </summary>
        /// <param name="topLevelFunction">The function.</param>
        /// <param name="entry">The resolved entry.</param>
        /// <returns>True, if the entry could be resolved.</returns>
        public bool TryGetEntry(TopLevelFunction topLevelFunction, out Entry entry) =>
            entries.TryGetValue(topLevelFunction, out entry);

        /// <summary>
        /// Computes all entries.
        /// </summary>
        /// <param name="functionView">The source function view.</param>
        /// <param name="dataProvider">A custom data provider.</param>
        protected void Init<TFunctionView, TPredicate, TDataProvider>(
            in TFunctionView functionView,
            TDataProvider dataProvider)
            where TFunctionView : IFunctionCollection<TPredicate>
            where TPredicate : IFunctionCollectionPredicate
            where TDataProvider : IDataProvider
        {
            var context = functionView.Context;
            var predicate = functionView.Predicate;
            var functions = new ConcurrentBag<Entry>();

            Parallel.ForEach(
                functionView,
                fun =>
                {
                    var scope = Scope.Create(context, fun);
                    var references = scope.ComputeFunctionReferences(predicate);

                    var data = dataProvider.GetData(scope, references);
                    var entry = new Entry(fun, scope, references, data);
                    functions.Add(entry);
                });

            foreach (var entry in functions)
            {
                entries[entry.Function] = entry;
                if (!entry.HasReferences)
                    sinks.Add(entry);
            }
            foreach (var entry in entries.Values)
            {
                foreach (var reference in entry.References)
                    entries[reference].AddUse(entry.Function);
            }

            if (sinks.Count > 0)
                ComputeOrder();
        }

        /// <summary>
        /// Computes the post order of the nested call graph.
        /// </summary>
        private void ComputeOrder()
        {
            var toProcess = new Stack<(Entry, HashSet<TopLevelFunction>.Enumerator, bool)>(entries.Count - sinks.Count);
            var visited = new HashSet<Entry>();
            var currentSink = 0;
            var current = (Fun: sinks[0], Enumerator: sinks[0].GetEnumerator(), Init: true);

            while (true)
            {
                var currentFunction = current.Fun;

                if (current.Init && !visited.Add(currentFunction))
                    goto next;

                var enumerator = current.Enumerator;
                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    postOrder.Add(current.Fun);
                    goto next;
                }
                else
                {
                    toProcess.Push((current.Fun, enumerator, false));
                    var nextReference = this[enumerator.Current];
                    current = (nextReference, nextReference.GetEnumerator(), true);
                }

                continue;
                next:
                if (toProcess.Count < 1)
                {
                    if (++currentSink >= sinks.Count)
                        break;

                    // Check next sink
                    var nextSink = sinks[currentSink];
                    current = (nextSink, nextSink.GetEnumerator(), true);
                }
                else
                    current = toProcess.Pop();
            }
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all functions in the call graph in post order.
        /// </summary>
        /// <returns>An enumerator that enumerates all functions in the call graph.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        #endregion
    }

    /// <summary>
    /// Represents the structure of multiple <see cref="TopLevelFunction"/> objects.
    /// This includes the call graph, function size and dependency information.
    /// </summary>
    public sealed class FunctionLandscape : FunctionLandscape<object>
    {
        #region Nested Types

        /// <summary>
        /// The default data provider.
        /// </summary>
        private readonly struct DataProvider : IDataProvider
        {
            /// <summary cref="FunctionLandscape{T}.IDataProvider.GetData(Scope, FunctionReferences)"/>
            public object GetData(Scope scope, FunctionReferences functionReferences) => null;
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a function structure instance.
        /// </summary>
        /// <typeparam name="TFunctionView">The type of the function view.</typeparam>
        /// <typeparam name="TPredicate">The view predicate.</typeparam>
        /// <param name="functionView">The source function view.</param>
        /// <returns>The created function structure object.</returns>
        public static FunctionLandscape Create<TFunctionView, TPredicate>(in TFunctionView functionView)
            where TFunctionView : IFunctionCollection<TPredicate>
            where TPredicate : IFunctionCollectionPredicate
        {
            var landscape = new FunctionLandscape();
            landscape.Init<TFunctionView, TPredicate, DataProvider>(functionView, new DataProvider());
            return landscape;
        }

        /// <summary>
        /// Creates a function structure instance.
        /// </summary>
        /// <typeparam name="T">The custom information type.</typeparam>
        /// <typeparam name="TFunctionView">The type of the function view.</typeparam>
        /// <typeparam name="TPredicate">The view predicate.</typeparam>
        /// <typeparam name="TDataProvider">The custom data provider type.</typeparam>
        /// <param name="functionView">The source function view.</param>
        /// <param name="dataProvider">A custom data provider.</param>
        /// <returns>The created function structure object.</returns>
        public static FunctionLandscape<T> Create<T, TFunctionView, TPredicate, TDataProvider>(
            in TFunctionView functionView,
            in TDataProvider dataProvider)
            where TFunctionView : IFunctionCollection<TPredicate>
            where TPredicate : IFunctionCollectionPredicate
            where TDataProvider : FunctionLandscape<T>.IDataProvider =>
            FunctionLandscape<T>.Create<TFunctionView, TPredicate, TDataProvider>(functionView, dataProvider);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new function structure instance.
        /// </summary>
        private FunctionLandscape() { }

        #endregion
    }
}
