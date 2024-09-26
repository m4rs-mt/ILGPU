// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: SequencePointEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a sequence-point enumerator for methods.
    /// </summary>
    public struct SequencePointEnumerator : IDebugInformationEnumerator<Location>
    {
        #region Constants

        /// <summary>
        /// Represents an empty sequence-point enumerator.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Security",
            "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "The empty sequence-point-enumerator is immutable")]
        public static readonly SequencePointEnumerator Empty =
            new SequencePointEnumerator(ImmutableArray<SequencePoint>.Empty);

        #endregion

        #region Instance

        private int currentPoint;

        /// <summary>
        /// Constructs an empty sequence-point enumerator.
        /// </summary>
        /// <param name="sequencePoints">The wrapped sequence points.</param>
        internal SequencePointEnumerator(ImmutableArray<SequencePoint> sequencePoints)
        {
            SequencePoints = sequencePoints;
            currentPoint = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated sequence points.
        /// </summary>
        public ImmutableArray<SequencePoint> SequencePoints { get; }

        /// <summary>
        /// Returns true if the current enumerator state points to a valid sequence
        /// point.
        /// </summary>
        public bool IsValid =>
            currentPoint >= 0 && currentPoint < SequencePoints.Length;

        /// <summary>
        /// Returns the current sequence point.
        /// </summary>
        public Location Current =>
            IsValid ? SequencePoints[currentPoint] : Location.Unknown;

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
            while (
                currentPoint - 1 >= 0 &&
                offset <= SequencePoints[currentPoint - 1].Offset)
            {
                --currentPoint;
            }

            // Move forward
            while (
                currentPoint + 1 < SequencePoints.Length &&
                offset >= SequencePoints[currentPoint + 1].Offset)
            {
                ++currentPoint;
            }

            return IsValid;
        }

        #endregion
    }
}
