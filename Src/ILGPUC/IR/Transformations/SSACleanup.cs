// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SSACleanup.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Performs SSA cleanup by removing trivial phi values that were created during
/// SSA construction but can be eliminated.
/// </summary>
/// <remarks>
/// This transformation implements the TryRemoveTrivialPhi algorithm from the paper:
/// "Simple and Efficient Construction of Static Single Assignment Form"
///
/// A phi value is trivial if all of its operands are the same (ignoring the phi
/// itself). This situation commonly arises during SSA construction and these phi
/// values can be safely eliminated by replacing them with the common operand.
/// Irreducible control flow additionally creates strongly connected components
/// of phis that only reference each other and a single outer value. These are
/// redundant as a whole and are replaced by the outer value (see section 3.2 of
/// the paper).
/// </remarks>
/// <param name="args">The transformation args.</param>
sealed class SSACleanup(TransformationArgs args) :
    Transformation<ValueMap<Method, PhiValue, Value>>(args)
{
    /// <summary>
    /// Computes sets of trivial phi values.
    /// </summary>
    protected override ValueMap<Method, PhiValue, Value> CreateIntermediate(
        ModuleTransform transform,
        Method method)
    {
        var trivialPhis = method.CreateMap<PhiValue, Value>();

        // Process all phi values in the current method
        var phis = new List<PhiValue>();
        method.ForEachValue<PhiValue>(phiValue =>
        {
            phis.Add(phiValue);
            if (!trivialPhis.ContainsKey(phiValue))
                TryDetectTrivialPhi(phiValue, trivialPhis);
        });

        // Remove redundant phi components that remain after trivial phi removal
        RemoveRedundantPhiComponents(phis, trivialPhis);

        // Store the set of trivial phis for this method
        return trivialPhis;
    }

    /// <summary>
    /// Tries to detect a trivial phi value and recursively processes dependent phis.
    /// </summary>
    /// <param name="phiValue">The phi value to check.</param>
    /// <param name="trivialPhis">The set of trivial phis to track.</param>
    /// <returns>
    /// The value to use instead of the phi (or the phi itself if not trivial).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void TryDetectTrivialPhi(
        PhiValue phiValue,
        ValueMap<Method, PhiValue, Value> trivialPhis)
    {
        // Implements the SSA-construction algorithm from the paper:
        // Simple and Efficient Construction of Static Single Assignment Form

        Value? same = null;
        foreach (Value argument in phiValue.Arguments)
        {
            // Skip self-references and duplicates
            if (same == argument || argument == phiValue)
                continue;
            // If we find two different values, this phi is not trivial
            if (same != null)
                return;
            same = argument;
        }

        // Unreachable phi (no non-self operands) - not trivial
        if (same is null)
            return;

        // A replacement that resolves back to this phi would create a cycle of
        // replacements (e.g. two phis that only reference each other)
        if (Resolve(same, trivialPhis) == phiValue)
            return;

        // This phi is trivial - mark it for removal and recursively check uses
        if (!trivialPhis.Add(phiValue, same))
            return;

        // Recursively check all uses to see if they become trivial
        foreach (var use in phiValue.Uses)
        {
            if (use.Target is PhiValue usedPhi && !trivialPhis.ContainsKey(usedPhi))
                TryDetectTrivialPhi(usedPhi, trivialPhis);
        }
    }

    /// <summary>
    /// Resolves the final replacement of the given value by following the
    /// replacements of trivial phis.
    /// </summary>
    private static Value Resolve(
        Value value,
        ValueMap<Method, PhiValue, Value> trivialPhis)
    {
        while (value is PhiValue phi && trivialPhis.TryGetValue(phi, out var next))
            value = next;
        return value;
    }

    /// <summary>
    /// Finds all strongly connected components of the remaining phis, in which
    /// every phi only references other phis of the same component and a single
    /// outer value, and replaces all phis of such components with that value.
    /// </summary>
    /// <param name="phis">All phi values of the method.</param>
    /// <param name="trivialPhis">The replacements of trivial phis.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void RemoveRedundantPhiComponents(
        List<PhiValue> phis,
        ValueMap<Method, PhiValue, Value> trivialPhis)
    {
        // Iterative Tarjan over the graph of phis and their phi arguments.
        // Components are completed in reverse topological order, so the
        // outer values of a component are fully resolved when it is visited.
        var indices = new Dictionary<PhiValue, int>();
        var lowLinks = new Dictionary<PhiValue, int>();
        var stack = new Stack<PhiValue>();
        var onStack = new HashSet<PhiValue>();
        var work = new Stack<(PhiValue Phi, int ArgIndex)>();
        int index = 0;

        PhiValue? GetPhiArgument(PhiValue phi, int argIndex) =>
            Resolve(phi.Arguments[argIndex], trivialPhis) is PhiValue argPhi
                ? argPhi
                : null;

        foreach (var root in phis)
        {
            if (trivialPhis.ContainsKey(root) || indices.ContainsKey(root))
                continue;

            indices[root] = lowLinks[root] = index++;
            stack.Push(root);
            onStack.Add(root);
            work.Push((root, 0));

            while (work.Count > 0)
            {
                var (phi, argIndex) = work.Pop();
                if (argIndex < phi.NumArguments)
                {
                    work.Push((phi, argIndex + 1));
                    var target = GetPhiArgument(phi, argIndex);
                    if (target is null)
                        continue;
                    if (!indices.TryGetValue(target, out var targetIndex))
                    {
                        indices[target] = lowLinks[target] = index++;
                        stack.Push(target);
                        onStack.Add(target);
                        work.Push((target, 0));
                    }
                    else if (onStack.Contains(target))
                    {
                        lowLinks[phi] = Math.Min(lowLinks[phi], targetIndex);
                    }
                    continue;
                }

                // All arguments visited: propagate the low link to the parent
                if (work.Count > 0)
                {
                    var parent = work.Peek().Phi;
                    lowLinks[parent] = Math.Min(lowLinks[parent], lowLinks[phi]);
                }

                if (lowLinks[phi] != indices[phi])
                    continue;

                // Pop the component rooted at this phi
                var component = new HashSet<PhiValue>();
                PhiValue member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    component.Add(member);
                }
                while (member != phi);

                ReplaceRedundantComponent(component, trivialPhis);
            }
        }
    }

    /// <summary>
    /// Replaces all phis of the given component with its single outer value,
    /// if the component references exactly one value outside of itself.
    /// </summary>
    private static void ReplaceRedundantComponent(
        HashSet<PhiValue> component,
        ValueMap<Method, PhiValue, Value> trivialPhis)
    {
        // Single phis have already been checked by the trivial phi detection
        if (component.Count < 2)
            return;

        Value? outer = null;
        foreach (var phi in component)
        {
            foreach (var argument in phi.Arguments)
            {
                var resolved = Resolve(argument, trivialPhis);
                if (resolved is PhiValue argPhi && component.Contains(argPhi))
                    continue;
                if (outer is not null && outer != resolved)
                    return;
                outer = resolved;
            }
        }

        // Components without any outer value are left unchanged
        if (outer is null)
            return;

        foreach (var phi in component)
            trivialPhis.Add(phi, outer);
    }

    /// <summary>
    /// Maps trivial phi values to their replacement values.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        MapBasicBlockValue<PhiValue>((transform, phi) =>
        {
            var trivialPhis = GetIntermediate(transform.OldMethod);
            return trivialPhis.TryGetValue(phi, out var same) ? same : phi;
        });

        base.OnMap(transform);
    }
}
