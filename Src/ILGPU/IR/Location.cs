// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Location.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.IR
{
    /// <summary>
    /// An abstract source location.
    /// </summary>
    public abstract class Location : ILocation
    {
        #region Constants

        /// <summary>
        /// Represents an unknown location.
        /// </summary>
        public static Location Unknown { get; } = new UnknownLocation();

        /// <summary>
        /// Represents no location.
        /// </summary>
        public static Location Nowhere { get; } = new NoLocation();

        #endregion

        #region Nested Types

        /// <summary>
        /// An unknown location implementation.
        /// </summary>
        private sealed class UnknownLocation : Location
        {
            /// <summary>
            /// Returns the original message.
            /// </summary>
            public override string FormatErrorMessage(string message) => message;
        }

        /// <summary>
        /// A no location implementation.
        /// </summary>
        private sealed class NoLocation : Location
        {
            /// <summary>
            /// Returns the original message.
            /// </summary>
            public override string FormatErrorMessage(string message) => message;
        }

        #endregion

        #region Static

        /// <summary>
        /// Merges two locations into one.
        /// </summary>
        /// <param name="start">The start location.</param>
        /// <param name="end">The end location.</param>
        /// <returns>The merged location.</returns>
        [SuppressMessage(
            "Style",
            "IDE0046:Convert to conditional expression",
            Justification = "Will be more difficult to read")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Location Merge(Location start, Location end)
        {
            Debug.Assert(start != null && end != null, "Invalid locations");
            if (!start.IsKnown)
                return end;
            if (!end.IsKnown)
                return start;

            return start is FileLocation startFile &&
                end is FileLocation endFile
                ? startFile.Merge(endFile)
                : start;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this location is a known location.
        /// </summary>
        public bool IsKnown => this != Unknown;

        #endregion

        #region Methods

        /// <summary>
        /// Formats an error message to include specific location information.
        /// </summary>
        /// <param name="message">The source error message.</param>
        /// <returns>The formatted error message.</returns>
        public abstract string FormatErrorMessage(string message);

        #endregion
    }

    /// <summary>
    /// A location that is based on file information.
    /// </summary>
    public class FileLocation : Location
    {
        #region Instance

        /// <summary>
        /// Constructs a new file location.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="startColumn">The start column.</param>
        /// <param name="endColumn">The end column.</param>
        /// <param name="startLine">The start line.</param>
        /// <param name="endLine">The end line.</param>
        public FileLocation(
            string fileName,
            int startColumn,
            int endColumn,
            int startLine,
            int endLine)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));
            if (startColumn < 0)
                throw new ArgumentOutOfRangeException(nameof(startColumn));
            if (endColumn < 0)
                throw new ArgumentOutOfRangeException(nameof(endColumn));
            if (startLine < 0)
                throw new ArgumentOutOfRangeException(nameof(startLine));
            if (endLine < 0)
                throw new ArgumentOutOfRangeException(nameof(endLine));

            FileName = fileName;
            StartColumn = startColumn;
            EndColumn = endColumn;
            StartLine = startLine;
            EndLine = endLine;
        }

        #endregion

        #region Properties

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

        #region Methods

        /// <summary>
        /// Formats the given message to include detailed file location information.
        /// </summary>
        public override string FormatErrorMessage(string message) =>
            StartLine == EndLine
            ? StartColumn == EndColumn
                ? string.Format(
                    ErrorMessages.LocationFileMessageL1C1,
                        message,
                        FileName,
                        StartLine,
                        StartColumn)
                : string.Format(
                    ErrorMessages.LocationFileMessageL1C2,
                        message,
                        FileName,
                        StartLine,
                        StartColumn,
                        EndColumn)
            : string.Format(
                ErrorMessages.LocationFileMessageL2C2,
                message,
                FileName,
                StartLine,
                StartColumn,
                EndLine,
                EndColumn);

        /// <summary>
        /// Merges this location with the other one.
        /// </summary>
        /// <param name="other">The other one to merge with.</param>
        /// <returns>The merged location.</returns>
        protected internal virtual FileLocation Merge(FileLocation other) =>
            new FileLocation(
                FileName,
                Math.Min(StartColumn, other.StartColumn),
                Math.Max(EndColumn, other.EndColumn),
                Math.Min(StartLine, other.StartLine),
                Math.Max(EndLine, other.EndLine));

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current location.
        /// </summary>
        /// <param name="obj">The other location.</param>
        /// <returns>
        /// True, if the given object is equal to the current location.
        /// </returns>
        public override bool Equals(object obj) =>
            obj is FileLocation other &&
            FileName == other.FileName &&
            StartColumn == other.StartColumn &&
            EndColumn == other.EndColumn &&
            StartLine == other.StartLine &&
            EndLine == other.EndLine;

        /// <summary>
        /// Returns the hash code of this sequence point.
        /// </summary>
        /// <returns>The hash code of this sequence point.</returns>
        public override int GetHashCode() =>
            StartLine ^ EndLine ^ StartColumn;

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

    /// <summary>
    /// A collection of locations than is manipulated as a stack.
    /// </summary>
    public sealed class CompilationStackLocation : Location
    {
        #region Instance

        /// <summary>
        /// Constructs a new stack location.
        /// </summary>
        /// <param name="location">The initial location.</param>
        public CompilationStackLocation(Location location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));
            Stack = ImmutableStack.Create(location);
        }

        /// <summary>
        /// Constructs a new stack location.
        /// </summary>
        /// <param name="locations">The initial locations.</param>
        internal CompilationStackLocation(ImmutableStack<Location> locations)
        {
            if (locations == null || locations.IsEmpty)
                throw new ArgumentNullException(nameof(locations));
            Stack = locations;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the stack of locations.
        /// </summary>
        private ImmutableStack<Location> Stack { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Formats the given message to include detailed location information.
        /// </summary>
        public override string FormatErrorMessage(string message)
        {
            var builder = new StringBuilder(message);
            foreach (var location in Stack)
            {
                var line = location.FormatErrorMessage(string.Empty);
                if (!string.IsNullOrEmpty(line))
                {
                    builder.AppendLine();
                    builder.Append(ErrorMessages.LocationCompilationStackLinePrefix);
                    builder.Append(line);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Merges this location with the other one.
        /// </summary>
        /// <param name="other">The other one to merge with.</param>
        /// <returns>The merged location.</returns>
        public CompilationStackLocation Append(Location other) =>
            new CompilationStackLocation(Stack.Push(other));

        /// <summary>
        /// Returns the most recent location of the given type from the compilation
        /// stack, if any.
        /// </summary>
        /// <param name="location">Filled in with the location found.</param>
        /// <returns>True if this compilation stack has the location type.</returns>
        public bool TryGetLocation<T>(out T location)
            where T : Location
        {
            foreach (var loc in Stack.Reverse())
            {
                if (loc is T typedLocation)
                {
                    location = typedLocation;
                    return true;
                }
            }

            location = null;
            return false;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current location.
        /// </summary>
        /// <param name="obj">The other location.</param>
        /// <returns>
        /// True, if the given object is equal to the current location.
        /// </returns>
        public override bool Equals(object obj) =>
            obj is CompilationStackLocation other &&
            Stack == other.Stack;

        /// <summary>
        /// Returns the hash code of this sequence point.
        /// </summary>
        /// <returns>The hash code of this sequence point.</returns>
        public override int GetHashCode() =>
            Stack.GetHashCode();

        /// <summary>
        /// Returns the location information of this sequence point.
        /// </summary>
        /// <returns>
        /// The location information string that represents this sequence point.
        /// </returns>
        public override string ToString() =>
            $"{FormatErrorMessage(string.Empty)})";

        #endregion
    }
}
