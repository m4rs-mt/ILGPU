// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Transformer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Applies transformations to contexts.
/// </summary>
readonly record struct Transformer(ImmutableArray<Transformation> Transformations)
{
    /// <summary>
    /// A transformer builder.
    /// </summary>
    internal readonly struct Builder()
    {
        private readonly ImmutableArray<Transformation>.Builder _builder =
            ImmutableArray.CreateBuilder<Transformation>();

        /// <summary>
        /// Adds the given transformation to the manager.
        /// </summary>
        /// <param name="transformation">The transformation to add.</param>
        public readonly void Add(Transformation transformation) =>
            _builder.Add(transformation
                ?? throw new ArgumentNullException(nameof(transformation)));

        /// <summary>
        /// Converts this builder to an immutable array.
        /// </summary>
        /// <returns>The immutable transformation array.</returns>
        public readonly Transformer ToTransformer() =>
            new(_builder.ToImmutable());
    }

    /// <summary>
    /// Creates a new transformer builder.
    /// </summary>
    /// <returns>A new builder.</returns>
    public static Builder CreateBuilder() => new();

    /// <summary>
    /// Creates a transformer.
    /// </summary>
    /// <param name="transform">The specification to use.</param>
    /// <returns>The created transformer.</returns>
    public static Transformer Create(Transformation transform) =>
        Create([transform]);

    /// <summary>
    /// Creates a transformer.
    /// </summary>
    /// <param name="transform">The first transformation.</param>
    /// <param name="transformations">The other transformations.</param>
    /// <returns>The created transformer.</returns>
    public static Transformer Create(
        Transformation transform,
        params Transformation[] transformations) =>
        Create([transform, .. transformations]);

    /// <summary>
    /// Creates a transformer.
    /// </summary>
    /// <param name="transforms">The transformations.</param>
    /// <returns>The created transformer.</returns>
    public static Transformer Create(ImmutableArray<Transformation> transforms) =>
        new(transforms);
}
