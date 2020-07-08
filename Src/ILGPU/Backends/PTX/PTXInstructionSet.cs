// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXInstructionSet.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a PTX ISA (Instruction Set Architecture).
    /// </summary>
    [DebuggerDisplay("ISA {Major}.{Minor}")]
    public readonly struct PTXInstructionSet :
        IEquatable<PTXInstructionSet>,
        IComparable<PTXInstructionSet>
    {
        #region Constants

        /// <summary>
        /// The 3.0 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_30 = new PTXInstructionSet(3, 0);

        /// <summary>
        /// The 3.1 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_31 = new PTXInstructionSet(3, 1);

        /// <summary>
        /// The 3.2 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_32 = new PTXInstructionSet(3, 2);

        /// <summary>
        /// The 4.0 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_40 = new PTXInstructionSet(4, 0);

        /// <summary>
        /// The 4.1 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_41 = new PTXInstructionSet(4, 1);

        /// <summary>
        /// The 4.2 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_42 = new PTXInstructionSet(4, 2);

        /// <summary>
        /// The 4.3 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_43 = new PTXInstructionSet(4, 3);

        /// <summary>
        /// The 5.0 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_50 = new PTXInstructionSet(5, 0);

        /// <summary>
        /// The 6.0 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_60 = new PTXInstructionSet(6, 0);

        /// <summary>
        /// The 6.1 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_61 = new PTXInstructionSet(6, 1);

        /// <summary>
        /// The 6.2 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_62 = new PTXInstructionSet(6, 2);

        /// <summary>
        /// The 6.3 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_63 = new PTXInstructionSet(6, 3);

        /// <summary>
        /// The 6.4 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_64 = new PTXInstructionSet(6, 4);

        /// <summary>
        /// The 6.5 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_65 = new PTXInstructionSet(6, 5);

        /// <summary>
        /// The 7.0 ISA.
        /// </summary>
        public static readonly PTXInstructionSet ISA_70 = new PTXInstructionSet(7, 0);

        #endregion

        #region Instance

        private PTXInstructionSet(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given instruction set is equal to this instruction set.
        /// </summary>
        /// <param name="other">The other instruction set.</param>
        /// <returns>
        /// True, if the given instruction set is equal to this instruction set.
        /// </returns>
        public bool Equals(PTXInstructionSet other) => this == other;

        #endregion

        #region IComparable

        /// <summary>
        /// Compares this instruction set to the given one.
        /// </summary>
        /// <param name="other">The object to compare to.</param>
        /// <returns>The comparison result.</returns>
        public int CompareTo(PTXInstructionSet other)
        {
            if (this < other)
                return -1;
            else if (this > other)
                return 1;
            Debug.Assert(this == other);
            return 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The major version
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// The minor version
        /// </summary>
        public int Minor { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to this instruction set.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True,
        /// if the given object is equal to this instruction set.</returns>
        public override bool Equals(object obj) =>
            obj is PTXInstructionSet instructionSet && instructionSet == this;

        /// <summary>
        /// Returns the hash code of this instruction set.
        /// </summary>
        /// <returns>The hash code of this instruction set.</returns>
        public override int GetHashCode() => Major.GetHashCode() ^ Minor.GetHashCode();

        /// <summary>
        /// Returns the string representation of the instruction set.
        /// </summary>
        /// <returns>The string representation of the instruction set.</returns>
        public override string ToString() => $"{Major}.{Minor}";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and the second instruction set are the same.
        /// </summary>
        /// <param name="first">The first instruction set.</param>
        /// <param name="second">The second instruction set.</param>
        /// <returns>
        /// True, if the first and the second instruction set are the same.
        /// </returns>
        public static bool operator ==(
            PTXInstructionSet first,
            PTXInstructionSet second) =>
            first.Major == second.Major && first.Minor == second.Minor;

        /// <summary>
        /// Returns true if the first and the second instruction set are not the same.
        /// </summary>
        /// <param name="first">The first instruction set.</param>
        /// <param name="second">The second instruction set.</param>
        /// <returns>
        /// True, if the first and the second instruction set are not the same.
        /// </returns>
        public static bool operator !=(
            PTXInstructionSet first,
            PTXInstructionSet second) =>
            first.Major != second.Major || first.Minor != second.Minor;

        /// <summary>
        /// Returns true if the first instruction set is smaller than the second one.
        /// </summary>
        /// <param name="first">The first instruction set.</param>
        /// <param name="second">The second instruction set.</param>
        /// <returns>
        /// True, if the first instruction set is smaller than the second one.
        /// </returns>
        public static bool operator <(
            PTXInstructionSet first,
            PTXInstructionSet second) =>
            first.Major < second.Major
            ? true
            : first.Major == second.Major && first.Minor < second.Minor;

        /// <summary>
        /// Returns true if the first instruction set is less than or equal to the
        /// second instruction set.
        /// </summary>
        /// <param name="first">The first instruction set.</param>
        /// <param name="second">The second instruction set.</param>
        /// <returns>
        /// True, if the first instruction set is less or equal to the second instruction
        /// set.
        /// </returns>
        public static bool operator <=(
            PTXInstructionSet first,
            PTXInstructionSet second) =>
            first.Major < second.Major
            ? true
            : first.Major == second.Major && first.Minor <= second.Minor;

        /// <summary>
        /// Returns true if the first instruction set is greater than the second one.
        /// </summary>
        /// <param name="first">The first instruction set.</param>
        /// <param name="second">The second instruction set.</param>
        /// <returns>
        /// True, if the first instruction set is greater than the second one.
        /// </returns>
        public static bool operator >(
            PTXInstructionSet first,
            PTXInstructionSet second) =>
            first.Major > second.Major
            ? true
            : first.Major == second.Major && first.Minor > second.Minor;

        /// <summary>
        /// Returns true if the first instruction set is greater than or equal to the
        /// second instruction set.
        /// </summary>
        /// <param name="first">The first instruction set.</param>
        /// <param name="second">The second instruction set.</param>
        /// <returns>
        /// True, if the first instruction set is greater or equal to the second
        /// instruction set.
        /// </returns>
        public static bool operator >=(
            PTXInstructionSet first,
            PTXInstructionSet second) =>
            first.Major > second.Major
            ? true
            : first.Major == second.Major && first.Minor >= second.Minor;

        #endregion
    }
}
