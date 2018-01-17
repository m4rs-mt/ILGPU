// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SequencePoint.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Compiler.DebugInformation
{
    /// <summary>
    /// Represents a single sequence point of an instruction.
    /// </summary>
    public struct SequencePoint : IEquatable<SequencePoint>
    {
        #region Instance

        /// <summary>
        /// Constructs a new sequence point.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="offset">The byte offset.</param>
        /// <param name="startColumn">The start column.</param>
        /// <param name="endColumn">The end column.</param>
        /// <param name="startLine">The start line.</param>
        /// <param name="endLine">The end line.</param>
        public SequencePoint(
            string fileName,
            int offset,
            int startColumn,
            int endColumn,
            int startLine,
            int endLine)
        {
            FileName = fileName;
            Offset = offset;
            StartColumn = startColumn;
            EndColumn = endColumn;
            StartLine = startLine;
            EndLine = endLine;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true iff the current sequence point might represent
        /// a valid point within a file.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(FileName) &&
            Offset >= 0 &&
            StartColumn > 0 &&
            EndColumn > 0 &&
            StartLine > 0 &&
            EndLine > 0;

        /// <summary>
        /// Returns the associated offset.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Return the associated file name.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Returns the start column.
        /// </summary>
        public int StartColumn { get; }

        /// <summary>
        /// Returns the end column.
        /// </summary>
        public int EndColumn { get; }

        /// <summary>
        /// Returns the start line.
        /// </summary>
        public int StartLine { get; }

        /// <summary>
        /// Returns the end line.
        /// </summary>
        public int EndLine { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given sequence point is equal to the current sequence point.
        /// </summary>
        /// <param name="other">The other sequence point.</param>
        /// <returns>True, iff the given sequence point is equal to the current sequence point.</returns>
        public bool Equals(SequencePoint other)
        {
            return other == this;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current sequence point.
        /// </summary>
        /// <param name="obj">The other sequence object.</param>
        /// <returns>True, iff the given object is equal to the current sequence point.</returns>
        public override bool Equals(object obj)
        {
            if (obj is SequencePoint other)
                return Equals(other);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this sequence point.
        /// </summary>
        /// <returns>The hash code of this sequence point.</returns>
        public override int GetHashCode()
        {
            return StartColumn ^ EndColumn ^ StartLine ^ EndLine;
        }

        /// <summary>
        /// Returns the location information of this sequence point in VS format.
        /// </summary>
        /// <returns>The location information string that represents this sequence point.</returns>
        public string ToVisualStudioErrorString()
        {
            if (IsValid)
                return $"{FileName}({StartLine}, {StartColumn}, {EndLine}, {EndColumn})";
            return "<Unknown>";
        }

        /// <summary>
        /// Returns the location information of this sequence point.
        /// </summary>
        /// <returns>The location information string that represents this sequence point.</returns>
        public override string ToString()
        {
            if (IsValid)
                return $"{FileName}: ({StartLine}, {StartColumn}) - ({EndLine}, {EndColumn})";
            return "<Unknown>";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first sequence point and the second sequence point are the same.
        /// </summary>
        /// <param name="first">The first sequence point.</param>
        /// <param name="second">The second sequence point.</param>
        /// <returns>True, iff the first and the second sequence point are the same.</returns>
        public static bool operator ==(SequencePoint first, SequencePoint second)
        {
            return first.Offset == second.Offset;
        }

        /// <summary>
        /// Returns true iff the first sequence point and the second sequence point are not the same.
        /// </summary>
        /// <param name="first">The first sequence point.</param>
        /// <param name="second">The second sequence point.</param>
        /// <returns>True, iff the first and the second sequence point are not the same.</returns>
        public static bool operator !=(SequencePoint first, SequencePoint second)
        {
            return first.Offset != second.Offset;
        }

        #endregion
    }
}
