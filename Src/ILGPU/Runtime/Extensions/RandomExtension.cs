// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: RandomExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Random;
using System;
using RandomGenerator = System.Random;

namespace ILGPU.Runtime.Extensions;

/// <summary>
/// An abstract interface describing an accelerator RNG extension.
/// </summary>
public interface IRandomExtension : IRandomProvider, IAcceleratorExtension
{
    /// <summary>
    /// Returns the unique random extension identifier.
    /// </summary>
    static Guid IAcceleratorExtension.Id { get; } =
        new Guid("c2261e36-dcfd-430b-806c-c023cbed7d14");

    /// <summary>
    /// Generates a new of uniformly distributed random numbers and writes it into the
    /// target span provided.
    /// </summary>
    /// <param name="target">The target span to fill.</param>
    void Generate(Span<int> target);

    /// <summary>
    /// Generates a new of uniformly distributed random numbers and writes it into the
    /// target span provided.
    /// </summary>
    /// <param name="target">The target span to fill.</param>
    void Generate(Span<float> target);

    /// <summary>
    /// Generates a new of uniformly distributed random numbers and writes it into the
    /// target span provided.
    /// </summary>
    /// <param name="target">The target span to fill.</param>
    void Generate(Span<double> target);

    /// <summary>
    /// Generates a new of uniformly distributed random numbers and writes it into the
    /// target span provided.
    /// </summary>
    /// <param name="target">The target span to fill.</param>
    void Generate(Span<long> target);

    /// <summary>
    /// Generates a new of set of random providers using the internal RNG as source
    /// generator in a deterministic way.
    /// </summary>
    /// <param name="target">The target span to fill.</param>
    void Generate<TProvider>(Span<TProvider> target)
        where TProvider : struct, IRandomProvider<TProvider>;
}

/// <summary>
/// Represents an extension to control random number generation in kernels.
/// </summary>
/// <typeparam name="TRandom">The random number generator.</typeparam>
/// <param name="random"></param>
public sealed class RandomExtension<TRandom>(TRandom random) :
    AcceleratorExtension, IRandomExtension
    where TRandom : struct, IRandomProvider<TRandom>
{
    private TRandom _random = random.NextProvider();

    /// <summary>
    /// Generates a new random extension instance using the given random number generator
    /// as a source RNG.
    /// </summary>
    /// <param name="random">The random number generator to use.</param>
    public RandomExtension(RandomGenerator random)
        : this(TRandom.CreateProvider(random))
    { }

    /// <summary>
    /// Performs a nop.
    /// </summary>
    /// <param name="shift">The period shift.</param>
    void IRandomProvider.ShiftPeriod(int shift) { }

    /// <inheritdoc cref="IRandomProvider.Next"/>
    public int Next() => _random.Next();

    /// <inheritdoc cref="IRandomProvider.NextDouble"/>
    public double NextDouble() => _random.NextDouble();

    /// <inheritdoc cref="IRandomProvider.NextFloat"/>
    public float NextFloat() => _random.NextFloat();

    /// <inheritdoc cref="IRandomProvider.NextLong"/>
    public long NextLong() => _random.NextLong();

    /// <inheritdoc cref="IRandomExtension.Generate(Span{int})"/>
    public void Generate(Span<int> target)
    {
        for (int i = 0; i < target.Length; ++i)
            target[i] = Next();
    }

    /// <inheritdoc cref="IRandomExtension.Generate(Span{float})"/>
    public void Generate(Span<float> target)
    {
        for (int i = 0; i < target.Length; ++i)
            target[i] = NextFloat();
    }

    /// <inheritdoc cref="IRandomExtension.Generate(Span{double})"/>
    public void Generate(Span<double> target)
    {
        for (int i = 0; i < target.Length; ++i)
            target[i] = NextDouble();
    }

    /// <inheritdoc cref="IRandomExtension.Generate(Span{long})"/>
    public void Generate(Span<long> target)
    {
        for (int i = 0; i < target.Length; ++i)
            target[i] = NextLong();
    }

    /// <inheritdoc cref="IRandomExtension.Generate{TProvider}(Span{TProvider})"/>
    public void Generate<TProvider>(Span<TProvider> target)
        where TProvider : struct, IRandomProvider<TProvider>
    {
        for (int i = 0; i < target.Length; ++i)
            target[i] = TProvider.CreateProvider(ref _random);
    }
}
