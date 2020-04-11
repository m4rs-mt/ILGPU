// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: VariableRef.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPU.Frontend
{
    /// <summary>
    /// The type of a single variable reference.
    /// </summary>
    enum VariableRefType
    {
        /// <summary>
        /// Represents a reference to a function argument.
        /// </summary>
        Argument,

        /// <summary>
        /// Represents a reference to a local variable.
        /// </summary>
        Local,

        /// <summary>
        /// Represents a reference to a stack slot.
        /// </summary>
        Stack,

        /// <summary>
        /// Represents an abstract memory monad.
        /// </summary>
        Memory,
    }

    /// <summary>
    /// Represents a single variable.
    /// </summary>
    readonly struct VariableRef : IEquatable<VariableRef>
    {
        #region Constants

        /// <summary>
        /// Represents a reference to a memory monad.
        /// </summary>
        public static readonly VariableRef Memory =
            new VariableRef(0, VariableRefType.Memory);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new variable entry. 
        /// </summary>
        /// <param name="index">Index of the variable.</param>
        /// <param name="refType">Type of this variable reference.</param>
        public VariableRef(int index, VariableRefType refType)
        {
            Debug.Assert(index >= 0, "Invalid index");
            Index = index;
            RefType = refType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the index of the variable.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Returns the variable-reference type.
        /// </summary>
        public VariableRefType RefType { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given variable ref is equal to the current one.
        /// </summary>
        /// <param name="other">The other variable reference.</param>
        /// <returns>
        /// True, if the given variable ref is equal to the current one.
        /// </returns>
        public bool Equals(VariableRef other) => other == this;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given variable ref is equal to the current one.
        /// </returns>
        public override bool Equals(object obj) =>
            obj is VariableRef variableRef && variableRef == this;

        /// <summary>
        /// Returns the hash code of this variable reference.
        /// </summary>
        /// <returns>The hash code of this variable reference.</returns>
        public override int GetHashCode() =>
            Index.GetHashCode() ^ RefType.GetHashCode();

        /// <summary>
        /// Returns the string representation of this variable.
        /// </summary>
        /// <returns>The string representation of this variable.</returns>
        public override string ToString() => $"{Index} [{RefType}]";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if both variable references represent the same variable.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference.</param>
        /// <returns>
        /// True, if both variable references represent the same variable.
        /// </returns>
        public static bool operator ==(VariableRef first, VariableRef second) =>
            first.Index == second.Index && first.RefType == second.RefType;

        /// <summary>
        /// Returns true if both variable references do not represent the same variable.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The second reference.</param>
        /// <returns>
        /// True, if both variable references do not represent the same variable.
        /// </returns>
        public static bool operator !=(VariableRef first, VariableRef second) =>
            first.Index != second.Index || first.RefType != second.RefType;

        #endregion
    }
}
