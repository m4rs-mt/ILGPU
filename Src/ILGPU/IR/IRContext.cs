// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IRContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Construction;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an IR context.
    /// </summary>
    public sealed partial class IRContext : DisposeBase, ICache, IDumpable
    {
        #region Nested Types

        /// <summary>
        /// Represents no transformer handler.
        /// </summary>
        private readonly struct NoHandler : ITransformerHandler
        {
            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void BeforeTransformation(
                IRContext context,
                Transformation transformation)
            { }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AfterTransformation(
                IRContext context,
                Transformation transformation)
            { }
        }

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim irLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Action<Method> gcDelegate;

        private readonly MethodMapping<Method> methods = new MethodMapping<Method>();

        /// <summary>
        /// Constructs a new IR context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        public IRContext(Context context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            TypeContext = context.TypeContext;

            UndefinedValue = new UndefinedValue(
                new ValueInitializer(
                    this,
                    null,
                    Location.Nowhere));

            gcDelegate = (Method method) => method.GC();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the main ILGPU context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns an undefined value.
        /// </summary>
        public UndefinedValue UndefinedValue { get; }

        /// <summary>
        /// Returns the associated flags.
        /// </summary>
        public ContextFlags Flags => Context.Flags;

        /// <summary>
        /// Internal (unsafe) access to all top-level functions.
        /// </summary>
        /// <remarks>
        /// The resulting collection is not thread safe in terms
        /// of parallel operations on this context.
        /// </remarks>
        public UnsafeMethodCollection<MethodCollections.AllMethods> UnsafeMethods =>
            GetUnsafeMethodCollection(new MethodCollections.AllMethods());

        /// <summary>
        /// Returns all top-level functions.
        /// </summary>
        public MethodCollection<MethodCollections.AllMethods> Methods =>
            GetMethodCollection(new MethodCollections.AllMethods());

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the current context has the given flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, if the current context has the given flags.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFlags(ContextFlags flags) => Context.HasFlags(flags);

        /// <summary>
        /// Returns an unsafe (not thread-safe) function view.
        /// </summary>
        /// <typeparam name="TPredicate">The type of the predicate to apply.</typeparam>
        /// <param name="predicate">The predicate to apply.</param>
        /// <returns>The resolved function view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeMethodCollection<TPredicate>
            GetUnsafeMethodCollection<TPredicate>(TPredicate predicate)
            where TPredicate : IMethodCollectionPredicate =>
            new UnsafeMethodCollection<TPredicate>(
                this,
                methods.AsReadOnly(),
                predicate);

        /// <summary>
        /// Returns a thread-safe function view.
        /// </summary>
        /// <typeparam name="TPredicate">The type of the predicate to apply.</typeparam>
        /// <param name="predicate">The predicate to apply.</param>
        /// <returns>The resolved function view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodCollection<TPredicate>
            GetMethodCollection<TPredicate>(TPredicate predicate)
            where TPredicate : IMethodCollectionPredicate
        {
            irLock.EnterReadLock();
            try
            {
                var builder = ImmutableArray.CreateBuilder<Method>(
                    methods.Count);
                foreach (var function in methods)
                {
                    if (predicate.Match(function))
                        builder.Add(function);
                }
                return new MethodCollection<TPredicate>(
                    this,
                    builder.ToImmutable(),
                    predicate);
            }
            finally
            {
                irLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Tries to resolve the given managed method to function reference.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="handle">The resolved function reference (if any).</param>
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetMethodHandle(MethodBase method, out MethodHandle handle)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            irLock.EnterReadLock();
            try
            {
                return methods.TryGetHandle(method, out handle);
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
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetMethod(MethodHandle handle, out Method function)
        {
            if (handle.IsEmpty)
            {
                function = null;
                return false;
            }

            irLock.EnterReadLock();
            try
            {
                return methods.TryGetData(handle, out function);
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
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetMethod(MethodBase method, out Method function)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            function = null;
            irLock.EnterReadLock();
            try
            {
                return !methods.TryGetHandle(method, out MethodHandle handle)
                    ? false
                    : methods.TryGetData(handle, out function);
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
        public Method GetMethod(MethodHandle method)
        {
            if (!TryGetMethod(method, out Method function))
            {
                throw new InvalidOperationException(string.Format(
                    ErrorMessages.CouldNotFindCorrespondingIRMethod,
                    method.Name));
            }

            return function;
        }

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="methodBase">The method to declare.</param>
        /// <param name="created">True, if the method has been created.</param>
        /// <returns>The declared method.</returns>
        public Method Declare(MethodBase methodBase, out bool created)
        {
            Debug.Assert(methodBase != null, "Invalid method base");
            // Check for existing method
            irLock.EnterUpgradeableReadLock();
            try
            {
                if (methods.TryGetHandle(methodBase, out MethodHandle handle))
                {
                    created = false;
                    return methods[handle];
                }

                handle = MethodHandle.Create(methodBase.Name);
                var declaration = new MethodDeclaration(
                    handle,
                    CreateType(methodBase.GetReturnType()),
                    methodBase);
                return Declare(declaration, out created);
            }
            finally
            {
                irLock.ExitUpgradeableReadLock();
            }

        }

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="declaration">The method declaration.</param>
        /// <param name="created">True, if the method has been created.</param>
        /// <returns>The declared method.</returns>
        public Method Declare(in MethodDeclaration declaration, out bool created)
        {
            Debug.Assert(declaration.ReturnType != null, "Invalid return type");

            created = false;
            irLock.EnterUpgradeableReadLock();
            try
            {
                if (!methods.TryGetData(declaration.Handle, out Method method))
                {
                    irLock.EnterWriteLock();
                    try
                    {
                        created = true;
                        method = DeclareNewMethod_Sync(declaration, out var handle);
                        methods.Register(handle, method);
                    }
                    finally
                    {
                        irLock.ExitWriteLock();
                    }
                }
                return method;
            }
            finally
            {
                irLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Declares a new method.
        /// </summary>
        /// <param name="declaration">The method declaration to use.</param>
        /// <param name="handle">The created handle.</param>
        /// <returns>The declared method.</returns>
        private Method DeclareNewMethod_Sync(
            MethodDeclaration declaration,
            out MethodHandle handle)
        {
            var methodId = Context.CreateMethodHandle();
            var methodName = declaration.HasSource
                ? declaration.Source.Name
                : declaration.Handle.Name ?? "Func";
            handle = new MethodHandle(methodId, methodName);
            var specializedDeclaration = declaration.Specialize(handle);

            // TODO: we might want to extend the method location information to point to
            // the location of the first instruction instead
            var method = new Method(
                this,
                specializedDeclaration,
                Location.Unknown);

            // Check for external function
            if (declaration.HasFlags(
                MethodFlags.External | MethodFlags.Intrinsic))
            {
                using var builder = method.CreateBuilder();
                var bbBuilder = builder.EntryBlockBuilder;
                var returnValue = bbBuilder.CreateNull(
                    method.Location,
                    method.ReturnType);
                bbBuilder.CreateReturn(method.Location, returnValue);
            }
            return method;
        }

        /// <summary>
        /// Imports the given method (and all dependencies) into this context.
        /// </summary>
        /// <param name="source">The method to import.</param>
        /// <returns>The imported method.</returns>
        public Method Import(Method source)
        {
            if (source is null)
                throw source.GetArgumentException(nameof(source));
            if (source.Context == this)
                throw source.GetInvalidOperationException();

            irLock.EnterUpgradeableReadLock();
            try
            {
                if (methods.TryGetData(source.Handle, out Method method))
                    return method;

                irLock.EnterWriteLock();
                try
                {
                    return ImportInternal(source);
                }
                finally
                {
                    irLock.ExitWriteLock();
                }
            }
            finally
            {
                irLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Imports the given method (and all dependencies) into this context.
        /// </summary>
        /// <param name="source">The method to import.</param>
        /// <returns>The imported method.</returns>
        private Method ImportInternal(Method source)
        {
            var allReferences = References.CreateRecursive(
                source.Blocks,
                new MethodCollections.AllMethods());

            // Declare all functions and build mapping
            var methodsToRebuild = new List<Method>();
            var targetMapping = new Dictionary<Method, Method>();
            foreach (var entry in allReferences)
            {
                var declared = Declare(entry.Declaration, out bool created);
                targetMapping.Add(entry, declared);
                if (created)
                    methodsToRebuild.Add(entry);
            }

            // Rebuild all functions while using the created mapping
            var methodMapping = new Method.MethodMapping(targetMapping);
            foreach (var sourceMethod in methodsToRebuild)
            {
                var targetMethod = methodMapping[sourceMethod];

                {
                    using var builder = targetMethod.CreateBuilder();
                    // Build new parameters to match the old ones
                    var parameterArguments = ValueList.Create(
                        sourceMethod.NumParameters);
                    foreach (var param in sourceMethod.Parameters)
                    {
                        var newParam = builder.AddParameter(param.Type, param.Name);
                        parameterArguments.Add(newParam);
                    }
                    var parameterMapping = sourceMethod.CreateParameterMapping(
                        parameterArguments);

                    // Rebuild the source function into this context
                    var rebuilder = builder.CreateRebuilder<IRRebuilder.CloneMode>(
                        parameterMapping,
                        methodMapping,
                        sourceMethod.Blocks);

                    // Create an appropriate return instruction
                    var (exitBlock, exitValue) = rebuilder.Rebuild();
                    exitBlock.CreateReturn(exitValue.Location, exitValue);
                }
                Verifier.Verify(targetMethod);
            }

            return targetMapping[source];
        }

        /// <summary>
        /// Applies all default optimization transformations.
        /// </summary>
        public void Optimize() => Transform(Context.ContextTransformer);

        /// <summary>
        /// Applies the given transformer to the current context.
        /// </summary>
        /// <param name="transformer">The target transformer.</param>
        public void Transform(in Transformer transformer) =>
            Transform(transformer, new NoHandler());

        /// <summary>
        /// Applies the given transformer to the current context.
        /// </summary>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="transformer">The target transformer.</param>
        /// <param name="handler">The target handler.</param>
        public void Transform<THandler>(in Transformer transformer, THandler handler)
            where THandler : ITransformerHandler
        {
            irLock.EnterWriteLock();
            try
            {
                transformer.Transform(this, handler);
            }
            finally
            {
                irLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Dumps the IR context to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public void Dump(TextWriter textWriter)
        {
            foreach (var method in UnsafeMethods)
            {
                method.Dump(textWriter);
                textWriter.WriteLine();
                textWriter.WriteLine("------------------------------");
            }
        }

        #endregion

        #region GC

        /// <summary>
        /// Rebuilds all nodes and clears up the IR.
        /// </summary>
        /// <remarks>
        /// This method must not be invoked in the context of other
        /// parallel operations using this context.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Reliability",
            "CA2001:AvoidCallingProblematicMethods",
            Justification = "Users might want to force a global GC to free memory " +
            "after an internal ILGPU GC run")]
        public void GC()
        {
            irLock.EnterWriteLock();
            try
            {
                Parallel.ForEach(UnsafeMethods, gcDelegate);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                irLock.ExitWriteLock();
            }

            // Check whether to call a forced system GC
            if (HasFlags(ContextFlags.ForceSystemGC))
                System.GC.Collect();
        }

        /// <summary>
        /// Clears cached IR nodes.
        /// </summary>
        [Obsolete("Use ClearCache(ClearCacheMode.Everything) instead")]
        public void Clear() => ClearCache(ClearCacheMode.Everything);

        /// <summary>
        /// Clears cached IR nodes.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public void ClearCache(ClearCacheMode mode)
        {
            irLock.EnterWriteLock();
            try
            {
                methods.Clear();
            }
            finally
            {
                irLock.ExitWriteLock();
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                irLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
