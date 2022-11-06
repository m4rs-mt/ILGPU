// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaArchitecture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda architecture.
    /// </summary>
    public readonly struct CudaArchitecture :
        IEquatable<CudaArchitecture>,
        IComparable<CudaArchitecture>
    {
        #region Constants

        /// <summary>
        /// The 3.0 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_30 = new CudaArchitecture(3, 0);

        /// <summary>
        /// The 3.2 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_32 = new CudaArchitecture(3, 2);

        /// <summary>
        /// The 3.5 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_35 = new CudaArchitecture(3, 5);

        /// <summary>
        /// The 3.7 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_37 = new CudaArchitecture(3, 7);

        /// <summary>
        /// The 5.0 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_50 = new CudaArchitecture(5, 0);

        /// <summary>
        /// The 5.2 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_52 = new CudaArchitecture(5, 2);

        /// <summary>
        /// The 5.3 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_53 = new CudaArchitecture(5, 3);

        /// <summary>
        /// The 6.0 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_60 = new CudaArchitecture(6, 0);

        /// <summary>
        /// The 6.1 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_61 = new CudaArchitecture(6, 1);

        /// <summary>
        /// The 6.2 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_62 = new CudaArchitecture(6, 2);

        /// <summary>
        /// The 7.0 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_70 = new CudaArchitecture(7, 0);

        /// <summary>
        /// The 7.2 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_72 = new CudaArchitecture(7, 2);

        /// <summary>
        /// The 7.5 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_75 = new CudaArchitecture(7, 5);

        /// <summary>
        /// The 8.0 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_80 = new CudaArchitecture(8, 0);

        /// <summary>
        /// The 8.6 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_86 = new CudaArchitecture(8, 6);

        /// <summary>
        /// The 8.7 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_87 = new CudaArchitecture(8, 7);

        /// <summary>
        /// The 8.9 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_89 = new CudaArchitecture(8, 9);

        /// <summary>
        /// The 9.0 architecture.
        /// </summary>
        public static readonly CudaArchitecture SM_90 = new CudaArchitecture(9, 0);

        #endregion

        #region Instance

        /// <summary>
        /// Creates the architecture from major/minor values.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        public CudaArchitecture(int major, int minor)
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
        public bool Equals(CudaArchitecture other) => this == other;

        #endregion

        #region IComparable

        /// <summary>
        /// Compares this architecture to the given one.
        /// </summary>
        /// <param name="other">The object to compare to.</param>
        /// <returns>The comparison result.</returns>
        public int CompareTo(CudaArchitecture other)
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
            obj is CudaArchitecture architecture && architecture == this;

        /// <summary>
        /// Returns the hash code of this architecture.
        /// </summary>
        /// <returns>The hash code of this architecture.</returns>
        public override int GetHashCode() => Major.GetHashCode() ^ Minor.GetHashCode();

        /// <summary>
        /// Returns the string representation of the architecture.
        /// </summary>
        /// <returns>The string representation of the architecture.</returns>
        public override string ToString() => $"SM_{Major}{Minor}";

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
            CudaArchitecture first,
            CudaArchitecture second) =>
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
            CudaArchitecture first,
            CudaArchitecture second) =>
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
            CudaArchitecture first,
            CudaArchitecture second) =>
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
            CudaArchitecture first,
            CudaArchitecture second) =>
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
            CudaArchitecture first,
            CudaArchitecture second) =>
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
            CudaArchitecture first,
            CudaArchitecture second) =>
            first.Major > second.Major ||
            first.Major == second.Major && first.Minor >= second.Minor;

        #endregion
    }
}
