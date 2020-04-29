// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MethodScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Text;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a default method scope.
    /// </summary>
    public sealed class MethodScope : IEquatable<MethodScope>
    {
        #region Constants

        /// <summary>
        /// Represents an invalid method scope.
        /// </summary>
        public static readonly MethodScope Invalid = default;

        #endregion

        #region Static

        /// <summary>
        /// Loads local variables from the given scope.
        /// </summary>
        /// <param name="localScope">The parent local scope.</param>
        /// <param name="reader">The reader to read from.</param>
        /// <returns>The array of local variables.</returns>
        private static ImmutableArray<LocalVariable> LoadVariables(
            in LocalScope localScope,
            MetadataReader reader)
        {
            var variables = localScope.GetLocalVariables();
            var result = ImmutableArray.CreateBuilder<LocalVariable>(variables.Count);
            using var enumerator = variables.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var variable = reader.GetLocalVariable(enumerator.Current);
                var variableName = variable.Name.IsNil ? string.Empty :
                    reader.GetString(variable.Name);
                result.Add(new LocalVariable(variable.Index, variableName));
            }
            return result.MoveToImmutable();
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new scope.
        /// </summary>
        /// <param name="localScope">The current local scope.</param>
        /// <param name="reader">The associated metadata reader.</param>
        internal MethodScope(in LocalScope localScope, MetadataReader reader)
        {
            MetadataReader = reader;
            StartOffset = localScope.StartOffset;
            Length = localScope.Length;
            Variables = LoadVariables(localScope, reader);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated metadata reader.
        /// </summary>
        internal MetadataReader MetadataReader { get; }

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
        public ImmutableArray<LocalVariable> Variables { get; }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an unboxed variable enumerator.
        /// </summary>
        /// <returns>An unboxed variable enumerator.</returns>
        public ImmutableArray<LocalVariable>.Enumerator GetEnumerator() =>
            Variables.GetEnumerator();

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given scope is equal to the current scope.
        /// </summary>
        /// <param name="other">The other scope.</param>
        /// <returns>True, if the given scope is equal to the current scope.</returns>
        public bool Equals(MethodScope other) =>
            other != null &&
            StartOffset == other.StartOffset &&
            Length == other.Length;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current scope.
        /// </summary>
        /// <param name="obj">The other sequence object.</param>
        /// <returns>True, if the given object is equal to the current scope.</returns>
        public override bool Equals(object obj) =>
            obj is MethodScope other && Equals(other);

        /// <summary>
        /// Returns the hash code of this scope.
        /// </summary>
        /// <returns>The hash code of this scope.</returns>
        public override int GetHashCode() => StartOffset ^ Length;

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
}
