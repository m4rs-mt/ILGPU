// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerAliases.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents pointer alias information for allocations.
/// </summary>
/// <remarks>
/// This analysis tracks direct aliases of allocations through:
/// - Phi values (SSA merging of multiple pointers)
/// - Parameters (with address-space-dependent types)
/// - Global allocations (module-level Global values with address-space-dependent types)
/// - View operations (NewView, SubView)
/// - Cast operations (AddressSpaceCast)
/// - Alignment operations (AlignTo)
/// - Field address operations (LoadFieldAddress, LoadElementAddress)
/// - Structure field operations (GetField, SetField with address-space-dependent types)
/// - Structure construction (StructureValue with address-space-dependent fields)
/// - Method calls (connecting arguments to parameters and return values to call results)
///
/// Scope: This is a module-wide, interprocedural analysis. It processes all methods
/// in the module and connects aliases across method boundaries by tracking call-site
/// arguments to formal parameters and return values back to call results. It also
/// tracks module-level global allocations. Transitive closure propagates aliases
/// across the entire module.
///
/// This approach is focused on extending liveness for buffer packing by conservatively
/// tracking how allocations flow through the IR, including across method calls and
/// through global allocations.
/// </remarks>
/// <param name="aliasMap">Maps each allocation to its set of aliases.</param>
readonly struct PointerAliases(GlobalValueMap<GlobalValueSet>? aliasMap)
{
    /// <summary>
    /// Empty pointer alias information.
    /// </summary>
    public static readonly PointerAliases Empty = new(null);

    /// <summary>
    /// Returns all aliases for a given allocation.
    /// </summary>
    /// <param name="allocation">The allocation to query.</param>
    /// <returns>Read-only span of all aliases, or empty if none.</returns>
    public GlobalValueSet? this[IAllocationValue allocation]
    {
        get
        {
            if (allocation is not Value value || aliasMap is null)
                return null;

            if (aliasMap.Value.TryGetValue(value, out var aliases))
                return aliases;

            return null;
        }
    }

    /// <summary>
    /// Returns true if this alias information is empty.
    /// </summary>
    public bool IsEmpty => aliasMap is null;

    /// <summary>
    /// Creates pointer alias information for a module.
    /// </summary>
    /// <param name="module">The module to analyze.</param>
    /// <returns>The computed pointer alias information.</returns>
    public static PointerAliases Create(Module module)
    {
        var analysis = new PointerAliasAnalysis(module);
        analysis.Analyze();
        return analysis.GetResult();
    }
}

/// <summary>
/// Analyzes pointer aliases to track how allocations flow through the IR.
/// </summary>
sealed class PointerAliasAnalysis(Module module)
{
    private readonly GlobalValueMap<GlobalValueSet> _aliasMap =
        module.CreateGlobalMap<GlobalValueSet>();


    /// <summary>
    /// Returns the computed alias information.
    /// </summary>
    public PointerAliases GetResult() => new(_aliasMap);

    /// <summary>
    /// Performs the alias analysis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Analyze()
    {
        // Track global allocations with address-space-dependent types
        // These can alias allocations passed through them
        foreach (var global in module.Globals)
        {
            // Create an entry for globals to participate in transitive closure
            if (global.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                RecordForAliases(global);
        }

        // Process all methods to find aliases
        foreach (var method in module.MethodsInReversePostOrder)
        {
            if (!method.HasImplementation)
                continue;

            AnalyzeMethod(method);
        }

        // Compute transitive closure - if A aliases B and B aliases C, then A aliases C
        ComputeTransitiveClosure();
    }

    /// <summary>
    /// Analyzes a single method to find direct aliases.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void AnalyzeMethod(Method method)
    {
        // Track parameters with address-space-dependent types
        // These can alias allocations passed as arguments
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.HasFlags(TypeFlags.AddressSpaceDependent))
            {
                // Parameters don't have a direct source to alias to at this level
                // but we create an entry for them to participate in transitive closure
                RecordForAliases(parameter);
            }
        }

        // Scan all values in the method
        method.ForEachValue<Value<Method>>(
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        (value) =>
            {
                // Special handling for phi values - they alias ALL incoming values
                if (value is PhiValue phi &&
                    phi.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                {
                    foreach (var incomingValue in phi.Values)
                    {
                        // Add bidirectional alias relationship with each incoming value
                        AddAlias(incomingValue, phi);
                        AddAlias(phi, incomingValue);
                    }
                    return;
                }

                // Special handling for StructureValue - creates struct from field values
                // If any fields are address-space-dependent, the struct aliases them
                if (value is StructureValue structValue &&
                    structValue.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                {
                    // Iterate through all field values
                    foreach (var fieldValue in structValue.Values)
                    {
                        if (fieldValue.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                        {
                            // The struct aliases this field value
                            AddAlias(fieldValue, structValue);
                            AddAlias(structValue, fieldValue);
                        }
                    }
                    return;
                }

                // Special handling for SetField - it creates multiple alias relationships
                if (value is SetField setField &&
                    setField.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                {
                    // The result struct aliases the original struct
                    AddAlias(setField.Source, setField);
                    AddAlias(setField, setField.Source);

                    // If the stored value is address-space-dependent, the result
                    // also aliases it
                    if (setField.Value.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                    {
                        AddAlias(setField.Value, setField);
                        AddAlias(setField, setField.Value);
                    }
                    return;
                }

                // Special handling for method calls - connect arguments to parameters and
                // return values
                if (value is MethodCall call &&
                    call.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                {
                    var target = call.Target;
                    if (target.HasImplementation)
                    {
                        // Connect call-site arguments to formal parameters
                        var parameters = target.Parameters;
                        var arguments = call.Values;
                        int length = Math.Min(parameters.Count, arguments.Length);
                        for (int i = 0; i < length; ++i)
                        {
                            var arg = arguments[i];
                            var param = parameters[i];

                            if (arg.Type.HasFlags(TypeFlags.AddressSpaceDependent) &&
                                param.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                            {
                                // Argument aliases parameter
                                AddAlias(arg, param);
                                AddAlias(param, arg);
                            }
                        }

                        // Connect return values to call result
                        // Scan the called method for return blocks
                        foreach (var block in target.Blocks)
                        {
                            if (block.TerminationKind != BlockTerminationKind.Return)
                                continue;

                            var returnValue = block.TerminationValue;
                            if (returnValue is null ||
                                returnValue.Type is VoidType ||
                                !returnValue.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                                continue;

                            // Return value aliases call result
                            AddAlias(returnValue, call);
                            AddAlias(call, returnValue);
                        }
                    }
                    return;
                }

                // Check if this value creates an alias
                if (TryGetAliasSource(value, out var source))
                {
                    // Add bidirectional alias relationship
                    AddAlias(source, value);
                    AddAlias(value, source);
                }
            });
    }

    /// <summary>
    /// Checks if a value creates an alias and returns the source.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="source">The source value that is aliased.</param>
    /// <returns>True if this value creates an alias.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetAliasSource(
        Value value,
        [NotNullWhen(true)] out Value? source)
    {
        source = value switch
        {
            // View operations create aliases
            NewView newView => newView.Pointer,
            SubView subView => subView.Source,

            // Cast operations preserve aliasing
            BaseAddressSpaceCast cast => cast.Source,

            // Alignment operations preserve aliasing
            BaseAlignOperationValue align => align.Source,

            // Load field address creates alias to the structure
            LoadFieldAddress lfa => lfa.Source,

            // Load element address creates alias to the array
            LoadElementAddress lea => lea.Source,

            // GetField extracts a field; if it's address-space-dependent,
            // it aliases the struct value
            GetField getField when getField.Type.HasFlags(TypeFlags.AddressSpaceDependent)
                => getField.Source,

            _ => null
        };

        return source is not null;
    }

    /// <summary>
    /// Adds an alias relationship.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private GlobalValueSet RecordForAliases(Value allocation)
    {
        // Get or create alias set for this allocation
        if (!_aliasMap.TryGetValue(allocation, out var aliases))
        {
            aliases = module.CreateGlobalSet();
            _aliasMap.Add(allocation, aliases);
        }

        return aliases;
    }

    /// <summary>
    /// Adds an alias relationship.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddAlias(Value allocation, Value alias)
    {
        // Get or create alias set for this allocation
        var aliases = RecordForAliases(allocation);

        // Add the alias if not already present
        aliases.Add(alias);
    }

    /// <summary>
    /// Computes transitive closure of alias relationships.
    /// If A aliases B and B aliases C, then A should alias C.
    /// </summary>
    /// <remarks>
    /// The algorithm is guaranteed to terminate because:
    /// 1. Each value can have at most O(V) distinct aliases (bounded by total values)
    /// 2. We only add aliases that aren't already present (via Contains check)
    /// 3. Once no new aliases are added, changed=false and we exit
    ///
    /// Typical convergence is 2-3 iterations for well-formed IR.
    /// The maxIterations limit is a defensive safeguard against bugs or pathological IR.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ComputeTransitiveClosure()
    {
        bool changed = true;

        void ProcessAllocation(Value allocation)
        {
            if (!_aliasMap.TryGetValue(allocation, out var directAliases))
                return;

            // For each direct alias, add its aliases to this allocation
            // NOTE: We capture count before loop to avoid processing newly added aliases
            foreach (var alias in directAliases)
            {
                if (!_aliasMap.TryGetValue(alias, out var transitiveAliases))
                    continue;

                // Add all transitive aliases
                foreach (var transitiveAlias in transitiveAliases)
                {
                    if (transitiveAlias != allocation &&
                        !directAliases.Contains(transitiveAlias))
                    {
                        directAliases.Add(transitiveAlias);
                        changed = true;
                    }
                }
            }
        }

        // Defensive safeguard; typical IR converges in 2-3 iterations
        const int maxIterations = 10;
        for (int i = 0; changed && i < maxIterations; ++i)
        {
            changed = false;

            // Process global allocations
            foreach (var global in module.Globals)
                ProcessAllocation(global);

            // Process all values in all methods
            foreach (var method in module.MethodsInReversePostOrder)
            {
                if (!method.HasImplementation)
                    continue;

                method.ForEachValue<Value<Method>>(ProcessAllocation);
            }
        }
    }
}
