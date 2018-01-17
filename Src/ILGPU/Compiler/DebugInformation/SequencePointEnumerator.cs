// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SequencePointEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Compiler.DebugInformation
{
    /// <summary>
    /// Represents a sequence-point enumerator for methods.
    /// </summary>
    public sealed class SequencePointEnumerator : IDebugInformationEnumerator<SequencePoint>
    {
        #region Constants

        /// <summary>
        /// Represents an empty sequence-point enumerator.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "The empty sequence-point-enumerator is immutable")]
        public static readonly SequencePointEnumerator Empty = new SequencePointEnumerator();

        #endregion

        #region Instance

        private readonly SequencePoint[] points;
        private int currentPoint;

        /// <summary>
        /// Constructs an empty sequence-point enumerator.
        /// </summary>
        private SequencePointEnumerator() { }

        /// <summary>
        /// Constructs a new sequence-point enumerator.
        /// </summary>
        /// <param name="sequencePoints">The wrapped sequence points.</param>
        internal SequencePointEnumerator(SequencePoint[] sequencePoints)
        {
            points = sequencePoints;
            currentPoint = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true iff the current enumerator state points to a valid sequence point.
        /// </summary>
        public bool IsValid => points != null && currentPoint >= 0 && currentPoint < points.Length;

        /// <summary>
        /// Returns the current sequence point.
        /// </summary>
        public SequencePoint? Current => IsValid ? points[currentPoint] : new SequencePoint?();

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve a debug-location string for the current debug location.
        /// </summary>
        /// <param name="debugLocationString">The location string (or null).</param>
        /// <returns>True, iff the requested location string could be resolved.</returns>
        public bool TryGetCurrentDebugLocationString(out string debugLocationString)
        {
            debugLocationString = Current.ToString();
            return Current.HasValue;
        }

        /// <summary>
        /// Tries to move the enumerator to the given offset in bytes.
        /// </summary>
        /// <param name="offset">The target instruction offset in bytes.</param>
        /// <returns></returns>
        public bool MoveTo(int offset)
        {
            if (points == null)
                return false;

            // Move back
            while (
                currentPoint - 1 >= 0 &&
                offset <= points[currentPoint - 1].Offset)
                --currentPoint;

            // Move forward
            while (
                currentPoint + 1 < points.Length &&
                offset >= points[currentPoint + 1].Offset)
                ++currentPoint;

            return IsValid;
        }

        #endregion
    }
}
