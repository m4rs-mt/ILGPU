// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Intrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

// disable: max_line_length

namespace ILGPUC.Frontend.Intrinsic;

/// <summary>
/// Contains default ILGPU intrinsics.
/// </summary>
static unsafe partial class Intrinsics
{
    #region Constants

    const BindingFlags DefaultBindingFlags =
        BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags RemapBindingFlags = DefaultBindingFlags;
    const BindingFlags GeneratorBindingFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    #endregion

    #region Nested Types

    /// <summary>
    /// Method lookup helper class to retrieve methods by name and number of arguments.
    /// </summary>
    readonly struct MethodLookup
    {
        private readonly Dictionary<string, List<MethodInfo>> _lookup;
        private readonly Dictionary<(string, int), List<MethodInfo>> _paramLookup;

        public MethodLookup(Type type, BindingFlags bindingFlags)
        {
            var methods = type.GetMethods(bindingFlags);
            _lookup = new(methods.Length);
            _paramLookup = new(methods.Length);
            foreach (var method in methods)
            {
                if (_lookup.TryGetValue(method.Name, out var value))
                    value.Add(method);
                else
                    _lookup.Add(method.Name, [method]);

                var paramKey = (method.Name, method.GetParameters().Length);
                if (_paramLookup.TryGetValue(paramKey, out value))
                    value.Add(method);
                else
                    _paramLookup.Add(paramKey, [method]);
            }
        }

        public IReadOnlyList<MethodInfo> this[string name] => _lookup[name];
        public IReadOnlyList<MethodInfo> this[string name, int numArguments] =>
            _paramLookup[(name, numArguments)];
    }

    /// <summary>
    /// Implements a backend-mapping allowing for efficient lookups of backend methods.
    /// </summary>
    /// <typeparam name="T">The mapping type.</typeparam>
    /// <param name="il">The IL backend implementation.</param>
    /// <param name="ptx">The PTX backend implementation.</param>
    /// <param name="openCL">The OpenCL backend implementation.</param>
    readonly struct BackendMap<T>(T il, T ptx, T openCL)
    {
        private readonly T[] _data = [il, default!, ptx, openCL];

        public T this[BackendType backendType] => _data[(int)backendType];
    }

    #endregion

    #region Intrinsic Generators

    /// <summary>
    /// Intrinsic generator delegate to implement intrinsic operations on the IR level.
    /// </summary>
    /// <param name="context">The current context.</param>
    /// <returns>The value reference return.</returns>
    delegate ValueReference IntrinsicGenerator(ref InvocationContext context);

    /// <summary>
    /// An internal intrinsic generator wrapper to handle generic arguments.
    /// </summary>
    delegate ValueReference IntrinsicGenerator<T>(
        ref InvocationContext context,
        T argument);

    /// <summary>
    /// An internal intrinsic generator wrapper to handle generic arguments.
    /// </summary>
    delegate ValueReference IntrinsicGenerator<T1, T2>(
        ref InvocationContext context,
        T1 argument1,
        T2 argument2);

    /// <summary>
    /// An intrinsic wrapper to handle untyped arguments.
    /// </summary>
    delegate ValueReference IntrinsicGeneratorWrapperHandler1(
        ref InvocationContext context,
        object argument);

    /// <summary>
    /// An intrinsic wrapper to handle untyped arguments.
    /// </summary>
    delegate ValueReference IntrinsicGeneratorWrapperHandler2(
        ref InvocationContext context,
        object argument1,
        object argument2);

    /// <summary>
    /// An intrinsic wrapper implementation to handle untyped arguments.
    /// </summary>
    static ValueReference IntrinsicGeneratorWrapper1<T>(
        IntrinsicGenerator<T> generator,
        ref InvocationContext context,
        object value) =>
        generator(ref context, (T)value);

    /// <summary>
    /// An intrinsic wrapper implementation to handle untyped arguments.
    /// </summary>
    static ValueReference IntrinsicGeneratorWrapper2<T1, T2>(
        IntrinsicGenerator<T1, T2> generator,
        ref InvocationContext context,
        object value1,
        object value2) =>
        generator(ref context, (T1)value2, (T2)value2);

    /// <summary>
    /// Holds a static reference to a generic intrinsic generator type.
    /// </summary>
    static readonly Type GenericIntrinsicGeneratorType1 =
        typeof(IntrinsicGenerator<>);

    /// <summary>
    /// Holds a reference to an intrinsic wrapper handler implementation.
    /// </summary>
    static readonly MethodInfo GenericIntrinsicGeneratorWrapperMethod1 =
        typeof(Intrinsics)
        .GetMethod("IntrinsicGeneratorWrapper1`1", GeneratorBindingFlags)
        .ThrowIfNull();

    /// <summary>
    /// Holds a static reference to a generic intrinsic generator type.
    /// </summary>
    static readonly Type GenericIntrinsicGeneratorType2 =
        typeof(IntrinsicGenerator<,>);

    /// <summary>
    /// Holds a reference to an intrinsic wrapper handle implementation..
    /// </summary>
    static readonly MethodInfo GenericIntrinsicGeneratorWrapperMethod2 =
        typeof(Intrinsics)
        .GetMethod("IntrinsicGeneratorWrapper2`2", GeneratorBindingFlags)
        .ThrowIfNull();

    /// <summary>
    /// Gets an intrinsic generator for the given implementation type and name.
    /// </summary>
    static IntrinsicGenerator GetIntrinsicGenerator(Type type, string name)
    {
        var methodInfo = type.GetMethod(name, GeneratorBindingFlags).ThrowIfNull();
        return methodInfo.CreateDelegate<IntrinsicGenerator>();
    }

    /// <summary>
    /// Gets an intrinsic generator for the given implementation type and name accepting
    /// an additional argument.
    /// </summary>
    static IntrinsicGenerator GetIntrinsicGenerator(
        Type type,
        string name,
        object argument)
    {
        var argumentType = argument.GetType();
        var wrapperMethod = GenericIntrinsicGeneratorWrapperMethod1
            .MakeGenericMethod(argumentType);
        var wrapperDelegateType = typeof(IntrinsicGeneratorWrapperHandler1);
        var targetDelegateType = GenericIntrinsicGeneratorType1
            .MakeGenericType(argumentType);

        // Prepare target generator
        var methodInfo = type.GetMethod(name, GeneratorBindingFlags).ThrowIfNull();
        var generatorMethod = methodInfo.CreateDelegate(targetDelegateType);

        // Prepare generator wrapper
        var wrapperHandler = wrapperMethod
            .CreateDelegate<IntrinsicGeneratorWrapperHandler1>();

        return (ref InvocationContext context) =>
            wrapperHandler(ref context, argument);
    }

    /// <summary>
    /// Gets an intrinsic generator for the given implementation type and name accepting
    /// two additional arguments.
    /// </summary>
    static IntrinsicGenerator GetIntrinsicGenerator(
        Type type,
        string name,
        object argument1,
        object argument2)
    {
        var argumentType1 = argument1.GetType();
        var argumentType2 = argument2.GetType();
        var wrapperMethod = GenericIntrinsicGeneratorWrapperMethod2
            .MakeGenericMethod(argumentType1, argumentType2);
        var wrapperDelegateType = typeof(IntrinsicGeneratorWrapperHandler2);
        var targetDelegateType = GenericIntrinsicGeneratorType2
            .MakeGenericType(argumentType1, argumentType2);

        // Prepare target generator
        var methodInfo = type.GetMethod(name, GeneratorBindingFlags).ThrowIfNull();
        var generatorMethod = methodInfo.CreateDelegate(targetDelegateType);

        // Prepare generator wrapper
        var wrapperHandler = wrapperMethod
            .CreateDelegate<IntrinsicGeneratorWrapperHandler2>();
        return (ref InvocationContext context) =>
            wrapperHandler(ref context, argument1, argument2);
    }

    #endregion

    /// <summary>
    /// Intrinsics initialization.
    /// </summary>
    static Intrinsics()
    {
        InitializeRemapping();
        InitializeGeneration();
        InitializeBackendImplementations();
    }

    /// <summary>
    /// Returns true if the given method is an ILGPU intrinsic function.
    /// </summary>
    /// <param name="method">The method to test.</param>
    /// <returns>True if the given method is an ILGPU intrinsic function.</returns>
    public static bool IsILGPUIntrinsic(this MethodBase method) =>
        method.GetCustomAttribute<IntrinsicAttribute>() is not null;

    /// <summary>
    /// Tries to remap the given method to an intrinsic implementation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="method">The method to be (potentially) remapped.</param>
    /// <param name="backendType">Current backend type.</param>
    /// <param name="remapped">The remapped method (if any).</param>
    /// <returns>True if the given method could be remapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool TryImplement(
        Location location,
        MethodBase method,
        BackendType backendType,
       [NotNullWhen(true)] out MethodBase? remapped)
    {
        // Determine whether the given method has an intrinsic remapping
        if (_remappings.TryGetValue(method, out remapped))
            return true;

        // Try to get a backend implementation for this method
        if (_backendImplementations.TryGetValue(method, out var map))
        {
            remapped = map[backendType];
            return true;
        }

        // Ignore methods that are not known intrinsics
        if (!method.IsILGPUIntrinsic())
            return false;

        // Sanity check existence of intrinsic handlers
        if (!_generators.ContainsKey(method))
            throw location.GetInvalidOperationException();

        return false;
    }

    /// <summary>
    /// Tries to generate code for a specific invocation context. This method
    /// can generate custom code instead of default method-invocation flow.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="result">The resulting value of the intrinsic call.</param>
    /// <returns>True if this call could handle the call.</returns>
    public static bool TryGenerateCode(
        ref InvocationContext context,
        out ValueReference result)
    {
        result = default;
        var method = context.Method;

        // Remappings are not supported by this method and need to be handled upfront
        if (_remappings.TryGetValue(method, out var _))
            throw context.Location.GetInvalidOperationException();

        // Check for immediate code generators and backend implementations
        if (_generators.TryGetValue(method, out var handler))
        {
            // Implement the intrinsic
            result = handler(ref context);
            return true;
        }

        return false;
    }
}
