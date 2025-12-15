// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SSAAllocations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Metadata about a memory allocation that can be converted to SSA form.
/// </summary>
/// <param name="BaseAllocation">The base allocation value (Alloca or Malloc).</param>
/// <param name="AllocType">The type of elements being allocated.</param>
/// <param name="ArrayLength">The array length (0 for simple allocations).</param>
/// <param name="NumElementFields">Number of fields per array element.</param>
/// <param name="TotalStructFields">Total fields in flattened structure.</param>
/// <param name="BaseFieldRef">The base field reference for SSA tracking.</param>
/// <param name="UsedInMethod">The method reference for which the .</param>
sealed record class SSAAllocationInfo(
    Value BaseAllocation,
    TypeValue AllocType,
    int ArrayLength,
    int NumElementFields,
    int TotalStructFields,
    FieldRef BaseFieldRef,
    Method UsedInMethod)
{
    /// <summary>
    /// Creates an SSA allocation info for a simple (non-array) allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SSAAllocationInfo Create(
        Value allocation,
        TypeValue allocType,
        Method usedInMethod) =>
        new(allocation, allocType, 0, 0, 0, new FieldRef(allocation), usedInMethod);

    /// <summary>
    /// Creates an SSA allocation info for an array allocation that will be converted
    /// to a structure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SSAAllocationInfo Create(
        Value allocation,
        TypeValue allocType,
        int arrayLength,
        Method usedInMethod)
    {
        int numElementFields = StructureType.GetNumFields(allocType);
        int totalFields = arrayLength * numElementFields;

        return new SSAAllocationInfo(
            allocation,
            allocType,
            arrayLength,
            numElementFields,
            totalFields,
            new FieldRef(allocation, new FieldSpan(0, totalFields)),
            usedInMethod);
    }

    /// <summary>
    /// Returns true if this allocation should be converted to a structure.
    /// </summary>
    public bool IsArrayToStructure => ArrayLength > 0;

    /// <summary>
    /// The structure type for array-to-structure conversions.
    /// Set during transformation.
    /// </summary>
    public StructureType? StructureType { get; private set; }

    public TypeValue GetInitializedType(ModuleBuilder moduleBuilder)
    {
        if (!IsArrayToStructure)
            return AllocType;
        if (StructureType is not null)
            return StructureType;

        var structTypeBuilder = moduleBuilder.CreateStructureType(TotalStructFields);
        for (int i = 0; i < ArrayLength; ++i)
            structTypeBuilder.Add(AllocType);

        return StructureType = structTypeBuilder
            .Seal()
            .AsNotNullCast<StructureType>();
    }
}

/// <summary>
/// Field reference with associated allocation info.
/// </summary>
/// <param name="FieldRef">The field reference.</param>
/// <param name="AllocInfo">The allocation metadata.</param>
readonly record struct SSAValueFieldRef(
    FieldRef FieldRef,
    SSAAllocationInfo AllocInfo);

/// <summary>
/// Abstract interface for SSA allocation analyses.
/// </summary>
interface ISSAAllocations
{
    /// <summary>
    /// Returns true if there are any convertible allocations for the given method.
    /// </summary>
    bool HasAllocationsForMethod(Method method);

    /// <summary>
    /// Tries to return allocation info for the given value.
    /// </summary>
    /// <param name="value">The value to get allocation info for.</param>
    /// <param name="allocationInfo">The resulting allocation info (if any).</param>
    /// <returns>True if the given value could be mapped to allocation info.</returns>
    bool TryGetValue<TValue>(
        TValue value,
        [NotNullWhen(true)] out SSAAllocationInfo? allocationInfo)
        where TValue : Value, IAllocationValue;

    /// <summary>
    /// Tries to return a value-based field ref for the given value.
    /// </summary>
    /// <param name="value">The value to get the field ref for.</param>
    /// <param name="fieldRef">The field ref (if any).</param>
    /// <returns>True if the value could be mapped to a field ref.</returns>
    bool TryGetRef(Value value, out SSAValueFieldRef fieldRef);
}

/// <summary>
/// Shared helper methods for SSA allocation analyses.
/// </summary>
/// <param name="valueRefs">Mapping of values to field references (if any).</param>
abstract class SSAAllocations(GlobalValueMap<SSAValueFieldRef>? valueRefs) :
    ISSAAllocations
{
    #region Static

    /// <summary>
    /// Returns true if an array allocation can be converted to a structure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected static bool CanConvertArrayToStructure(Value allocation, int arrayLength)
    {
        foreach (Value use in allocation.Uses)
        {
            switch (use)
            {
                case LoadElementAddress lea:
                    if (lea.Offset is not PrimitiveValue index ||
                        index.Int32Value < 0 ||
                        index.Int32Value >= arrayLength)
                        return false;
                    if (RequiresAddress(lea))
                        return false;
                    break;
                case NewView nv:
                    if (!CanConvertArrayToStructure(nv, arrayLength))
                        return false;
                    break;
                case GetViewLength:
                case Load:
                case Store:
                    break;
                case AddressSpaceCast cast:
                    if (RequiresAddress(cast))
                        return false;
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns true if the given value requires an explicit address in memory.
    /// </summary>
    protected static bool RequiresAddress(Value node)
    {
        foreach (Value use in node.Uses)
        {
            if (RequiresAddressForUse(use))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the given use requires an explicit address in memory.
    /// </summary>
    private static bool RequiresAddressForUse(Value use) =>
        use switch
        {
            BasicBlock _ => false,
            Load _ => false,
            Store _ => false,
            LoadFieldAddress lfa => RequiresAddress(lfa),
            AddressSpaceCast cast => RequiresAddress(cast),
            _ => true
        };

    /// <summary>
    /// Processes a value and its children to build field/element references.
    /// </summary>
    protected static void ProcessValueForRefs(
        Value value,
        GlobalValueMap<SSAValueFieldRef> valueRefs)
    {
        bool TryGetValue(Value value, out SSAValueFieldRef fieldRef) =>
            valueRefs.TryGetValue(value, out fieldRef);
        void SetValue(Value value, SSAValueFieldRef valueRef) =>
            valueRefs[value] = valueRef;

        // Process child values FIRST (post-order traversal) so that
        // intermediate values in a chain (e.g. alloca → addrcast → lfa)
        // are added to valueRefs before we try to look up the source
        // of the current value.
        if (value is PureValue pureValue)
        {
            foreach (var childValue in pureValue.Values)
                ProcessValueForRefs(childValue, valueRefs);
        }

        switch (value)
        {
            case LoadFieldAddress lfa when TryGetValue(lfa.Source, out var baseRef):
                {
                    var newFieldRef = baseRef.FieldRef.Access(lfa.FieldSpan);
                    SetValue(lfa, new(newFieldRef, baseRef.AllocInfo));
                }
                break;

            case LoadElementAddress lea when TryGetValue(lea.Source, out var arrayRef):
                {
                    // Convert element access to field access
                    var allocInfo = arrayRef.AllocInfo;
                    if (lea.Offset is PrimitiveValue index)
                    {
                        int elementIndex = index.Int32Value;
                        var fieldAccess = new FieldAccess(
                            elementIndex * allocInfo.NumElementFields);
                        var elementFieldRef = allocInfo.BaseFieldRef.Access(
                            new FieldSpan(fieldAccess, allocInfo.NumElementFields));
                        SetValue(lea, new(elementFieldRef, allocInfo));
                    }
                }
                break;

            case AddressSpaceCast cast when TryGetValue(cast.Source, out var castRef):
                SetValue(cast, castRef);
                break;

            case NewView view when TryGetValue(view.Pointer, out var viewRef):
                SetValue(view, viewRef);
                break;

            case PhiValue phi:
                {
                    // Track phis whose arguments all point to the same
                    // tracked allocation. This ensures that ref chains
                    // flowing through phis (e.g. alloca → phi → addrspacecast
                    // → load) are properly tracked for SSA conversion.
                    SSAValueFieldRef? phiRef = null;
                    bool allTracked = true;
                    for (int i = 0; i < phi.NumArguments; i++)
                    {
                        if (TryGetValue(phi.Arguments[i], out var argRef))
                        {
                            phiRef ??= argRef;
                            if (phiRef.Value.AllocInfo != argRef.AllocInfo)
                            {
                                allTracked = false;
                                break;
                            }
                        }
                        else
                        {
                            allTracked = false;
                            break;
                        }
                    }
                    if (allTracked && phiRef.HasValue)
                        SetValue(phi, phiRef.Value);
                }
                break;
        }
    }

    #endregion

    #region Instance

    /// <summary>
    /// Returns true if there are any convertible allocations for the given method.
    /// </summary>
    public abstract bool HasAllocationsForMethod(Method method);

    /// <summary>
    /// Tries to return allocation info for the given value.
    /// </summary>
    /// <param name="value">The value to get allocation info for.</param>
    /// <param name="allocationInfo">The resulting allocation info (if any).</param>
    /// <returns>True if the given value could be mapped to allocation info.</returns>
    public abstract bool TryGetValue<TValue>(
        TValue value,
        [NotNullWhen(true)] out SSAAllocationInfo? allocationInfo)
        where TValue : Value, IAllocationValue;

    /// <summary>
    /// Tries to return a value-based field ref for the given value.
    /// </summary>
    /// <param name="value">The value to get the field ref for.</param>
    /// <param name="fieldRef">The field ref (if any).</param>
    /// <returns>True if the value could be mapped to a field ref.</returns>
    public bool TryGetRef(Value value, out SSAValueFieldRef fieldRef)
    {
        fieldRef = default;
        return valueRefs?.TryGetValue(value, out fieldRef) ?? false;
    }

    #endregion
}

/// <summary>
/// Method-level analysis that identifies Alloca allocations that can be converted to
/// SSA form.
/// </summary>
/// <remarks>
/// This analysis operates on a single method and determines which Alloca values can be
/// safely converted to SSA form with phi nodes. It handles:
/// - Simple local allocations (converted to SSA variables)
/// - Array allocations with constant size (converted to structures)
/// - Field and element access chains (LoadFieldAddress, LoadElementAddress)
///
/// Allocas can be assumed to have a static length when the ArrayLength property
/// returns an instance of PrimitiveValueBox and the length is greater than 1.
/// </remarks>
sealed class SSALocalAllocations : SSAAllocations
{
    #region Analysis Internals

    /// <summary>
    /// Returns true if the given local allocation can be transformed to SSA form.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool CanConvert(
        Alloca alloca,
        [NotNullWhen(true)] out SSAAllocationInfo? allocInfo)
    {
        allocInfo = null;
        var allocType = alloca.AllocType;

        // Check if this alloca a static array allocation
        if (alloca.IsStaticAllocation(out var arrayLength))
        {
            // Only convert arrays with length > 1 to structures
            if (arrayLength.Int32Value <= 1)
            {
                if (RequiresAddress(alloca))
                    return false;

                allocInfo = SSAAllocationInfo.Create(
                    alloca,
                    allocType,
                    alloca.BasicBlock.Method);
                return true;
            }

            // Check if we can convert to structure
            if (!CanConvertArrayToStructure(alloca, arrayLength.Int32Value))
                return false;

            allocInfo = SSAAllocationInfo.Create(
                alloca,
                allocType,
                arrayLength.Int32Value,
                alloca.BasicBlock.Method);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a map of values to their field/element references.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static GlobalValueMap<SSAValueFieldRef> BuildValueRefMap(
        Method method,
        ValueMap<Method, Alloca, SSAAllocationInfo> allocations)
    {
        // Map base allocations
        var result = method.Module.CreateGlobalMap<SSAValueFieldRef>(
            allocations.Count * 4);
        foreach (var (allocation, allocInfo) in allocations)
            result[allocation] = new(allocInfo.BaseFieldRef, allocInfo);

        // Process all blocks in the method to find uses of local allocations
        foreach (var block in method.Blocks)
        {
            foreach (BasicBlockValue bbValue in block)
            {
                // Process the basic block value and all its pure value children
                foreach (var childValue in bbValue.Values)
                    ProcessValueForRefs(childValue, result);
            }
        }

        return result;
    }

    #endregion

    #region Instance

    /// <summary>
    /// Creates an SSA local allocations analysis for a single method.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>The SSA local allocations analysis.</returns>
    public static SSALocalAllocations Create(Method method)
    {
        var allocations = method.CreateMap<Alloca, SSAAllocationInfo>();

        // Find all convertible local allocations in this method
        method.ForEachValue<Alloca>(alloca =>
        {
            if (CanConvert(alloca, out var allocInfo) && allocInfo != null)
                allocations[alloca] = allocInfo;
        });

        // Build value reference map for all values derived from local allocations
        GlobalValueMap<SSAValueFieldRef>? valueRefs = null;
        if (allocations.Count > 0)
            valueRefs = BuildValueRefMap(method, allocations);

        return new SSALocalAllocations(method, allocations, valueRefs);
    }

    private readonly ValueMap<Method, Alloca, SSAAllocationInfo> _allocations;

    private SSALocalAllocations(
        Method method,
        ValueMap<Method, Alloca, SSAAllocationInfo> allocations,
        GlobalValueMap<SSAValueFieldRef>? valueRefs)
        : base(valueRefs)
    {
        Debug.Assert(allocations.Count < 1 || valueRefs.HasValue);

        _allocations = allocations;
        Method = method;
    }

    /// <summary>
    /// Returns the method this analysis was run on.
    /// </summary>
    public Method Method { get; }

    /// <inheritdoc/>
    public override bool HasAllocationsForMethod(Method method) => Method == method;

    /// <inheritdoc/>
    public override bool TryGetValue<TValue>(
        TValue value,
        [NotNullWhen(true)] out SSAAllocationInfo? allocationInfo)
    {
        allocationInfo = null;
        if (value is not Alloca alloca)
            return false;
        return _allocations.TryGetValue(alloca, out allocationInfo);
    }

    #endregion
}

/// <summary>
/// Module-level analysis that identifies Global allocations that can be converted to
/// SSA form.
/// </summary>
/// <remarks>
/// This analysis determines which Global values can be safely converted to SSA form
/// with phi nodes. It handles:
/// - Simple global allocations (converted to SSA variables)
/// - Array allocations with constant size (converted to structures)
/// - Field and element access chains (LoadFieldAddress, LoadElementAddress)
///
/// Globals can be assumed to have a static length when the ArrayLength property
/// returns an instance of PrimitiveValueBox and the length is greater than 1.
/// </remarks>
sealed class SSAGlobalAllocations : SSAAllocations
{
    /// <summary>
    /// Creates an SSA global allocations analysis for the entire module.
    /// </summary>
    /// <param name="module">The module to analyze.</param>
    /// <param name="addressSpace">The target address space to analyze.</param>
    /// <returns>The SSA global allocations analysis.</returns>
    public static SSAGlobalAllocations Create(
        Module module,
        MemoryAddressSpace? addressSpace = null)
    {
        var allocations = module.CreateMap<Global, SSAAllocationInfo>();

        // Find all convertible global allocations
        module.ForEachValue<Global>(global =>
        {
            if ((!addressSpace.HasValue || global.AddressSpace == addressSpace) &&
                CanConvert(global, out var allocInfo) &&
                allocInfo != null)
            {
                allocations[global] = allocInfo;
            }
        });

        // Build value reference map for all values derived from global allocations
        GlobalValueMap<SSAValueFieldRef>? valueRefs = null;
        if (allocations.Count > 0)
            valueRefs = BuildValueRefMap(module, allocations);

        return new(allocations, valueRefs);
    }

    /// <summary>
    /// Returns true if the given global allocation can be transformed to SSA form.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool CanConvert(
        Global global,
        [NotNullWhen(true)] out SSAAllocationInfo? allocInfo)
    {
        allocInfo = null;

        // Shared memory globals represent physical GPU memory shared across
        // threads — they must persist as actual allocations, never promote
        // to SSA. Local memory globals (per-thread private) can be promoted
        // as long as their uses fall within a single function (checked below).
        if (global.AddressSpace is MemoryAddressSpace.Shared)
        {
            return false;
        }

        var allocType = global.AllocType;

        // Check whether all uses are in the same method
        Method? usedInMethod = null;
        bool usedInMultipleMethods = global.Uses.TryFind<Value>(value =>
        {
            var methodRef = value switch
            {
                BasicBlockValue bbValue => bbValue.Scope,
                Method method => method,
                _ => null
            };

            usedInMethod ??= methodRef;
            if (methodRef is not null && usedInMethod != methodRef)
                return true;

            return false;
        }, out _);

        if (usedInMethod is null || usedInMultipleMethods)
            return false;

        // Check if it's a static array allocation
        // Globals can be assumed to have a static length when ArrayLength property
        // returns an instance of PrimitiveValueBox and the length is greater than 1
        if (global.IsStaticAllocation(out var arrayLength))
        {
            // Only convert arrays with length > 1 to structures
            if (arrayLength.Int32Value <= 1)
            {
                // Treat as simple allocation
                if (RequiresAddress(global))
                    return false;

                allocInfo = SSAAllocationInfo.Create(global, allocType, usedInMethod);
                return true;
            }

            // Check if we can convert to structure
            if (!CanConvertArrayToStructure(global, arrayLength.Int32Value))
                return false;

            allocInfo = SSAAllocationInfo.Create(
                global,
                allocType,
                arrayLength.Int32Value,
                usedInMethod);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a map of values to their field/element references.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static GlobalValueMap<SSAValueFieldRef> BuildValueRefMap(
        Module module,
        ValueMap<Module, Global, SSAAllocationInfo> allocations)
    {
        // Map base allocations
        var result = module.CreateGlobalMap<SSAValueFieldRef>(allocations.Count * 4);
        foreach (var (allocation, allocInfo) in allocations)
            result[allocation] = new(allocInfo.BaseFieldRef, allocInfo);

        // Process all methods in the module to find uses of globals
        foreach (var method in module.Methods)
        {
            foreach (var block in method.Blocks)
            {
                foreach (BasicBlockValue bbValue in block)
                {
                    // Process the basic block value and all its pure value children
                    foreach (var childValue in bbValue.Values)
                        ProcessValueForRefs(childValue, result);
                }
            }
        }

        return result;
    }

    private readonly ValueMap<Module, Global, SSAAllocationInfo> _allocations;

    private SSAGlobalAllocations(
        ValueMap<Module, Global, SSAAllocationInfo> allocations,
        GlobalValueMap<SSAValueFieldRef>? valueRefs)
        : base(valueRefs)
    {
        _allocations = allocations;
    }

    /// <inheritdoc/>
    public override bool HasAllocationsForMethod(Method method)
    {
        foreach (var entry in _allocations)
        {
            if (entry.Value.UsedInMethod == method)
                return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetValue<TValue>(
        TValue value,
        [NotNullWhen(true)] out SSAAllocationInfo? allocationInfo)
    {
        allocationInfo = null;
        if (value is not Global global)
            return false;
        return _allocations.TryGetValue(global, out allocationInfo);
    }
}

readonly record struct SSAAllocations<T1, T2>(
    T1 allocations1,
    T2 allocations2) : ISSAAllocations
    where T1 : ISSAAllocations
    where T2 : ISSAAllocations
{
    /// <inheritdoc/>
    public bool HasAllocationsForMethod(Method method) =>
        allocations1.HasAllocationsForMethod(method) ||
        allocations2.HasAllocationsForMethod(method);

    /// <inheritdoc/>
    public bool TryGetRef(Value value, out SSAValueFieldRef fieldRef) =>
        allocations1.TryGetRef(value, out fieldRef) ||
        allocations2.TryGetRef(value, out fieldRef);

    /// <inheritdoc/>
    public bool TryGetValue<TValue>(
        TValue value,
        [NotNullWhen(true)] out SSAAllocationInfo? allocationInfo)
        where TValue : Value, IAllocationValue =>
        allocations1.TryGetValue(value, out allocationInfo) ||
        allocations2.TryGetValue(value, out allocationInfo);
}
