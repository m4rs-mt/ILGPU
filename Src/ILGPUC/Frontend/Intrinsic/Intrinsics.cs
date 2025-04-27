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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ILGPU.Intrinsic;
using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.IR.Values;

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
    public const BindingFlags RemapBindingFlags = DefaultBindingFlags;
    public const BindingFlags GeneratorBindingFlags = DefaultBindingFlags;

    #endregion

    #region Nested Types

    /// <summary>
    /// Method lookup helper class to retrieve methods by name and number of arguments.
    /// </summary>
    readonly struct MethodLookup
    {
        private readonly Dictionary<string, List<MethodBase>> _lookup;
        private readonly Dictionary<(string, int), List<MethodBase>> _paramLookup;

        /// <summary>
        /// Creates a new method lookup for the given type.
        /// </summary>
        /// <param name="type">The type to create the lookup for.</param>
        /// <param name="bindingFlags">Binding flags to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public MethodLookup(Type type, BindingFlags bindingFlags)
        {
            var methods = type.GetMethods(bindingFlags);
            var constructors = type.GetConstructors(bindingFlags);
            var combined = methods.Cast<MethodBase>()
                .Concat(constructors.Cast<MethodBase>())
                .ToArray();
            _lookup = new(combined.Length);
            _paramLookup = new(combined.Length);
            foreach (var method in combined)
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

        public IReadOnlyList<MethodBase> this[string name] => _lookup[name];
        public IReadOnlyList<MethodBase> this[string name, int numArguments] =>
            _paramLookup[(name, numArguments)];
    }

    /// <summary>
    /// Implements a backend-mapping allowing for efficient lookups of backend methods.
    /// </summary>
    /// <typeparam name="T">The mapping type.</typeparam>
    /// <param name="ptx">The PTX backend implementation.</param>
    readonly struct BackendMap<T>(T ptx)
    {
        private readonly T[] _data = [ptx];

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
    /// Triggers implicit initialization.
    /// </summary>
    public static void Init() { }

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
