// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MethodScope.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Text;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a default method scope.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "This is known to be a scope")]
    public readonly struct MethodScope
        : IEnumerable<LocalVariable>
        , IEquatable<MethodScope>
        , IDebugInformationEnumeratorValue
    {
        #region Nested Types

        /// <summary>
        /// Represents a variable enumerator.
        /// </summary>
        public struct VariableEnumerator : IEnumerator<LocalVariable>
        {
            #region Instance

            private LocalVariableHandleCollection.Enumerator enumerator;

            /// <summary>
            /// Constructs a new variable enumerator.
            /// </summary>
            /// <param name="localVariables">The collection of local variables.</param>
            /// <param name="metadataReader">The assocated metadata reader.</param>
            internal VariableEnumerator(
                LocalVariableHandleCollection localVariables,
                MetadataReader metadataReader)
            {
                LocalVariables = localVariables;
                MetadataReader = metadataReader;
                enumerator = LocalVariables.GetEnumerator();
                Current = default(LocalVariable);
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the assocated collection of local variables.
            /// </summary>
            internal LocalVariableHandleCollection LocalVariables { get; }

            /// <summary>
            /// Returns the assocated metadata reader.
            /// </summary>
            internal MetadataReader MetadataReader { get; }

            /// <summary cref="IEnumerator{T}.Current"/>
            public LocalVariable Current { get; private set; }

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.Reset"/>
            public void Reset()
            {
                enumerator = LocalVariables.GetEnumerator();
            }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                while (enumerator.MoveNext())
                {
                    var variable = MetadataReader.GetLocalVariable(enumerator.Current);
                    if ((variable.Attributes & LocalVariableAttributes.DebuggerHidden) ==
                        LocalVariableAttributes.DebuggerHidden)
                        continue;
                    var variableName = variable.Name.IsNil ? string.Empty :
                        MetadataReader.GetString(variable.Name);
                    Current = new LocalVariable(variable.Index, variableName);
                    return true;
                }
                return false;
            }

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            #endregion
        }

        #endregion

        #region Constants

        /// <summary>
        /// Represents an invalid method scope.
        /// </summary>
        public static readonly MethodScope Invalid = default;

        #endregion

        #region Instance

        private readonly LocalVariableHandleCollection localVariables;

        /// <summary>
        /// Constructs a new scope.
        /// </summary>
        /// <param name="localScope">The current local scope.</param>
        /// <param name="metadataReader">The assocated metadata reader.</param>
        internal MethodScope(
            LocalScope localScope,
            MetadataReader metadataReader)
        {
            MetadataReader = metadataReader;
            StartOffset = localScope.StartOffset;
            Length = localScope.Length;
            localVariables = localScope.GetLocalVariables();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated metadata reader.
        /// </summary>
        internal MetadataReader MetadataReader { get; }

        /// <summary>
        /// Returns true iff the current method scope might represent
        /// a valid scope of an existing method.
        /// </summary>
        public bool IsValid => MetadataReader != null;

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
        /// Returns the number of declared variables.
        /// </summary>
        public int NumVariables => localVariables.Count;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an unboxed variable enumerator.
        /// </summary>
        /// <returns>An unboxed variable enumerator.</returns>
        public VariableEnumerator GetEnumerator()
        {
            return new VariableEnumerator(localVariables, MetadataReader);
        }

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<LocalVariable> IEnumerable<LocalVariable>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given scope is equal to the current scope.
        /// </summary>
        /// <param name="other">The other scope.</param>
        /// <returns>True, iff the given scope is equal to the current scope.</returns>
        public bool Equals(MethodScope other)
        {
            return other == this;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current scope.
        /// </summary>
        /// <param name="obj">The other sequence object.</param>
        /// <returns>True, iff the given object is equal to the current scope.</returns>
        public override bool Equals(object obj)
        {
            if (obj is SequencePoint other)
                return Equals(other);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this scope.
        /// </summary>
        /// <returns>The hash code of this scope.</returns>
        public override int GetHashCode()
        {
            return StartOffset ^ Length;
        }

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
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
                builder.AppendLine(enumerator.Current.ToString());
            return builder.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first scope and the second scope are the same.
        /// </summary>
        /// <param name="first">The first scope.</param>
        /// <param name="second">The second scope.</param>
        /// <returns>True, iff the first and second the scope are the same.</returns>
        public static bool operator ==(MethodScope first, MethodScope second)
        {
            return first.StartOffset == second.StartOffset &&
                first.Length == second.Length;
        }

        /// <summary>
        /// Returns true iff the first scope and the second scope are not the same.
        /// </summary>
        /// <param name="first">The first scope.</param>
        /// <param name="second">The second scope.</param>
        /// <returns>True, iff the first and second the scope are not the same.</returns>
        public static bool operator !=(MethodScope first, MethodScope second)
        {
            return first.StartOffset != second.StartOffset ||
                first.Length != second.Length;
        }

        #endregion
    }
}
