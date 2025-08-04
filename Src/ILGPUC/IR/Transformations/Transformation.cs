// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Transformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses;
using System.Threading.Tasks;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Represents a generic transformation.
/// </summary>
abstract class Transformation
{
    protected static Method.Builder GetMethodBuilder(Value value) =>
        value.BasicBlock.Method.MethodBuilder;

    protected static BasicBlock.Builder GetBuilder(Value value) =>
        GetMethodBuilder(value)[value.BasicBlock];

    /// <summary>
    /// Transforms all method in the given context.
    /// </summary>
    /// <param name="methods">The methods to transform.</param>
    public void Transform(in MethodCollection methods)
    {
        foreach (var method in methods)
            method.CreateBuilder();

        PerformTransformation(methods);

        // Finish builders
        foreach (var method in methods)
        {
            var builder = method.MethodBuilder;

            builder.Complete();
            builder.Dispose();
        }

        // Perform GC runs
        Parallel.ForEach(methods, method => method.GC());
    }

    /// <summary>
    /// Transforms all given methods using the provided builder.
    /// </summary>
    /// <param name="methods">The methods to transform.</param>
    protected abstract void PerformTransformation(in MethodCollection methods);
}

/// <summary>
/// Represents a generic transformation that can be applied in an unordered manner.
/// </summary>
/// <remarks>
/// Note that this transformation is applied in parallel to all methods.
/// </remarks>
abstract class UnorderedTransformation : Transformation
{
    /// <inheritdoc cref="Transformation.PerformTransformation(in MethodCollection)"/>
    protected override void PerformTransformation(in MethodCollection methods)
    {
        var context = methods.Context;
        Parallel.ForEach(methods, method =>
            PerformTransformation(context, method.MethodBuilder));
    }

    /// <summary>
    /// Transforms the given method using the provided builder.
    /// </summary>
    /// <param name="context">The current context.</param>
    /// <param name="builder">The current method builder.</param>
    protected abstract void PerformTransformation(
        IRContext context,
        Method.Builder builder);
}

/// <summary>
/// Represents a generic transformation that can be applied in an unordered manner.
/// </summary>
/// <remarks>
/// Note that this transformation is applied sequentially to all methods.
/// </remarks>
abstract class SequentialUnorderedTransformation : Transformation
{
    /// <inheritdoc cref="Transformation.PerformTransformation(in MethodCollection)"/>
    protected override void PerformTransformation(in MethodCollection methods)
    {
        foreach (var method in methods)
            PerformTransformation(methods.Context, method.MethodBuilder);
    }

    /// <summary>
    /// Transforms the given method using the provided builder.
    /// </summary>
    /// <param name="context">The current context.</param>
    /// <param name="builder">The current method builder.</param>
    protected abstract void PerformTransformation(
        IRContext context,
        Method.Builder builder);
}

/// <summary>
/// Represents a generic transformation that can be applied in an unordered manner.
/// </summary>
/// <typeparam name="TIntermediate">The type of the intermediate values.</typeparam>
abstract class UnorderedTransformation<TIntermediate> : Transformation
{
    /// <summary>
    /// Creates a new intermediate value.
    /// </summary>
    /// <returns>The resulting intermediate value.</returns>
    protected abstract TIntermediate CreateIntermediate(in MethodCollection methods);

    /// <summary>
    /// Is invoked after all methods have been transformed.
    /// </summary>
    /// <param name="intermediate">The current intermediate value.</param>
    protected abstract void FinishProcessing(TIntermediate intermediate);

    /// <summary>
    /// Transforms all methods in the given context.
    /// </summary>
    /// <param name="methods">The methods to transform.</param>
    protected override void PerformTransformation(in MethodCollection methods)
    {
        var intermediate = CreateIntermediate(methods);

        // Apply transformation to all methods
        foreach (var method in methods)
            PerformTransformation(method.MethodBuilder, intermediate);

        FinishProcessing(intermediate);
    }

    /// <summary>
    /// Transforms the given method using the provided builder.
    /// </summary>
    /// <param name="builder">The current method builder.</param>
    /// <param name="intermediate">The intermediate value.</param>
    protected abstract bool PerformTransformation(
        Method.Builder builder,
        TIntermediate intermediate);
}

/// <summary>
/// Represents a generic transformation that will be applied in the post order
/// of the induced call graph.
/// </summary>
/// <typeparam name="TIntermediate">The type of the intermediate values.</typeparam>
abstract class OrderedTransformation<TIntermediate> : Transformation
{
    /// <summary>
    /// Creates a new intermediate value.
    /// </summary>
    /// <returns>The resulting intermediate value.</returns>
    protected abstract TIntermediate? CreateIntermediate(in MethodCollection methods);

    /// <summary>
    /// Is invoked after all methods have been transformed.
    /// </summary>
    /// <param name="intermediate">The current intermediate value.</param>
    protected abstract void FinishProcessing(in TIntermediate? intermediate);

    /// <summary>
    /// Transforms all methods in the given context.
    /// </summary>
    /// <param name="methods">The methods to transform.</param>
    protected sealed override void PerformTransformation(in MethodCollection methods)
    {
        var landscape = Landscape.Create(methods);
        if (landscape.Count < 1)
            return;

        var intermediate = CreateIntermediate(methods);
        foreach (var entry in landscape)
        {
            PerformTransformation(
                methods.Context,
                entry.Method.MethodBuilder,
                intermediate,
                landscape,
                entry);
        }
        FinishProcessing(intermediate);
    }

    /// <summary>
    /// Transforms the given method using the provided builder.
    /// </summary>
    /// <param name="context">The parent IR context to operate on.</param>
    /// <param name="builder">The current method builder.</param>
    /// <param name="intermediate">The intermediate value.</param>
    /// <param name="landscape">The global processing landscape.</param>
    /// <param name="current">The current landscape entry.</param>
    protected abstract void PerformTransformation(
        IRContext context,
        Method.Builder builder,
        in TIntermediate? intermediate,
        Landscape landscape,
        Landscape.Entry current);
}

/// <summary>
/// Represents a generic transformation that will be applied in the post order
/// of the induced call graph.
/// </summary>
abstract class OrderedTransformation : OrderedTransformation<object>
{
    /// <summary>
    /// Creates a new intermediate value.
    /// </summary>
    /// <returns>The resulting intermediate value.</returns>
    protected sealed override object? CreateIntermediate(
        in MethodCollection methods) => null;

    /// <summary>
    /// Is invoked after all methods have been transformed.
    /// </summary>
    /// <param name="intermediate">The current intermediate value.</param>
    protected sealed override void FinishProcessing(in object? intermediate) { }

    /// <summary>
    /// Transforms the given method using the provided builder.
    /// </summary>
    /// <param name="context">The parent IR context to operate on.</param>
    /// <param name="builder">The current method builder.</param>
    /// <param name="intermediate">The intermediate value.</param>
    /// <param name="landscape">The global processing landscape.</param>
    /// <param name="current">The current landscape entry.</param>
    protected sealed override void PerformTransformation(
        IRContext context,
        Method.Builder builder,
        in object? intermediate,
        Landscape landscape,
        Landscape.Entry current) =>
        PerformTransformation(context, builder, landscape, current);

    /// <summary>
    /// Transforms the given method using the provided builder.
    /// </summary>
    /// <param name="context">The parent IR context to operate on.</param>
    /// <param name="builder">The current method builder.</param>
    /// <param name="landscape">The global processing landscape.</param>
    /// <param name="current">The current landscape entry.</param>
    protected abstract void PerformTransformation(
        IRContext context,
        Method.Builder builder,
        Landscape landscape,
        Landscape.Entry current);
}
