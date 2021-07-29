// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SequencePoint.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using System;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a single sequence point of an instruction.
    /// </summary>
    public sealed class SequencePoint : FileLocation
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
            : base(
                fileName,
                startColumn,
                endColumn,
                startLine,
                endLine)
        {
            Offset = offset;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated offset (optional)
        /// </summary>
        public int Offset { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Merges this sequence point with the other file location.
        /// </summary>
        protected internal override FileLocation Merge(FileLocation other) =>
            other is SequencePoint sequencePoint
                ? new SequencePoint(
                    FileName,
                    Math.Min(Offset, sequencePoint.Offset),
                    Math.Min(StartColumn, sequencePoint.StartColumn),
                    Math.Max(EndColumn, sequencePoint.EndColumn),
                    Math.Min(StartLine, sequencePoint.StartLine),
                    Math.Max(EndLine, sequencePoint.EndLine))
                : base.Merge(other);

        #endregion

        #region Object

        /// <summary>
        /// Returns the location information of this sequence point.
        /// </summary>
        /// <returns>
        /// The location information string that represents this sequence point.
        /// </returns>
        public override string ToString() =>
            $"{FileName}({StartLine}, {StartColumn}, {EndLine}, {EndColumn})";

        #endregion
    }
}
