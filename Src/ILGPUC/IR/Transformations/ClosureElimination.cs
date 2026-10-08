// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ClosureElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Eliminates closure allocations by performing scalar replacement of aggregates
/// (SROA) on closure <see cref="Global"/> values. Each closure struct field is
/// decomposed into an individual <see cref="Alloca"/>, allowing subsequent SSA
/// construction to promote them to direct SSA values.
/// </summary>
/// <remarks>
/// <para>
/// This pass runs after <see cref="LowerGCInit"/> (GCInit nodes replaced by bare
/// Globals) and before backend-specific lowering. It ensures that closure
/// allocations are eliminated at the IR level, which is critical for GPU backends
/// where closures cannot exist at runtime.
/// </para>
/// <para>
/// A closure Global is eligible for SROA when:
/// <list type="bullet">
/// <item>It is in the <see cref="MemoryAddressSpace.Local"/> address space</item>
/// <item>Its element type is a <see cref="StructureType"/></item>
/// <item>It is a simple allocation (array length == 1)</item>
/// <item>All uses are <see cref="LoadFieldAddress"/> or
///       <see cref="AddressSpaceCast"/> → <see cref="LoadFieldAddress"/>
///       chains (the closure does not escape)</item>
/// </list>
/// </para>
/// </remarks>
sealed class ClosureElimination(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// Set of Globals that are safe to decompose.
    /// </summary>
    private readonly HashSet<ValueId> _candidates = [];

    /// <summary>
    /// Per-field allocas: (Global ID, field index) → Alloca.
    /// Shared across methods (a Global may be used by multiple methods).
    /// </summary>
    private readonly Dictionary<(ValueId, int), Alloca> _fieldAllocas = [];

    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        // Identify closure Globals that are safe to decompose
        foreach (var global in args.Module.Globals)
        {
            if (IsClosureCandidate(global) && AllUsesAreSafe(global))
                _candidates.Add(global.Id);
        }

        // Map LoadFieldAddress values that reference closure Globals
        MapPureValue<LoadFieldAddress>((pureTransform, lfa) =>
        {
            var source = ResolveSource(lfa.Source);
            if (source is not Global global || !_candidates.Contains(global.Id))
                return lfa;

            var key = (global.Id, lfa.FieldSpan.Index);
            if (_fieldAllocas.TryGetValue(key, out var alloca))
                return alloca;

            // Alloca not yet created — return unchanged for now.
            // OnTransform will create allocas and register replacements.
            return lfa;
        });
    }

    /// <inheritdoc/>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        if (_candidates.Count == 0)
            return;

        // Find LoadFieldAddress values in this method that reference
        // closure Globals, create per-field allocas, and register
        // replacements.
        transform.OldMethod.ForEachValue<LoadFieldAddress>(lfa =>
        {
            var source = ResolveSource(lfa.Source);
            if (source is not Global global || !_candidates.Contains(global.Id))
                return;

            var key = (global.Id, lfa.FieldSpan.Index);
            if (_fieldAllocas.TryGetValue(key, out var existing))
            {
                // Already created — just register replacement
                transform.Replace(lfa, existing);
                return;
            }

            // Create alloca for this field in the method's entry block.
            // Rewrite the field type to the current generation.
            var entryBT = (BasicBlockTransform)transform.EntryBuilder;
            var fieldType = transform.Rewrite(lfa.FieldType);
            var alloca = entryBT.CreateAlloca(
                lfa.Location,
                fieldType);
            if (alloca is null) return;

            _fieldAllocas[key] = alloca;
            transform.Replace(lfa, alloca);
        });
    }

    /// <summary>
    /// Follows through <see cref="AddressSpaceCast"/> nodes to find the
    /// underlying source value.
    /// </summary>
    private static Value ResolveSource(Value source)
    {
        while (source is AddressSpaceCast cast)
            source = cast.Source;
        return source;
    }

    /// <summary>
    /// Returns true if the Global is a closure candidate:
    /// Local address space, StructureType element, single allocation.
    /// </summary>
    private static bool IsClosureCandidate(Global global) =>
        global.AddressSpace == MemoryAddressSpace.Local
        && global.AllocType is StructureType
        && global.IsSimpleAllocation();

    /// <summary>
    /// Returns true if all uses of the Global are decomposable field
    /// accesses (LoadFieldAddress, possibly through AddressSpaceCast).
    /// If any use escapes (e.g., passed to a method call), returns false.
    /// </summary>
    private static bool AllUsesAreSafe(Global global)
    {
        foreach (var use in global.Uses)
        {
            switch (use.Target)
            {
                case LoadFieldAddress:
                    continue;

                case AddressSpaceCast cast:
                    // All uses of the cast must be LoadFieldAddress
                    foreach (var castUse in cast.Uses)
                    {
                        if (castUse.Target is not LoadFieldAddress)
                            return false;
                    }
                    continue;

                default:
                    return false;
            }
        }
        return true;
    }
}
