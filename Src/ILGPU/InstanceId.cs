// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: InstanceId.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU
{
    internal readonly struct InstanceId : IEquatable<InstanceId>
    {
        #region Static

        /// <summary>
        /// Represents the empty instance id.
        /// </summary>
        public static readonly InstanceId Empty = new InstanceId(-1);

        /// <summary>
        /// A shared static instance id counter.
        /// </summary>
        private static long instanceIdCounter = 0;

        /// <summary>
        /// Creates a new unique instance id.
        /// </summary>
        /// <returns>The unique instance id.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InstanceId CreateNew() =>
            new InstanceId(Interlocked.Add(ref instanceIdCounter, 1L));

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new instance id.
        /// </summary>
        /// <param name="id">The raw id.</param>
        internal InstanceId(long id)
        {
            Value = id;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying raw id.
        /// </summary>
        public long Value { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given id is equal to this id.
        /// </summary>
        /// <param name="other">The other id.</param>
        /// <returns>True, if the given id is equal to this id.</returns>
        public bool Equals(InstanceId other) => this == other;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given object is equal to this id.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to this id.</returns>
        public override bool Equals(object obj) =>
            obj is InstanceId id && id == this;

        /// <summary>
        /// Returns the hash code of this id.
        /// </summary>
        /// <returns>The hash code of this id.</returns>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Returns the string representation of the <see cref="Value"/> property.
        /// </summary>
        /// <returns>
        /// The string representation of the <see cref="Value"/> property.
        /// </returns>
        public override string ToString() => Value.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given instance id into its underlying long value.
        /// </summary>
        /// <param name="id">The instance id.</param>
        public static implicit operator long(InstanceId id) => id.Value;

        /// <summary>
        /// Returns true if the first and the second id are the same.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>True, if the first and the second id are the same.</returns>
        public static bool operator ==(InstanceId first, InstanceId second) =>
            first.Value == second.Value;

        /// <summary>
        /// Returns true if the first and the second id are not the same.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>True, if the first and the second id are not the same.</returns>
        public static bool operator !=(InstanceId first, InstanceId second) =>
            first.Value != second.Value;

        #endregion
    }
}
