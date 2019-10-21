﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXArchitecture.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using ILGPU.Backends;
using ILGPU.Resources;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a CUDA driver version
    /// </summary>
    [DebuggerDisplay("CUDA {Major}.{Minor}")]
    public readonly struct CudaDriverVersion : IEquatable<CudaDriverVersion>, IComparable<CudaDriverVersion>
    {
        #region Constants

        private const int MajorMultiplier = 1000;
        private const int MinorMultiplier = 10;

        private const int MaxMajorVersion = int.MaxValue / MajorMultiplier;
        private const int MaxMinVersion = MajorMultiplier / MinorMultiplier;

        #endregion

        #region Instance

        private CudaDriverVersion(int value)
        {
            Value = value;
        }

        /// <summary>
        /// Returns the CUDA driver version from major/minor values
        /// </summary>
        /// <param name="major">The major version</param>
        /// <param name="minor">The minor version</param>
        /// <returns>The CUDA driver version</returns>
        public static CudaDriverVersion FromMajorMinor(int major, int minor)
        {
            if (major < 0 || major >= MaxMajorVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(major));
            }
            if (minor < 0 || minor >= MaxMinVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(minor));
            }
            return new CudaDriverVersion((major * MajorMultiplier) + (minor * MinorMultiplier));
        }

        /// <summary>
        /// Returns the CUDA driver version from a value
        /// </summary>
        /// <param name="value">The CUDA driver value</param>
        /// <returns>The CUDA driver version</returns>
        public static CudaDriverVersion FromValue(int value) =>
            new CudaDriverVersion(value);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given version is equal to this version.
        /// </summary>
        /// <param name="other">The other version.</param>
        /// <returns>True, iff the given version is equal to this version.</returns>
        public bool Equals(CudaDriverVersion other) => this == other;

        #endregion

        #region IComparable

        /// <summary>
        /// Compares this version to the given one.
        /// </summary>
        /// <param name="other">The object to compare to.</param>
        /// <returns>The comparison result.</returns>
        public int CompareTo(CudaDriverVersion other) => Value.CompareTo(other.Value);

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public int Major =>
            Value / MajorMultiplier;

        /// <summary>
        /// 
        /// </summary>
        public int Minor =>
            (Value % MajorMultiplier) / MinorMultiplier;

        /// <summary>
        /// 
        /// </summary>
        public int Value { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to this version.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to this version.</returns>
        public override bool Equals(object obj)
        {
            if (obj is CudaDriverVersion version)
                return version == this;
            return false;
        }

        /// <summary>
        /// Returns the hash code of this version.
        /// </summary>
        /// <returns>The hash code of this version.</returns>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Returns the string representation of the driver version.
        /// </summary>
        /// <returns>The string representation of the driver version.</returns>
        public override string ToString() => $"{Major}.{Minor}";

        #endregion

        #region Operators

        /// <summary>
        /// Converts a version into an int.
        /// </summary>
        /// <param name="version"></param>
        public static implicit operator int(CudaDriverVersion version) =>
            version.Value;

        /// <summary>
        /// Returns true iff the first and the second version are the same.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, iff the first and the second version are the same.</returns>
        public static bool operator ==(CudaDriverVersion first, CudaDriverVersion second) =>
            first.Value == second.Value;

        /// <summary>
        /// Returns true iff the first and the second version are not the same.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, iff the first and the second version are not the same.</returns>
        public static bool operator !=(CudaDriverVersion first, CudaDriverVersion second) =>
            first.Value != second.Value;

        /// <summary>
        /// Returns true iff the first version is smaller than the second one.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, iff the first version is smaller than the second one.</returns>
        public static bool operator <(CudaDriverVersion first, CudaDriverVersion second) =>
            first.Value < second.Value;

        /// <summary>
        /// Returns true iff the first version is less than or equal to the second version.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, iff the first version is less or equal to the second version.</returns>
        public static bool operator <=(CudaDriverVersion first, CudaDriverVersion second) =>
            first.Value <= second.Value;

        /// <summary>
        /// Returns true iff the first version is greater than the second one.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, iff the first version is greater than the second one.</returns>
        public static bool operator >(CudaDriverVersion first, CudaDriverVersion second) =>
            first.Value > second.Value;

        /// <summary>
        /// Returns true iff the first version is greater than or equal to the second version.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, iff the first version is greater or equal to the second version.</returns>
        public static bool operator >=(CudaDriverVersion first, CudaDriverVersion second) =>
            first.Value >= second.Value;

        #endregion
    }

    /// <summary>
    /// Utilities for <see cref="CudaDriverVersion"/>.
    /// </summary>
    public static class CudaDriverVersionUtils
    {
        #region Static

        /// <summary>
        /// Maps PTX architecture to their corresponding minimum CUDA driver version.
        /// </summary>
        private static readonly Dictionary<PTXArchitecture, CudaDriverVersion> ArchitectureLookup =
            new Dictionary<PTXArchitecture, CudaDriverVersion>
        {
            { PTXArchitecture.SM_30, CudaDriverVersion.FromMajorMinor(4, 1) },
            { PTXArchitecture.SM_32, CudaDriverVersion.FromMajorMinor(6, 0) },
            { PTXArchitecture.SM_35, CudaDriverVersion.FromMajorMinor(5, 0) },
            { PTXArchitecture.SM_37, CudaDriverVersion.FromMajorMinor(6, 5) },

            { PTXArchitecture.SM_50, CudaDriverVersion.FromMajorMinor(6, 5) },
            { PTXArchitecture.SM_52, CudaDriverVersion.FromMajorMinor(6, 5) },
            { PTXArchitecture.SM_53, CudaDriverVersion.FromMajorMinor(7, 0) },

            { PTXArchitecture.SM_60, CudaDriverVersion.FromMajorMinor(8, 0) },
            { PTXArchitecture.SM_61, CudaDriverVersion.FromMajorMinor(8, 0) },
            { PTXArchitecture.SM_62, CudaDriverVersion.FromMajorMinor(8, 0) },

            { PTXArchitecture.SM_70, CudaDriverVersion.FromMajorMinor(9, 0) },
            { PTXArchitecture.SM_72, CudaDriverVersion.FromMajorMinor(9, 1) },
            { PTXArchitecture.SM_75, CudaDriverVersion.FromMajorMinor(10, 0) },
        };

        /// <summary>
        /// Maps PTX ISA to their corresponding minimum CUDA driver version.
        /// </summary>
        private static readonly Dictionary<PTXInstructionSet, CudaDriverVersion> InstructionSetLookup =
            new Dictionary<PTXInstructionSet, CudaDriverVersion>
        {
            { PTXInstructionSet.ISA_30, CudaDriverVersion.FromMajorMinor(4, 1) },
            { PTXInstructionSet.ISA_31, CudaDriverVersion.FromMajorMinor(5, 0) },
            { PTXInstructionSet.ISA_32, CudaDriverVersion.FromMajorMinor(5, 5) },

            { PTXInstructionSet.ISA_40, CudaDriverVersion.FromMajorMinor(6, 0) },
            { PTXInstructionSet.ISA_41, CudaDriverVersion.FromMajorMinor(6, 5) },
            { PTXInstructionSet.ISA_42, CudaDriverVersion.FromMajorMinor(7, 0) },
            { PTXInstructionSet.ISA_43, CudaDriverVersion.FromMajorMinor(7, 5) },

            { PTXInstructionSet.ISA_50, CudaDriverVersion.FromMajorMinor(8, 0) },

            { PTXInstructionSet.ISA_60, CudaDriverVersion.FromMajorMinor(9, 0) },
            { PTXInstructionSet.ISA_61, CudaDriverVersion.FromMajorMinor(9, 1) },
            { PTXInstructionSet.ISA_62, CudaDriverVersion.FromMajorMinor(9, 2) },
            { PTXInstructionSet.ISA_63, CudaDriverVersion.FromMajorMinor(10, 0) },
            { PTXInstructionSet.ISA_64, CudaDriverVersion.FromMajorMinor(10, 1) },
        };

        /// <summary>
        /// Resolves the minimum CUDA driver version for the PTX architecture
        /// </summary>
        /// <param name="architecture">The PTX architecture</param>
        /// <returns>The minimum driver version</returns>
        public static CudaDriverVersion GetMinimumDriverVersion(PTXArchitecture architecture)
        {
            if (!ArchitectureLookup.TryGetValue(architecture, out var result))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedPTXArchitecture);
            return result;
        }

        /// <summary>
        /// Resolves the minimum CUDA driver version for the PTX instruction set
        /// </summary>
        /// <param name="instructionSet">The PTX instruction set</param>
        /// <returns>The minimum driver version</returns>
        public static CudaDriverVersion GetMinimumDriverVersion(PTXInstructionSet instructionSet)
        {
            if (!InstructionSetLookup.TryGetValue(instructionSet, out var result))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedPTXInstructionSet);
            return result;
        }

        #endregion
    }
}
