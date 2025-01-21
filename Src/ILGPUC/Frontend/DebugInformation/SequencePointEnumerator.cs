// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SequencePointEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System;

namespace ILGPUC.Frontend.DebugInformation;

/// <summary>
/// Represents a sequence-point enumerator for methods.
/// </summary>
/// <param name="sequencePoints">The wrapped sequence points.</param>
sealed class SequencePointEnumerator(
    ReadOnlyMemory<SequencePoint> sequencePoints) : IDebugInformationEnumerator<Location>
{
    #region Constants

    /// <summary>
    /// Represents an empty sequence-point enumerator.
    /// </summary>
    public static readonly SequencePointEnumerator Empty = new(
        ReadOnlyMemory<SequencePoint>.Empty);

    #endregion

    #region Instance

    private int _currentPoint;

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated sequence points.
    /// </summary>
    public ReadOnlyMemory<SequencePoint> SequencePoints { get; } = sequencePoints;

    /// <summary>
    /// Returns true if the current enumerator state points to a valid sequence
    /// point.
    /// </summary>
    public bool IsValid =>
        _currentPoint >= 0 && _currentPoint < SequencePoints.Length;

    /// <summary>
    /// Returns the current sequence point.
    /// </summary>
    public Location Current =>
        IsValid ? SequencePoints.Span[_currentPoint] : Location.Unknown;

    #endregion

    #region Methods

    /// <summary>
    /// Tries to move the enumerator to the given offset in bytes.
    /// </summary>
    /// <param name="offset">The target instruction offset in bytes.</param>
    /// <returns>True, is the next sequence point is valid.</returns>
    public bool MoveTo(int offset)
    {
        if (SequencePoints.Length < 1)
            return false;

        // Move back
        var sequencePoints = SequencePoints.Span;
        while (
            _currentPoint - 1 >= 0 &&
            offset <= sequencePoints[_currentPoint - 1].Offset)
        {
            --_currentPoint;
        }

        // Move forward
        while (
            _currentPoint + 1 < SequencePoints.Length &&
            offset >= sequencePoints[_currentPoint + 1].Offset)
        {
            ++_currentPoint;
        }

        return IsValid;
    }

    #endregion
}
