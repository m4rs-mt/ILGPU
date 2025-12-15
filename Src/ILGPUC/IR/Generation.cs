// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Generation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPUC.IR;

/// <summary>
/// An object that belongs to a particular generation.
/// </summary>
interface IGenerationObject
{
    /// <summary>
    /// Returns the current generation.
    /// </summary>
    Generation Generation { get; }
}

/// <summary>
/// Represents a particular generation.
/// </summary>
/// <param name="Index">The internal generation index.</param>
readonly record struct Generation(int Index = 0) : IComparable<Generation>
{
    /// <summary>
    /// Represents an invalid generation.
    /// </summary>
    public static readonly Generation Invalid = new(-1);

    /// <summary>
    /// Creates a new generation value representing the next generation.
    /// </summary>
    /// <returns>The next generation value.</returns>
    public Generation NextGeneration() => new(Index + 1);

    /// <summary>
    /// Returns the previous generation.
    /// </summary>
    public Generation PreviousGeneration() => new(Math.Max(Index - 1, 0));

    /// <summary>
    /// Ensures th given value has a compatible generation.
    /// </summary>
    /// <param name="other">The other item to validate.</param>
    [Conditional("DEBUG")]
    public void ValidateGeneration<T>(T? other)
        where T : IGenerationObject, ILocation =>
        other.Assert(other is null || this == other.Generation);

    /// <summary>
    /// Ensures th given value has a compatible generation.
    /// </summary>
    /// <param name="other">The other item to validate.</param>
    [Conditional("DEBUG")]
    public void ValidatePreviousGeneration<T>(T? other)
        where T : IGenerationObject, ILocation =>
        PreviousGeneration().ValidateGeneration(other);

    /// <summary>
    /// Ensures th given value has a compatible generation.
    /// </summary>
    /// <param name="other">The other item to validate.</param>
    [Conditional("DEBUG")]
    public void ValidateCurrentOrPreviousGeneration<T>(T? other)
        where T : IGenerationObject, ILocation =>
        other.Assert(
            other is null ||
            this == other.Generation ||
            PreviousGeneration() == other.Generation);

    public int CompareTo(Generation other) => Index.CompareTo(other.Index);

    public static bool operator >(Generation a, Generation b) => a.CompareTo(b) > 0;
    public static bool operator >=(Generation a, Generation b) => a.CompareTo(b) >= 0;

    public static bool operator <(Generation a, Generation b) => a.CompareTo(b) < 0;
    public static bool operator <=(Generation a, Generation b) => a.CompareTo(b) <= 0;

    public override string ToString() => $"Gen_{Index}";
}

/// <summary>
/// Represents a generation validator for value maps and sets.
/// This is a ref struct to avoid heap allocations.
/// </summary>
/// <remarks>
/// Creates a double-generation validator.
/// </remarks>
readonly struct GenerationValidator(Generation current, Generation? other = null)
{
    /// <summary>
    /// Returns the main (current) generation.
    /// </summary>
    public Generation Generation => current;

    /// <summary>
    /// Returns the previous generation (may be Invalid if single-gen mode).
    /// </summary>
    public Generation OtherGeneration { get; } = other ?? Generation.Invalid;

    /// <summary>
    /// Returns true if this validator supports two generations.
    /// </summary>
    public bool IsDoubleGeneration => OtherGeneration != Generation.Invalid;

    /// <summary>
    /// Validates that the given value's generation is supported.
    /// </summary>
    [Conditional("DEBUG")]
    public void ValidateGeneration<T>(T? value)
        where T : class, IGenerationObject, ILocation
    {
        if (IsDoubleGeneration)
        {
            // Double generation: validate current or previous
            value.Assert(
                value is null ||
                Generation == value.Generation ||
                OtherGeneration == value.Generation);
        }
        else
        {
            // Single generation: validate current only
            Generation.ValidateGeneration(value);
        }
    }

    /// <summary>
    /// Checks if the given generation is is supported.
    /// </summary>
    public bool SupportsGeneration(Generation gen) =>
        gen == Generation || (IsDoubleGeneration && gen == OtherGeneration);
}