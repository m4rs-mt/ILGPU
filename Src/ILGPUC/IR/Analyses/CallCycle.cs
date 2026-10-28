// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CallCycle.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// A cycle of the call graph: methods that call themselves, directly or through
/// each other.
/// </summary>
/// <param name="Methods">
/// The methods of the cycle, in the order of their declarations (see
/// <see cref="Compare"/>), which does not depend on the method a kernel enters the
/// cycle at.
/// </param>
/// <param name="Calls">
/// A call of each method of the cycle from a method of the cycle, in the order of
/// <paramref name="Methods"/>.
/// </param>
sealed record CallCycle(Method[] Methods, MethodCall[] Calls)
{
    /// <summary>
    /// Finds all cycles in the call graph of the given methods. Each cycle is a
    /// strongly connected component of the call graph that holds more than one
    /// method, or a single method calling itself.
    /// </summary>
    /// <param name="methods">
    /// The methods to analyze. Methods without a sealed implementation are
    /// skipped, and so are calls of methods that are not given.
    /// </param>
    /// <returns>The cycles, in the order their first methods were given.</returns>
    public static List<CallCycle> FindAll(IEnumerable<Method> methods)
    {
        // The calls of every method, in the order of its blocks
        var calls = new Dictionary<Method, List<MethodCall>>();
        var order = new Dictionary<Method, int>();
        foreach (var method in methods)
        {
            if (!method.HasImplementation || !method.IsSealed)
                continue;
            var methodCalls = new List<MethodCall>();
            foreach (var block in method.Blocks)
            {
                foreach (var (value, _) in block.BasicBlockValues)
                {
                    if (value is MethodCall call)
                        methodCalls.Add(call);
                }
            }
            calls.Add(method, methodCalls);
            order.Add(method, order.Count);
        }

        // Tarjan's algorithm: a method whose low link equals its own index is the
        // root of a strongly connected component, which holds the methods above it
        // on the stack
        var cycles = new List<CallCycle>();
        var index = new Dictionary<Method, int>();
        var lowLink = new Dictionary<Method, int>();
        var stack = new Stack<Method>();
        var onStack = new HashSet<Method>();
        int nextIndex = 0;

        void Visit(Method method)
        {
            index[method] = lowLink[method] = nextIndex++;
            stack.Push(method);
            onStack.Add(method);

            foreach (var call in calls[method])
            {
                var target = call.Target;
                if (!calls.ContainsKey(target))
                    continue;
                if (!index.TryGetValue(target, out int targetIndex))
                {
                    Visit(target);
                    lowLink[method] = Math.Min(lowLink[method], lowLink[target]);
                }
                else if (onStack.Contains(target))
                {
                    lowLink[method] = Math.Min(lowLink[method], targetIndex);
                }
            }

            if (lowLink[method] != index[method])
                return;

            var component = new List<Method>();
            Method member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != method);

            if (component.Count > 1 || calls[method].Exists(c => c.Target == method))
                cycles.Add(Create(component, calls, order));
        }

        foreach (var method in calls.Keys)
        {
            if (!index.ContainsKey(method))
                Visit(method);
        }

        cycles.Sort((left, right) =>
            order[left.Methods[0]].CompareTo(order[right.Methods[0]]));
        return cycles;
    }

    /// <summary>
    /// Creates the cycle of the given strongly connected component.
    /// </summary>
    /// <param name="component">The methods of the component.</param>
    /// <param name="calls">The calls of all methods.</param>
    /// <param name="order">The order in which all methods were given.</param>
    /// <returns>The created cycle.</returns>
    private static CallCycle Create(
        List<Method> component,
        Dictionary<Method, List<MethodCall>> calls,
        Dictionary<Method, int> order)
    {
        component.Sort((left, right) =>
        {
            int result = Compare(left, right);
            return result != 0 ? result : order[left].CompareTo(order[right]);
        });
        var members = new HashSet<Method>(component);
        var entries = new Dictionary<Method, MethodCall>();
        foreach (var method in component)
        {
            foreach (var call in calls[method])
            {
                if (members.Contains(call.Target))
                    entries.TryAdd(call.Target, call);
            }
        }

        var methods = component.ToArray();
        var cycleCalls = new MethodCall[methods.Length];
        for (int i = 0; i < methods.Length; ++i)
            cycleCalls[i] = entries[methods[i]];
        return new CallCycle(methods, cycleCalls);
    }

    /// <summary>
    /// Compares two methods by their declarations: by module, metadata token and
    /// the type arguments of their instantiations. Every kernel compiles its own
    /// methods, and this order lets the kernels entering a cycle at different
    /// methods report the same cycle in the same way.
    /// </summary>
    /// <param name="left">The first method.</param>
    /// <param name="right">The second method.</param>
    /// <returns>
    /// The relative order of the methods, or zero if their declarations are the
    /// same, such as for specializations of one method.
    /// </returns>
    private static int Compare(Method left, Method right)
    {
        var leftSource = left.Source;
        var rightSource = right.Source;
        if (leftSource is null || rightSource is null)
        {
            int bySource = (leftSource is null).CompareTo(rightSource is null);
            return bySource != 0
                ? bySource
                : string.CompareOrdinal(left.Name, right.Name);
        }

        int result = string.CompareOrdinal(
            leftSource.Module.FullyQualifiedName,
            rightSource.Module.FullyQualifiedName);
        if (result == 0)
            result = leftSource.MetadataToken.CompareTo(rightSource.MetadataToken);
        if (result == 0)
            result = string.CompareOrdinal(
                FormatInstantiation(leftSource),
                FormatInstantiation(rightSource));
        return result;

        // The type arguments of the method and its declaring type
        static string FormatInstantiation(MethodBase method) =>
            string.Join(
                ",",
                (method.DeclaringType?.GetGenericArguments() ?? [])
                    .Concat(method.IsGenericMethod ? method.GetGenericArguments() : [])
                    .Select(type => type.AssemblyQualifiedName ?? type.Name));
    }

    /// <summary>
    /// Returns the names of all methods of this cycle as written in source code,
    /// quoted and separated by commas.
    /// </summary>
    public string FormatMethodNames() =>
        string.Join(
            ", ",
            Methods.Select(method =>
                $"'{method.Source?.GetDisplayName() ?? method.Name}'"));
}
