// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.CodeGeneration;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.Construction;
using ILGPUC.IR.Transformations;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Values.ValueReference>;

namespace ILGPUC.IR;

/// <summary>
/// An abstract base IR context.
/// </summary>
abstract partial class IRBaseContext : DisposeBase
{
    #region Instance

    /// <summary>
    /// Constructs a new IR context.
    /// </summary>
    /// <param name="properties">The associated properties.</param>
    /// <param name="typeContext">The type context.</param>
    protected IRBaseContext(CompilationProperties properties, IRTypeContext typeContext)
    {
        Properties = properties;
        TypeContext = typeContext;

        UndefinedValue = new UndefinedValue(
            new ValueInitializer(
                this,
                null,
                Location.Nowhere));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the main context properties.
    /// </summary>
    public CompilationProperties Properties { get; }

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
/// <remarks>
/// Constructs a new IR context.
/// </remarks>
/// <param name="properties">The associated properties.</param>
/// <param name="typeContext">The type context.</param>
sealed class IRContext(CompilationProperties properties, IRTypeContext typeContext) :
    IRBaseContext(properties, typeContext), IDumpable
{
    #region Static

    /// <summary>
    /// Represents the current IR version.
    /// </summary>
    public static readonly Version IRVersion = new(1, 0, 0);

    private static int _methodHandleCounter;

    #endregion

    #region Instance

    private readonly ReaderWriterLockSlim _irLock = new(
        LockRecursionPolicy.SupportsRecursion);
    private readonly MethodMapping<Method> _methods = new();

    #endregion

    #region Properties

    /// <summary>
    /// Returns all top-level functions.
    /// </summary>
    public MethodCollection Methods => GetMethodCollection();

    /// <summary>
    /// Returns the arithmetic type of a native pointer.
    /// </summary>
    public ArithmeticBasicValueType PointerArithmeticType { get; } =
        properties.TargetPlatform.Is64Bit()
        ? ArithmeticBasicValueType.UInt64
        : ArithmeticBasicValueType.UInt32;

    /// <summary>
    /// Returns the basic type of a native pointer.
    /// </summary>
    public BasicValueType PointerBasicValueType => PointerType.BasicValueType;

    /// <summary>
    /// Returns the type of a native pointer.
    /// </summary>
    public PrimitiveType PointerType { get; } =
        typeContext.GetPrimitiveType(
            properties.TargetPlatform.Is64Bit()
            ? BasicValueType.Int64
            : BasicValueType.Int32);

    /// <summary>
    /// Returns the pointer size of a native pointer type.
    /// </summary>
    public int PointerSize => PointerType.Size;

    #endregion

    #region Methods

    /// <summary>
    /// Returns a thread-safe function view.
    /// </summary>
    /// <param name="predicate">The predicate to apply.</param>
    /// <returns>The resolved function view.</returns>
    public MethodCollection GetMethodCollection(Predicate<Method>? predicate = null)
    {
        // Synchronize all accesses below using a read scope
        using var readScope = _irLock.EnterReadScope();

        return GetMethodCollection_Sync(predicate);
    }

    /// <summary>
    /// Returns a thread-safe function view.
    /// </summary>
    /// <param name="predicate">The predicate to apply.</param>
    /// <returns>The resolved function view.</returns>
    private MethodCollection GetMethodCollection_Sync(
        Predicate<Method>? predicate = null)
    {
        var builder = ImmutableArray.CreateBuilder<Method>(
            _methods.Count);
        foreach (var function in _methods)
        {
            if (predicate is null || predicate(function))
                builder.Add(function);
        }
        return new MethodCollection(
            this,
            builder.Count == _methods.Count
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
        using var readScope = _irLock.EnterUpgradeableReadScope();

        return _methods.TryGetHandle(method, out handle);
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
        using var readScope = _irLock.EnterUpgradeableReadScope();

        return _methods.TryGetData(handle, out function);
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
        using var readScope = _irLock.EnterUpgradeableReadScope();

        function = null;
        return _methods.TryGetHandle(method, out MethodHandle? handle)
            && _methods.TryGetData(handle.Value, out function);
    }

    /// <summary>
    /// Resolves the given method to a top-level function.
    /// </summary>
    /// <param name="method">The method to resolve.</param>
    /// <returns>The resolved function.</returns>
    public Method GetMethod(MethodBase method) =>
        TryGetMethod(method, out Method? function)
        ? function
        : throw new InvalidOperationException(string.Format(
            ErrorMessages.CouldNotFindCorrespondingIRMethod,
            method.Name));

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
        using var readScope = _irLock.EnterUpgradeableReadScope();

        // Check for existing method
        if (_methods.TryGetHandle(methodBase, out MethodHandle? handle))
        {
            created = false;
            return _methods[handle.Value];
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
        using var writeScope = _irLock.EnterWriteScope();

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
        if (_methods.TryGetData(declaration.Handle, out Method? method))
            return method;

        created = true;
        method = DeclareNewMethod_Sync(declaration, out var handle);
        _methods.Register(handle, method);
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
        var methodId = Interlocked.Add(ref _methodHandleCounter, 1);
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
        using var readScope = _irLock.EnterUpgradeableReadScope();

        // Check for the requested method handle
        if (_methods.TryGetData(source.Handle, out Method? method))
            return method;

        // CAUTION: we have to acquire the current irLock in write mode and have
        // to ensure a read-only access on the other context to avoid cross-thread
        // manipulations of the IR!
        using var otherReadScope = sourceContext._irLock.EnterReadScope();
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
        var allReferences = References.CreateRecursive(source.Blocks);

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
        }

        return targetMapping[source];
    }

    /// <summary>
    /// Applies the given transformer to the current context.
    /// </summary>
    /// <param name="transformer">The target transformer.</param>
    public void Transform(in Transformer transformer)
    {
        // Synchronize all accesses below using a write scope
        using var writeScope = _irLock.EnterWriteScope();

        var toTransform = GetMethodCollection_Sync();
        if (toTransform.Count < 1)
            return;

        // Apply all transformations
        foreach (var transform in transformer.Transformations)
            transform.Transform(toTransform);
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

    #region IDisposable

    /// <summary cref="DisposeBase.Dispose(bool)"/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _irLock.Dispose();
        base.Dispose(disposing);
    }

    #endregion
}
