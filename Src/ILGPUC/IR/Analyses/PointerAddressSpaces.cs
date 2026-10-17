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
    /// Creates a new pointer analysis instance.
    /// </summary>
    /// <param name="flags">The analysis flags.</param>
    /// <returns>The created analysis instance.</returns>
    public static PointerAddressSpaces Create(AnalysisFlags flags) => new(flags);

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

        // Globals live in the address space of their type (shared, local or
        // constant memory)
        var globalMap = rootMethod.Module.CreateGlobalMap<
            AnalysisValue<AddressSpaceInfo>>();
        foreach (var global in rootMethod.Module.Globals)
        {
            globalMap.Add(
                global,
                AddressSpaceInfo.AnalysisValueFromType(global.Type));
        }

        // Kernel parameters point to global memory
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
    /// Pure values are analyzed: address chains (casts, element and field
    /// addresses, views) inherit the address spaces of their sources.
    /// </summary>
    protected override bool AnalyzesPureValues => true;

    /// <summary>
    /// Returns the initial value of all values that are not seeded explicitly.
    /// </summary>
    /// <remarks>
    /// When ignoring the generic address space, every value starts without any
    /// address space and receives its information from <see cref="Analyze"/>
    /// alone. This keeps casts to the generic address space and generic phi or
    /// parameter types from polluting values whose sources are all known.
    /// Otherwise, every value starts with the address spaces of its type.
    /// </remarks>
    protected override AnalysisValue<AddressSpaceInfo>?
        TryProvideForType(TypeValue type) =>
        HasFlags(AnalysisFlags.IgnoreGenericAddressSpace)
        ? AnalysisValue.Create(default(AddressSpaceInfo), type)
        : AddressSpaceInfo.AnalysisValueFromType(type);

    /// <summary>
    /// Returns the unified address-space flags.
    /// </summary>
    protected override AddressSpaceInfo Merge(
        AddressSpaceInfo first,
        AddressSpaceInfo second) =>
        AddressSpaceInfo.Merge(first, second);

    /// <summary>
    /// Returns the current analysis value of the given operand.
    /// </summary>
    private AnalysisValue<AddressSpaceInfo> GetOperand(
        Value operand,
        GlobalValueMap<AnalysisValue<AddressSpaceInfo>> data) =>
        data.TryGetValue(operand, out var result)
        ? result
        : CreateInitialValue(operand.Type);

    /// <summary>
    /// Returns the information of the given field of a structure value.
    /// </summary>
    private static AddressSpaceInfo GetField(
        in AnalysisValue<AddressSpaceInfo> value,
        int index) =>
        value.IsStructure && index < value.NumFields
        ? value[index]
        : value.Data;

    /// <summary>
    /// Creates a structure analysis value from the given field information.
    /// </summary>
    private static AnalysisValue<AddressSpaceInfo> CreateStructure(
        AddressSpaceInfo[] fields)
    {
        var data = new AddressSpaceInfo();
        foreach (var field in fields)
            data = AddressSpaceInfo.Merge(data, field);
        return new AnalysisValue<AddressSpaceInfo>(data, fields);
    }

    /// <summary>
    /// Analyzes a field load from a structure value.
    /// </summary>
    private AnalysisValue<AddressSpaceInfo> AnalyzeGetField(
        GetField getField,
        GlobalValueMap<AnalysisValue<AddressSpaceInfo>> data)
    {
        var source = GetOperand(getField.Source, data);
        var span = getField.FieldSpan;
        if (getField.Type is not StructureType structureType)
            return AnalysisValue.Create(GetField(source, span.Index), getField.Type);

        var fields = new AddressSpaceInfo[structureType.NumFields];
        for (int i = 0; i < fields.Length; ++i)
            fields[i] = GetField(source, span.Index + i);
        return CreateStructure(fields);
    }

    /// <summary>
    /// Analyzes a field store into a structure value.
    /// </summary>
    private AnalysisValue<AddressSpaceInfo> AnalyzeSetField(
        SetField setField,
        GlobalValueMap<AnalysisValue<AddressSpaceInfo>> data)
    {
        var source = GetOperand(setField.Source, data);
        var value = GetOperand(setField.Value, data);
        var span = setField.FieldSpan;

        var fields = new AddressSpaceInfo[setField.StructureType.NumFields];
        for (int i = 0; i < fields.Length; ++i)
        {
            fields[i] = i >= span.Index && i < span.Index + span.Span
                ? GetField(value, i - span.Index)
                : GetField(source, i);
        }
        return CreateStructure(fields);
    }

    /// <summary>
    /// Analyzes a structure value built from its (flattened) fields.
    /// </summary>
    private AnalysisValue<AddressSpaceInfo> AnalyzeStructure(
        StructureValue structureValue,
        GlobalValueMap<AnalysisValue<AddressSpaceInfo>> data)
    {
        var operands = structureValue.Values;
        var fields = new AddressSpaceInfo[structureValue.StructureType.NumFields];
        for (int i = 0; i < fields.Length; ++i)
        {
            fields[i] = i < operands.Length
                ? GetOperand(operands[i], data).Data
                : default;
        }
        return CreateStructure(fields);
    }

    /// <summary>
    /// Analyzes a value to compute its address space information.
    /// </summary>
    /// <remarks>
    /// Values that derive an address from another one point into the address
    /// spaces of their source. All other values point into the address spaces
    /// of their types, including the generic one: a pointer loaded from memory
    /// or returned from a call may point anywhere.
    /// </remarks>
    protected override AnalysisValue<AddressSpaceInfo> Analyze(
        Value value,
        GlobalValueMap<AnalysisValue<AddressSpaceInfo>> data)
    {
        switch (value)
        {
            case PhiValue phiValue:
                return MergePhiValue(phiValue, data);

            // A cast into a specific address space asserts it; a cast into the
            // generic one keeps pointing into the source's address spaces.
            case AddressSpaceCast cast:
                return cast.TargetAddressSpace != MemoryAddressSpace.Generic
                    ? AddressSpaceInfo.AnalysisValueFromType(cast.Type)
                    : AnalysisValue.Create(
                        GetOperand(cast.Source, data).Data,
                        cast.Type);

            // Address computations and views preserve the address space
            case PointerCast:
            case ViewCast:
            case LoadElementAddress:
            case LoadFieldAddress:
            case NewView:
            case SubView:
            case BaseAlignOperationValue:
                return AnalysisValue.Create(
                    GetOperand(value.Values[0], data).Data,
                    value.Type);

            case Predicate predicate:
                return Merge(
                    GetOperand(predicate.TrueValue, data),
                    GetOperand(predicate.FalseValue, data));

            case StructureValue structureValue:
                return AnalyzeStructure(structureValue, data);
            case GetField getField:
                return AnalyzeGetField(getField, data);
            case SetField setField:
                return AnalyzeSetField(setField, data);

            // A null pointer is compatible with every address space
            case NullValue:
                return AnalysisValue.Create(default(AddressSpaceInfo), value.Type);

            default:
                return AddressSpaceInfo.AnalysisValueFromType(value.Type);
        }
    }
}
