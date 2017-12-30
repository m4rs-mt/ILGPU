// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelSpecialization.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents a kernel specialization.
    /// </summary>
    public struct KernelSpecialization : IEquatable<KernelSpecialization>
    {
        #region Constants

        /// <summary>
        /// Represents an empty (or *no*) specialization that allows the compiler to
        /// freely decide on its own.
        /// </summary>
        public static readonly KernelSpecialization Empty = new KernelSpecialization();

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new specialization object.
        /// </summary>
        /// <param name="maxNumThreadsPerGroup">The maximum number of threads per group.</param>
        /// <param name="minNumGroupsPerMultiprocessor">The minimum number of groups per multiprocessor.</param>
        public KernelSpecialization(
            int? maxNumThreadsPerGroup,
            int? minNumGroupsPerMultiprocessor)
        {
            if (maxNumThreadsPerGroup.HasValue && maxNumThreadsPerGroup < 1)
                throw new ArgumentOutOfRangeException(nameof(maxNumThreadsPerGroup));
            if (minNumGroupsPerMultiprocessor.HasValue && minNumGroupsPerMultiprocessor < 1)
                throw new ArgumentOutOfRangeException(nameof(minNumGroupsPerMultiprocessor));
            MaxNumThreadsPerGroup = maxNumThreadsPerGroup;
            MinNumGroupsPerMultiprocessor = minNumGroupsPerMultiprocessor;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the desired maximum number of threads per group.
        /// </summary>
        public int? MaxNumThreadsPerGroup { get; }

        /// <summary>
        /// Returns the desired minimum number of groups per multiprocessor.
        /// </summary>
        public int? MinNumGroupsPerMultiprocessor { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given specialization is equal to the current specialization.
        /// </summary>
        /// <param name="other">The other specialization.</param>
        /// <returns>True, iff the given specialization is equal to the current specialization.</returns>
        public bool Equals(KernelSpecialization other)
        {
            return this == other;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Checks whether the given accelerator is compatible with the current specialization.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns>True, iff the given accelerator is compatible with the current specialization.</returns>
        public bool IsCompatibleWith(Accelerator accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));

            // TODO: We might want to verify MinNumGroupsPerMultiprocessor in the future at this point.
            // However, this requires further extensions of the accelerator api.
            if (!MaxNumThreadsPerGroup.HasValue)
                return true;

            return MaxNumThreadsPerGroup <= accelerator.MaxNumThreadsPerGroup;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current specialization.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current specialization.</returns>
        public override bool Equals(object obj)
        {
            if (obj is KernelSpecialization other)
                return Equals(other);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this specialization.
        /// </summary>
        /// <returns>The hash code of this specialization.</returns>
        public override int GetHashCode()
        {
            return MaxNumThreadsPerGroup.GetHashCode() ^ MinNumGroupsPerMultiprocessor.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this specialization.
        /// </summary>
        /// <returns>The string representation of this specialization.</returns>
        public override string ToString()
        {
            return $"MaxNumThreadsPerGroup: {MaxNumThreadsPerGroup ?? 0}, MinNumGroupsPerMP: {MinNumGroupsPerMultiprocessor ?? 0}";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the specialization and second specialization are the same.
        /// </summary>
        /// <param name="first">The first specialization.</param>
        /// <param name="second">The second specialization.</param>
        /// <returns>True, iff the first and second specialization are the same.</returns>
        public static bool operator ==(KernelSpecialization first, KernelSpecialization second)
        {
            return first.MaxNumThreadsPerGroup.GetValueOrDefault(0) ==
                second.MaxNumThreadsPerGroup.GetValueOrDefault(0) &&
                first.MinNumGroupsPerMultiprocessor.GetValueOrDefault(0) ==
                second.MinNumGroupsPerMultiprocessor.GetValueOrDefault(0);
        }

        /// <summary>
        /// Returns true iff the first and second specialization are not the same.
        /// </summary>
        /// <param name="first">The first specialization.</param>
        /// <param name="second">The second specialization.</param>
        /// <returns>True, iff the first and second specialization are not the same.</returns>
        public static bool operator !=(KernelSpecialization first, KernelSpecialization second)
        {
            return !(first == second);
        }

        #endregion
    }
}
