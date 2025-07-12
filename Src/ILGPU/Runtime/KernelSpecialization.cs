// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelSpecialization.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.IO;

namespace ILGPU.Runtime;

/// <summary>
/// Marks kernels to be specialized for specific execution scenarios.
/// </summary>
/// <param name="maxNumThreadsPerGroup">
/// The maximum number of threads per group.
/// </param>
/// <param name="minNumGroupsPerMultiprocessor">
/// The minimum number of groups per multiprocessor.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class KernelSpecializationAttribute(
    int? maxNumThreadsPerGroup = null,
    int? minNumGroupsPerMultiprocessor = null) : Attribute
{
    /// <summary>
    /// Returns the kernel specialization.
    /// </summary>
    public KernelSpecialization Specialization { get; } = KernelSpecialization.Create(
        maxNumThreadsPerGroup,
        minNumGroupsPerMultiprocessor);

    /// <summary>
    /// Returns the desired maximum number of threads per group.
    /// </summary>
    public int? MaxNumThreadsPerGroup => Specialization.MaxNumThreadsPerGroup;

    /// <summary>
    /// Returns the desired minimum number of groups per multiprocessor.
    /// </summary>
    public int? MinNumGroupsPerMultiprocessor =>
        Specialization.MinNumGroupsPerMultiprocessor;
}

/// <summary>
/// Marks kernels to be specialized for specific execution scenarios.
/// </summary>
/// <param name="MaxNumThreadsPerGroup">
/// The desired maximum number of threads per group.
/// </param>
/// <param name="MinNumGroupsPerMultiprocessor">
/// The desired minimum number of groups per multiprocessor.
/// </param>
public readonly record struct KernelSpecialization(
    int? MaxNumThreadsPerGroup,
    int? MinNumGroupsPerMultiprocessor)
{
    /// <summary>
    /// Constructs a new specialization object.
    /// </summary>
    /// <param name="maxNumThreadsPerGroup">
    /// The maximum number of threads per group.
    /// </param>
    /// <param name="minNumGroupsPerMultiprocessor">
    /// The minimum number of groups per multiprocessor.
    /// </param>
    public static KernelSpecialization Create(
        int? maxNumThreadsPerGroup = null,
        int? minNumGroupsPerMultiprocessor = null)
    {
        if (maxNumThreadsPerGroup.HasValue && maxNumThreadsPerGroup < 1)
            throw new ArgumentOutOfRangeException(nameof(maxNumThreadsPerGroup));
        if (minNumGroupsPerMultiprocessor.HasValue &&
            minNumGroupsPerMultiprocessor < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minNumGroupsPerMultiprocessor));
        }

        return new(maxNumThreadsPerGroup, minNumGroupsPerMultiprocessor);
    }

    /// <summary>
    /// Writes this specialization kernel to the given writer.
    /// </summary>
    /// <param name="writer">The target writer to write to.</param>
    public void Write(BinaryWriter writer)
    {
        writer.Write(MaxNumThreadsPerGroup.HasValue);
        if (MaxNumThreadsPerGroup.HasValue)
            writer.Write(MaxNumThreadsPerGroup.Value);
        writer.Write(MinNumGroupsPerMultiprocessor.HasValue);
        if (MinNumGroupsPerMultiprocessor.HasValue)
            writer.Write(MinNumGroupsPerMultiprocessor.Value);
    }

    /// <summary>
    /// Loads a kernel specialization definition from the given reader.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>The loaded specialization.</returns>
    public static KernelSpecialization Read(BinaryReader reader)
    {
        bool hasMasNumThreads = reader.ReadBoolean();
        int? maxNumThreadsPerGroup = hasMasNumThreads ? reader.ReadInt32() : null;
        bool hasMinNumGroups = reader.ReadBoolean();
        int? minNumGroupsPerMultiprocessor = hasMinNumGroups ? reader.ReadInt32() : null;
        return new(maxNumThreadsPerGroup, minNumGroupsPerMultiprocessor);
    }
}
