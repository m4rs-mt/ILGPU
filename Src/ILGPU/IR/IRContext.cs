// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using ILGPU.IR.Transformations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using ILGPU.Util;
using ILGPU.IR.Types;
using System.Runtime.CompilerServices;
#if PARALLEL_PROCESSING
using System.Threading.Tasks;
#endif

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an IR context.
    /// </summary>
    public sealed partial class IRContext : DisposeBase
    {
        #region Instance

        private readonly ReaderWriterLockSlim irLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private long idCounter = 0;
        private long functionHandleCounter = 0;
        private long generationCounter = 0;
        private long nodeMarker = 0L;

        private readonly Transformer[] transformers;
        private volatile IRBuilder currentBuilder;

        private readonly FunctionMapping<TopLevelFunction> topLevelFunctions =
            new FunctionMapping<TopLevelFunction>();

        /// <summary>
        /// Constructs a new IR context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        /// <param name="flags">The context flags.</param>
        public IRContext(Context context, IRContextFlags flags)
            : this(context.TypeInformationManger, flags)
        { }

        /// <summary>
        /// Constructs a new IR context.
        /// </summary>
        /// <param name="typeInformationManager">The associated type context.</param>
        /// <param name="flags">The context flags.</param>
        public IRContext(
            TypeInformationManager typeInformationManager,
            IRContextFlags flags)
        {
            basicValueTypes = new PrimitiveType[BasicValueTypes.Length + 1];
            TypeInformationManager = typeInformationManager ?? throw new ArgumentNullException(nameof(typeInformationManager));
            Flags = flags;

            CreateGlobalTypes();

            transformers = new Transformer[]
            {
                Optimizer.CreateTransformer(
                    OptimizationLevel.Debug,
                    TransformerConfiguration.Transformed,
                    new DefaultInliningConfiguration()),
                Optimizer.CreateTransformer(
                    OptimizationLevel.Release,
                    TransformerConfiguration.Transformed,
                    new DefaultInliningConfiguration()),
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type information manager.
        /// </summary>
        public TypeInformationManager TypeInformationManager { get; }

        /// <summary>
        /// Returns the associated flags.
        /// </summary>
        public IRContextFlags Flags { get; }

        /// <summary>
        /// Returns the current node generation.
        /// </summary>
        public ValueGeneration CurrentGeneration => new ValueGeneration(generationCounter);

        /// <summary>
        /// Internal (unsafe) access to all top-level functions.
        /// </summary>
        /// <remarks>
        /// The resulting collection is not thread safe in terms
        /// of parallel operations on this context.
        /// </remarks>
        public UnsafeFunctionCollection<FunctionCollections.AllFunctions> UnsafeTopLevelFunctions =>
            GetUnsafeFunctionCollection(new FunctionCollections.AllFunctions());

        /// <summary>
        /// Returns all top-level functions.
        /// </summary>
        public FunctionCollection<FunctionCollections.AllFunctions> TopLevelFunctions =>
            GetFunctionCollection(new FunctionCollections.AllFunctions());

        #endregion

        #region Methods

        /// <summary>
        /// Returns an unsafe (not thread-safe) function view.
        /// </summary>
        /// <typeparam name="TPredicate">The type of the predicate to apply.</typeparam>
        /// <param name="predicate">The predicate to apply.</param>
        /// <returns>The resolved function view.</returns>
        public UnsafeFunctionCollection<TPredicate> GetUnsafeFunctionCollection<TPredicate>(TPredicate predicate)
            where TPredicate : IFunctionCollectionPredicate
        {
            return new UnsafeFunctionCollection<TPredicate>(this, topLevelFunctions.AsReadOnly(), predicate);
        }

        /// <summary>
        /// Returns a thread-safe function view.
        /// </summary>
        /// <typeparam name="TPredicate">The type of the predicate to apply.</typeparam>
        /// <param name="predicate">The predicate to apply.</param>
        /// <returns>The resolved function view.</returns>
        public FunctionCollection<TPredicate> GetFunctionCollection<TPredicate>(TPredicate predicate)
            where TPredicate : IFunctionCollectionPredicate
        {
            irLock.EnterReadLock();
            try
            {
                var builder = ImmutableArray.CreateBuilder<TopLevelFunction>(
                    topLevelFunctions.Count);
                foreach (var function in topLevelFunctions)
                {
                    if (predicate.Match(function))
                        builder.Add(function);
                }
                return new FunctionCollection<TPredicate>(this, builder.ToImmutable(), predicate);
            }
            finally
            {
                irLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Creates a new unique node marker.
        /// </summary>
        /// <returns>The new node marker.</returns>
        public NodeMarker NewNodeMarker()
        {
            return new NodeMarker(Interlocked.Add(ref nodeMarker, 1L));
        }

        /// <summary>
        /// Tries to resolve the given managed method to function reference.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="handle">The resolved function reference (if any).</param>
        /// <returns>True, iff the requested function could be resolved.</returns>
        public bool TryGetFunctionHandle(MethodBase method, out FunctionHandle handle)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            irLock.EnterReadLock();
            try
            {
                return topLevelFunctions.TryGetHandle(method, out handle);
            }
            finally
            {
                irLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Tries to resolve the given handle to a top-level function.
        /// </summary>
        /// <param name="handle">The function handle to resolve.</param>
        /// <param name="function">The resolved function (if any).</param>
        /// <returns>True, iff the requested function could be resolved.</returns>
        public bool TryGetFunction(FunctionHandle handle, out TopLevelFunction function)
        {
            if (handle.IsEmpty)
            {
                function = null;
                return false;
            }

            irLock.EnterReadLock();
            try
            {
                return topLevelFunctions.TryGetData(handle, out function);
            }
            finally
            {
                irLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Tries to resolve the given method to a top-level function.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="function">The resolved function (if any).</param>
        /// <returns>True, iff the requested function could be resolved.</returns>
        public bool TryGetFunction(MethodBase method, out TopLevelFunction function)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            function = null;
            irLock.EnterReadLock();
            try
            {
                if (!topLevelFunctions.TryGetHandle(method, out FunctionHandle handle))
                    return false;
                return topLevelFunctions.TryGetData(handle, out function);
            }
            finally
            {
                irLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Resolves the given method to a top-level function.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <returns>The resolved function.</returns>
        public TopLevelFunction GetFunction(FunctionHandle method)
        {
            if (!TryGetFunction(method, out TopLevelFunction function))
                throw new InvalidOperationException("Could not find the corresponding top-level function");
            return function;
        }

        /// <summary>
        /// Refereshes the given top-level function.
        /// </summary>
        /// <param name="topLevelFunction">The function to refresh.</param>
        public void RefreshFunction(ref TopLevelFunction topLevelFunction)
        {
            if (topLevelFunction == null)
                throw new ArgumentNullException(nameof(topLevelFunction));
            topLevelFunction = GetFunction(topLevelFunction.Handle);
        }

        /// <summary>
        /// Creates a new IR builder.
        /// </summary>
        /// <returns>The created IR builder.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope",
            Justification = "The builder will be disposed later on")]
        public IRBuilder CreateBuilder() => CreateBuilder(IRBuilderFlags.None);

        /// <summary>
        /// Creates a new IR builder.
        /// </summary>
        /// <param name="flags">The builder flags.</param>
        /// <returns>The created IR builder.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope",
            Justification = "The builder will be disposed later on")]
        public IRBuilder CreateBuilder(IRBuilderFlags flags)
        {
            var newBuilder = new IRBuilder(this, flags);
            if (Interlocked.CompareExchange(ref currentBuilder, newBuilder, null) != null)
                throw new InvalidOperationException();
            return newBuilder;
        }

        /// <summary>
        /// Finalizes the given builder.
        /// </summary>
        /// <param name="builder">The builder to finalize.</param>
        /// <param name="updatedTopLevelFunctions">A collection of updated top-level functions.</param>
        internal void FinalizeBuilder(
            IRBuilder builder,
            FunctionMapping<FunctionBuilder> updatedTopLevelFunctions)
        {
            irLock.EnterWriteLock();
            try
            {
                Debug.Assert(builder != null, "Invalid builder to finalize");
                if (Interlocked.CompareExchange(ref currentBuilder, null, builder) != builder)
                    throw new InvalidOperationException();

                // Update top-level functions
                foreach (var functionBuilder in updatedTopLevelFunctions)
                {
                    var topLevelFunction = functionBuilder.FunctionValue as TopLevelFunction;
                    Debug.Assert(topLevelFunction != null, "Invalid top-level function");

#if VERIFICATION
                    Debug.Assert(topLevelFunction.IsSealed, "Function not sealed");
#endif


                    // Register new function
                    topLevelFunctions.Register(topLevelFunction.Handle, topLevelFunction);
                }
            }
            finally
            {
                irLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Unloads all unreachable methods.
        /// </summary>
        /// <param name="reachableFunctions">The axiomatically reachable functions.</param>
        public void UnloadUnreachableMethods(ImmutableArray<TopLevelFunction> reachableFunctions)
        {
            if (reachableFunctions.IsDefaultOrEmpty)
                throw new ArgumentOutOfRangeException(nameof(reachableFunctions));

            irLock.EnterWriteLock();
            try
            {
                var toProcess = new Stack<TopLevelFunction>(reachableFunctions.Length << 1);
                var reachable = new HashSet<TopLevelFunction>();
                var current = reachableFunctions[0];
                for (int i = 1, e = reachableFunctions.Length; i < e; ++i)
                    toProcess.Push(reachableFunctions[i]);

                while (true)
                {
                    if (reachable.Add(current))
                    {
                        var scope = Scope.Create(this, current);
                        var references = scope.ComputeFunctionReferences(
                            new FunctionCollections.AllFunctions());
                        foreach (var reference in references)
                            toProcess.Push(reference);
                    }
                    if (toProcess.Count < 1)
                        break;
                    current = toProcess.Pop();
                }

                foreach (var func in TopLevelFunctions)
                {
                    if (!reachable.Contains(func))
                        topLevelFunctions.Remove(func.Handle);
                }
            }
            finally
            {
                irLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Unloads all related nodes that are associated with the given method.
        /// </summary>
        /// <param name="handle">The function handle to unload.</param>
        /// <remarks>All effects of the unloading process will be visible after a GC step.</remarks>
        public void UnloadMethod(FunctionHandle handle)
        {
            irLock.EnterWriteLock();
            try
            {
                if (!topLevelFunctions.Remove(handle))
                    throw new ArgumentOutOfRangeException(nameof(handle));
            }
            finally
            {
                irLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Imports the given function into this context.
        /// </summary>
        /// <param name="sourceContext">The source context.</param>
        /// <param name="sourceFunction">The function to import.</param>
        /// <param name="importSpecification">The import specification.</param>
        /// <returns>The imported function.</returns>
        public TopLevelFunction Import(
            IRContext sourceContext,
            TopLevelFunction sourceFunction,
            in ContextImportSpecification importSpecification)
        {
            using (var builder = CreateBuilder())
            {
                sourceFunction = builder.Import(
                    sourceContext,
                    sourceFunction,
                    importSpecification.ToSpecializer());
            }

            RefreshFunction(ref sourceFunction);
            return sourceFunction;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new unique node id.
        /// </summary>
        /// <returns>A new unique node id.</returns>
        private NodeId CreateNodeId()
        {
            return new NodeId(Interlocked.Add(ref idCounter, 1));
        }

        /// <summary>
        /// Prepares the given function declaration by creating a new handle
        /// if no handle was specified.
        /// </summary>
        /// <param name="declaration">The declaration to prepare.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PrepareFunctionDeclaration(ref FunctionDeclaration declaration)
        {
            if (declaration.HasHandle)
                return;
            var functionId = Interlocked.Add(ref functionHandleCounter, 1);
            var functionName = declaration.HasSource ? declaration.Source.Name :
                declaration.Handle.Name ?? "Func";
            var handle = new FunctionHandle(functionId, functionName);
            declaration = declaration.Specialize(handle);
        }

#if VERIFICATION
        /// <summary>
        /// Verifies the given generation and raises an exception if the given
        /// generation is not compatible with the current one.
        /// </summary>
        /// <param name="value">The value to verify.</param>
        internal void VerifyGeneration(Value value)
        {
            Debug.Assert(
                value.Generation == CurrentGeneration,
                "Invalid node operation on an invalid generation");
        }
#endif

        /// <summary>
        /// Creates an instantiated node by assigning a unique node id.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="value">The node to create.</param>
        /// <returns>The created node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T CreateInstantiated<T>(T value)
            where T : Value
        {
#if VERIFICATION
            VerifyGeneration(value);
#endif
            value.Id = CreateNodeId();
            return value;
        }

        /// <summary>
        /// Performs an aggressive optimization step.
        /// </summary>
        public void Optimize()
        {
            Optimize(OptimizationLevel.Release);
        }

        /// <summary>
        /// Performs an optimization step with a particular
        /// optimization level.
        /// </summary>
        /// <param name="level">The optimization level.</param>
        public void Optimize(OptimizationLevel level)
        {
            if (level < OptimizationLevel.Debug || level > OptimizationLevel.Release)
                throw new ArgumentOutOfRangeException(nameof(level));
            transformers[(int)level].Transform(this);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            irLock.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Defines import settings for a function import.
    /// </summary>
    public readonly struct ContextImportSpecification
    {
        #region Nested Types

        /// <summary>
        /// Represents an import specialization wrapper.
        /// </summary>
        public readonly struct Specializer : IFunctionImportSpecializer
        {
            /// <summary>
            /// Constructs a new specializer.
            /// </summary>
            /// <param name="specification">The associated specification.</param>
            internal Specializer(in ContextImportSpecification specification)
            {
                Specification = specification;
            }

            /// <summary>
            /// Returns the associated specification.
            /// </summary>
            public ContextImportSpecification Specification { get; }

            /// <summary cref="IFunctionImportSpecializer.Map(IRContext, TopLevelFunction, IRBuilder, IRRebuilder)"/>
            public void Map(
                IRContext sourceContext,
                TopLevelFunction sourceFunction,
                IRBuilder builder,
                IRRebuilder rebuilder)
            {
                Specification.Map(sourceFunction, rebuilder);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Maps this specifiction to the given rebuilder.
        /// </summary>
        /// <param name="sourceFunction">The source function to import.</param>
        /// <param name="rebuilder">The target rebuilder.</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic",
            Justification = "This method will contain functionality in future versions")]
        public void Map(TopLevelFunction sourceFunction, IRRebuilder rebuilder)
        {
            // Add future functionality here
        }

        /// <summary>
        /// Turns this specification into an import specializer.
        /// </summary>
        /// <returns>The specialization wrapper.</returns>
        public Specializer ToSpecializer() => new Specializer(this);

        #endregion
    }
}
