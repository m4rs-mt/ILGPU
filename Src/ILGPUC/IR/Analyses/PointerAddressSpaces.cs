// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerAddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU;
using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents different address spaces that can coexist via flags.
/// </summary>
[Flags]
enum AddressSpaceFlags : int
{
    /// <summary>
    /// No specific address spaces.
    /// </summary>
    None = 0,

    /// <summary cref="MemoryAddressSpace.Generic"/>
    Generic = 1 << MemoryAddressSpace.Generic,

    /// <summary cref="MemoryAddressSpace.Global"/>
    Global = 1 << MemoryAddressSpace.Global,

    /// <summary cref="MemoryAddressSpace.Shared"/>
    Shared = 1 << MemoryAddressSpace.Shared,

    /// <summary cref="MemoryAddressSpace.Local"/>
    Local = 1 << MemoryAddressSpace.Local,
}

/// <summary>
/// An internal address-space information object used to manage
/// <see cref="AddressSpaceFlags"/> flags.
/// </summary>
/// <remarks>
/// Constructs a new address-space information object.
/// </remarks>
/// <param name="flags">The associated flags.</param>
readonly struct AddressSpaceInfo(AddressSpaceFlags flags) : IEquatable<AddressSpaceInfo>
{
    /// <summary>
    /// Iterates over all internally stored address spaces.
    /// </summary>
    internal struct Enumerator(AddressSpaceInfo info)
    {
        private int index = -1;

        /// <summary>
        /// Returns the current address space.
        /// </summary>
        public MemoryAddressSpace Current { get; private set; } =
            MemoryAddressSpace.Generic;

        /// <summary cref="IEnumerator.MoveNext"/>
        public bool MoveNext()
        {
            do
            {
                Current = (MemoryAddressSpace)(++index);
                if (info.HasAddressSpace(Current))
                    return true;
            }
            while (index <= (int)MemoryAddressSpace.Local);
            return false;
        }
    }

    /// <summary>
    /// Determines an address-space information instance based on type
    /// information.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>
    /// The resolved address-space information for the given type.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static AddressSpaceInfo FromType(TypeValue type)
    {
        // Check for address-space dependencies
        if (!type.HasFlags(TypeFlags.AddressSpaceDependent))
            return default;

        // Determine the unified address space of all elements
        if (type is AddressSpaceType addressSpaceType)
            return addressSpaceType.AddressSpace;

        // Unify all address space flags from each dependent field
        var structureType = type.AsNotNullCast<StructureType>();
        var result = new AddressSpaceInfo();
        foreach (var fieldType in structureType.Fields)
            result = Merge(result, FromType(fieldType));
        return result;
    }

    /// <summary>
    /// Creates an <see cref="AnalysisValue{T}"/> instance based on the given
    /// type information.
    /// information.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>
    /// The resolved analysis value holding detailed address-space information
    /// for the given type.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static AnalysisValue<AddressSpaceInfo> AnalysisValueFromType(
        TypeValue type)
    {
        var unifiedAddressSpace = FromType(type);
        if (unifiedAddressSpace.Flags != AddressSpaceFlags.None &&
            type is StructureType structureType)
        {
            var childData = new AddressSpaceInfo[structureType.NumFields];
            for (int i = 0; i < structureType.NumFields; i++)
                childData[i] = FromType(structureType.Fields[i]);
            return new AnalysisValue<AddressSpaceInfo>(
                unifiedAddressSpace,
                childData);
        }
        return AnalysisValue.Create(unifiedAddressSpace, type);
    }

    /// <summary>
    /// Merges two information objects.
    /// </summary>
    /// <param name="first">The first info object.</param>
    /// <param name="second">The second info object.</param>
    /// <returns>
    /// Merged address space information based on both operands.
    /// </returns>
    public static AddressSpaceInfo Merge(
        AddressSpaceInfo first,
        AddressSpaceInfo second) =>
        new(first.Flags | second.Flags);

    /// <summary>
    /// Returns the underlying address-space flags.
    /// </summary>
    public AddressSpaceFlags Flags { get; } = flags;

    /// <summary>
    /// Returns the most generic address space that is compatible with all
    /// internally gathered address spaces.
    /// </summary>
    public MemoryAddressSpace UnifiedAddressSpace =>
        Flags == AddressSpaceFlags.None || !XMath.IsPowerOf2((int)Flags)
        ? MemoryAddressSpace.Generic
        : HasFlags(AddressSpaceFlags.Global)
        ? MemoryAddressSpace.Global
        : HasFlags(AddressSpaceFlags.Shared)
        ? MemoryAddressSpace.Shared
        : HasFlags(AddressSpaceFlags.Local)
        ? MemoryAddressSpace.Local
        : MemoryAddressSpace.Generic;

    /// <summary>
    /// Returns true if the given flags are set.
    /// </summary>
    /// <param name="flags">The flags to test.</param>
    /// <returns>True, if the given flags are set.</returns>
    public readonly bool HasFlags(AddressSpaceFlags flags) =>
        (Flags & flags) == flags;

    /// <summary>
    /// Returns true if the current instance is associated with the given
    /// address space.
    /// </summary>
    /// <param name="addressSpace">The address space to test.</param>
    /// <returns>
    /// True, if the current instance is associated with the given address space.
    /// </returns>
    public readonly bool HasAddressSpace(MemoryAddressSpace addressSpace) =>
        HasFlags((AddressSpaceFlags)(1 << (int)addressSpace));

    /// <summary>
    /// Returns true if the given info object is equal to the current instance.
    /// </summary>
    /// <param name="other">The other info object.</param>
    /// <returns>
    /// True, if the given info object is equal to the current instance.
    /// </returns>
    public readonly bool Equals(AddressSpaceInfo other) =>
        Flags == other.Flags;

    /// <summary>
    /// Returns an enumerator to iterate over all address spaces.
    /// </summary>
    /// <returns>The enumerator instance.</returns>
    public readonly Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Returns true if the given object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>
    /// True, if the given object is equal to the current instance.
    /// </returns>
    public override readonly bool Equals(object? obj) =>
        obj is AddressSpaceInfo info && Equals(info);

    /// <summary>
    /// Returns the hash code of this instance.
    /// </summary>
    /// <returns>The hash code of this instance.</returns>
    public override readonly int GetHashCode() => (int)Flags;

    /// <summary>
    /// Returns the string representation of this instance.
    /// </summary>
    /// <returns>The string representation of this instance.</returns>
    public override readonly string ToString() =>
        Flags == AddressSpaceFlags.None
        ? "<None>"
        : UnifiedAddressSpace.ToString();

    /// <summary>
    /// Converts nullable <see cref="MemoryAddressSpace"/> values to information
    /// instances.
    /// </summary>
    /// <param name="addressSpace">The address space to convert.</param>
    public static implicit operator AddressSpaceInfo(
        MemoryAddressSpace? addressSpace) =>
        !addressSpace.HasValue
        ? new AddressSpaceInfo()
        : new AddressSpaceInfo(
            (AddressSpaceFlags)(1 << (int)addressSpace.Value));

    /// <summary>
    /// Returns true if the first and second information instances are the same.
    /// </summary>
    /// <param name="first">The first instance.</param>
    /// <param name="second">The second instance.</param>
    /// <returns>True, if the first and second instances are the same.</returns>
    public static bool operator ==(
        AddressSpaceInfo first,
        AddressSpaceInfo second) =>
        first.Equals(second);

    /// <summary>
    /// Returns true if the first and second information instances are not the
    /// same.
    /// </summary>
    /// <param name="first">The first instance.</param>
    /// <param name="second">The second instance.</param>
    /// <returns>
    /// True, if the first and second instances are not the same.
    /// </returns>
    public static bool operator !=(
        AddressSpaceInfo first,
        AddressSpaceInfo second) =>
        !first.Equals(second);
}

/// <summary>
/// Stores address space information from an address space analysis run.
/// </summary>
/// <param name="result">The result mapping.</param>
/// <remarks>
/// Constructs a new address space analysis result.
/// </remarks>
readonly struct AddressSpaceResults(FixPointAnalysisResult<AddressSpaceInfo>? result)
{
    /// <summary>
    /// Empty address space information.
    /// </summary>
    public static readonly AddressSpaceResults Empty = new(null);

    /// <summary>
    /// Returns address space information for the given value.
    /// </summary>
    /// <param name="value">The value to get address space information for.</param>
    /// <returns>Address space information for the value.</returns>
    public AddressSpaceInfo this[Value value] => result?.GetResult(value).Data ?? default;

    /// <summary>
    /// Returns true if this address space information object is empty.
    /// </summary>
    public bool IsEmpty => !result.HasValue;

    /// <summary>
    /// Returns the unified address space for the given value.
    /// </summary>
    /// <param name="value">The value to get the unified address space for.</param>
    /// <returns>The unified address space.</returns>
    public MemoryAddressSpace GetUnifiedAddressSpace(Value value) =>
        this[value].UnifiedAddressSpace;

    /// <summary>
    /// Returns the full analysis value (including per-field information) for the given value.
    /// </summary>
    /// <param name="value">The value to get analysis information for.</param>
    /// <returns>The full analysis value for the value.</returns>
    public AnalysisValue<AddressSpaceInfo> GetAnalysisValue(Value value) =>
        result?.GetResult(value)
        ?? AnalysisValue.Create(default(AddressSpaceInfo), value.Type);
}

/// <summary>
/// An analysis to determine safe address-space information for all values.
/// </summary>
/// <remarks>
/// Constructs a new analysis implementation.
/// </remarks>
/// <param name="flags">The analysis flags.</param>
sealed class PointerAddressSpaces(PointerAddressSpaces.AnalysisFlags flags) :
    FixPointAnalysis<AddressSpaceInfo, Forwards>()
{
    /// <summary>
    /// The analysis flags.
    /// </summary>
    [Flags]
    internal enum AnalysisFlags : int
    {
        /// <summary>
        /// Performs a conservative analysis.
        /// </summary>
        None = 0 << 0,

        /// <summary>
        /// Ignores generic address-space types during the analysis.
        /// </summary>
        IgnoreGenericAddressSpace = 1 << 0,
    }

    /// <summary>
    /// Creates a new pointer analysis instance using the default analysis flags.
    /// </summary>
    /// <returns>The created analysis instance.</returns>
    public static PointerAddressSpaces Create() =>
        Create(AnalysisFlags.None);

    /// <summary>
    /// Creates a new pointer analysis instance.
    /// </summary>
    /// <param name="flags">The analysis flags.</param>
    /// <returns>The created analysis instance.</returns>
    public static PointerAddressSpaces Create(AnalysisFlags flags) => new(flags);

    /// <summary>
    /// Applies address space analysis to a single method.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <param name="flags">The analysis flags.</param>
    /// <returns>Address space analysis results.</returns>
    public static AddressSpaceResults AnalyzeMethod(
        Method method,
        AnalysisFlags flags = AnalysisFlags.None)
    {
        var analysis = Create(flags);
        var result = analysis.AnalyzeMethod(method.Blocks);
        return new AddressSpaceResults(result);
    }

    /// <summary>
    /// Applies address space analysis to an entire module starting from an entry point.
    /// </summary>
    /// <param name="rootMethod">The root (entry) method.</param>
    /// <param name="flags">The analysis flags.</param>
    /// <returns>Address space analysis results for all methods in the module.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static AddressSpaceResults AnalyzeModule(
        Method rootMethod,
        AnalysisFlags flags = AnalysisFlags.None)
    {
        var analysis = Create(flags);

        // Mark all global values as global
        var globalMap = rootMethod.Module.CreateGlobalMap<
            AnalysisValue<AddressSpaceInfo>>();
        foreach (var global in rootMethod.Module.Globals)
        {
            globalMap.Add(global, AnalysisValue.Create(
                new AddressSpaceInfo(AddressSpaceFlags.Global), global.Type));
        }

        // Initialize kernel parameters
        foreach (var parameter in rootMethod.Parameters)
        {
            globalMap.Add(parameter, AnalysisValue.Create(
                new AddressSpaceInfo(AddressSpaceFlags.Global), parameter.Type));
        }

        var result = analysis.AnalyzeModule(rootMethod, globalMap);
        return new AddressSpaceResults(result);
    }

    /// <summary>
    /// Returns true if the analysis has the given flags.
    /// </summary>
    /// <param name="otherFlags">The flags.</param>
    /// <returns>True, if the analysis has the given flags.</returns>
    private bool HasFlags(AnalysisFlags otherFlags) =>
        (flags & otherFlags) != AnalysisFlags.None;

    /// <summary>
    /// Returns initial and address space information.
    /// </summary>
    /// <param name="node">The IR node.</param>
    /// <returns>The initial address space information.</returns>
    private AddressSpaceInfo GetInitialInfo(Value node)
    {
        if (node.Type is not AddressSpaceType type)
            return default;
        return
            type.AddressSpace != MemoryAddressSpace.Generic ||
            !HasFlags(AnalysisFlags.IgnoreGenericAddressSpace)
            ? type.AddressSpace
            : default;
    }

    /// <summary>
    /// Tries to convert the given type into an <see cref="AddressSpaceType"/>
    /// and returns the determined address space.
    /// </summary>
    protected override AnalysisValue<AddressSpaceInfo>?
        TryProvideForType(TypeValue type) =>
        type is AddressSpaceType spaceType
        ? AnalysisValue.Create<AddressSpaceInfo>(spaceType.AddressSpace, type)
        : null;

    /// <summary>
    /// Returns the unified address-space flags.
    /// </summary>
    protected override AddressSpaceInfo Merge(
        AddressSpaceInfo first,
        AddressSpaceInfo second) =>
        AddressSpaceInfo.Merge(first, second);

    /// <summary>
    /// Analyzes a value to compute its address space information.
    /// </summary>
    protected override AnalysisValue<AddressSpaceInfo> Analyze(
        Value value,
        GlobalValueMap<AnalysisValue<AddressSpaceInfo>> data)
    {
        // Load information about address-space types for phi values
        if (value is PhiValue phiValue)
            return MergePhiValue(phiValue, data);

        // PointerCast: inherit address space from source.
        // PointerCast changes element type but preserves address space.
        // Propagating source info allows tracing alloca-derived pointers
        // through Unsafe.As<T,U>() reinterpret casts.
        if (value is PointerCast ptrCast
            && data.TryGetValue(ptrCast.Source, out var ptrSrcData))
            return AnalysisValue.Create(ptrSrcData.Data, value.Type);

        // AddressSpaceCast: inherit address space from source.
        // The cast target space is in the type, but the source's actual
        // space may be more specific (e.g., Local from alloca).
        if (value is AddressSpaceCast asCast
            && data.TryGetValue(asCast.Source, out var asSrcData))
            return AnalysisValue.Create(asSrcData.Data, value.Type);

        // Default behavior: use initial info from type
        return AnalysisValue.Create(GetInitialInfo(value), value.Type);
    }
}
