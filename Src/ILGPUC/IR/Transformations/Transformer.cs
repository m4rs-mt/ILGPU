// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Transformer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Represents a function providing transformation instances.
/// </summary>
/// <param name="args">The transformation arguments to use.</param>
/// <returns>The created function.</returns>
delegate Transformation? TransformationProvider(TransformationArgs args);

/// <summary>
/// Applies transformations to contexts.
/// </summary>
readonly record struct Transformer(ImmutableArray<TransformationProvider> Transformations)
{
    /// <summary>
    /// A transformer builder.
    /// </summary>
    internal readonly struct Builder()
    {
        private readonly ImmutableArray<TransformationProvider>.Builder _builder =
            ImmutableArray.CreateBuilder<TransformationProvider>();

        /// <summary>
        /// Adds the given transformation to the manager.
        /// </summary>
        /// <param name="transformation">The transformation to add.</param>
        public void Add(TransformationProvider transformation) =>
            _builder.Add(transformation);

        /// <summary>
        /// Converts this builder to an immutable array.
        /// </summary>
        /// <returns>The immutable transformation array.</returns>
        public Transformer ToTransformer() =>
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
    public static Transformer Create(TransformationProvider transform) =>
        Create([transform]);

    /// <summary>
    /// Creates a transformer.
    /// </summary>
    /// <param name="transform">The first transformation.</param>
    /// <param name="transformations">The other transformations.</param>
    /// <returns>The created transformer.</returns>
    public static Transformer Create(
        TransformationProvider transform,
        params TransformationProvider[] transformations) =>
        Create([transform, .. transformations]);

    /// <summary>
    /// Creates a transformer.
    /// </summary>
    /// <param name="transforms">The transformations.</param>
    /// <returns>The created transformer.</returns>
    public static Transformer Create(ImmutableArray<TransformationProvider> transforms) =>
        new(transforms);

    /// <summary>
    /// Applies this transformer to the given module.
    /// </summary>
    /// <param name="properties">The compilation properties to use.</param>
    /// <param name="typeInformationManager">The parent type information manager.</param>
    /// <param name="module">The module to transform.</param>
    /// <returns>The transformed module.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Module Apply(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module)
    {
        var current = module;
        foreach (var provider in Transformations)
        {
            var args = new TransformationArgs(
                properties,
                typeInformationManager,
                current);
            var transformation = provider(args);
            current = transformation?.Transform() ?? current;
            IRVerifier.Verify(current, transformation?.GetType().Name);
        }

        return current;
    }

}
