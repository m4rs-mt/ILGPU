// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRandomProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Random;

/// <summary>
/// Specifies an abstract RNG provider.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Generates a random int in [0..int.MaxValue].
    /// </summary>
    /// <returns>A random int in [0..int.MaxValue].</returns>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Like the method System.Random.Next()")]
    int Next();

    /// <summary>
    /// Generates a random long in [0..long.MaxValue].
    /// </summary>
    /// <returns>A random long in [0..long.MaxValue].</returns>
    long NextLong();

    /// <summary>
    /// Generates a random float in [0..1).
    /// </summary>
    /// <returns>A random float in [0..1).</returns>
    float NextFloat();

    /// <summary>
    /// Generates a random double in [0..1).
    /// </summary>
    /// <returns>A random double in [0..1).</returns>
    double NextDouble();

    /// <summary>
    /// Shifts the current period.
    /// </summary>
    /// <param name="shift">The shift amount.</param>
    void ShiftPeriod(int shift);
}

/// <summary>
/// An abstract RNG provider that supports period shifts.
/// </summary>
/// <typeparam name="TSelf">The implementing provider type.</typeparam>
public interface IRandomProvider<TSelf> : IRandomProvider
    where TSelf : struct, IRandomProvider<TSelf>
{
    /// <summary>
    /// Instantiates a new provider using the internal random state.
    /// </summary>
    /// <returns>The next provider instance.</returns>
    TSelf NextProvider();

    /// <summary>
    /// Instantiates a new provider using the given random.
    /// </summary>
    /// <param name="random">The parent RNG instance.</param>
    /// <returns>The next provider instance.</returns>
    static abstract TSelf CreateProvider(System.Random random);

    /// <summary>
    /// Instantiates a new provider using the given random.
    /// </summary>
    /// <param name="random">The parent RNG instance.</param>
    /// <returns>The next provider instance.</returns>
    static abstract TSelf CreateProvider<TRandomProvider>(ref TRandomProvider random)
        where TRandomProvider : struct, IRandomProvider<TRandomProvider>;
}
