// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXArchitecture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a PTX architecture.
    /// </summary>
    public readonly struct PTXArchitecture :
        IEquatable<PTXArchitecture>,
        IComparable<PTXArchitecture>
    {
        #region Constants

        /// <summary>
        /// The 3.0 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_30 = new PTXArchitecture(3, 0);

        /// <summary>
        /// The 3.2 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_32 = new PTXArchitecture(3, 2);

        /// <summary>
        /// The 3.5 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_35 = new PTXArchitecture(3, 5);

        /// <summary>
        /// The 3.7 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_37 = new PTXArchitecture(3, 7);

        /// <summary>
        /// The 5.0 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_50 = new PTXArchitecture(5, 0);

        /// <summary>
        /// The 5.2 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_52 = new PTXArchitecture(5, 2);

        /// <summary>
        /// The 5.3 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_53 = new PTXArchitecture(5, 3);

        /// <summary>
        /// The 6.0 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_60 = new PTXArchitecture(6, 0);

        /// <summary>
        /// The 6.1 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_61 = new PTXArchitecture(6, 1);

        /// <summary>
        /// The 6.2 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_62 = new PTXArchitecture(6, 2);

        /// <summary>
        /// The 7.0 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_70 = new PTXArchitecture(7, 0);

        /// <summary>
        /// The 7.2 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_72 = new PTXArchitecture(7, 2);

        /// <summary>
        /// The 7.5 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_75 = new PTXArchitecture(7, 5);

        /// <summary>
        /// The 8.0 architecture.
        /// </summary>
        public static readonly PTXArchitecture SM_80 = new PTXArchitecture(8, 0);

        #endregion

        #region Instance

        /// <summary>
        /// Creates the architecture from major/minor values.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        public PTXArchitecture(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given architecture is equal to this architecture.
        /// </summary>
        /// <param name="other">The other architecture.</param>
        /// <returns>
        /// True, if the given architecture is equal to this architecture.
        /// </returns>
        public bool Equals(PTXArchitecture other) => this == other;

        #endregion

        #region IComparable

        /// <summary>
        /// Compares this architecture to the given one.
        /// </summary>
        /// <param name="other">The object to compare to.</param>
        /// <returns>The comparison result.</returns>
        public int CompareTo(PTXArchitecture other)
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
        /// The major version.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// The minor version.
        /// </summary>
        public int Minor { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to this architecture.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True,
        /// if the given object is equal to this architecture.</returns>
        public override bool Equals(object obj) =>
            obj is PTXArchitecture architecture && architecture == this;

        /// <summary>
        /// Returns the hash code of this architecture.
        /// </summary>
        /// <returns>The hash code of this architecture.</returns>
        public override int GetHashCode() => Major.GetHashCode() ^ Minor.GetHashCode();

        /// <summary>
        /// Returns the string representation of the architecture.
        /// </summary>
        /// <returns>The string representation of the architecture.</returns>
        public override string ToString() => $"SM {Major}.{Minor}";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and the second architecture are the same.
        /// </summary>
        /// <param name="first">The first architecture.</param>
        /// <param name="second">The second architecture.</param>
        /// <returns>
        /// True, if the first and the second architecture are the same.
        /// </returns>
        public static bool operator ==(
            PTXArchitecture first,
            PTXArchitecture second) =>
            first.Major == second.Major && first.Minor == second.Minor;

        /// <summary>
        /// Returns true if the first and the second architecture are not the same.
        /// </summary>
        /// <param name="first">The first architecture.</param>
        /// <param name="second">The second architecture.</param>
        /// <returns>
        /// True, if the first and the second architecture are not the same.
        /// </returns>
        public static bool operator !=(
            PTXArchitecture first,
            PTXArchitecture second) =>
            first.Major != second.Major || first.Minor != second.Minor;

        /// <summary>
        /// Returns true if the first architecture is smaller than the second one.
        /// </summary>
        /// <param name="first">The first architecture.</param>
        /// <param name="second">The second architecture.</param>
        /// <returns>
        /// True, if the first architecture is smaller than the second one.
        /// </returns>
        public static bool operator <(
            PTXArchitecture first,
            PTXArchitecture second) =>
            first.Major < second.Major ||
            first.Major == second.Major && first.Minor < second.Minor;

        /// <summary>
        /// Returns true if the first architecture is less than or equal to the
        /// second architecture.
        /// </summary>
        /// <param name="first">The first architecture.</param>
        /// <param name="second">The second architecture.</param>
        /// <returns>
        /// True, if the first architecture is less or equal to the second architecture.
        /// </returns>
        public static bool operator <=(
            PTXArchitecture first,
            PTXArchitecture second) =>
            first.Major < second.Major ||
            first.Major == second.Major && first.Minor <= second.Minor;

        /// <summary>
        /// Returns true if the first architecture is greater than the second one.
        /// </summary>
        /// <param name="first">The first architecture.</param>
        /// <param name="second">The second architecture.</param>
        /// <returns>
        /// True, if the first architecture is greater than the second one.
        /// </returns>
        public static bool operator >(
            PTXArchitecture first,
            PTXArchitecture second) =>
            first.Major > second.Major ||
            first.Major == second.Major && first.Minor > second.Minor;

        /// <summary>
        /// Returns true if the first architecture is greater than or equal to the
        /// second architecture.
        /// </summary>
        /// <param name="first">The first architecture.</param>
        /// <param name="second">The second architecture.</param>
        /// <returns>
        /// True, if the first architecture is greater or equal to the second
        /// architecture.
        /// </returns>
        public static bool operator >=(
            PTXArchitecture first,
            PTXArchitecture second) =>
            first.Major > second.Major ||
            first.Major == second.Major && first.Minor >= second.Minor;

        #endregion
    }

    /// <summary>
    /// Utilities for the <see cref="PTXArchitecture"/> enumeration.
    /// </summary>
    public static class PTXArchitectureUtils
    {
        #region Static

        /// <summary>
        /// Resolves the PTX architecture for the given major and minor versions.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <returns>The resolved PTX version.</returns>
        public static PTXArchitecture GetArchitecture(int major, int minor)
        {
            var architecture = new PTXArchitecture(major, minor);
            return architecture >= PTXArchitecture.SM_30
            ? architecture
            : throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedPTXArchitecture);
        }

        #endregion
    }
}
