// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AnalysisValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// An analysis value to encapsulate static program analysis values.
/// </summary>
/// <typeparam name="T">The underlying element type.</typeparam>
/// <remarks>
/// This type encapsulates a general <see cref="Data"/> element that represents
/// accumulated analysis information for the whole value. For structure types, it
/// stores fine-grained information about each field element to improve precision.
/// </remarks>
/// <remarks>
/// Constructs a new analysis value for a structure type.
/// </remarks>
/// <param name="data">The accumulated data value.</param>
/// <param name="fieldData">Per-field data values (null for scalars).</param>
readonly struct AnalysisValue<T>(T data, ReadOnlyMemory<T>? fieldData = null) :
    IEquatable<AnalysisValue<T>>
    where T : IEquatable<T>
{
    private readonly ReadOnlyMemory<T> _fieldData = fieldData ?? ReadOnlyMemory<T>.Empty;

    /// <summary>
    /// Returns the underlying accumulated data value.
    /// </summary>
    public T Data { get; } = data;

    /// <summary>
    /// Returns the number of fields (0 for scalars).
    /// </summary>
    public int NumFields => _fieldData.Length;

    /// <summary>
    /// Returns true if this is a scalar value (no fields).
    /// </summary>
    public bool IsScalar => _fieldData.Length == 0;

    /// <summary>
    /// Returns true if this is a structure value (has fields).
    /// </summary>
    public bool IsStructure => _fieldData.Length > 0;

    /// <summary>
    /// Returns the i-th field data element.
    /// </summary>
    /// <param name="index">The field index.</param>
    /// <returns>The field data value.</returns>
    public T this[int index] => _fieldData.Span[index];

    /// <summary>
    /// Clones the internal field data array into a new one.
    /// </summary>
    /// <returns>The cloned field data array (or null for scalars).</returns>
    public ReadOnlyMemory<T> CloneFieldData() =>
        IsScalar ? ReadOnlyMemory<T>.Empty : _fieldData.Span.ToArray();

    /// <summary>
    /// Returns true if the given value is equal to the current one.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>True, if the given value is equal to the current one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Equals(AnalysisValue<T> other)
    {
        if (!Data.Equals(other.Data) || NumFields != other.NumFields)
            return false;

        for (int i = 0, e = _fieldData.Length; i < e; ++i)
        {
            if (!_fieldData.Span.GetItemRef(i).Equals(
                other._fieldData.Span.GetItemRef(i)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns true if the given object is equal to the current value.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True, if the given object is equal to the current value.</returns>
    public override bool Equals(object? obj) =>
        obj is AnalysisValue<T> value && Equals(value);

    /// <summary>
    /// Returns the hash code of this value.
    /// </summary>
    /// <returns>The hash code of this value.</returns>
    public override int GetHashCode() =>
        HashCode.Combine(Data.GetHashCode(), NumFields);

    /// <summary>
    /// Returns the string representation of this value.
    /// </summary>
    /// <returns>The string representation of this value.</returns>
    public override string? ToString() =>
        IsScalar
        ? $"{Data} [{string.Join(", ", _fieldData)}]"
        : Data.ToString();

    /// <summary>
    /// Returns true if the first and second value are the same.
    /// </summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns>True, if the first and second value are the same.</returns>
    public static bool operator ==(AnalysisValue<T> first, AnalysisValue<T> second) =>
        first.Equals(second);

    /// <summary>
    /// Returns true if the first and second value are not the same.
    /// </summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns>True, if the first and second value are not the same.</returns>
    public static bool operator !=(AnalysisValue<T> first, AnalysisValue<T> second) =>
        !(first == second);
}

/// <summary>
/// Helper methods for <see cref="AnalysisValue{T}"/>.
/// </summary>
static class AnalysisValue
{
    /// <summary>
    /// Creates a new analysis value for the given type.
    /// </summary>
    /// <param name="data">The data value.</param>
    /// <param name="type">The type node.</param>
    /// <returns>The created analysis value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static AnalysisValue<T> Create<T>(T data, TypeValue type)
        where T : IEquatable<T>
    {
        if (type is StructureType structureType)
        {
            var fieldData = new T[structureType.NumFields];
            for (int i = 0, e = fieldData.Length; i < e; ++i)
                fieldData[i] = data;
            return new AnalysisValue<T>(data, fieldData);
        }
        return new AnalysisValue<T>(data);
    }
}
