// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateSpecializations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ILGPUC.Frontend;

/// <summary>
/// A copy of a method specialized for statically known delegate arguments.
/// </summary>
/// <param name="Source">The managed method that is specialized.</param>
/// <param name="Targets">
/// The method bound to each delegate parameter of the source method, indexed
/// by parameter position (null for all other parameters).
/// </param>
/// <param name="Builder">The builder of the specialized IR method.</param>
sealed record DelegateSpecialization(
    MethodBase Source,
    MethodBase?[] Targets,
    MethodBuilder Builder);

/// <summary>
/// Manages the specializations of methods for statically known delegate
/// arguments within a single module. Each distinct combination of a method and
/// its bound delegate targets is specialized once; the closure pointers of the
/// delegates are passed on as regular arguments.
/// </summary>
/// <param name="moduleBuilder">The parent module builder.</param>
sealed class DelegateSpecializations(ModuleBuilder moduleBuilder)
{
    private readonly Dictionary<MethodBase, List<DelegateSpecialization>>
        _specializations = [];
    private readonly Queue<DelegateSpecialization> _pending = new();
    private readonly Dictionary<Method, List<(Method Callee, Location Location)>>
        _calls = [];

    /// <summary>
    /// Records a call of the given method from the code of the given caller,
    /// which lets <see cref="VerifyNoRecursion"/> detect recursive calls of
    /// specializations.
    /// </summary>
    /// <param name="callee">The called method.</param>
    /// <param name="caller">The calling method.</param>
    /// <param name="location">The location of the call.</param>
    public void RecordCall(Method callee, Method caller, Location location)
    {
        if (!_calls.TryGetValue(caller, out var calls))
        {
            calls = [];
            _calls.Add(caller, calls);
        }
        calls.Add((callee, location));
    }

    /// <summary>
    /// Rejects specializations that call themselves, directly or through the
    /// methods they call. Kernels cannot recurse, and the delegates of all
    /// recursive calls would be bound to the same targets.
    /// </summary>
    public void VerifyNoRecursion()
    {
        foreach (var specializations in _specializations.Values)
        {
            foreach (var specialization in specializations)
            {
                var method = specialization.Builder.Method;
                if (!TryFindCall(method, method, [], out var location))
                    continue;
                throw new KernelCompilationException(
                    KernelDiagnostic.DelegateRecursionId,
                    string.Format(
                        ErrorMessages.KernelDiagnosticDelegateRecursion,
                        specialization.Source.GetDisplayName()),
                    location);
            }
        }
    }

    /// <summary>
    /// Tries to find a call of the given target that is reachable from the
    /// given caller.
    /// </summary>
    /// <param name="caller">The calling method.</param>
    /// <param name="target">The method to find calls of.</param>
    /// <param name="visited">The methods visited so far.</param>
    /// <param name="location">The location of the found call.</param>
    /// <returns>True if a call of the target is reachable.</returns>
    private bool TryFindCall(
        Method caller,
        Method target,
        HashSet<Method> visited,
        [NotNullWhen(true)] out Location? location)
    {
        location = null;
        if (!visited.Add(caller) || !_calls.TryGetValue(caller, out var calls))
            return false;
        foreach (var (callee, callLocation) in calls)
        {
            if (callee == target)
            {
                location = callLocation;
                return true;
            }
            if (TryFindCall(callee, target, visited, out location))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets or creates the specialization of the given method that binds its
    /// delegate parameters to the given targets.
    /// </summary>
    /// <param name="source">The method to specialize.</param>
    /// <param name="targets">The targets of all delegate parameters.</param>
    /// <returns>The specialized IR method.</returns>
    public Method GetOrCreate(MethodBase source, MethodBase?[] targets)
    {
        if (!_specializations.TryGetValue(source, out var specializations))
        {
            specializations = [];
            _specializations.Add(source, specializations);
        }
        foreach (var existing in specializations)
        {
            if (existing.Targets.SequenceEqual(targets))
                return existing.Builder.Method;
        }

        var declaration = moduleBuilder.CreateMethodDeclaration(source)
            .AddFlags(MethodFlags.Specialized);
        var specialization = new DelegateSpecialization(
            source,
            targets,
            moduleBuilder.GetOrCreateMethod(declaration));
        specializations.Add(specialization);
        _pending.Enqueue(specialization);
        return specialization.Builder.Method;
    }

    /// <summary>
    /// Tries to dequeue a specialization whose code has not been generated yet.
    /// </summary>
    /// <param name="specialization">The dequeued specialization.</param>
    /// <returns>True if a pending specialization was dequeued.</returns>
    public bool TryDequeue(
        [NotNullWhen(true)] out DelegateSpecialization? specialization) =>
        _pending.TryDequeue(out specialization);
}
