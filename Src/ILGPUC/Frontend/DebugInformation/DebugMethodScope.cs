// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugMethodScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPUC.Frontend.DebugInformation;

/// <summary>
/// Represents a default method scope.
/// </summary>
sealed class DebugMethodScope : IEquatable<DebugMethodScope>
{
    #region Static

    /// <summary>
    /// Loads a new scope from the given reader.
    /// </summary>
    /// <param name="localScope">The current local scope.</param>
    /// <param name="reader">The associated metadata reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static DebugMethodScope Load(in LocalScope localScope, MetadataReader reader)
    {
        var localVariables = localScope.GetLocalVariables();
        var variables = InlineList<LocalVariable>.Create(localVariables.Count);
        var enumerator = localVariables.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var variable = reader.GetLocalVariable(enumerator.Current);
            var variableName = variable.Name.IsNil ? string.Empty :
                reader.GetString(variable.Name);
            variables.Add(new LocalVariable(variable.Index, variableName));
        }

        return new(localScope, variables.AsReadOnlyMemory());
    }

    #endregion

    #region Instance

    private readonly ReadOnlyMemory<LocalVariable> _variables;

    /// <summary>
    /// Constructs a new scope.
    /// </summary>
    /// <param name="localScope">The current local scope.</param>
    /// <param name="variables">The associated variables.</param>
    private DebugMethodScope(
        in LocalScope localScope,
        ReadOnlyMemory<LocalVariable> variables)
    {
        StartOffset = localScope.StartOffset;
        Length = localScope.Length;
        _variables = variables;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the start offset of the current scope.
    /// </summary>
    public int StartOffset { get; }

    /// <summary>
    /// Returns the end offset of the current scope.
    /// </summary>
    public int EndOffset => StartOffset + Length;

    /// <summary>
    /// Returns the length of the current scope.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Returns all local variables.
    /// </summary>
    public ReadOnlySpan<LocalVariable> Variables => _variables.Span;

    #endregion

    #region IEnumerable

    /// <summary>
    /// Returns an unboxed variable enumerator.
    /// </summary>
    /// <returns>An unboxed variable enumerator.</returns>
    public ReadOnlySpan<LocalVariable>.Enumerator GetEnumerator() =>
        Variables.GetEnumerator();

    #endregion

    #region IEquatable

    /// <summary>
    /// Returns true if the given scope is equal to the current scope.
    /// </summary>
    /// <param name="other">The other scope.</param>
    /// <returns>True, if the given scope is equal to the current scope.</returns>
    public bool Equals(DebugMethodScope? other) =>
        other is not null &&
        StartOffset == other.StartOffset &&
        Length == other.Length;

    #endregion

    #region Object

    /// <summary>
    /// Returns true if the given object is equal to the current scope.
    /// </summary>
    /// <param name="obj">The other sequence object.</param>
    /// <returns>True, if the given object is equal to the current scope.</returns>
    public override bool Equals(object? obj) =>
        obj is DebugMethodScope other && Equals(other);

    /// <summary>
    /// Returns the hash code of this scope.
    /// </summary>
    /// <returns>The hash code of this scope.</returns>
    public override int GetHashCode() => HashCode.Combine(StartOffset, Length);

    /// <summary>
    /// Returns the string representation of this scope.
    /// </summary>
    /// <returns>The string representation of this scope.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append('[');
        builder.Append(StartOffset);
        builder.Append(", ");
        builder.Append(EndOffset);
        builder.Append("]: ");
        foreach (var variable in Variables)
            builder.AppendLine(variable.ToString());
        return builder.ToString();
    }

    #endregion
}
