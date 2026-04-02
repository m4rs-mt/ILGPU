// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelRegistry.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Tests.Kernels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Auto-discovers all kernel methods from the Kernels/ directory.
/// Generic methods are expanded with representative type arguments.
/// All data is string-based for xUnit serialization compatibility.
/// </summary>
static class KernelRegistry
{
    private static readonly Type[] IntegerTypeArgs = [typeof(int), typeof(long)];
    private static readonly Type[] FloatTypeArgs = [typeof(float), typeof(double)];

    /// <summary>
    /// Maps declaring class names to the type arguments used for generic expansion.
    /// Classes not listed here but containing generic methods will be skipped.
    /// </summary>
    private static readonly Dictionary<string, Type[]> GenericExpansion = new()
    {
        ["BinaryIntOpKernels"] = IntegerTypeArgs,
        ["UnaryIntOpKernels"] = IntegerTypeArgs,
        ["CompareIntKernels"] = IntegerTypeArgs,
        ["CompareFloatKernels"] = FloatTypeArgs,
    };

    private static readonly Dictionary<string, BackendCapability> s_capabilities = [];
    private static readonly Dictionary<string, MethodInfo> s_kernels = Discover();

    /// <summary>
    /// All kernel names as single-element object[] arrays for [MemberData].
    /// </summary>
    public static IEnumerable<object[]> AllKernelNames =>
        s_kernels.Keys.OrderBy(k => k).Select(k => new object[] { k });

    /// <summary>
    /// Resolves a kernel name to its concrete MethodInfo.
    /// </summary>
    public static MethodInfo Resolve(string name) =>
        s_kernels.TryGetValue(name, out var method)
            ? method
            : throw new ArgumentException($"Unknown kernel: '{name}'");

    /// <summary>
    /// Returns the inferred backend capabilities required by the given kernel.
    /// </summary>
    public static BackendCapability GetRequiredCapabilities(string name) =>
        s_capabilities.TryGetValue(name, out var caps)
            ? caps
            : BackendCapability.None;

    /// <summary>
    /// Total number of discovered kernels.
    /// </summary>
    public static int Count => s_kernels.Count;

    private static Dictionary<string, MethodInfo> Discover()
    {
        var results = new Dictionary<string, MethodInfo>();
        var assembly = typeof(BasicIfKernels).Assembly;

        var kernelTypes = assembly.GetTypes()
            .Where(t =>
                t.Namespace == "ILGPUC.Tests.Kernels"
                && t.Name.EndsWith("Kernels")
                && t.IsAbstract && t.IsSealed) // static class
            .OrderBy(t => t.Name);

        foreach (var type in kernelTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(m => m.Name);

            foreach (var method in methods)
            {
                if (method.IsGenericMethodDefinition)
                    ExpandGeneric(results, type, method);
                else
                {
                    var name = $"{type.Name}.{method.Name}";
                    results[name] = method;
                    s_capabilities[name] = InferCapabilities(method);
                }
            }
        }

        return results;
    }

    private static void ExpandGeneric(
        Dictionary<string, MethodInfo> results,
        Type declaringType,
        MethodInfo genericMethod)
    {
        if (!GenericExpansion.TryGetValue(declaringType.Name, out var typeArgs))
            return;

        foreach (var ta in typeArgs)
        {
            try
            {
                var concrete = genericMethod.MakeGenericMethod(ta);
                var name = $"{declaringType.Name}.{genericMethod.Name}<{ta.Name}>";
                results[name] = concrete;
                s_capabilities[name] = InferCapabilities(concrete);
            }
            catch (ArgumentException)
            {
                // Type argument doesn't satisfy constraints — skip
            }
        }
    }

    /// <summary>
    /// Infers required backend capabilities from a kernel method's signature.
    /// Checks parameter types, generic type arguments, and return type for
    /// types that require specific hardware support.
    /// </summary>
    private static BackendCapability InferCapabilities(MethodInfo method)
    {
        var caps = BackendCapability.None;

        // Check parameter types
        foreach (var param in method.GetParameters())
        {
            caps |= InferFromType(param.ParameterType);
        }

        // Check generic type arguments
        if (method.IsGenericMethod)
        {
            foreach (var ta in method.GetGenericArguments())
                caps |= InferFromType(ta);
        }

        // Check return type
        caps |= InferFromType(method.ReturnType);

        return caps;
    }

    /// <summary>
    /// Checks whether a type (or its generic arguments) requires specific capabilities.
    /// </summary>
    private static BackendCapability InferFromType(Type type)
    {
        if (type == typeof(double))
            return BackendCapability.Float64;
        if (type == typeof(Half))
            return BackendCapability.Float16;

        // Check generic type arguments (e.g., ArrayView1D<double, ...>)
        if (type.IsGenericType)
        {
            var caps = BackendCapability.None;
            foreach (var ga in type.GetGenericArguments())
                caps |= InferFromType(ga);
            return caps;
        }

        return BackendCapability.None;
    }
}
