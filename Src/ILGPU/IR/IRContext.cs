// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend;
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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR
{
    /// <summary>
    /// An abstract base IR context.
    /// </summary>
    public abstract partial class IRBaseContext : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs a new IR context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        /// <param name="forExport">
        /// Flag determining whether this context should export its data.
        /// </param>
        protected IRBaseContext(Context context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            TypeContext = context.TypeContext;

            UndefinedValue = new UndefinedValue(
                new ValueInitializer(
                    this,
                    null,
                    Location.Nowhere));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the main ILGPU context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the main context properties.
        /// </summary>
        public ContextProperties Properties => Context.Properties;

        /// <summary>
        /// Returns the current verifier instance.
        /// </summary>
        internal Verifier Verifier => Context.Verifier;

        /// <summary>
        /// Returns the associated type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns an undefined value.
        /// </summary>
        public UndefinedValue UndefinedValue { get; }

        #endregion
    }

    /// <summary>
    /// Represents an IR context.
    /// </summary>
    public sealed class IRContext : IRBaseContext, ICache, IDumpable, IExportable<IRContext.Exported>
    {
        #region Nested Types

        /// <summary>
        /// Represents an immutable, exported version
        /// of an <see cref="IRContext"/> instance.
        /// </summary>
        /// <param name="Methods">Collection of <see cref="IRMethod"/> instances</param>
        /// <param name="Values">Collection of <see cref="IRValue"/> instances</param>
        /// <param name="Types">Collection of <see cref="IRType"/> instances</param>
        public record struct Exported(
            ImmutableArray<IRMethod> Methods,
            ImmutableArray<IRValue> Values,
            ImmutableArray<IRType> Types)
        {
        }

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim irLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private readonly Action<Method> gcDelegate;

        private readonly MethodMapping<Method> methods = new MethodMapping<Method>();

        /// <summary>
        /// Constructs a new IR context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        /// <param name="forExport">
        /// Flag determining whether this context should export its data.
        /// </param>
        public IRContext(Context context)
            : base(context)
        {
            gcDelegate = (Method method) => method.GC();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns all top-level functions.
        /// </summary>
        public MethodCollection Methods =>
            GetMethodCollection(new MethodCollections.AllMethods());

        #endregion

        #region Methods

        /// <summary>
        /// Returns a thread-safe function view.
        /// </summary>
        /// <typeparam name="TPredicate">The type of the predicate to apply.</typeparam>
        /// <param name="predicate">The predicate to apply.</param>
        /// <returns>The resolved function view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodCollection GetMethodCollection<TPredicate>(TPredicate predicate)
            where TPredicate : struct, IMethodCollectionPredicate
        {
            // Synchronize all accesses below using a read scope
            using var readScope = irLock.EnterReadScope();

            return GetMethodCollection_Sync(predicate);
        }

        /// <summary>
        /// Returns a thread-safe function view.
        /// </summary>
        /// <typeparam name="TPredicate">The type of the predicate to apply.</typeparam>
        /// <param name="predicate">The predicate to apply.</param>
        /// <returns>The resolved function view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MethodCollection GetMethodCollection_Sync<TPredicate>(
            TPredicate predicate)
            where TPredicate : struct, IMethodCollectionPredicate
        {
            var builder = ImmutableArray.CreateBuilder<Method>(
                methods.Count);
            foreach (var function in methods)
            {
                if (predicate.Match(function))
                    builder.Add(function);
            }
            return new MethodCollection(
                this,
                builder.Count == methods.Count
                ? builder.MoveToImmutable()
                : builder.ToImmutable());
        }

        /// <summary>
        /// Tries to resolve the given managed method to function reference.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="handle">The resolved function reference (if any).</param>
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetMethodHandle(
            MethodBase method,
            [NotNullWhen(true)] out MethodHandle? handle)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            // Synchronize all accesses below using a read scope
            using var readScope = irLock.EnterUpgradeableReadScope();

            return methods.TryGetHandle(method, out handle);
        }

        /// <summary>
        /// Tries to resolve the given handle to a top-level function.
        /// </summary>
        /// <param name="handle">The function handle to resolve.</param>
        /// <param name="function">The resolved function (if any).</param>
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetMethod(
            MethodHandle handle,
            [NotNullWhen(true)] out Method? function)
        {
            if (handle.IsEmpty)
            {
                function = null;
                return false;
            }

            // Synchronize all accesses below using a read scope
            using var readScope = irLock.EnterUpgradeableReadScope();

            return methods.TryGetData(handle, out function);
        }

        /// <summary>
        /// Tries to resolve the given method to a top-level function.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="function">The resolved function (if any).</param>
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetMethod(
            MethodBase method,
            [NotNullWhen(true)] out Method? function)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            // Synchronize all accesses below using a read scope
            using var readScope = irLock.EnterUpgradeableReadScope();

            function = null;
            return methods.TryGetHandle(method, out MethodHandle? handle)
                && methods.TryGetData(handle.Value, out function);
        }

        /// <summary>
        /// Resolves the given method to a top-level function.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <returns>The resolved function.</returns>
        public Method GetMethod(MethodHandle method) =>
            TryGetMethod(method, out Method? function)
            ? function
            : throw new InvalidOperationException(string.Format(
                ErrorMessages.CouldNotFindCorrespondingIRMethod,
                method.Name));

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="methodBase">The method to declare.</param>
        /// <param name="created">True, if the method has been created.</param>
        /// <returns>The declared method.</returns>
        public Method Declare(MethodBase methodBase, out bool created)
        {
            Debug.Assert(methodBase != null, "Invalid method base");

            // Synchronize all accesses below using a read scope
            using var readScope = irLock.EnterUpgradeableReadScope();

            // Check for existing method
            if (methods.TryGetHandle(methodBase, out MethodHandle? handle))
            {
                created = false;
                return methods[handle.Value];
            }

            var externalAttribute = methodBase.GetCustomAttribute<ExternalAttribute>();
            var methodName = externalAttribute?.Name ?? methodBase.Name;
            handle = MethodHandle.Create(methodName);
            var declaration = new MethodDeclaration(
                handle.Value,
                CreateType(methodBase.GetReturnType()),
                methodBase);

            // Declare a new method sync using a write scope
            using var writeScope = readScope.EnterWriteScope();
            return Declare_Sync(declaration, out created);
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

            // Declare a new method sync using a write scope
            using var writeScope = irLock.EnterWriteScope();

            return Declare_Sync(declaration, out created);
        }

        /// <summary>
        /// Declares a method based on the given declaration (sync).
        /// </summary>
        /// <param name="declaration">The method declaration.</param>
        /// <param name="created">True, if the method has been created.</param>
        /// <returns>The declared method.</returns>
        private Method Declare_Sync(in MethodDeclaration declaration, out bool created)
        {
            Debug.Assert(declaration.ReturnType != null, "Invalid return type");

            created = false;
            if (methods.TryGetData(declaration.Handle, out Method? method))
                return method;

            created = true;
            method = DeclareNewMethod_Sync(declaration, out var handle);
            methods.Register(handle, method);
            return method;
        }

        /// <summary>
        /// Declares a new method (sync).
        /// </summary>
        /// <param name="declaration">The method declaration to use.</param>
        /// <param name="handle">The created handle.</param>
        /// <returns>The declared method.</returns>
        /// <remarks>
        /// Helper method for <see cref="Declare_Sync(in MethodDeclaration, out bool)"/>.
        /// </remarks>
        private Method DeclareNewMethod_Sync(
            MethodDeclaration declaration,
            out MethodHandle handle)
        {
            var methodId = Context.CreateMethodHandle();
            var methodName = declaration.Handle.Name ??
                (declaration.HasSource ? declaration.Source.AsNotNull().Name : "Func");
            handle = new MethodHandle(methodId, methodName);
            var specializedDeclaration = declaration.Specialize(handle);

            // TODO: we might want to extend the method location information to point to
            // the location of the first instruction instead
            var method = new Method(
                this,
                specializedDeclaration,
                Location.Unknown);

            // Check for external and intrinsic functions
            if (declaration.HasFlags(MethodFlags.External | MethodFlags.Intrinsic))
                SealMethodWithoutImplementation(method);
            return method;
        }

        /// <summary>
        /// Seals intrinsic or external methods.
        /// </summary>
        /// <param name="method">The method to seal.</param>
        private static void SealMethodWithoutImplementation(Method method)
        {
            using var builder = method.CreateBuilder();
            var bbBuilder = builder.EntryBlockBuilder;

            // Attach all method parameters
            if (method.HasSource)
            {
                var parameters = method.Source.GetParameters();
                foreach (var parameter in parameters)
                {
                    var paramType = bbBuilder.CreateType(parameter.ParameterType);
                    builder.AddParameter(paramType, parameter.Name);
                }
            }

            // "Seal" the current method
            var returnValue = bbBuilder.CreateNull(
                method.Location,
                method.ReturnType);
            bbBuilder.CreateReturn(method.Location, returnValue);
            builder.Complete();
        }

        /// <summary>
        /// Imports the given method (and all dependencies) into this context.
        /// </summary>
        /// <param name="source">The method to import.</param>
        /// <returns>The imported method.</returns>
        /// <remarks>
        /// CAUTION: This method can cause deadlocks if improperly used. The import
        /// function needs to acquire write access to the current context and needs
        /// to request safe read access from the source context. This can lead to
        /// unintended deadlocks.
        /// </remarks>
        public Method Import(Method source)
        {
            if (source is null)
                throw source.GetArgumentException(nameof(source));

            // Determine the actual source context reference
            var sourceContext = (source.BaseContext as IRContext).AsNotNull();
            if (sourceContext == this)
                throw source.GetInvalidOperationException();

            // Synchronize all accesses below using a read scope
            using var readScope = irLock.EnterUpgradeableReadScope();

            // Check for the requested method handle
            if (methods.TryGetData(source.Handle, out Method? method))
                return method;

            // CAUTION: we have to acquire the current irLock in write mode and have
            // to ensure a read-only access on the other context to avoid cross-thread
            // manipulations of the IR!
            using var otherReadScope = sourceContext.irLock.EnterReadScope();
            using var writeScope = readScope.EnterWriteScope();
            return Import_Sync(source);
        }

        /// <summary>
        /// Imports the given method (and all dependencies) into this context.
        /// </summary>
        /// <param name="source">The method to import.</param>
        /// <returns>The imported method.</returns>
        private Method Import_Sync(Method source)
        {
            var allReferences = References.CreateRecursive(
                source.Blocks,
                new MethodCollections.AllMethods());

            // Declare all functions and build mapping
            var methodsToRebuild = new List<Method>();
            var targetMapping = new Dictionary<Method, Method>();
            foreach (var entry in allReferences)
            {
                var declared = Declare_Sync(entry.Declaration, out bool created);
                targetMapping.Add(entry, declared);
                if (created)
                    methodsToRebuild.Add(entry);
            }

            // Rebuild all functions while using the created mapping
            var methodMapping = new Method.MethodMapping(targetMapping);
            foreach (var sourceMethod in methodsToRebuild)
            {
                var targetMethod = methodMapping[sourceMethod];
                if (sourceMethod.HasImplementation)
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
                    builder.Complete();
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
        public void Transform(in Transformer transformer)
        {
            // Get the current transformation
            var configuration = transformer.Configuration;

            // Synchronize all accesses below using a write scope
            using var writeScope = irLock.EnterWriteScope();

            var toTransform = GetMethodCollection_Sync(configuration.Predicate);
            if (toTransform.Count < 1)
                return;

            // Apply all transformations
            foreach (var transform in transformer.Transformations)
            {
                transform.Transform(toTransform);
                Verifier.Verify(toTransform);
            }

            // Apply final flags
            var transformationFlags = transformer.Configuration.TransformationFlags;
            foreach (var entry in toTransform)
                entry.AddTransformationFlags(transformationFlags);

            // Cleanup the IR
            Parallel.ForEach(toTransform, gcDelegate);
        }

        /// <summary>
        /// Dumps the IR context to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public void Dump(TextWriter textWriter)
        {
            foreach (var method in Methods)
            {
                method.Dump(textWriter);
                textWriter.WriteLine();
                textWriter.WriteLine("------------------------------");
            }
        }

        #endregion

        #region Cache

        /// <summary>
        /// Clears cached IR nodes.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public void ClearCache(ClearCacheMode mode)
        {
            // Synchronize all accesses below using a write scope
            using var writeScope = irLock.EnterWriteScope();

            methods.Clear();
        }

        #endregion

        #region IExportable

        /// <summary>
        /// Exports this context to a portable representation.
        /// </summary>
        /// <returns>
        /// The exported context as an immutable type
        /// </returns>
        public Exported Export()
        {
            var exportedMethods = new HashSet<IRMethod>();
            var exportedValues = new HashSet<IRValue>();
            var exportedTypes = new HashSet<IRType>();

            var allMethods = GetMethodCollection(new MethodCollections.AllMethods());
            foreach (var method in allMethods)
            {
                exportedMethods.Add(method.Export());
                exportedTypes.UnionWith(method.ReturnType.Export());

                foreach (var param in method.Parameters)
                {
                    exportedValues.Add(param.Export());
                    exportedTypes.UnionWith(param.Type.Export());
                }

                foreach (var entry in method.Values)
                {
                    exportedValues.Add(entry.Value.Export());
                    exportedTypes.UnionWith(entry.Value.Type.Export());
                }
            }

            return new Exported(
                exportedMethods.ToImmutableArray(),
                exportedValues.ToImmutableArray(),
                exportedTypes.ToImmutableArray()
                );
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
