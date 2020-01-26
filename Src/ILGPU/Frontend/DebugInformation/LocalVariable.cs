// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: LocalVariable.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a local variable in a scope.
    /// </summary>
    public struct LocalVariable : IEquatable<LocalVariable>
    {
        #region Instance

        /// <summary>
        /// Constructs a new local variable.
        /// </summary>
        /// <param name="index">The variable index.</param>
        /// <param name="name">The variable name.</param>
        public LocalVariable(int index, string name)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
            Name = name ?? string.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the referenced local-variable index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Returns the variable name.
        /// </summary>
        public string Name { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given local variable is equal to the current local variable.
        /// </summary>
        /// <param name="other">The other local variable.</param>
        /// <returns>True, iff the given index is equal to the current local variable.</returns>
        public bool Equals(LocalVariable other)
        {
            return other == this;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current local variable.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current local variable.</returns>
        public override bool Equals(object obj)
        {
            if (obj is LocalVariable localVariable)
                return Equals(localVariable);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this index.
        /// </summary>
        /// <returns>The hash code of this index.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1307:Specify StringComparison",
            Justification = "string.GetHashCode(StringComparison) not available in net47")]
        public override int GetHashCode()
        {
            return Index.GetHashCode() ^ Name.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this local variable.
        /// </summary>
        /// <returns>The string representation of this local variable.</returns>
        public override string ToString()
        {
            return Index.ToString() + ": " + Name.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second local variable are the same.
        /// </summary>
        /// <param name="first">The first local variable.</param>
        /// <param name="second">The second local variable.</param>
        /// <returns>True, iff the first and second local variable are the same.</returns>
        public static bool operator ==(LocalVariable first, LocalVariable second)
        {
            return first.Index == second.Index &&
                first.Name == second.Name;
        }

        /// <summary>
        /// Returns true iff the first and second local variable are not the same.
        /// </summary>
        /// <param name="first">The first local variable.</param>
        /// <param name="second">The second local variable.</param>
        /// <returns>True, iff the first and second local variable are not the same.</returns>
        public static bool operator !=(LocalVariable first, LocalVariable second)
        {
            return first.Index != second.Index ||
                first.Name != second.Name;
        }

        #endregion
    }
}
