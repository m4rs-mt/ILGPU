// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodDebugInformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend.DebugInformation;

/// <summary>
/// Represents method debug information.
/// </summary>
readonly struct MethodDebugInformation
{
    #region Static

    /// <summary>
    /// Loads debug information for the given method definition handle.
    /// </summary>
    /// <param name="reader">The parent reader.</param>
    /// <param name="handle">The source information handle.</param>
    /// <returns>Method debug information holding sequence points and scopes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static MethodDebugInformation Load(
        MetadataReader reader,
        MethodDefinitionHandle handle)
    {
        // Gather sequence points
        var debugInformation = reader.GetMethodDebugInformation(handle);
        var infoPoints = debugInformation.GetSequencePoints();
        var sequencePoints = InlineList<SequencePoint>.Create(infoPoints.Count());
        foreach (var sequencePoint in infoPoints)
        {
            if (sequencePoint.IsHidden || sequencePoint.Document.IsNil)
                continue;
            var doc = reader.GetDocument(sequencePoint.Document);
            sequencePoints.Add(new SequencePoint(
                doc.Name.IsNil ? string.Empty : reader.GetString(doc.Name),
                sequencePoint.Offset,
                sequencePoint.StartColumn,
                sequencePoint.EndColumn,
                sequencePoint.StartLine,
                sequencePoint.EndLine));
        }

        // Gather method scopes
        var infoScopes = reader.GetLocalScopes(handle);
        var scopes = InlineList<DebugMethodScope>.Create(infoScopes.Count);
        var enumerator = infoScopes.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var localScope = reader.GetLocalScope(enumerator.Current);
            scopes.Add(DebugMethodScope.Load(localScope, reader));
        }

        // Assemble method debug information
        return new(sequencePoints.AsReadOnlyMemory(), scopes.AsReadOnlyMemory());
    }

    #endregion

    #region Instance

    /// <summary>
    /// All associated sequence points.
    /// </summary>
    private readonly ReadOnlyMemory<SequencePoint> _sequencePoints;

    /// <summary>
    /// All associated method scopes.
    /// </summary>
    private readonly ReadOnlyMemory<DebugMethodScope> _methodScopes;

    /// <summary>
    /// Constructs method debug information.
    /// </summary>
    /// <param name="sequencePoints">
    /// All sequence points for this method.
    /// </param>
    /// <param name="methodScopes">All debug method scopes for this method.</param>
    private MethodDebugInformation(
        ReadOnlyMemory<SequencePoint> sequencePoints,
        ReadOnlyMemory<DebugMethodScope> methodScopes)
    {
        _sequencePoints = sequencePoints;
        _methodScopes = methodScopes;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns all sequence points of the current method.
    /// </summary>
    public ReadOnlySpan<SequencePoint> SequencePoints => _sequencePoints.Span;

    /// <summary>
    /// Returns all method scopes of the current method.
    /// </summary>
    public ReadOnlySpan<DebugMethodScope> MethodScopes => _methodScopes.Span;

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new sequence-point enumerator for the current method.
    /// </summary>
    /// <returns>The created sequence-point enumerator.</returns>
    public SequencePointEnumerator CreateSequencePointEnumerator() =>
        new(_sequencePoints);

    #endregion
}
