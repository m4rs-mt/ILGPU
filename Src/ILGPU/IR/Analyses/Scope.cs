// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Scope.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// General scope flags.
    /// </summary>
    [Flags]
    public enum ScopeFlags : int
    {
        /// <summary>
        /// Default scope flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Adds already visited nodes to the post order list.
        /// </summary>
        AddAlreadyVisitedNodes = 1 << 0,
    }

    /// <summary>
    /// Represents a collection of all basic blocks.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of this program analysis")]
    public sealed class Scope : IReadOnlyList<BasicBlock>
    {
        #region Nested Types

        /// <summary>
        /// Represents a visitor for function calls.
        /// </summary>
        public interface IFunctionCallVisitor
        {
            /// <summary>
            /// Visits the given function call.
            /// </summary>
            /// <param name="functionCall">The function call.</param>
            /// <returns>True, iff the process should be continued.</returns>
            bool Visit(MethodCall functionCall);
        }

        /// <summary>
        /// Enumerates all actual basic blocks in post order.
        /// </summary>
        public struct PostOrderEnumerator : IEnumerator<BasicBlock>
        {
            private int index;

            /// <summary>
            /// Constructs a new basic block enumerator.
            /// </summary>
            /// <param name="scope">All blocks.</param>
            internal PostOrderEnumerator(Scope scope)
            {
                BasicBlocks = scope;
                index = -1;
            }

            /// <summary>
            /// Returns the associated basic block collection.
            /// </summary>
            public Scope BasicBlocks { get; }

            /// <summary>
            /// Returns the current basic block.
            /// </summary>
            public BasicBlock Current => BasicBlocks.postOrder[index];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => ++index < BasicBlocks.postOrder.Count;

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// Enumerates all actual basic blocks in reverse post order.
        /// </summary>
        public struct Enumerator : IEnumerator<BasicBlock>
        {
            private int index;

            /// <summary>
            /// Constructs a new basic block enumerator.
            /// </summary>
            /// <param name="basicBlocks">All blocks.</param>
            internal Enumerator(in Scope basicBlocks)
            {
                BasicBlocks = basicBlocks;
                index = basicBlocks.postOrder.Count;
            }

            /// <summary>
            /// Returns the associated basic block collection.
            /// </summary>
            public Scope BasicBlocks { get; }

            /// <summary>
            /// Returns the current basic block.
            /// </summary>
            public BasicBlock Current => BasicBlocks.postOrder[index];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => --index >= 0;

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// An abstract view on all values.
        /// </summary>
        public readonly struct ValueCollection : IEnumerable<BasicBlock.ValueEntry>
        {
            #region Nested Types

            /// <summary>
            /// Enumerates all nodes in all blocks.
            /// </summary>
            public struct Enumerator : IEnumerator<BasicBlock.ValueEntry>
            {
                private Scope.Enumerator functionEnumerator;
                private BasicBlock.Enumerator valueEnumerator;

                /// <summary>
                /// Constructs a new basic block enumerator.
                /// </summary>
                /// <param name="scope">The parent function scope.</param>
                internal Enumerator(Scope scope)
                {
                    functionEnumerator = scope.GetEnumerator();
                    // There must be at least a single block
                    functionEnumerator.MoveNext();
                    valueEnumerator = functionEnumerator.Current.GetEnumerator();
                }

                /// <summary>
                /// Returns the current value and its parent basic block.
                /// </summary>
                public BasicBlock.ValueEntry Current => valueEnumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose()
                {
                    functionEnumerator.Dispose();
                }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext()
                {
                    while (true)
                    {
                        if (valueEnumerator.MoveNext())
                            return true;
                        valueEnumerator.Dispose();

                        // Try to move to the next function
                        if (!functionEnumerator.MoveNext())
                            return false;

                        valueEnumerator = functionEnumerator.Current.GetEnumerator();
                    }
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new value collection.
            /// </summary>
            /// <param name="scope">The parent function scope.</param>
            internal ValueCollection(Scope scope)
            {
                FunctionScope = scope;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated function scope.
            /// </summary>
            public Scope FunctionScope { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns a value enumerator.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(FunctionScope);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<BasicBlock.ValueEntry> IEnumerable<BasicBlock.ValueEntry>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Represents a post-order view of a scope.
        /// </summary>
        public readonly struct PostOrderCollection : IEnumerable<BasicBlock>
        {
            #region Instance

            /// <summary>
            /// Constructs a new post-order view.
            /// </summary>
            /// <param name="scope">The parent scope.</param>
            internal PostOrderCollection(Scope scope)
            {
                Scope = scope;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent scope.
            /// </summary>
            public Scope Scope { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns a post-order enumerator.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public PostOrderEnumerator GetEnumerator() => new PostOrderEnumerator(Scope);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Specifies the handling of already visited nodes during post order traversal.
        /// </summary>
        private interface IPostOrderNodeHandler
        {
            /// <summary>
            /// Adds an already visited node to the target list.
            /// </summary>
            /// <param name="target">The target post order list.</param>
            /// <param name="block">The block to add.</param>
            void AddAlreadyVisitedNode(List<BasicBlock> target, BasicBlock block);
        }

        /// <summary>
        /// Does not add already visited nodes to the post oder list.
        /// </summary>
        private readonly struct DefaultPostOrderHandler : IPostOrderNodeHandler
        {
            /// <summary cref="IPostOrderNodeHandler.AddAlreadyVisitedNode(List{BasicBlock}, BasicBlock)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddAlreadyVisitedNode(List<BasicBlock> target, BasicBlock block) { }
        }

        /// <summary>
        /// Adds already visited nodes to the post oder list.
        /// </summary>
        private readonly struct AddAlreadyVisitedNodesPostOrderHandler : IPostOrderNodeHandler
        {
            /// <summary cref="IPostOrderNodeHandler.AddAlreadyVisitedNode(List{BasicBlock}, BasicBlock)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddAlreadyVisitedNode(List<BasicBlock> target, BasicBlock block)
            {
                target.Add(block);
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Computes the post order of all attached blocks
        /// starting with the entry block.
        /// </summary>
        /// <param name="entryBlock">The starting block.</param>
        /// <returns>The resolved list of blocks in post order.</returns>
        private static List<BasicBlock> ComputePostOrder<THandler>(BasicBlock entryBlock)
            where THandler : struct, IPostOrderNodeHandler
        {
            THandler handler = default;

            var result = new List<BasicBlock>();
            var foundBlocks = new HashSet<BasicBlock>();
            var processed = new Stack<(BasicBlock, int)>();
            var current = (Block: entryBlock, Child: 0);

            while (true)
            {
                var currentBlock = current.Block;

                if (current.Child == 0)
                {
                    if (!foundBlocks.Add(currentBlock))
                    {
                        handler.AddAlreadyVisitedNode(result, current.Block);
                        goto next;
                    }
                }

                if (current.Child >= currentBlock.Successors.Length)
                {
                    result.Add(currentBlock);
                    goto next;
                }
                else
                {
                    processed.Push((current.Block, current.Child + 1));
                    current = (current.Block.Successors[current.Child], 0);
                }

                continue;
                next:
                if (processed.Count < 1)
                    break;
                current = processed.Pop();
            }
            return result;
        }

        /// <summary>
        /// Creates a new scope with default scope flags.
        /// </summary>
        /// <param name="method">The parent method.</param>
        /// <returns>The created scope.</returns>
        public static Scope Create(Method method) =>
            Create(method, ScopeFlags.None);

        /// <summary>
        /// Creates a new scope.
        /// </summary>
        /// <param name="method">The parent method.</param>
        /// <param name="scopeFlags">The scope flags.</param>
        /// <returns>The created scope.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Scope Create(Method method, ScopeFlags scopeFlags)
        {
            Debug.Assert(method != null, "Invalid method");
            return new Scope(method, scopeFlags);
        }

        #endregion

        #region Instance

        private readonly List<BasicBlock> postOrder;

        /// <summary>
        /// Creates a new method scope.
        /// </summary>
        /// <param name="method">The parent method.</param>
        /// <param name="scopeFlags">The current scope flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Scope(Method method, ScopeFlags scopeFlags)
        {
            Method = method;
            ScopeFlags = scopeFlags;

            postOrder =
                (scopeFlags & ScopeFlags.AddAlreadyVisitedNodes) == ScopeFlags.AddAlreadyVisitedNodes ?
                ComputePostOrder<AddAlreadyVisitedNodesPostOrderHandler>(method.EntryBlock) :
                ComputePostOrder<DefaultPostOrderHandler>(method.EntryBlock);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent context.
        /// </summary>
        public IRContext Context => Method.Context;

        /// <summary>
        /// Returns the associated function entry point.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns the current scope flags.
        /// </summary>
        public ScopeFlags ScopeFlags { get; }

        /// <summary>
        /// Returns the method's entry block.
        /// </summary>
        public BasicBlock EntryBlock => Method.EntryBlock;

        /// <summary>
        /// Returns the number of detected blocks.
        /// </summary>
        public int Count => postOrder.Count;

        /// <summary>
        /// Returns the i-th basic block.
        /// </summary>
        /// <param name="index">The basic block index.</param>
        /// <returns>The resolved basic block.</returns>
        public BasicBlock this[int index] => postOrder[index];

        /// <summary>
        /// Returns an abstract view on all values.
        /// </summary>
        public ValueCollection Values => new ValueCollection(this);

        /// <summary>
        /// Returns a post-order view of this scope.
        /// </summary>
        public PostOrderCollection PostOrder => new PostOrderCollection(this);

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new CFG.
        /// </summary>
        /// <returns>The created CFG.</returns>
        public CFG CreateCFG() => CFG.Create(this);

        /// <summary>
        /// Computes method references to all called methods.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>A references instance.</returns>
        public References ComputeReferences<TPredicate>(in TPredicate predicate)
            where TPredicate : IMethodCollectionPredicate =>
            References.Create(this, predicate);

        /// <summary>
        /// Visits all function calls in this scope.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitFunctionCalls<TVisitor>(ref TVisitor visitor)
            where TVisitor : IFunctionCallVisitor
        {
            foreach (Value value in Values)
            {
                if (value is MethodCall call)
                    visitor.Visit(call);
            }
        }

        /// <summary>
        /// Returns a reverse post-order enumerator.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Represents an abstract scope provider.
    /// </summary>
    public interface IScopeProvider
    {
        /// <summary>
        /// Resolves the scope that belongs to the given method.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <returns>The resolved scope</returns>
        Scope this[Method method] { get; }
    }

    /// <summary>
    /// Represents a thread-safe scope cache.
    /// </summary>
    public sealed class AsyncCachedScopeProvider : DisposeBase, IScopeProvider
    {
        #region Instance

        private readonly ReaderWriterLockSlim cacheLock =
            new ReaderWriterLockSlim();
        private readonly Dictionary<Method, Scope> scopes =
            new Dictionary<Method, Scope>();

        /// <summary>
        /// Creates a new scope cache.
        /// </summary>
        public AsyncCachedScopeProvider() { }

        #endregion

        #region Properties

        /// <summary>
        /// Resolves the scope that belongs to the given method.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <returns>The resolved scope</returns>
        public Scope this[Method method]
        {
            get
            {
                cacheLock.EnterUpgradeableReadLock();
                try
                {
                    if (!scopes.TryGetValue(method, out Scope scope))
                    {
                        cacheLock.EnterWriteLock();
                        try
                        {
                            scope = method.CreateScope();
                            scopes.Add(method, scope);
                        }
                        finally
                        {
                            cacheLock.ExitWriteLock();
                        }
                    }
                    return scope;
                }
                finally
                {
                    cacheLock.ExitUpgradeableReadLock();
                }
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                cacheLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Represents a non-thread-safe scope cache.
    /// </summary>
    public sealed class CachedScopeProvider : IScopeProvider
    {
        #region Nested Types

        /// <summary>
        /// An enumerator to iterate over all cached elements.
        /// </summary>
        public struct Enumerator : IEnumerator<(Method, Scope)>
        {
            #region Instance

            private Dictionary<Method, Scope>.Enumerator enumerator;

            internal Enumerator(CachedScopeProvider provider)
            {
                enumerator = provider.scopes.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current scope.
            /// </summary>
            public (Method, Scope) Current
            {
                get
                {
                    var entry = enumerator.Current;
                    return (entry.Key, entry.Value);
                }
            }

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => enumerator.Dispose();

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        #endregion

        #region Instance

        private readonly Dictionary<Method, Scope> scopes =
            new Dictionary<Method, Scope>();

        /// <summary>
        /// Creates a new scope cache.
        /// </summary>
        public CachedScopeProvider() { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of cached scopes.
        /// </summary>
        public int Count => scopes.Count;

        /// <summary>
        /// Resolves the scope that belongs to the given method.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <returns>The resolved scope</returns>
        public Scope this[Method method]
        {
            get
            {
                Resolve(method, out Scope scope);
                return scope;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the scope for the given method and returns true
        /// if the scope was not cached before.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="scope">The resolved scope.</param>
        /// <returns>True, if the scope was not registered before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Resolve(Method method, out Scope scope)
        {
            if (!scopes.TryGetValue(method, out scope))
            {
                scope = method.CreateScope();
                scopes.Add(method, scope);
                return true;
            }
            return false;
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Resolves an enumerator to iterate over all cached elements.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        #endregion
    }

    /// <summary>
    /// Creates a new scope for every method.
    /// </summary>
    public readonly struct NewScopeProvider : IScopeProvider
    {
        /// <summary>
        /// Creates a new scope that belongs to the given method.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <returns>The created scope</returns>
        public Scope this[Method method] => method.CreateScope();
    }
}
