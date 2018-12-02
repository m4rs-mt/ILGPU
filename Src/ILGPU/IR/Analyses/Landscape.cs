// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Landscape.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents the structure of multiple <see cref="Method"/> objects.
    /// This includes the call graph, function size and dependency information.
    /// </summary>
    /// <typeparam name="T">Custom information type per entry.</typeparam>
    public class Landscape<T>
    {
        #region Nested Types

        /// <summary>
        /// Represents a landscape entry.
        /// </summary>
        public sealed class Entry
        {
            /// <summary>
            /// Compares two entries according to their associated method's id.
            /// </summary>
            internal static readonly Comparison<Entry> Comparison =
                (first, second) => first.Method.Id.CompareTo(second.Method.Id);

            private readonly HashSet<Method> usesSet = new HashSet<Method>();
            private List<Method> uses;

            internal Entry(
                Method method,
                Scope scope,
                References references,
                in T data)
            {
                Debug.Assert(method != null, "Invalid method");
                Debug.Assert(scope != null, "Invalid scope");

                Method = method;
                Scope = scope;
                References = references;
                Data = data;
            }

            /// <summary>
            /// Returns the associated method.
            /// </summary>
            public Method Method { get; }

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
            /// Returns the number of basic block.
            /// </summary>
            public int NumBlocks => Scope.Count;

            /// <summary>
            /// Returns the number of uses.
            /// </summary>
            public int NumUses => uses.Count;

            /// <summary>
            /// Returns true if this function has references.
            /// </summary>
            public bool HasReferences => !References.IsEmpty;

            /// <summary>
            /// Returns all method references to other methods.
            /// </summary>
            public References References { get; }

            /// <summary>
            /// Registers all resolved uses (backward edges).
            /// </summary>
            /// <param name="method">The method.</param>
            /// <returns>True, if this method is used by the given one.</returns>
            public bool IsUsedBy(Method method) => usesSet.Contains(method);

            /// <summary>
            /// Registers the given method use.
            /// </summary>
            /// <param name="method">The method to register.</param>
            internal void AddUse(Method method) => usesSet.Add(method);

            /// <summary>
            /// Finishes the adding of use nodes.
            /// </summary>
            internal void FinishUses()
            {
                uses = new List<Method>(usesSet.Count);
                foreach (var use in usesSet)
                    uses.Add(use);
                uses.Sort(Method.Comparison);
            }

            /// <summary>
            /// Returns an enumerator to enumerate all method entries that
            /// depend on this one (backward edges).
            /// </summary>
            /// <returns>An enumerator to enumerate all depending method entries.</returns>
            internal List<Method>.Enumerator GetEnumerator() => uses.GetEnumerator();

            /// <summary>
            /// Returns the string representation of this entry.
            /// </summary>
            /// <returns>The string representation of this entry.</returns>
            public override string ToString() => Method.ToString();
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
            /// <param name="methodReferences">All references to other methods.</param>
            /// <returns>The resolved custom data.</returns>
            T GetData(Scope scope, References methodReferences);
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
            internal Enumerator(Landscape<T> landscape)
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
            public Landscape<T> Landscape { get; }

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
        public static Landscape<T> Create<TPredicate, TDataProvider>(
            in MethodCollection<TPredicate> functionView,
            in TDataProvider dataProvider)
            where TPredicate : IMethodCollectionPredicate
            where TDataProvider : IDataProvider =>
            Create<MethodCollection<TPredicate>, TPredicate, TDataProvider>(
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
        public static Landscape<T> Create<TPredicate, TDataProvider>(
            in UnsafeMethodCollection<TPredicate> functionView,
            in TDataProvider dataProvider)
            where TPredicate : IMethodCollectionPredicate
            where TDataProvider : IDataProvider =>
            Create<UnsafeMethodCollection<TPredicate>, TPredicate, TDataProvider>(
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
        public static Landscape<T> Create<TFunctionView, TPredicate, TDataProvider>(
            in TFunctionView functionView,
            in TDataProvider dataProvider)
            where TFunctionView : IMethodCollection<TPredicate>
            where TPredicate : IMethodCollectionPredicate
            where TDataProvider : IDataProvider
        {
            var result = new Landscape<T>();
            result.Init<TFunctionView, TPredicate, TDataProvider>(functionView, dataProvider);
            return result;
        }

        #endregion

        #region Instance

        private readonly Dictionary<Method, Entry> entries = new Dictionary<Method, Entry>();
        private readonly List<Entry> sinks = new List<Entry>();
        private readonly List<Entry> postOrder = new List<Entry>();

        /// <summary>
        /// Constructs a new structure instance.
        /// </summary>
        protected Landscape() { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the landscape entry of the given method.
        /// </summary>
        /// <param name="method">The source method.</param>
        /// <returns>The resolved landscape entry.</returns>
        public Entry this[Method method]
        {
            get
            {
                if (TryGetEntry(method, out Entry entry))
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
        /// Tries to resolve the landscape entry of the given method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="entry">The resolved entry.</param>
        /// <returns>True, if the entry could be resolved.</returns>
        public bool TryGetEntry(Method method, out Entry entry) =>
            entries.TryGetValue(method, out entry);

        /// <summary>
        /// Computes all entries.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="dataProvider">A custom data provider.</param>
        protected void Init<TView, TPredicate, TDataProvider>(
            in TView view,
            TDataProvider dataProvider)
            where TView : IMethodCollection<TPredicate>
            where TPredicate : IMethodCollectionPredicate
            where TDataProvider : IDataProvider
        {
            var predicate = view.Predicate;
            var methods = new ConcurrentBag<Entry>();

            Parallel.ForEach(
                view,
                method =>
                {
                    var scope = method.CreateScope();
                    var references = scope.ComputeReferences(predicate);

                    var data = dataProvider.GetData(scope, references);
                    var entry = new Entry(method, scope, references, data);
                    methods.Add(entry);
                });

            foreach (var entry in methods)
            {
                entries[entry.Method] = entry;
                if (!entry.HasReferences)
                    sinks.Add(entry);
            }
            foreach (var entry in entries.Values)
            {
                foreach (var reference in entry.References)
                {
                    if (entries.TryGetValue(reference, out Entry referenceEntry))
                        referenceEntry.AddUse(entry.Method);
                }
            }

            Parallel.ForEach(entries.Values, entry => entry.FinishUses());

            if (sinks.Count > 0)
            {
                // Sort sinks to have a deterministic order
                sinks.Sort(Entry.Comparison);
                ComputeOrder();
            }
        }

        /// <summary>
        /// Computes the post order of the nested call graph.
        /// </summary>
        private void ComputeOrder()
        {
            var toProcess = new Stack<(Entry, List<Method>.Enumerator, bool)>(entries.Count - sinks.Count);
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
    /// Represents the structure of multiple <see cref="Method"/> objects.
    /// This includes the call graph, function size and dependency information.
    /// </summary>
    public sealed class Landscape : Landscape<object>
    {
        #region Nested Types

        /// <summary>
        /// The default data provider.
        /// </summary>
        private readonly struct DataProvider : IDataProvider
        {
            /// <summary cref="Landscape{T}.IDataProvider.GetData(Scope, References)"/>
            public object GetData(Scope scope, References references) => null;
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
        public static Landscape Create<TFunctionView, TPredicate>(in TFunctionView functionView)
            where TFunctionView : IMethodCollection<TPredicate>
            where TPredicate : IMethodCollectionPredicate
        {
            var landscape = new Landscape();
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
        public static Landscape<T> Create<T, TFunctionView, TPredicate, TDataProvider>(
            in TFunctionView functionView,
            in TDataProvider dataProvider)
            where TFunctionView : IMethodCollection<TPredicate>
            where TPredicate : IMethodCollectionPredicate
            where TDataProvider : Landscape<T>.IDataProvider =>
            Landscape<T>.Create<TFunctionView, TPredicate, TDataProvider>(functionView, dataProvider);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new function structure instance.
        /// </summary>
        private Landscape() { }

        #endregion
    }
}
