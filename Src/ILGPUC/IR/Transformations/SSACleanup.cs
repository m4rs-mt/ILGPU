// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SSACleanup.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
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
        method.ForEachValue<PhiValue>(phiValue =>
        {
            if (!trivialPhis.ContainsKey(phiValue))
                TryDetectTrivialPhi(phiValue, trivialPhis);
        });

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
